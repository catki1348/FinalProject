#!/usr/bin/env pwsh
<#
.SYNOPSIS
AI 세션 백업 생성 도구

.DESCRIPTION
~/.claude 디렉토리의 AI 세션 데이터(대화 기록, 파일 히스토리)만 백업합니다.
Unity 프로젝트 파일은 포함하지 않습니다 (사용자가 별도 백업).

.PARAMETER BackupPath
백업 저장 위치 (기본값: $BackupRoot)

.PARAMETER Format
백업 형식: tar | 7z | copy (기본값: tar)

.PARAMETER IncludeBackups
기존 자동 백업 파일 포함 여부 (기본값: $false)

.PARAMETER ExcludeAuth
인증 정보 제외 (기본값: $true)

.EXAMPLE
.\backup-session.ps1
기본 설정으로 백업 생성

.EXAMPLE
.\backup-session.ps1 -BackupPath "D:\backup" -Format 7z
D:\backup에 7z 형식으로 저장
#>

param(
    [string]$BackupPath,
    [ValidateSet('tar', '7z', 'copy')]
    [string]$Format = 'tar',
    [switch]$IncludeBackups,
    [switch]$ExcludeAuth = $true,
    [switch]$Verify
)

# 상수
$ClaudeHome = "$env:USERPROFILE\.claude"

# LHS_ROOT 동적 결정 (PC 독립)
$lhsRootCandidates = @(
    if ($env:OneDrive) { "$env:OneDrive\문서\LHS" },
    if ($env:OneDrive) { "$env:OneDrive\Documents\LHS" },
    "$env:USERPROFILE\Documents\LHS",
    "$env:USERPROFILE\Downloads\LHS"
)

$DefaultBackupRoot = $lhsRootCandidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1

if (-not $DefaultBackupRoot) {
    $DefaultBackupRoot = "$env:USERPROFILE\Documents\LHS"
    New-Item -ItemType Directory -Path $DefaultBackupRoot -Force | Out-Null
}

$Timestamp = Get-Date -Format "yyyy-MM-dd_HH-mm-ss"
$BackupName = "claude_backup_$Timestamp"

if (-not $BackupPath) {
    $BackupPath = $DefaultBackupRoot
}

# 함수
function Write-Status {
    param([string]$Message, [string]$Status = "info")
    $color = @{
        "info"    = "Cyan"
        "success" = "Green"
        "warning" = "Yellow"
        "error"   = "Red"
    }[$Status]
    Write-Host "[$(Get-Date -Format 'HH:mm:ss')] $Message" -ForegroundColor $color
}

function Get-FileHash-SHA256 {
    param([string]$FilePath)
    $hash = (Get-FileHash -Path $FilePath -Algorithm SHA256).Hash
    return $hash
}

function Create-Manifest {
    param([string]$SourcePath, [string]$ManifestPath)

    Write-Status "매니페스트 생성 중..."

    $files = Get-ChildItem -Path $SourcePath -Recurse -Force
    $manifest = @{
        timestamp = Get-Date -Format "o"
        source = $SourcePath
        format = $Format
        files = @()
    }

    foreach ($file in $files | Where-Object {$_.PSIsContainer -eq $false}) {
        $relativePath = $file.FullName.Substring($SourcePath.Length + 1)
        $hash = Get-FileHash-SHA256 $file.FullName

        $manifest.files += @{
            path = $relativePath
            size = $file.Length
            hash = $hash
            modified = $file.LastWriteTimeUtc.ToString("o")
        }
    }

    $manifest | ConvertTo-Json -Depth 10 | Out-File -FilePath $ManifestPath -Encoding UTF8
    Write-Status "매니페스트 저장: $ManifestPath" "success"
}

function Backup-AsTar {
    param([string]$SourcePath, [string]$DestPath)

    Write-Status "TAR 압축 중 (이것은 시간이 걸릴 수 있습니다)..."

    try {
        $tarFile = "$DestPath.tar.gz"
        tar -czf $tarFile -C (Split-Path $SourcePath) (Split-Path $SourcePath -Leaf)

        if ($LASTEXITCODE -eq 0) {
            Write-Status "TAR 백업 완료: $tarFile" "success"
            return $tarFile
        } else {
            Write-Status "TAR 압축 실패 (code: $LASTEXITCODE)" "error"
            return $null
        }
    } catch {
        Write-Status "TAR 압축 오류: $_" "error"
        return $null
    }
}

