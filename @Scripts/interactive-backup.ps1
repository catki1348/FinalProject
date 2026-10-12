#!/usr/bin/env pwsh
<#
.SYNOPSIS
대화형 AI 세션 선택 백업 도구

.DESCRIPTION
사용자가 선택한 특정 AI 세션들만 백업합니다.
Unity 프로젝트 파일은 포함하지 않습니다 (사용자가 별도 백업).
AI의 /backup 명령어에서 사용합니다.

.PARAMETER SessionIds
백업할 세션 ID 배열 (또는 쉼표 구분 문자열)

.PARAMETER BackupPath
백업 저장 경로

.PARAMETER Format
백업 형식: tar | 7z | copy

.PARAMETER Export
내보내기 포함 여부

.EXAMPLE
.\interactive-backup.ps1 -SessionIds "2812579a-ecee-485f-bc45-60f1e059f3bb","43c33153-e8e3-4494-afbd-9b2403e25664"
지정된 세션들만 백업

.EXAMPLE
.\interactive-backup.ps1 -SessionIds "all" -Export
모든 세션 백업 + Markdown 내보내기
#>

param(
    [Parameter(Mandatory=$true)]
    [string[]]$SessionIds,

    [string]$BackupPath,
    [ValidateSet('tar', '7z', 'copy')]
    [string]$Format = 'tar',
    [switch]$Export,
    [switch]$Verify
)

# 상수
$ClaudeHome = "$env:USERPROFILE\.claude"
$ProjectDir = Join-Path $ClaudeHome "projects" "C--Users-MBC-501-21-Documents-FinalProject"
$DefaultBackupRoot = "C:\Users\MBC-501-21\Documents\LHS"

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

function Get-AllSessions {
    param([string]$ProjectDirPath)

    $sessions = @()

    Get-ChildItem -Path $ProjectDirPath -Directory -Force |
        Where-Object {$_.Name -notlike "memory"} |
        ForEach-Object {
            $sessionId = $_.Name
            $metaFiles = Get-ChildItem -Path (Join-Path $ClaudeHome "sessions") -Filter "*.json"

            foreach ($metaFile in $metaFiles) {
                $meta = Get-Content $metaFile | ConvertFrom-Json

                if ($meta.sessionId -eq $sessionId) {
                    $sessions += @{
                        id       = $sessionId
                        pid      = $meta.pid
                        name     = $meta.name
                        cwd      = $meta.cwd
                        status   = $meta.status
                        startedAt = $meta.startedAt
                        updatedAt = $meta.updatedAt
                    }
                    break
                }
            }
        }

    return $sessions
}

function Get-SessionName {
    param([string]$SessionId)

    $metaFiles = Get-ChildItem -Path (Join-Path $ClaudeHome "sessions") -Filter "*.json"

    foreach ($metaFile in $metaFiles) {
        $meta = Get-Content $metaFile | ConvertFrom-Json

        if ($meta.sessionId -eq $SessionId) {
            return $meta.name
        }
    }

    return $SessionId.Substring(0, 8)
}

function Backup-SingleSession {
    param(
        [string]$SessionId,
        [string]$DestPath
    )

    Write-Status "세션 백업 중: $SessionId..."

    $sourceClaude = Join-Path $env:TEMP "claude_backup_$([guid]::NewGuid().ToString().Substring(0, 8))"
    New-Item -ItemType Directory -Path $sourceClaude -Force | Out-Null

    # 세션 데이터만 복사
    $sessionFolder = Join-Path $ProjectDir $SessionId
    $sessionLog = Join-Path $ProjectDir "$SessionId.jsonl"

    if (Test-Path $sessionFolder) {
        Copy-Item -Path $sessionFolder -Destination (Join-Path $sourceClaude $SessionId) -Recurse -Force
    }

    if (Test-Path $sessionLog) {
        Copy-Item -Path $sessionLog -Destination (Join-Path $sourceClaude "$SessionId.jsonl") -Force
    }

    # 메타데이터 복사
    $metaDir = Join-Path $sourceClaude "sessions"
    New-Item -ItemType Directory -Path $metaDir -Force | Out-Null

    $metaFiles = Get-ChildItem -Path (Join-Path $ClaudeHome "sessions") -Filter "*.json"
    foreach ($metaFile in $metaFiles) {
        $meta = Get-Content $metaFile | ConvertFrom-Json

        if ($meta.sessionId -eq $SessionId) {
            Copy-Item -Path $metaFile.FullName -Destination (Join-Path $metaDir $metaFile.Name) -Force

            # 키 파일도 복사
            $keyFile = (Join-Path (Split-Path $metaFile.FullName) "$($metaFile.BaseName).*.key")
            Get-Item -Path $keyFile -Force -ErrorAction SilentlyContinue | ForEach-Object {
                Copy-Item -Path $_.FullName -Destination $metaDir -Force
            }
            break
        }
    }

    return $sourceClaude
}

