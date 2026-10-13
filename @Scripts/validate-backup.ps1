#!/usr/bin/env pwsh
<#
.SYNOPSIS
Claude 백업 검증 도구

.DESCRIPTION
백업 파일의 무결성을 검증합니다.
- 압축 파일 인사이드 검증
- MANIFEST.json 해시 확인
- 누락 파일 감지
- 파일 크기 비교

.PARAMETER BackupFile
검증할 백업 파일 (tar.gz, 7z, 또는 디렉토리)

.PARAMETER Verbose
상세 출력 (기본값: $false)

.EXAMPLE
.\validate-backup.ps1 -BackupFile "E:\backup\claude_backup_2026-10-12_16-54.tar.gz"
백업 파일 검증

.EXAMPLE
.\validate-backup.ps1 -BackupFile "E:\backup\claude_backup_2026-10-12_16-54.tar.gz" -Verbose
상세 모드로 검증
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$BackupFile
)

# 함수
function Write-Status {
    param([string]$Message, [string]$Status = "info", [switch]$Verbose)
    if ($VerboseMode -or -not $Verbose) {
        $color = @{"info"="Cyan";"success"="Green";"warning"="Yellow";"error"="Red"}[$Status]
        Write-Host "[$(Get-Date -Format 'HH:mm:ss')] $Message" -ForegroundColor $color
    }
}

function Validate-Tar {
    param([string]$FilePath)

    Write-Status "TAR 파일 검증 중..." "info"

    try {
        tar -tzf $FilePath > $null 2>&1

        if ($LASTEXITCODE -eq 0) {
            Write-Status "TAR 파일 무결성: OK" "success"

            if ($VerboseMode) {
                Write-Host ""
                Write-Host "TAR 파일 내용:" -ForegroundColor Cyan
                tar -tzf $FilePath | Select-Object -First 20 | ForEach-Object {
                    Write-Host "  $_"
                }

                $fileCount = (tar -tzf $FilePath | Measure-Object).Count
                Write-Host "  ... (총 $fileCount 파일)"
            }

            return $true
        } else {
            Write-Status "TAR 파일 손상됨 (code: $LASTEXITCODE)" "error"
            return $false
        }
    } catch {
        Write-Status "TAR 검증 오류: $_" "error"
        return $false
    }
}

function Validate-7z {
    param([string]$FilePath)

    Write-Status "7z 파일 검증 중..." "info"

    if (-not (Get-Command 7z -ErrorAction SilentlyContinue)) {
        Write-Status "7z를 설치해주세요" "error"
        return $false
    }

    try {
        7z t $FilePath > $null 2>&1

        if ($LASTEXITCODE -eq 0) {
            Write-Status "7z 파일 무결성: OK" "success"
            return $true
        } else {
            Write-Status "7z 파일 손상됨 (code: $LASTEXITCODE)" "error"
            return $false
        }
    } catch {
        Write-Status "7z 검증 오류: $_" "error"
        return $false
    }
}

function Validate-Directory {
    param([string]$DirPath)

    Write-Status "디렉토리 구조 검증 중..." "info"

    $claudePath = Join-Path $DirPath ".claude"

    if (-not (Test-Path $claudePath)) {
        Write-Status ".claude 디렉토리 없음" "error"
        return $false
    }

    $required = @(
        "sessions"
        "projects"
    )

    $allExist = $true
    foreach ($dir in $required) {
        $dirPath = Join-Path $claudePath $dir
        if (Test-Path $dirPath) {
            Write-Status "  ✓ $dir/" "success" -Verbose
        } else {
            Write-Status "  ✗ $dir/ (누락)" "error"
            $allExist = $false
        }
    }

    return $allExist
}

