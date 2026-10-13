#!/usr/bin/env pwsh
<#
.SYNOPSIS
Claude + GPT(Codex) 전체 세션 통합 백업.
.DESCRIPTION
Claude 기록 및 Codex 대화 원본, 세션 목록, SQLite 스냅샷을 저장합니다.
Unity 파일과 로그인 인증 정보는 기본 제외합니다. 저장 후 SHA256 검증은 항상 실행합니다.
#>
param(
    [string]$BackupPath,
    [ValidateSet('tar', '7z', 'copy')][string]$Format = 'tar',
    [switch]$IncludeBackups,
    [switch]$ExcludeAuth = $true,
    [switch]$Verify,
    [string]$ClaudeHome = "$env:USERPROFILE\.claude",
    [string]$CodexHome = $(if ($env:CODEX_HOME) { $env:CODEX_HOME } else { "$env:USERPROFILE\.codex" })
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'session-python.ps1')
$sessionPython = Get-SessionPython
if (-not $BackupPath) {
    $candidates = @()
    if ($env:OneDrive) {
        $candidates += "$env:OneDrive\문서\LHS"
        $candidates += "$env:OneDrive\Documents\LHS"
    }
    $candidates += "$env:USERPROFILE\Documents\LHS"
    $candidates += "$env:USERPROFILE\Downloads\LHS"
    $BackupPath = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (-not $BackupPath) { $BackupPath = "$env:USERPROFILE\Documents\LHS" }
}
Write-Host 'Claude + GPT(Codex) 전체 세션 백업 (Unity 파일 제외)'
Write-Host "백업 경로: $BackupPath"
$backupArgs = @((Join-Path $PSScriptRoot 'session-backup.py'), 'backup', '--output', $BackupPath,
    '--format', $Format, '--claude-home', $ClaudeHome, '--codex-home', $CodexHome)
if ($IncludeBackups) { $backupArgs += '--include-backups' }
if (-not $ExcludeAuth) { $backupArgs += '--include-auth' }
& $sessionPython @backupArgs
if ($LASTEXITCODE -ne 0) { throw '통합 세션 백업 또는 검증 실패' }
Write-Host 'Claude + GPT(Codex) 백업 및 SHA256 검증 완료'
