[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)

$inputRaw = [Console]::In.ReadToEnd()
$hookInput = $null
try { $hookInput = $inputRaw | ConvertFrom-Json } catch {}

# Avoid infinite loop: if this Stop hook already forced a continuation, don't block again.
if ($hookInput -and $hookInput.stop_hook_active -eq $true) {
    exit 0
}

$projectDir = $env:CLAUDE_PROJECT_DIR
if (-not $projectDir) { $projectDir = (Get-Location).Path }

$claudeDir = Join-Path $projectDir ".claude"
$stateFile = Join-Path $claudeDir ".usage-state.json"
$flagFile = Join-Path $claudeDir ".usage-handoff-flag"
$debugLog = Join-Path $claudeDir ".stophook-debug.log"
try {
    $line = "$(Get-Date -Format o) invoked; stop_hook_active=$($hookInput.stop_hook_active)"
    Add-Content -Path $debugLog -Value $line -Encoding utf8
} catch {}

if (-not (Test-Path $stateFile)) { exit 0 }

$state = $null
try { $state = Get-Content $stateFile -Raw | ConvertFrom-Json } catch {}
if (-not $state) { exit 0 }

$usedPct = $state.usedPercentage
$resetsAt = $state.resetsAt

# Once the 5-hour window has reset, clear the flag so the next low-usage period can trigger again.
if ($resetsAt) {
    try {
        $resetTime = [DateTime]::Parse($resetsAt)
        if ((Get-Date) -gt $resetTime -and (Test-Path $flagFile)) {
            Remove-Item $flagFile -Force -ErrorAction SilentlyContinue
        }
    } catch {}
}

if ($null -eq $usedPct) { exit 0 }

$threshold = 90
if ($usedPct -ge $threshold -and -not (Test-Path $flagFile)) {
    New-Item -ItemType File -Path $flagFile -Force | Out-Null
    $remaining = [math]::Round(100 - $usedPct, 1)
    $reason = "[Auto-notice] Only about ${remaining}% of the 5-hour usage limit remains. Before ending this turn, follow the procedure in .claude/commands/handoff.md and write/update HANDOFF.md at the project root in Korean, summarizing current progress and next steps. Do not run git add or git commit. Once HANDOFF.md is written, briefly report that in Korean and then finish."
    $output = @{ decision = "block"; reason = $reason } | ConvertTo-Json -Compress
    Write-Output $output
}

exit 0