function Validate-Manifest {
    param([string]$DirPath)

    Write-Status "MANIFEST.json 검증 중..." "info"

    $manifestFile = Join-Path $DirPath ".claude" "MANIFEST.json"

    if (-not (Test-Path $manifestFile)) {
        Write-Status "MANIFEST.json 없음 (검증 스킵)" "warning"
        return $true
    }

    try {
        $manifest = Get-Content $manifestFile | ConvertFrom-Json

        Write-Status "매니페스트 파일 수: $($manifest.files.Count)" "info"

        $verified = 0
        $missing = 0
        $mismatch = 0

        foreach ($fileInfo in $manifest.files) {
            $filePath = Join-Path $DirPath ".claude" $fileInfo.path

            if (Test-Path $filePath) {
                $actualHash = (Get-FileHash -Path $filePath -Algorithm SHA256).Hash

                if ($actualHash -eq $fileInfo.hash) {
                    $verified++
                    Write-Status "  ✓ $($fileInfo.path)" "success" -Verbose
                } else {
                    $mismatch++
                    Write-Status "  ✗ $($fileInfo.path) (해시 불일치)" "error"
                }
            } else {
                $missing++
                Write-Status "  ✗ $($fileInfo.path) (파일 없음)" "error"
            }
        }

        Write-Host ""
        Write-Status "검증 결과: $verified/$($manifest.files.Count) OK" "info"

        if ($mismatch -gt 0) {
            Write-Status "  ⚠️  해시 불일치: $mismatch 파일" "warning"
        }

        if ($missing -gt 0) {
            Write-Status "  ✗ 누락: $missing 파일" "error"
            return $false
        }

        $percent = [math]::Round(($verified / $manifest.files.Count) * 100, 1)

        if ($percent -ge 95) {
            Write-Status "백업 품질: 양호 ($percent%)" "success"
            return $true
        } elseif ($percent -ge 85) {
            Write-Status "백업 품질: 주의 ($percent%)" "warning"
            return $true
        } else {
            Write-Status "백업 품질: 불량 ($percent%)" "error"
            return $false
        }

    } catch {
        Write-Status "MANIFEST 검증 오류: $_" "error"
        return $false
    }
}

function Validate-FileSize {
    param([string]$FilePath)

    if (Test-Path $FilePath) {
        $size = (Get-Item $FilePath).Length
        $sizeMB = [math]::Round($size / 1MB, 2)

        Write-Status "백업 파일 크기: $sizeMB MB" "info"

        if ($sizeMB -lt 1) {
            Write-Status "⚠️  백업 파일이 매우 작음 (손상 의심)" "warning"
            return $false
        }

        return $true
    }

    return $true
}

# 메인 로직
$VerboseMode = $VerbosePreference -eq 'Continue'
if ((Split-Path $BackupFile -Leaf) -like 'ai_sessions_backup_*' -or
    ((Test-Path -LiteralPath $BackupFile -PathType Container) -and
     (Test-Path -LiteralPath (Join-Path $BackupFile 'MANIFEST.json')))) {
    . (Join-Path $PSScriptRoot 'session-python.ps1')
    $sessionPython = Get-SessionPython
    & $sessionPython (Join-Path $PSScriptRoot 'session-backup.py') verify $BackupFile
    exit $LASTEXITCODE
}

Write-Status "Claude 백업 검증 도구 시작"
Write-Host ""

# 1. 파일 존재 확인
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

Write-Status "백업 파일: $BackupFile"
Write-Status "백업 형식: $backupType"
Write-Host ""

# 2. 크기 검증
$sizeOK = Validate-FileSize -FilePath $BackupFile
Write-Host ""

# 3. 형식별 검증
$validationResults = @{}

switch ($backupType) {
    "tar" {
        $validationResults["TAR 무결성"] = Validate-Tar -FilePath $BackupFile
    }
    "7z" {
        $validationResults["7z 무결성"] = Validate-7z -FilePath $BackupFile
    }
    "directory" {
        $validationResults["디렉토리 구조"] = Validate-Directory -DirPath $BackupFile

        if ($validationResults["디렉토리 구조"]) {
            $validationResults["MANIFEST"] = Validate-Manifest -DirPath $BackupFile
        }
    }
    default {
        Write-Status "지원하지 않는 백업 형식: $backupType" "error"
        exit 1
    }
}

Write-Host ""
Write-Host "검증 결과 요약:" -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

$allPassed = $true
foreach ($result in $validationResults.GetEnumerator()) {
    $status = if ($result.Value) { "✓ PASS" } else { "✗ FAIL" }
    $color = if ($result.Value) { "Green" } else { "Red" }

    Write-Host "  $($result.Name): " -NoNewline
    Write-Host $status -ForegroundColor $color

    if (-not $result.Value) { $allPassed = $false }
}

Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
Write-Host ""

if ($allPassed) {
    Write-Status "✅ 백업 검증 성공! 복원 가능합니다." "success"
    exit 0
} else {
    Write-Status "❌ 백업 검증 실패! 복원 전 문제를 해결하세요." "error"
    exit 1
}
