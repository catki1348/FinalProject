#!/usr/bin/env pwsh
<#
.SYNOPSIS
Claude 세션을 Markdown/JSON으로 내보내기

.DESCRIPTION
세션의 메시지 로그를 읽을 수 있는 Markdown, JSON 형식으로 내보냅니다.
암호화 없음 (평문), 앱 없이 읽을 수 있습니다.

.PARAMETER SessionId
내보낼 세션 ID (UUID)

.PARAMETER ProjectPath
프로젝트 경로 (기본값: 현재 FinalProject)

.PARAMETER OutputPath
출력 디렉토리 (기본값: ./session_export_{sessionId})

.PARAMETER Format
내보내기 형식: markdown | json | both (기본값: both)

.EXAMPLE
.\export-session.ps1 -SessionId "2812579a-ecee-485f-bc45-60f1e059f3bb"
현재 프로젝트에서 해당 세션 내보내기

.EXAMPLE
.\export-session.ps1 -SessionId "xxx" -Format json
JSON 형식으로만 내보내기
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$SessionId,

    [string]$ProjectPath = (Join-Path $env:USERPROFILE "Documents\FinalProject"),
    [string]$OutputPath,
    [ValidateSet('markdown', 'json', 'both')]
    [string]$Format = 'both'
)

# 상수
$ClaudeHome = "$env:USERPROFILE\.claude"
$ProjectDir = Join-Path $ClaudeHome "projects" ($ProjectPath -replace '[:\\/]', '-')

if (-not $OutputPath) {
    $OutputPath = ".\session_export_$($SessionId.Substring(0, 8))"
}

# 함수
function Write-Status {
    param([string]$Message, [string]$Status = "info")
    $color = @{"info"="Cyan";"success"="Green";"warning"="Yellow";"error"="Red"}[$Status]
    Write-Host "[$(Get-Date -Format 'HH:mm:ss')] $Message" -ForegroundColor $color
}

function Parse-JSONL {
    param([string]$FilePath)

    $lines = @()
    if (-not (Test-Path $FilePath)) {
        Write-Status "파일 없음: $FilePath" "warning"
        return $lines
    }

    Get-Content $FilePath | ForEach-Object {
        try {
            $json = $_ | ConvertFrom-Json -ErrorAction Stop
            $lines += $json
        } catch {
            Write-Status "JSON 파싱 오류 (행 스킵): $_" "warning"
        }
    }

    return $lines
}

function Convert-To-Markdown {
    param([array]$Messages, [string]$SessionId, [string]$OutputPath)

    Write-Status "Markdown 생성 중..."

    $sb = New-Object System.Text.StringBuilder

    # 헤더
    $sb.AppendLine("# Claude 세션 기록") | Out-Null
    $sb.AppendLine("") | Out-Null
    $sb.AppendLine("**세션 ID**: `$SessionId`") | Out-Null
    $sb.AppendLine("**생성**: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss (UTC)')") | Out-Null
    $sb.AppendLine("**메시지 수**: $($Messages.Count)") | Out-Null
    $sb.AppendLine("") | Out-Null

    # 메시지 로그
    $sb.AppendLine("## 메시지 로그") | Out-Null
    $sb.AppendLine("") | Out-Null

    $messageIndex = 0
    foreach ($msg in $Messages) {
        $messageIndex++

        $timestamp = if ($msg.timestamp) { $msg.timestamp } else { "Unknown" }
        $type = if ($msg.type) { $msg.type } else { "unknown" }

        $sb.AppendLine("### $messageIndex. [$type] $timestamp") | Out-Null
        $sb.AppendLine("") | Out-Null

        # 타입별 처리
        switch ($type) {
            "user" {
                $content = if ($msg.content) { $msg.content } else { "(내용 없음)" }
                $sb.AppendLine("**사용자**: $content") | Out-Null
            }
            "assistant" {
                $content = if ($msg.content) { $msg.content } else { "(내용 없음)" }
                $sb.AppendLine("**Claude**: $content") | Out-Null
            }
            "attachment" {
                $sb.AppendLine("📎 **첨부 파일**") | Out-Null
                if ($msg.fileName) { $sb.AppendLine("  - 파일: $($msg.fileName)") | Out-Null }
                if ($msg.size) { $sb.AppendLine("  - 크기: $($msg.size) bytes") | Out-Null }
            }
            "artifact" {
                $sb.AppendLine("🎨 **아티팩트**") | Out-Null
                if ($msg.url) { $sb.AppendLine("  - URL: $($msg.url)") | Out-Null }
                if ($msg.type) { $sb.AppendLine("  - 타입: $($msg.type)") | Out-Null }
            }
            default {
                $sb.AppendLine("*$(($msg | ConvertTo-Json -Compress))*") | Out-Null
            }
        }

        $sb.AppendLine("") | Out-Null
    }

    $mdFile = Join-Path $OutputPath "messages.md"
    $sb.ToString() | Out-File -FilePath $mdFile -Encoding UTF8

    Write-Status "Markdown 저장: $mdFile" "success"
}

