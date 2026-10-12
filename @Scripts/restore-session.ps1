#!/usr/bin/env pwsh
<#
.SYNOPSIS
Claude 세션 백업 복원 도구

.DESCRIPTION
backup-session.ps1로 생성한 백업을 복원합니다.
경로 변경 (포맷 후 사용자명 변경 등)을 자동 처리합니다.

.PARAMETER BackupFile
복원할 백업 파일 경로 (tar.gz, 7z, 또는 디렉토리)

.PARAMETER Destination
복원 위치 (기본값: $env:USERPROFILE)

.PARAMETER UpdatePaths
경로 자동 업데이트 (기본값: $true)

.PARAMETER OldPath
이전 프로젝트 경로 (경로 변경 시)

.PARAMETER NewPath
새 프로젝트 경로 (경로 변경 시)

.PARAMETER DryRun
실제 복원하지 않고 시뮬레이션 (기본값: $false)

.EXAMPLE
.\restore-session.ps1 -BackupFile "E:\backup\claude_backup_2026-10-12_16-54.tar.gz"
백업 파일 복원

.EXAMPLE
.\restore-session.ps1 -BackupFile "E:\backup\claude_backup_2026-10-12_16-54.tar.gz" -UpdatePaths `
  -OldPath "C:\Users\old-user\Documents\FinalProject" `
  -NewPath "C:\Users\new-user\Documents\FinalProject"
경로 변경하여 복원
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$BackupFile,

    [string]$Destination = $env:USERPROFILE,
    [switch]$UpdatePaths,
    [string]$OldPath,
    [string]$NewPath,
    [switch]$DryRun
)

# 상수
$ClaudeHome = "$Destination\.claude"

# 함수
function Write-Status {
    param([string]$Message, [string]$Status = "info")
    $color = @{"info"="Cyan";"success"="Green";"warning"="Yellow";"error"="Red"}[$Status]
    Write-Host "[$(Get-Date -Format 'HH:mm:ss')] $Message" -ForegroundColor $color
}

function Backup-ExistingClaude {
    param([string]$DestPath)

    if (Test-Path $DestPath) {
        Write-Status "기존 .claude 백업 중..."
        $backupDir = "$DestPath.backup.$(Get-Date -Format 'yyyy-MM-dd_HH-mm-ss')"

        if (-not $DryRun) {
            Copy-Item -Path $DestPath -Destination $backupDir -Recurse -Force
        }

        Write-Status "기존 .claude 백업: $backupDir"
        return $backupDir
    }

    return $null
}

function Restore-FromTar {
    param([string]$FilePath, [string]$DestPath)

    Write-Status "TAR 압축 해제 중..."

    if ($DryRun) {
        Write-Status "[DRY-RUN] tar -xzf '$FilePath' -C '$DestPath'"
        return $true
    }

    try {
        tar -xzf $FilePath -C $DestPath

        if ($LASTEXITCODE -eq 0) {
            Write-Status "TAR 해제 성공" "success"
            return $true
        } else {
            Write-Status "TAR 해제 실패 (code: $LASTEXITCODE)" "error"
            return $false
        }
    } catch {
        Write-Status "TAR 해제 오류: $_" "error"
        return $false
    }
}

function Restore-From7z {
    param([string]$FilePath, [string]$DestPath)

    Write-Status "7z 압축 해제 중..."

    if (-not (Get-Command 7z -ErrorAction SilentlyContinue)) {
        Write-Status "7z를 설치해주세요" "error"
        return $false
    }

    if ($DryRun) {
        Write-Status "[DRY-RUN] 7z x '$FilePath' -o'$DestPath'"
        return $true
    }

    try {
        7z x $FilePath -o"$DestPath" | Out-Null

        if ($LASTEXITCODE -eq 0) {
            Write-Status "7z 해제 성공" "success"
            return $true
        } else {
            Write-Status "7z 해제 실패 (code: $LASTEXITCODE)" "error"
            return $false
        }
    } catch {
        Write-Status "7z 해제 오류: $_" "error"
        return $false
    }
}

function Restore-FromDirectory {
    param([string]$SourcePath, [string]$DestPath)

    Write-Status "디렉토리 복사 중..."

    if ($DryRun) {
        Write-Status "[DRY-RUN] Copy-Item -Path '$SourcePath\.claude' -Destination '$DestPath\.claude' -Recurse"
        return $true
    }

    try {
        $sourceClaude = Join-Path $SourcePath ".claude"
        Copy-Item -Path $sourceClaude -Destination "$DestPath\.claude" -Recurse -Force

        Write-Status "복사 성공" "success"
        return $true
    } catch {
        Write-Status "복사 오류: $_" "error"
        return $false
    }
}

