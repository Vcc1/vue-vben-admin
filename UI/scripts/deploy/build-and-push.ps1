# 通用前端镜像：构建 + 版本/时间戳标签 + 推送私有仓库
# 适用 @vben/admin 与 @vben/paycardhub。原生 PowerShell，无需 Git Bash / WSL，仅依赖 docker CLI。
# 用法：
#   powershell -ExecutionPolicy Bypass -File scripts/deploy/build-and-push.ps1 -App paycardhub -Target prod
#   powershell -ExecutionPolicy Bypass -File scripts/deploy/build-and-push.ps1 -App paycardhub -Target test
#   powershell -ExecutionPolicy Bypass -File scripts/deploy/build-and-push.ps1 -App admin      -Target prod
#   powershell -ExecutionPolicy Bypass -File scripts/deploy/build-and-push.ps1 -App admin      -Target test
#   powershell -ExecutionPolicy Bypass -File scripts/deploy/build-and-push.ps1 -ConfigFile path/to/custom.env
# 配置文件命名约定：release.<App>.<Target>.env（与本脚本同目录）
[CmdletBinding()]
param(
  # 本地 vue app：admin | paycardhub
  [ValidateSet('admin', 'paycardhub')]
  [string]$App = 'paycardhub',
  # 发布目标：prod=正式 | test=测试
  [ValidateSet('prod', 'test')]
  [string]$Target = 'prod',
  # 直接指定配置文件路径（优先级高于 -App/-Target）
  [string]$ConfigFile
)

$ErrorActionPreference = 'Stop'

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
# scripts/deploy -> UI 仓库根目录
$RepoRoot = (Resolve-Path (Join-Path $ScriptDir '..\..')).Path
if (-not $ConfigFile) { $ConfigFile = Join-Path $ScriptDir "release.$App.$Target.env" }
if (-not (Test-Path -LiteralPath $ConfigFile)) { throw "找不到配置文件: $ConfigFile" }
Write-Host ">> App=$App  Target=$Target  配置文件: $ConfigFile"

# 解析 KEY=value 配置（忽略空行与 # 注释，去除首尾引号）
# 必须显式按 UTF-8 读取：Windows PowerShell 5.1 默认用 GBK，会把 UTF-8 中文注释
# 的尾字节与下一行首字符配对，导致紧随注释之后的配置行被吞掉。
$cfg = @{}
foreach ($line in Get-Content -LiteralPath $ConfigFile -Encoding UTF8) {
  $t = $line.Trim()
  if (-not $t -or $t.StartsWith('#')) { continue }
  $idx = $t.IndexOf('=')
  if ($idx -lt 1) { continue }
  $k = $t.Substring(0, $idx).Trim()
  $v = $t.Substring($idx + 1).Trim() -replace '^"(.*)"$', '$1' -replace '^''(.*)''$', '$1'
  $cfg[$k] = $v
}

function Get-Cfg([string]$key, [bool]$required = $false, [string]$default = '') {
  $val = $cfg[$key]
  if ([string]::IsNullOrEmpty($val)) {
    if ($required) { throw "请在配置文件中设置 $key" }
    return $default
  }
  return $val
}

$Registry  = Get-Cfg 'DOCKER_REGISTRY' $true
$ImageName = Get-Cfg 'IMAGE_NAME' $true
$AppDir    = Get-Cfg 'APP_DIR' $true
$AppPkg    = Get-Cfg 'APP_PKG' $true
$TimeFmt   = Get-Cfg 'TAG_TIME_FORMAT' $false 'yyyyMMdd-HHmmss'
$Version   = Get-Cfg 'VERSION' $false ''
$ExtraTags = Get-Cfg 'EXTRA_TAGS' $false ''
$DoPush    = Get-Cfg 'PUSH' $false 'true'

# 版本标签：配置了 VERSION 就用它，否则用时间戳
if ($Version) { $Tag = $Version } else { $Tag = Get-Date -Format $TimeFmt }
$Repo  = $Registry.TrimEnd('/') + '/' + $ImageName
$Image = "${Repo}:${Tag}"

Set-Location $RepoRoot

$buildArgs = @(
  'build',
  '-f', 'scripts/deploy/Dockerfile.app',
  '--build-arg', "APP_DIR=$AppDir",
  '--build-arg', "APP_PKG=$AppPkg",
  '--build-arg', "VITE_GLOB_API_URL=$(Get-Cfg 'VITE_GLOB_API_URL')",
  '--build-arg', "VITE_OIDC_AUTHORITY=$(Get-Cfg 'VITE_OIDC_AUTHORITY')",
  '--build-arg', "VITE_OIDC_CLIENT_ID=$(Get-Cfg 'VITE_OIDC_CLIENT_ID')",
  '--build-arg', "VITE_OIDC_REDIRECT_URI=$(Get-Cfg 'VITE_OIDC_REDIRECT_URI')",
  '-t', $Image,
  '.'
)

Write-Host ">> 构建镜像 $Image  (app=$AppDir pkg=$AppPkg)"
& docker @buildArgs
if ($LASTEXITCODE -ne 0) { throw "docker build 失败 (exit $LASTEXITCODE)" }

# 收集所有需要推送的标签
$allRefs = @($Image)
if ($ExtraTags) {
  foreach ($t in $ExtraTags.Split(',')) {
    $tt = $t.Trim()
    if (-not $tt) { continue }
    $ref = "${Repo}:${tt}"
    & docker tag $Image $ref
    if ($LASTEXITCODE -ne 0) { throw "docker tag 失败: $ref" }
    $allRefs += $ref
  }
}

if ($DoPush -eq 'true') {
  foreach ($ref in $allRefs) {
    Write-Host ">> 推送 $ref"
    & docker push $ref
    if ($LASTEXITCODE -ne 0) { throw "docker push 失败: $ref (exit $LASTEXITCODE)" }
  }
}

Write-Host ">> 完成: $Image"
