# Start admin dev server proxying to PRODUCTION API (server-side, no CORS), freeing port 5101 first
$ErrorActionPreference = 'Stop'

$Port = 5101
$UiDir = Split-Path -Parent $PSScriptRoot

# 代理目标指到线上 Admin.Host，前端仍请求同源 /api，由 vite 服务端转发，规避浏览器跨域
$env:VITE_PROXY_TARGET = 'http://adminhost.v.m-tu.com'

function Get-PortPids([int]$targetPort) {
    $pids = @()
    try {
        $conns = Get-NetTCPConnection -LocalPort $targetPort -State Listen -ErrorAction SilentlyContinue
        if ($conns) {
            $pids += $conns | Select-Object -ExpandProperty OwningProcess
        }
    } catch {}
    $lines = netstat -ano | Select-String ":$targetPort\s.*LISTENING"
    foreach ($line in $lines) {
        $parts = ($line -split '\s+') | Where-Object { $_ -ne '' }
        if ($parts.Count -gt 0) {
            $procId = $parts[-1]
            if ($procId -match '^\d+$') { $pids += [int]$procId }
        }
    }
    return $pids | Where-Object { $_ -ne 0 } | Sort-Object -Unique
}

$targetPids = Get-PortPids $Port
if ($targetPids) {
    foreach ($procId in $targetPids) {
        Write-Host "Freeing port $Port : killing PID=$procId"
        Stop-Process -Id $procId -Force -ErrorAction SilentlyContinue
    }
    Start-Sleep -Seconds 1
} else {
    Write-Host "Port $Port is free"
}

Set-Location $UiDir
Write-Host "Starting @vben/admin (dev mode, proxy -> $env:VITE_PROXY_TARGET)"
Write-Host "http://localhost:$Port"
pnpm -F '@vben/admin' run dev