function Create-SessionBackup {
    param(
        [string[]]$SessionIdList,
        [string]$DestPath
    )

    Write-Status "✓ 백업 데이터 준비 중..."

    $allData = Join-Path $env:TEMP "claude_backup_all_$([guid]::NewGuid().ToString().Substring(0, 8))"
    New-Item -ItemType Directory -Path $allData -Force | Out-Null

    foreach ($sessionId in $SessionIdList) {
        Write-Status "  - $((Get-SessionName $sessionId))..." "info"

        $sessionBackup = Backup-SingleSession -SessionId $sessionId -DestPath $DestPath

        # 병합
        Copy-Item -Path "$sessionBackup\*" -Destination $allData -Recurse -Force
        Remove-Item -Path $sessionBackup -Recurse -Force
    }

    return $allData
}

# 메인 로직
Write-Status "대화형 세션 백업 시작"

# 1. 모든 세션 확인
if ($SessionIds -contains "all") {
    Write-Status "모든 세션 선택..."
    $allSessions = Get-AllSessions -ProjectDirPath $ProjectDir
    $SessionIds = $allSessions.id
    Write-Status "총 $($SessionIds.Count)개 세션 선택됨" "success"
} else {
    Write-Status "$($SessionIds.Count)개 세션 선택됨" "success"
}

# 2. 세션 검증
$validSessions = @()
foreach ($id in $SessionIds) {
    $name = Get-SessionName -SessionId $id
    Write-Status "  ✓ $name ($id)" "success"
    $validSessions += $id
}

if ($validSessions.Count -eq 0) {
    Write-Status "유효한 세션이 없습니다" "error"
    exit 1
}

# 3. 백업 경로 생성
if (-not (Test-Path $BackupPath)) {
    Write-Status "백업 디렉토리 생성: $BackupPath"
    New-Item -ItemType Directory -Path $BackupPath -Force | Out-Null
}

# 4. 백업 생성
Write-Host ""
Write-Status "데이터 준비 중 (약 1-2분)..."

$tempData = Create-SessionBackup -SessionIdList $validSessions -DestPath $BackupPath
$Timestamp = Get-Date -Format "yyyy-MM-dd_HH-mm-ss"
$BackupName = "claude_backup_$($validSessions.Count)sessions_$Timestamp"
$BackupDestPath = Join-Path $BackupPath $BackupName

# 5. 압축
Write-Status "압축 중 ($Format 형식)..."

switch ($Format) {
    'tar' {
        $tarFile = "$BackupDestPath.tar.gz"
        tar -czf $tarFile -C $env:TEMP (Split-Path $tempData -Leaf)
        $backupFile = $tarFile
    }
    '7z' {
        $sevenZFile = "$BackupDestPath.7z"
        7z a -r $sevenZFile $tempData | Out-Null
        $backupFile = $sevenZFile
    }
    'copy' {
        Copy-Item -Path $tempData -Destination $BackupDestPath -Recurse -Force
        $backupFile = $BackupDestPath
    }
}

# 6. 정리
Remove-Item -Path $tempData -Recurse -Force

# 7. 내보내기 (선택사항)
if ($Export) {
    Write-Status "Markdown 내보내기 중..."

    foreach ($sessionId in $validSessions) {
        $sessionName = Get-SessionName -SessionId $sessionId
        $exportPath = Join-Path $BackupPath "export_$($sessionName.Replace(' ', '_'))"

        Write-Status "  - $sessionName 내보내기..."

        & ".\export-session.ps1" -SessionId $sessionId -OutputPath $exportPath -Format markdown
    }
}

# 8. 검증 (선택사항)
if ($Verify) {
    Write-Status "백업 검증 중..."
    & ".\validate-backup.ps1" -BackupFile $backupFile
}

# 9. 완료
$size = (Get-Item $backupFile).Length / 1MB
Write-Host ""
Write-Status "✅ 백업 완료!" "success"
Write-Host ""
Write-Host "📦 백업 정보:"
Write-Host "  파일: $backupFile"
Write-Host "  크기: $([math]::Round($size, 2)) MB"
Write-Host "  세션: $($validSessions.Count)개"
Write-Host ""
Write-Host "🔄 복원하려면:"
Write-Host "  .\restore-session.ps1 -BackupFile '$backupFile'"
