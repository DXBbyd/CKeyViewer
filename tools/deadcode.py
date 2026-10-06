"""粗粒度死代码扫描：找出「只出现在自己声明处」的类型与成员。

用法: python tools/deadcode.py

做法是纯文本统计：把项目的 .cs 全文拼起来，对每个声明的名字数出现次数。
只有 1 次（就是声明本身）说明没人引用。
启发式，不是编译器 —— 反射、XAML、接口实现、事件订阅都可能造成误报，
所以结果要人工过一遍。
"""
import os
import re
import sys
from collections import Counter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "CKeyViewer")

TYPE_RE = re.compile(
    r"^\s*(?:public|internal|private|protected)?\s*(?:sealed\s+|static\s+|abstract\s+|partial\s+)*"
    r"(class|struct|enum|interface)\s+([A-Za-z_]\w*)", re.M)

MEMBER_RE = re.compile(
    r"^\s*(?:public|internal|private|protected)\s+"
    r"(?:static\s+|sealed\s+|override\s+|virtual\s+|async\s+|readonly\s+|const\s+|new\s+|extern\s+)*"
    r"(?:[A-Za-z_][\w<>,\[\]\.\?]*\s+)+"
    r"([A-Za-z_]\w*)\s*(?:\(|\{|=>|;)", re.M)


def collect():
    files = []
    for base, _dirs, names in os.walk(SRC):
        if any(p in base for p in ("\\obj", "\\bin")):
            continue
        for n in names:
            if n.endswith(".cs"):
                files.append(os.path.join(base, n))
    return files


def main():
    files = collect()
    blob = ""
    per_file = {}
    for f in files:
        text = open(f, encoding="utf-8").read()
        per_file[f] = text
        blob += text

    words = re.findall(r"[A-Za-z_]\w*", blob)
    counts = Counter(words)

    types = []
    members = []
    for f, text in per_file.items():
        rel = os.path.relpath(f, ROOT)
        for m in TYPE_RE.finditer(text):
            types.append((m.group(2), rel))
        for m in MEMBER_RE.finditer(text):
            members.append((m.group(1), rel))

    skip_names = {
        "Main", "ToString", "Equals", "GetHashCode", "Dispose", "GetEnumerator",
        "InitializeComponent", "OnRender", "MeasureOverride", "ArrangeOverride",
    }

    print("== 类型（只出现 1 次 = 没人用）==")
    for name, rel in sorted(set(types)):
        c = counts.get(name, 0)
        if c <= 1 and name not in skip_names:
            print("  %-28s %s" % (name, rel))

    print("\n== 成员（只出现 1 次 = 没人用）==")
    seen = set()
    for name, rel in sorted(set(members)):
        if name in skip_names or name in seen:
            continue
        seen.add(name)
        c = counts.get(name, 0)
        if c <= 1 and not name.startswith("_") and len(name) > 2:
            print("  %-32s %s" % (name, rel))

    print("\n共扫描 %d 个 .cs 文件" % len(files))
    return 0


if __name__ == "__main__":
    sys.exit(main())