function Backup-As7z {
    param([string]$SourcePath, [string]$DestPath)

    Write-Status "7z 압축 중..."

    if (-not (Get-Command 7z -ErrorAction SilentlyContinue)) {
        Write-Status "7z를 설치해주세요 (choco install 7zip)" "error"
        return $null
    }

    try {
        $sevenZFile = "$DestPath.7z"
        7z a -r $sevenZFile $SourcePath | Out-Null

        if ($LASTEXITCODE -eq 0) {
            Write-Status "7z 백업 완료: $sevenZFile" "success"
            return $sevenZFile
        } else {
            Write-Status "7z 압축 실패 (code: $LASTEXITCODE)" "error"
            return $null
        }
    } catch {
        Write-Status "7z 압축 오류: $_" "error"
        return $null
    }
}

function Backup-AsCopy {
    param([string]$SourcePath, [string]$DestPath)

    Write-Status "파일 복사 중..."

    try {
        Copy-Item -Path $SourcePath -Destination $DestPath -Recurse -Force
        Write-Status "복사 완료: $DestPath" "success"
        return $DestPath
    } catch {
        Write-Status "복사 오류: $_" "error"
        return $null
    }
}

# 메인 로직
Write-Status "AI 세션 백업 도구 시작"
Write-Status "📌 주의: AI 세션 데이터만 백업합니다 (Unity 파일 제외)"
Write-Status "기본 LHS_ROOT: $DefaultBackupRoot"
Write-Status "현재 백업 경로: $BackupPath"
Write-Status "백업 형식: $Format"

# 1. 검증
if (-not (Test-Path $ClaudeHome)) {
    Write-Status "Claude 홈을 찾을 수 없음: $ClaudeHome" "error"
    exit 1
}

# 2. 백업 디렉토리 생성
if (-not (Test-Path $BackupPath)) {
    Write-Status "백업 디렉토리 생성: $BackupPath"
    New-Item -ItemType Directory -Path $BackupPath -Force | Out-Null
}

# 3. 임시 디렉토리에 .claude 복사
$tempDir = Join-Path $env:TEMP "claude_backup_$([guid]::NewGuid().ToString().Substring(0, 8))"
Write-Status "임시 디렉토리: $tempDir"

New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

$sourceClaude = Join-Path $tempDir ".claude"
Copy-Item -Path $ClaudeHome -Destination $sourceClaude -Recurse -Force

# 4. 제외 항목 삭제
if ($ExcludeAuth) {
    Write-Status "인증 정보 제외 중..."
    $authFile = Join-Path $sourceClaude "auth.json"
    if (Test-Path $authFile) {
        Remove-Item -Path $authFile -Force
        Write-Status "auth.json 삭제됨"
    }
}

if (-not $IncludeBackups) {
    Write-Status "자동 백업 파일 제외 중..."
    $backupDir = Join-Path $sourceClaude "backups"
    if (Test-Path $backupDir) {
        Remove-Item -Path $backupDir -Recurse -Force
        Write-Status "backups/ 삭제됨"
    }
}

# 5. 매니페스트 생성
$manifestPath = Join-Path $sourceClaude "MANIFEST.json"
Create-Manifest -SourcePath $sourceClaude -ManifestPath $manifestPath

# 6. 백업 생성
$backupDestPath = Join-Path $BackupPath $BackupName
$backupFile = $null

switch ($Format) {
    'tar' {
        $backupFile = Backup-AsTar -SourcePath $sourceClaude -DestPath $backupDestPath
    }
    '7z' {
        $backupFile = Backup-As7z -SourcePath $sourceClaude -DestPath $backupDestPath
    }
    'copy' {
        $backupFile = Backup-AsCopy -SourcePath $sourceClaude -DestPath $backupDestPath
    }
}

# 7. 정리
Write-Status "임시 디렉토리 삭제 중..."
Remove-Item -Path $tempDir -Recurse -Force

# 8. 검증 (선택사항)
if ($Verify -and $backupFile) {
    Write-Status "백업 검증 중..."

    if ($Format -eq 'tar') {
        tar -tzf $backupFile > $null 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Status "TAR 검증 성공" "success"
        } else {
            Write-Status "TAR 검증 실패" "error"
        }
    } elseif ($Format -eq '7z') {
        7z t $backupFile > $null 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Status "7z 검증 성공" "success"
        } else {
            Write-Status "7z 검증 실패" "error"
        }
    }
}

# 9. 완료 보고
if ($backupFile) {
    $size = (Get-Item $backupFile).Length / 1MB
    Write-Status "✅ 백업 완료!" "success"
    Write-Host ""
    Write-Host "백업 정보:"
    Write-Host "  파일: $backupFile"
    Write-Host "  크기: $([math]::Round($size, 2)) MB"
    Write-Host "  타임스탐프: $Timestamp"
    Write-Host ""
    Write-Host "복원 명령:"

    if ($Format -eq 'tar') {
        Write-Host "  tar -xzf '$backupFile' -C `$HOME"
    } elseif ($Format -eq '7z') {
        Write-Host "  7z x '$backupFile' -o`$HOME"
    }
} else {
    Write-Status "❌ 백업 실패" "error"
    exit 1
}
