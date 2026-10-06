#!/usr/bin/env bash
# 打包 CKeyViewer 安装程序。
#
#   1. 发布主程序（自包含单文件，win-x64）
#   2. 把它放进 setup/payload/ —— 安装程序会把它作为嵌入资源打进自己
#   3. 发布安装程序本身（自包含单文件 + 整包压缩）
#   4. 产物：release/setup/CKeyViewerSetup.exe
#
# 用法：tools/build_setup.sh [输出目录]
#
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="${1:-$ROOT/release}"
DOTNET="${DOTNET:-dotnet}"

echo "== 1/4 发布主程序（自包含单文件，win-x64）=="
rm -rf "$OUT/app"
"$DOTNET" publish "$ROOT/CKeyViewer/CKeyViewer.csproj" \
  -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:DebugType=none -p:DebugSymbols=false \
  -o "$OUT/app"

echo
echo "== 2/4 放入安装程序的 payload =="
mkdir -p "$ROOT/setup/payload"
cp -f "$OUT/app/CKeyViewer.exe" "$ROOT/setup/payload/CKeyViewer.exe"
ls -la "$ROOT/setup/payload/"

echo
echo "== 3/4 发布安装程序（自包含单文件 + 整包压缩）=="
rm -rf "$OUT/setup"
"$DOTNET" publish "$ROOT/setup/CKeyViewerSetup.csproj" \
  -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -p:DebugType=none -p:DebugSymbols=false \
  -o "$OUT/setup"

echo
echo "== 4/4 产物 =="
ls -la "$OUT/setup/CKeyViewerSetup.exe"
echo
echo "装到本机默认位置：  \"$OUT/setup/CKeyViewerSetup.exe\" --silent"
echo "自定义目录：        \"$OUT/setup/CKeyViewerSetup.exe\" --dir=D:\\Apps\\CKeyViewer"
