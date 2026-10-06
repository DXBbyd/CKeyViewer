#!/usr/bin/env bash
# 启动 KV，按住某个键，截图，再退出。
# 用法: runhold.sh <输出png> <按键> <按住秒数> <截图前等待秒> [裁剪参数...]
set -u
APP="E:/dsh工作区/Keyviever/CKeyViewer/bin/Debug/net10.0-windows"
T="E:/dsh工作区/Keyviever/tools"
OUT="$1"; shift
KEY="$1"; shift
HOLD="$1"; shift
WAIT="$1"; shift

cd "$APP" || exit 1
python "$T/unlock.py" >/dev/null 2>&1
dotnet CKeyViewer.dll >/dev/null 2>&1 &
PID=$!
sleep 8

python "$T/press.py" "$KEY" "$HOLD" >/dev/null 2>&1 &
HOLDER=$!

sleep "$WAIT"
python "$T/unlock.py" >/dev/null 2>&1
python "$T/screenshot.py" "$OUT" "$@"

wait $HOLDER 2>/dev/null
echo "--- LOG ---"
tail -6 ckv_error.log 2>/dev/null
kill $PID 2>/dev/null
sleep 1
exit 0
