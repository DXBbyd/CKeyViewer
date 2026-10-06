#!/usr/bin/env bash
#把 .NET / NuGet 的缓存从 C 盘挪到 E 盘。
#
# 为什么需要它：dotnet build 默认把还原出来的包全塞进
#   C:\Users\<你>\.nuget\packages        （全局包目录，实测 317 MB）
#   C:\Users\<你>\AppData\Local\NuGet    （HTTP 缓存 + 插件，实测 446 MB）
# 这个工程每次构建都会往那两处写。C 盘空间紧张的话，
# 跑构建之前先 source 这个脚本，三个变量一设，缓存就全落 E 盘了。
#
# 用法：
#   source tools/env.sh
#   dotnet build -c Debug
#
# 一次性生效、不想每次 source，也可以写进用户环境变量：
#   setx NUGET_PACKAGES          "E:\dsh工作区\.cache\nuget-packages"
#   setx NUGET_HTTP_CACHE_PATH   "E:\dsh工作区\.cache\nuget-http"
#   setx NUGET_PLUGINS_CACHE_PATH "E:\dsh工作区\.cache\nuget-plugins"
#
# 注意：这三个目录**不要**进 git，也不要放进工程的 .gitignore ——
# 它们根本不在仓库里（仓库根是 E:\dsh工作区\Keyviever，缓存在它的上一级）。

# 仓库根（脚本所在目录的上一级）
_ckv_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
_ckv_cache="$(dirname "$_ckv_root")/.cache"

export NUGET_PACKAGES="$_ckv_cache/nuget-packages"
export NUGET_HTTP_CACHE_PATH="$_ckv_cache/nuget-http"
export NUGET_PLUGINS_CACHE_PATH="$_ckv_cache/nuget-plugins"

#顺手关掉两个每次都要往用户目录写东西的开关
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

unset _ckv_root _ckv_cache

cat <<'EOF'
NuGet 缓存已改道到 E 盘：
  NUGET_PACKAGES           = E:\dsh工作区\.cache\nuget-packages
  NUGET_HTTP_CACHE_PATH    = E:\dsh工作区\.cache\nuget-http
  NUGET_PLUGINS_CACHE_PATH = E:\dsh工作区\.cache\nuget-plugins
EOF