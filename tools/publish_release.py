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
TAG = os.environ.get("TAG", "v1.1.0")
VERSION = "1.1.0"
RELEASE_NAME = os.environ.get("RELEASE_NAME", "CKeyViewer " + VERSION)
TOKEN = os.environ.get("GITHUB_TOKEN") or os.environ.get("GH_TOKEN") or ""

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RELEASE_DIR = os.path.join(ROOT, "release")

ASSETS = [
    "CKeyViewerSetup-%s.exe" % VERSION,
    "CKeyViewer-%s-portable-win64.zip" % VERSION,
    "SHA256SUMS.txt",
]

BODY_FILE = os.path.join(ROOT, "docs", "RELEASE_NOTES_%s.md" % TAG)


def release_body():
    """Release notes 以 docs/RELEASE_NOTES_<tag>.md 为准，避免两处维护。"""
    for path in (BODY_FILE,
                 os.path.join(ROOT, "docs", "RELEASE_NOTES_v%s.md" % VERSION)):
        if os.path.exists(path):
            with open(path, "r", encoding="utf-8") as f:
                text = f.read().strip()
            if text:
                print("   用 %s 作为 Release notes（%d 字）" % (os.path.basename(path), len(text)))
                return text
    print("   没找到 Release notes 文件，用兜底文案")
    return ("CKeyViewer %s —— Windows 上的按键可视化覆盖层。\n\n"
            "下载 `CKeyViewerSetup-%s.exe` 安装，或用 `CKeyViewer-%s-portable-win64.zip` 便携版。\n\n"
            "**必须以管理员身份运行**，完整说明见 "
            "[README](https://github.com/DXBbyd/CKeyViewer#readme)。" % (VERSION, VERSION, VERSION))


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
        body = release_body()
        release = with_retry(lambda: api("POST", "/repos/%s/releases" % REPO, {
            "tag_name": TAG,
            "name": RELEASE_NAME,
            "body": body,
            "draft": False,
            "prerelease": False,
        }), label="创建 Release")
        print("   已创建：%s" % release["html_url"])
    elif "--update-body" in sys.argv:
        # 已经建好了，只把 notes 刷新一遍
        body = release_body()
        release = with_retry(lambda: api("PATCH", "/repos/%s/releases/%d" % (REPO, release["id"]),
                                         {"body": body}), label="更新 notes")
        print("   已更新 Release notes")

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
