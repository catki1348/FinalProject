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

function Test-FlagStillValid($flagPath) {
    if (-not (Test-Path $flagPath)) { return $false }
    try {
        $content = (Get-Content $flagPath -Raw).Trim()
        $resetTime = [DateTime]::Parse($content)
        return (Get-Date) -lt $resetTime
    } catch {
        return $false
    }
}

# Fast local path: only used when a statusLine command in this surface actually keeps
# .claude/.usage-state.json fresh (confirmed not to happen in the Desktop app Code tab,
# but kept for plain terminal/CLI sessions where statusLine does run).
if (Test-Path $stateFile) {
    $state = $null
    try { $state = Get-Content $stateFile -Raw | ConvertFrom-Json } catch {}
    if ($state) {
        $ageMinutes = ((Get-Date) - [DateTime]::Parse($state.updatedAt)).TotalMinutes
        if ($ageMinutes -le 20 -and $null -ne $state.usedPercentage) {
            if ((Get-Date) -gt [DateTime]::Parse($state.resetsAt) -and (Test-Path $flagFile)) {
                Remove-Item $flagFile -Force -ErrorAction SilentlyContinue
            }
            if ($state.usedPercentage -ge 90 -and -not (Test-FlagStillValid $flagFile)) {
                Set-Content -Path $flagFile -Value $state.resetsAt -Encoding utf8
                $remaining = [math]::Round(100 - $state.usedPercentage, 1)
                $reason = "[Auto-notice] Only about ${remaining}% of the 5-hour usage limit remains. Before ending this turn, follow .claude/commands/handoff.md (this now also stages and commits the working tree automatically as its last step). Then briefly report that in Korean and finish."
                Write-Output (@{ decision = "block"; reason = $reason } | ConvertTo-Json -Compress)
            }
            exit 0
        }
    }
}

# Fallback path: no fresh local usage data (this surface does not invoke statusLine),
# so ask Claude to check the account's real usage via the get_usage tool itself.
# Two independent triggers share one flag/cooldown: the 5-hour rate limit running out,
# OR this session's own context window running low (remaining tokens <=10%, i.e.
# percentUsed >= 90) even while the 5-hour limit is nowhere close to 90.
$reason = "[Auto usage check] Call mcp__ccd_session_mgmt__get_usage once. From its result, check BOTH: (A) the 'plan.windows' entry labeled '5-hour limit' has percentUsed >= 90, and (B) 'context.percentUsed' (this session's own context window usage) >= 90. If (A OR B) is true AND (.claude/.usage-handoff-flag is missing, unreadable, or its stored timestamp is already in the past): follow .claude/commands/handoff.md to write/update HANDOFF.md at the project root in Korean (this command now also stages and commits the working tree automatically as its last step - do not push), then overwrite .claude/.usage-handoff-flag with the '5-hour limit' window's resetsAt timestamp as plain text (use that same timestamp as the cooldown marker even if only (B) triggered). Otherwise, do nothing extra and just finish normally."
Write-Output (@{ decision = "block"; reason = $reason } | ConvertTo-Json -Compress)
exit 0
