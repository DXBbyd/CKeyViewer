"""把 release/ 下的资产发布到 GitHub Release。

用法（PowerShell）：
    $env:GITHUB_TOKEN = "ghp_xxx"        # 只需要 repo 权限（或 fine-grained 的 Contents: Read and write）
    python tools/build_release.py
    python tools/publish_release.py

用法（bash）：
    GITHUB_TOKEN=ghp_xxx python tools/publish_release.py

环境变量：
    GITHUB_TOKEN / GH_TOKEN   必填
    TAG                       默认 v1.0.0
    RELEASE_NAME              默认 "CKeyViewer 1.0.0"

为什么不走 git：本机 github.com:443 时通时不通，而且 Release 资产（108 MB）不能靠 git 推
（单文件超过 GitHub 的 100 MB 限制）。这里直接打 REST API。
"""
import json
import os
import sys
import time
import urllib.error
import urllib.parse
import urllib.request

REPO = "DXBbyd/CKeyViewer"
TAG = os.environ.get("TAG", "v1.0.0")
VERSION = "1.0.0"
RELEASE_NAME = os.environ.get("RELEASE_NAME", "CKeyViewer " + VERSION)
TOKEN = os.environ.get("GITHUB_TOKEN") or os.environ.get("GH_TOKEN") or ""

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RELEASE_DIR = os.path.join(ROOT, "release")

ASSETS = [
    "CKeyViewerSetup-%s.exe" % VERSION,
    "CKeyViewer-%s-portable-win64.zip" % VERSION,
    "SHA256SUMS.txt",
]

BODY = """## CKeyViewer {ver}

Windows 上的按键可视化覆盖层 —— 透明置顶窗口，实时显示按键按下状态、按键计数、KPS 与雨线特效。
配置格式与 [JipperKeyViewer](https://github.com/adofaiex/JipperKeyViewer) 双向兼容。

作者 **DXBbyd** · QQ `3157037483`

### 下载哪个

| 文件 | 说明 |
| --- | --- |
| `CKeyViewerSetup-{ver}.exe` | **安装程序**（推荐）。装到用户目录、建快捷方式、自动打上「以管理员运行」标记，可从「设置 → 应用」卸载 |
| `CKeyViewer-{ver}-portable-win64.zip` | **便携版**。解压即用，双击 `CKeyViewer.exe`（记得右键 → 以管理员身份运行） |
| `SHA256SUMS.txt` | 上面两个文件的校验和 |

两个包都是**自包含单文件**，自带 .NET 运行时，不用装任何依赖。

### 注意

- **必须以管理员身份运行**，否则会弹出「请使用管理员运行此程序」然后退出。
  这是设计如此：低完整性权限读不到更高级别进程（例如以管理员启动的游戏）的按键。
- 如果双击没反应，看 README 的「装不上 / 打不开？」一节 ——
  最常见的原因是所在目录被执行策略限制，换个普通目录即可。
- Win11 默认把新的托盘图标收进「隐藏的图标」折叠区，点托盘左侧的 `^` 就能看到。

### 主要内容

- 主键 8 种布局 + 脚键 8 种，含 Full108 全键盘
- 可绑任意字母 / 数字 / 功能键 / 小键盘，以及**左右区分的 Shift / Ctrl / Alt** 和**鼠标 5 键**
- 自由布局：节点可拖拽，位置 / 尺寸 / 层级 / 配色 / 按键绑定全可调，支持图层组
- KPS / Total / 每键计数，27 条缓动曲线的按压缩放，三行独立雨线 + 鬼键雨线
- 12 页深色设置面板（含「关于」），改动即时生效 + 防抖落盘，系统托盘，5 组全局热键
- 附带 `CKeyViewer.exe --selftest` 无界面自检（20 项断言）

**MIT License** · 完整说明见 [README](https://github.com/DXBbyd/CKeyViewer#readme)
""".format(ver=VERSION)