function Export-Metadata {
    param([string]$SessionId, [string]$OutputPath)

    Write-Status "메타데이터 내보내기..."

    $metadataFile = Join-Path $ClaudeHome "sessions" "$($SessionId).json"

    if (Test-Path $metadataFile) {
        $metadata = Get-Content $metadataFile | ConvertFrom-Json

        $export = @{
            sessionId = $SessionId
            name = $metadata.name
            project = $metadata.cwd
            startedAt = $metadata.startedAt
            updatedAt = $metadata.updatedAt
            status = $metadata.status
            exportedAt = (Get-Date -Format "o")
        }

        $metaJsonFile = Join-Path $OutputPath "metadata.json"
        $export | ConvertTo-Json | Out-File -FilePath $metaJsonFile -Encoding UTF8

        Write-Status "메타데이터 저장: $metaJsonFile" "success"
    } else {
        Write-Status "메타데이터 파일 없음: $metadataFile" "warning"
    }
}

# 메인 로직
Write-Status "세션 내보내기 시작: $SessionId"

# 1. 검증
if (-not (Test-Path $ProjectDir)) {
    Write-Status "프로젝트 디렉토리 없음: $ProjectDir" "error"
    exit 1
}

# 2. 출력 디렉토리 생성
if (-not (Test-Path $OutputPath)) {
    New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
    Write-Status "출력 디렉토리 생성: $OutputPath"
}

# 3. JSONL 파일 읽기
$jsonlFile = Join-Path $ProjectDir "$SessionId.jsonl"
Write-Status "메시지 로그 읽기: $jsonlFile"

$messages = Parse-JSONL -FilePath $jsonlFile
Write-Status "메시지 수: $($messages.Count)"

# 4. 내보내기
if ($Format -in @('markdown', 'both')) {
    Convert-To-Markdown -Messages $messages -SessionId $SessionId -OutputPath $OutputPath
}

if ($Format -in @('json', 'both')) {
    Write-Status "JSON 내보내기 중..."
    $jsonFile = Join-Path $OutputPath "messages.json"
    $messages | ConvertTo-Json -Depth 10 | Out-File -FilePath $jsonFile -Encoding UTF8
    Write-Status "JSON 저장: $jsonFile" "success"
}

# 5. 메타데이터
Export-Metadata -SessionId $SessionId -OutputPath $OutputPath

# 6. 완료
Write-Status "✅ 내보내기 완료!" "success"
Write-Host ""
Write-Host "내보낸 파일:"
Get-ChildItem -Path $OutputPath | ForEach-Object {
    $size = if ($_.PSIsContainer) { "폴더" } else { "$([math]::Round($_.Length/1KB, 1)) KB" }
    Write-Host "  - $($_.Name) ($size)"
}
