# AI 세션 백업 프로세스 — `/backup` 명령어 실행 절차

**버전**: 1.0  
**작성**: 2026-10-12  
**목표**: AI의 세션 데이터(대화 기록, 파일 히스토리)만 백업하여 포맷 후 복구 가능하게 함

⚠️ **주의**: Unity 프로젝트 파일은 백업하지 않습니다. 사용자가 별도로 백업하세요.

---

## 1️⃣ 사용자 입력

```
사용자: /backup
```

---

## 2️⃣ AI 단계별 처리

### 단계 1: 초기 메뉴 제시

```markdown
📦 AI 세션 백업을 시작합니다.

⚠️  이것은 **AI의 대화 기록, 파일 히스토리만 백업**합니다.
    Unity 프로젝트 파일은 포함하지 않습니다. (사용자가 별도 백업)

**백업 방식을 선택해주세요:**

1️⃣  **전체 세션 백업** (모든 AI 세션)
   - 모든 세션 메타데이터, 메시지 로그, 파일 히스토리 포함
   - 시간: ~1-2분, 용량: ~50-150 MB

2️⃣  **선택 세션 백업** (특정 세션만)
   - 백업할 세션을 직접 선택
   - 시간: 빠름, 용량: 가변

3️⃣  **세션 + Markdown 내보내기** (읽을 수 있는 형식)
   - 백업 후 각 세션을 Markdown으로 변환
   - 앱 없이 대화 기록 열기 가능
   - 시간: ~2-3분, 용량: ~100-250 MB

**선택해주세요 (1, 2, 또는 3):**
```

### 단계 2: 선택에 따른 분기

#### **경우 A: 전체 백업 선택 (1)**

```markdown
✅ **전체 세션 백업 선택**

**백업 위치:**
- 기본: C:\Users\MBC-501-21\Documents\LHS\
- 변경하려면 경로 입력 (또는 엔터 기본값)

**백업 형식:**
1️⃣  TAR (기본, 빠름)
2️⃣  7z (높은 압축)
3️⃣  복사 (압축 안 함)

**선택해주세요:**
```

→ 직진: **단계 5 (확인)**

---

#### **경우 B: 선택 세션 백업 (2)**

```markdown
✅ **선택 세션 백업 선택**

**현재 세션 목록:**
(세션 자동 로드)

1️⃣  수칙 추가
   - PID: 1732
   - 메시지: 150개
   - 시작: 2026-10-12 05:25

2️⃣  세부 조정
   - PID: 14760
   - 메시지: 280개
   - 시작: 2026-10-12 03:07

3️⃣  2D 맵 생성-메인
   - PID: 18904
   - 메시지: 1200개
   - 시작: 2026-10-11 15:30

...

**백업할 세션을 선택해주세요:**
(번호로 입력: "1,3" 또는 "1 3" 또는 "all")
```

사용자가 선택 후:

```markdown
📋 **선택된 세션:**
- ✓ 수칙 추가 (1732)
- ✓ 2D 맵 생성-메인 (18904)

**백업 위치 및 형식:**
(단계 2A와 동일)
```

→ 직진: **단계 5 (확인)**

---

#### **경우 C: 세션 + 내보내기 (3)**

```markdown
✅ **세션 + Markdown 내보내기 선택**

이 옵션은:
1️⃣  모든 세션 백업 (TAR)
2️⃣  각 세션을 Markdown으로 내보내기
3️⃣  메타데이터 + 메시지 로그 추출

**시간**: ~2-3분
**용량**: ~200-400 MB

진행할까요?
```

→ 직진: **단계 5 (확인)**

---

### 단계 3: 세션 목록 동적 로드

```powershell
# AI가 실행 (사용자에게 보이지 않음)
$projectDir = "$env:USERPROFILE\.claude\projects\C--Users-MBC-501-21-Documents-FinalProject"
$sessions = Get-ChildItem $projectDir -Directory | Where-Object {$_.Name -notlike "memory"}

foreach ($session in $sessions) {
    $metaFile = "$env:USERPROFILE\.claude\sessions\*.json" | 
                Where-Object {(Get-Content $_ | ConvertFrom-Json).sessionId -eq $session.Name}
    
    if (Test-Path $metaFile) {
        $meta = Get-Content $metaFile | ConvertFrom-Json
        Write-Output "$($meta.name) ($($meta.pid))"
    }
}
```

---

### 단계 4: 추가 옵션 (선택사항)

```markdown
**추가 옵션 (선택사항):**

☐ 백업 후 자동 검증
☐ 자동 외장 드라이브로 복사
☐ 백업 로그 저장

선택하려면 입력 (또는 엔터 스킵):
```

---