def api(method, path, payload=None, timeout=90):
    url = path if path.startswith("http") else "https://api.github.com" + path
    body = json.dumps(payload).encode("utf-8") if payload is not None else None
    req = urllib.request.Request(url, data=body, method=method)
    req.add_header("Authorization", "Bearer " + TOKEN)
    req.add_header("Accept", "application/vnd.github+json")
    req.add_header("User-Agent", "CKeyViewer-release")
    if body is not None:
        req.add_header("Content-Type", "application/json")
    with urllib.request.urlopen(req, timeout=timeout) as r:
        raw = r.read()
        return json.loads(raw.decode("utf-8")) if raw else None


def upload(url, path):
    """流式上传（Content-Length 固定长度，不走 chunked）。"""
    size = os.path.getsize(path)
    req = urllib.request.Request(url, method="POST")
    req.add_header("Authorization", "Bearer " + TOKEN)
    req.add_header("Accept", "application/vnd.github+json")
    req.add_header("User-Agent", "CKeyViewer-release")
    req.add_header("Content-Type", "application/octet-stream")
    req.add_header("Content-Length", str(size))
    with open(path, "rb") as f:
        req.data = f
        with urllib.request.urlopen(req, timeout=1800) as r:
            return json.loads(r.read().decode("utf-8"))


def with_retry(fn, tries=3, label=""):
    last = None
    for i in range(1, tries + 1):
        try:
            return fn()
        except urllib.error.HTTPError as e:
            detail = ""
            try:
                detail = e.read().decode("utf-8", "replace")[:400]
            except Exception:
                pass
            last = "HTTP %s %s\n%s" % (e.code, e.reason, detail)
            if e.code in (400, 401, 403, 404, 422):
                break                      # 这些重试也没用
        except Exception as e:
            last = "%s: %s" % (type(e).__name__, e)
        if i < tries:
            print("    %s 第 %d 次失败（%s），%d 秒后重试…" % (label, i, last, 5 * i))
            time.sleep(5 * i)
    raise RuntimeError(last)


def main():
    if not TOKEN:
        print("没找到 token。先设置环境变量 GITHUB_TOKEN（或 GH_TOKEN）再跑。")
        return 1

    missing = [a for a in ASSETS if not os.path.exists(os.path.join(RELEASE_DIR, a))]
    if missing:
        print("release/ 下缺少：" + "、".join(missing))
        print("先在 release/ 目录跑 tools/build_release.py 生成。")
        return 1

    # ---- 已存在就复用，不存在就建 ----
    print("== 查找 tag %s ==" % TAG)
    release = None
    try:
        release = api("GET", "/repos/%s/releases/tags/%s" % (REPO, TAG))
        print("   已存在 Release id=%s，直接复用" % release["id"])
    except urllib.error.HTTPError as e:
        if e.code != 404:
            print("   查询失败：HTTP %s" % e.code)
            return 1
        print("   还没有，创建新的 Release")

    if release is None:
        release = with_retry(lambda: api("POST", "/repos/%s/releases" % REPO, {
            "tag_name": TAG,
            "name": RELEASE_NAME,
            "body": BODY,
            "draft": False,
            "prerelease": False,
        }), label="创建 Release")
        print("   已创建：%s" % release["html_url"])

    # ---- 传资产（同名先删，方便反复跑）----
    upload_base = release["upload_url"].split("{")[0]
    existing = {a["name"]: a for a in release.get("assets", [])}

    for name in ASSETS:
        path = os.path.join(RELEASE_DIR, name)
        mb = os.path.getsize(path) / 1048576.0

        if name in existing:
            print("== %s（%.1f MB）已存在，先删除旧的 ==" % (name, mb))
            with_retry(lambda: api("DELETE", "/repos/%s/releases/assets/%d"
                                   % (REPO, existing[name]["id"])), label="删除旧资产")

        print("== 上传 %s（%.1f MB）==" % (name, mb))
        url = upload_base + "?name=" + urllib.parse.quote(name)
        info = with_retry(lambda: upload(url, path), label="上传")
        print("   ok  %s" % info.get("browser_download_url"))

    print()
    print("发布会话：%s" % release["html_url"])
    return 0


if __name__ == "__main__":
    sys.exit(main())
