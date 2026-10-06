#!/usr/bin/env bash
# 启动 KV、等一会儿、截图、再退出。用法: runshot.sh <输出png> [等待秒] [裁剪参数...]
set -u
APP="E:/dsh工作区/Keyviever/CKeyViewer/bin/Debug/net10.0-windows"
T="E:/dsh工作区/Keyviever/tools"
OUT="$1"; shift
WAIT="${1:-8}"; shift || true

cd "$APP" || exit 1
# 先把可能存在的锁屏/息屏顶掉，否则截图会全黑或只有壁纸
python "$T/unlock.py" >/dev/null 2>&1
dotnet CKeyViewer.dll >/dev/null 2>&1 &
PID=$!
sleep "$WAIT"
python "$T/unlock.py" >/dev/null 2>&1
python "$T/screenshot.py" "$OUT" "$@"
echo "--- LOG ---"
tail -10 ckv_error.log 2>/dev/null
kill $PID 2>/dev/null
sleep 1
exit 0
