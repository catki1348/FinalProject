param([Parameter(Mandatory=$true)][string]$Manifest)
# 워크플로우가 반환한 이미지 URL만 프로젝트 작업 폴더 안에 내려받는다.
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($PSScriptRoot)
$entries = Get-Content -LiteralPath $Manifest -Raw | ConvertFrom-Json
foreach ($entry in $entries) {
    if (-not $entry.url.StartsWith('https://3d.varco.ai/api/objects/')) { throw 'Unexpected image host' }
    $target = [IO.Path]::GetFullPath((Join-Path $root $entry.path))
    if (-not $target.StartsWith($root + [IO.Path]::DirectorySeparatorChar)) { throw 'Output outside work folder' }
    New-Item -ItemType Directory -Force -Path ([IO.Path]::GetDirectoryName($target)) | Out-Null
    Invoke-WebRequest -Uri $entry.url -OutFile $target -UseBasicParsing
    Write-Output $entry.path
}
