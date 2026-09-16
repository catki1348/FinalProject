[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$Utf8NoBom = [System.Text.UTF8Encoding]::new($false)

$inputRaw = [Console]::In.ReadToEnd()
$data = $null
try { $data = $inputRaw | ConvertFrom-Json } catch {}

$projectDir = $env:CLAUDE_PROJECT_DIR
if (-not $projectDir) { $projectDir = (Get-Location).Path }

$claudeDir = Join-Path $projectDir ".claude"
if (-not (Test-Path $claudeDir)) {
    New-Item -ItemType Directory -Path $claudeDir -Force | Out-Null
}
$stateFile = Join-Path $claudeDir ".usage-state.json"

$modelName = $null
$usedPct = $null
$resetsAt = $null

if ($data) {
    if ($data.model -and $data.model.display_name) { $modelName = $data.model.display_name }
    if ($data.rate_limits -and $data.rate_limits.five_hour) {
        $usedPct = $data.rate_limits.five_hour.used_percentage
        $resetsAt = $data.rate_limits.five_hour.resets_at
    }
}

if ($null -ne $usedPct) {
    $state = @{
        usedPercentage = $usedPct
        resetsAt       = $resetsAt
        updatedAt      = (Get-Date).ToString("o")
    }
    try {
        [System.IO.File]::WriteAllText($stateFile, ($state | ConvertTo-Json -Compress), $Utf8NoBom)
    } catch {}
}

$parts = @()
if ($modelName) { $parts += $modelName }
if ($null -ne $usedPct) {
    $remaining = [math]::Round(100 - $usedPct, 1)
    $parts += "5h left ${remaining}%"
}
if ($parts.Count -eq 0) { $parts += "Claude Code" }

Write-Output ($parts -join " | ")