### 단계 5: 최종 확인

```markdown
🔍 **백업 설정 확인:**

**대상**: 선택된 세션 (N개)
**형식**: [TAR/7z/복사]
**위치**: [경로]
**내보내기**: [예/아니오]
**검증**: [예/아니오]

이 설정으로 백업을 시작할까요?

(네 = 엔터 또는 Y)
(아니오 = N 또는 처음부터)
```

---

### 단계 6: 백업 실행

```markdown
🔄 **백업 실행 중...**

[진행 상황]
✓ 임시 디렉토리 생성
✓ 세션 데이터 복사
  - 세션 1/3: 2812579a...
  - 세션 2/3: 43c33153...
  - 세션 3/3: f080a93b...
✓ 매니페스트 생성
✓ TAR 압축 중... (30%)
```

---

### 단계 7: 완료 및 보고

```markdown
✅ **백업 완료!**

**백업 정보:**
- 파일: C:\Users\...\Claude\backups\claude_backup_2026-10-12_16-54.tar.gz
- 크기: 145 MB
- 세션: 3개
- 메시지: 1,630개
- 소요 시간: 2분 15초

**다음 단계:**
1. ✓ 외장 드라이브에 복사하기
2. ✓ 정기 백업 설정 (Task Scheduler)
3. ✓ 3개월마다 검증

**검증하려면:**
```powershell
.\@Scripts\validate-backup.ps1 -BackupFile "경로" -Verbose
```
```

---

## 3️⃣ AI 구현 체크리스트

### 정보 수집 (AskUserQuestion 또는 대화)

- [ ] 백업 방식 선택 (1/2/3)
- [ ] 세션 선택 (경우 B인 경우)
- [ ] 백업 경로 (기본값 또는 사용자 입력)
- [ ] 백업 형식 (TAR/7z/복사)
- [ ] 추가 옵션 (검증/내보내기)

### 유효성 검증

- [ ] 세션이 실제로 존재하는가
- [ ] 백업 경로가 쓰기 가능한가
- [ ] 디스크 공간이 충분한가 (~500MB 권장)

### 스크립트 실행

```powershell
# 선택된 옵션에 따라 실행
if ($backupType -eq "전체") {
    .\@Scripts\backup-session.ps1 -Format $format -BackupPath $path -Verify
} elseif ($backupType -eq "선택") {
    .\@Scripts\interactive-backup.ps1 -SessionIds $selectedIds -Format $format
} elseif ($backupType -eq "내보내기") {
    .\@Scripts\backup-session.ps1 ...
    foreach ($id in $sessionIds) {
        .\@Scripts\export-session.ps1 -SessionId $id -Format markdown
    }
}
```

### 완료 처리

- [ ] 백업 파일 검증 (선택사항)
- [ ] 완료 메시지 표시
- [ ] 복원 명령어 제공
- [ ] 다음 백업 권장 시점 안내

---

## 4️⃣ 예외 처리

| 상황 | 처리 |
|------|------|
| 세션이 없음 | "세션을 찾을 수 없습니다. 먼저 Claude를 실행해주세요." |
| 디스크 부족 | "디스크 공간이 부족합니다 (필요: 500MB, 가용: 50MB)" |
| 권한 없음 | "백업 경로에 쓰기 권한이 없습니다." |
| 사용자 취소 | "백업이 취소되었습니다." |
| 백업 실패 | "백업 중 오류가 발생했습니다: [오류 메시지]" |

---

## 5️⃣ 사용자 입력 방식 (선택지)

### 방식 A: AskUserQuestion (권장)
```
구조화된 선택지 제시
장점: 명확, 오류 적음
단점: 복잡한 선택은 어려움
```

### 방식 B: 자유 입력 (대화)
```
"선택해주세요:"에 대해 사용자가 자유롭게 입력
장점: 유연함
단점: 입력 오류 처리 필요
```

### 권장: 방식 A (초기 메뉴) + 방식 B (세부)

---

## 6️⃣ 관련 스크립트

| 스크립트 | 용도 |
|---------|------|
| `backup-session.ps1` | 전체 세션 백업 |
| `interactive-backup.ps1` | 선택 세션 백업 (작성 필요) |
| `export-session.ps1` | Markdown 내보내기 |
| `validate-backup.ps1` | 백업 검증 |

---

## 7️⃣ 참고 경로

```
Claude 홈:
C:\Users\[username]\.claude\

프로젝트:
C:\Users\[username]\.claude\projects\C--Users-MBC-501-21-Documents-FinalProject

세션 메타:
C:\Users\[username]\.claude\sessions\*.json

백업 기본 경로:
C:\Users\[username]\Claude\backups\
```
