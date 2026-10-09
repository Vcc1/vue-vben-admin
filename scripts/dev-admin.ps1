# Start admin dev server, freeing port 5101 first so Vite stays on 5101
$ErrorActionPreference = 'Stop'

$Port = 5101
$UiDir = Split-Path -Parent $PSScriptRoot

function Get-PortPids([int]$targetPort) {
    $pids = @()
    # Only LISTENING sockets -> the server holding the port, not connected clients
    try {
        $conns = Get-NetTCPConnection -LocalPort $targetPort -State Listen -ErrorAction SilentlyContinue
        if ($conns) {
            $pids += $conns | Select-Object -ExpandProperty OwningProcess
        }
    } catch {}
    # netstat fallback (catches IPv6 listeners reliably)
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
Write-Host "Starting @vben/admin (http://localhost:$Port)"
pnpm -F '@vben/admin' run dev
