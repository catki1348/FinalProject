# Codex (GPT-6), 2026-10-16. Deliver the user's completion notice only to the matching resumed Claude session.
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)

# Claude Code supplies the lifecycle event and session ID as JSON on stdin.
try { $hookInput = [Console]::In.ReadToEnd() | ConvertFrom-Json } catch { exit 0 }
if ($hookInput.session_id -ne '7a52062c-ef67-4a96-b8c3-e3fb1d05daa2') { exit 0 }
if ($hookInput.hook_event_name -ne 'SessionStart') { exit 0 }

# Read a project-local factual handoff; no transcript, credentials or other session is modified.
$projectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$noticePath = Join-Path $projectRoot '@Edit/Claude_Map_N-2_CompletionNotice_20261016.md'
$notice = [IO.File]::ReadAllText($noticePath, [Text.Encoding]::UTF8)
@{ hookSpecificOutput = @{ hookEventName = 'SessionStart'; additionalContext = $notice } } |
    ConvertTo-Json -Depth 4 -Compress