function Update-ProjectPaths {
    param(
        [string]$ClaudePath,
        [string]$OldProjectPath,
        [string]$NewProjectPath
    )

    Write-Status "프로젝트 경로 업데이트 중..."
    Write-Status "  이전: $OldProjectPath"
    Write-Status "  새로: $NewProjectPath"

    # 1. JSON 파일 업데이트
    $jsonFiles = Get-ChildItem -Path (Join-Path $ClaudePath "sessions") -Filter "*.json"

    foreach ($file in $jsonFiles) {
        Write-Status "업데이트: $($file.Name)"

        $content = Get-Content $file.FullName | ConvertFrom-Json

        if ($content.cwd -like "*$OldProjectPath*") {
            $content.cwd = $content.cwd.Replace($OldProjectPath, $NewProjectPath)

            if (-not $DryRun) {
                $content | ConvertTo-Json | Out-File $file.FullName -Encoding UTF8
                Write-Status "  ✓ $($file.Name) 업데이트됨" "success"
            } else {
                Write-Status "  [DRY-RUN] $($file.Name) 업데이트 예정"
            }
        }
    }

    # 2. 프로젝트 폴더 이름 변경
    # (프로젝트 폴더명이 경로의 해시이므로 변경 필요)
    $oldHash = "$($OldProjectPath.GetHashCode())"
    $newHash = "$($NewProjectPath.GetHashCode())"

    # URL 인코딩된 디렉토리명도 고려
    Write-Status "프로젝트 폴더 구조 확인..."
    Get-ChildItem -Path (Join-Path $ClaudePath "projects") -Directory | ForEach-Object {
        if ($_.Name -like "*$OldProjectPath*") {
            Write-Status "프로젝트 폴더 찾음: $($_.Name)"
        }
    }

    Write-Status "경로 업데이트 완료" "success"
}

function Verify-Manifest {
    param([string]$ClaudePath)

    Write-Status "매니페스트 검증 중..."

    $manifestFile = Join-Path $ClaudePath "MANIFEST.json"

    if (Test-Path $manifestFile) {
        $manifest = Get-Content $manifestFile | ConvertFrom-Json
        Write-Status "매니페스트 발견: $($manifest.files.Count) 파일"

        $verified = 0
        foreach ($fileInfo in $manifest.files) {
            $filePath = Join-Path $ClaudePath $fileInfo.path

            if (Test-Path $filePath) {
                $actualHash = (Get-FileHash -Path $filePath -Algorithm SHA256).Hash

                if ($actualHash -eq $fileInfo.hash) {
                    $verified++
                } else {
                    Write-Status "해시 불일치: $($fileInfo.path)" "warning"
                }
            } else {
                Write-Status "파일 없음: $($fileInfo.path)" "warning"
            }
        }

        $percent = [math]::Round(($verified / $manifest.files.Count) * 100, 1)
        Write-Status "검증: $verified/$($manifest.files.Count) 파일 OK ($percent%)" "success"

        return $percent -ge 95
    } else {
        Write-Status "매니페스트 파일 없음 (검증 스킵)" "warning"
        return $true
    }
}

# 메인 로직
Write-Status "Claude 세션 복원 도구 시작"

if ($DryRun) {
    Write-Status "[DRY-RUN 모드] 실제 복원하지 않습니다" "warning"
}

# 1. 백업 파일 검증
if (-not (Test-Path $BackupFile)) {
    Write-Status "백업 파일을 찾을 수 없음: $BackupFile" "error"
    exit 1
}

$backupType = if ($BackupFile -like "*.tar.gz") {
    "tar"
} elseif ($BackupFile -like "*.7z") {
    "7z"
} elseif ((Get-Item $BackupFile).PSIsContainer) {
    "directory"
} else {
    "unknown"
}

Write-Status "백업 파일: $BackupFile (형식: $backupType)"

# 2. 기존 .claude 백업
Backup-ExistingClaude -DestPath $ClaudeHome

# 3. 복원
$restoreSuccess = $false

switch ($backupType) {
    "tar" { $restoreSuccess = Restore-FromTar -FilePath $BackupFile -DestPath $Destination }
    "7z" { $restoreSuccess = Restore-From7z -FilePath $BackupFile -DestPath $Destination }
    "directory" { $restoreSuccess = Restore-FromDirectory -SourcePath $BackupFile -DestPath $Destination }
    default {
        Write-Status "지원하지 않는 백업 형식: $backupType" "error"
        exit 1
    }
}

if (-not $restoreSuccess) {
    Write-Status "복원 실패" "error"
    exit 1
}

# 4. 경로 업데이트 (필요시)
if ($UpdatePaths -and $OldPath -and $NewPath) {
    Update-ProjectPaths -ClaudePath $ClaudeHome -OldProjectPath $OldPath -NewProjectPath $NewPath
}

# 5. 검증
Write-Status "최종 검증..."
$verifyResult = Verify-Manifest -ClaudePath $ClaudeHome

# 6. 완료
if ($verifyResult) {
    Write-Status "✅ 복원 완료!" "success"
    Write-Host ""
    Write-Host "복원된 Claude 홈: $ClaudeHome"
    Write-Host "복원된 세션 수: $(((Get-ChildItem (Join-Path $ClaudeHome 'sessions') -Filter '*.json').Count))"
} else {
    Write-Status "⚠️ 복원 완료했으나 일부 검증 실패" "warning"
    Write-Status "수동으로 파일을 확인해주세요" "warning"
}

if ($DryRun) {
    Write-Host ""
    Write-Status "[DRY-RUN] 실제 복원을 수행하려면 -DryRun을 제거하세요" "info"
}
