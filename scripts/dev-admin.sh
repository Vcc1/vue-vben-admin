#!/usr/bin/env bash
# 启动 admin 前端开发服务器，先释放 5101 端口避免 Vite 漂移到 5102
set -euo pipefail

PORT=5101
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
UI_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

# 杀掉占用 5101 的进程（兼容 Git Bash / Windows）
PIDS="$(netstat -ano | grep -E ":${PORT}[[:space:]]" | awk '{print $NF}' | sort -u || true)"
if [ -n "$PIDS" ]; then
  for PID in $PIDS; do
    if [ "$PID" != "0" ]; then
      echo "释放端口 ${PORT}，终止进程 PID=${PID}"
      taskkill //PID "$PID" //F >/dev/null 2>&1 || true
    fi
  done
  sleep 1
else
  echo "端口 ${PORT} 空闲"
fi

cd "$UI_DIR"
echo "启动 @vben/admin (http://localhost:${PORT})"
exec pnpm -F @vben/admin run dev
