> 2026-10-14 백업 동작 규칙: 백업은 파일 복사·압축·검증·결과 보고로 종료한다.
> 백업 전후에 `git add`, `git commit`, `git push`를 실행하지 않는다.
> 자동 커밋을 수행하는 `/handoff` 절차를 백업의 일부로 호출하지 않는다.
> Git 커밋은 사용자가 직접 한다. 이 규칙은 아래의 이전 백업 절차에도 적용한다.
> 수정 기록: GPT(Codex), 2026-10-14.

> 2026-10-13 변경: `/BackUp`의 기본 동작은 **Claude + GPT(Codex) 전체 로컬 세션 통합 백업**입니다.
> 최신 명령 정의는 `.claude/commands/backup.md`, 실행은 `@Scripts/backup-session.ps1 -Verify`입니다.
> 파일명은 `ai_sessions_backup_*.tar.gz`이며 `.claude`, `.codex`, 통합 `MANIFEST.json`을 포함합니다.
> Codex 세션 원본·보관 세션·목록·SQLite 온라인 스냅샷을 저장하고 모든 파일의 SHA256을 검증합니다.
> 기존 선택 세션/Markdown 내보내기 스크립트는 Claude 전용 보조 기능입니다.
> 아래의 Claude 전용 전체 백업 설명·초기 메뉴·최종 확인 단계는 이전 버전 기록입니다.
> 기본 명령은 대상 재선택 없이 양쪽을 백업하며, 실행 환경의 외부 경로 권한 승인은 별도로 적용됩니다.
> Unity 파일과 로그인 인증 정보, ChatGPT 웹/클라우드 대화는 포함하지 않습니다.
> 복원은 두 앱을 종료한 뒤 실행하며 Codex는 기본 `.codex` 위치에 복원됩니다. 다른 PC의 경로는 자동 치환하지 않습니다.
> Python 3.12+가 필요하며 Codex 번들 런타임을 자동 탐색합니다.

# /backup 명령어 AI 스킬 정의

**파일**: `.claude/commands/backup.md`  
**프로세스**: `@Edit/BACKUP_PROCESS.md`  
**구현**: AI가 이 스킬을 실행할 때 따라야 할 절차

**목적**: AI 세션만 백업 (Unity 프로젝트 파일은 제외)

---

## 1. 스킬 호출 조건

```
사용자 입력: /backup
```

---

## 2. AI 실행 절차 (단계별)

### **STEP 1: 환영 메시지**

```markdown
📦 Claude 세션 백업 도구

현재 프로젝트의 세션을 백업합니다.
앞으로 수 분이 소요될 수 있습니다.

---
```

### **STEP 2: 백업 방식 선택 (AskUserQuestion 사용)**

```
질문: 어떤 방식으로 백업하시겠어요?

옵션:
1. 전체 세션 백업 (모든 세션 한 번에)
2. 선택 세션 백업 (특정 세션만)
3. 세션 + Markdown 내보내기 (읽을 수 있는 형식)
```

**코드 (의사코드):**
```python
option = ask_user_question(
    question="백업 방식을 선택해주세요",
    options=[
        {"label": "전체 세션 백업", "value": "all"},
        {"label": "선택 세션 백업", "value": "select"},
        {"label": "세션 + 내보내기", "value": "export"}
    ]
)
```

---

### **STEP 3-A: 전체 세션 백업 경로**

(옵션 1 선택 시)

```markdown
**백업 위치를 선택해주세요:**

기본값: C:\Users\<현재 사용자명>\Documents\LHS\

1. 기본값 사용 (권장)
2. 다른 위치 입력
3. 외장 드라이브 (자동 감지)
```

**사용자 입력**: 1 / 2 / 3

---

### **STEP 3-B: 세션 선택**

(옵션 2 선택 시)

AI가 실행:
```powershell
# 세션 목록 자동 조회
$projectDir = "$env:USERPROFILE\.claude\projects\C--Users-<현재 사용자명>-Documents-FinalProject"
$sessions = Get-ChildItem $projectDir -Directory | Where-Object {$_.Name -notlike "memory"}

# 각 세션의 메타데이터 로드
foreach ($session in $sessions) {
    # 메타데이터 출력
}
```

표시:
```markdown
**현재 세션 목록:**

1️⃣  수칙 추가
   PID: 1732 | 메시지: 150개 | 시작: 2026-10-12 05:25

2️⃣  세부 조정
   PID: 14760 | 메시지: 280개 | 시작: 2026-10-12 03:07

3️⃣  2D 맵 생성-메인
   PID: 18904 | 메시지: 1,200개 | 시작: 2026-10-11 15:30

**백업할 세션을 선택해주세요:**
(예: "1,3" 또는 "1 3" 또는 "all")
```

**사용자 입력**: "1,3" 또는 "1 3" 또는 "all"

**AI 처리**:
```python
# 입력 파싱
selected = parse_session_input(user_input)
# "1,3" → [session_id_1, session_id_3]

# 선택 확인
display_selected_sessions(selected)
```

확인 메시지:
```markdown
✅ **선택된 세션:**
- 수칙 추가 (1732)
- 2D 맵 생성-메인 (18904)

이 2개 세션을 백업할까요?
(네 / 아니오)
```

---

### **STEP 3-C: 내보내기 옵션**

(옵션 3 선택 시)

```markdown
✅ **세션 + Markdown 내보내기 선택**

이 옵션은:
• 모든 세션 전체 백업 (TAR)
• 각 세션을 Markdown으로 변환
• 메타데이터 + 메시지 추출

진행할까요?
```

---

### **STEP 4: 백업 형식 선택**

```markdown
**백업 형식을 선택해주세요:**

1️⃣  TAR (.tar.gz)
   - 빠른 속도 ⚡
   - 보통 압축률

2️⃣  7z (.7z)
   - 높은 압축률 📦
   - 느린 속도
   - 외장 저장소 권장

3️⃣  복사 (디렉토리)
   - 매우 빠름 ⚡⚡
   - 압축 안 함
   - 용량 큼
```

**사용자 입력**: 1 / 2 / 3

---

### **STEP 5: 추가 옵션 (선택사항)**

```markdown
**추가 옵션:**

□ 백업 후 자동 검증 (권장)
□ 완료 후 외장 드라이브로 복사

선택하려면 번호 입력 (또는 엔터 스킵):
(예: "1,2" 또는 "1")
```

---

### **STEP 6: 최종 확인**

```markdown
🔍 **백업 설정 확인:**

**백업 방식**: 선택 세션 (2개)
**세션**: 
  - 수칙 추가
  - 2D 맵 생성-메인
**형식**: TAR (.tar.gz)
**위치**: C:\Users\[username]\Claude\backups\
**내보내기**: 아니오
**검증**: 예

이 설정으로 진행할까요?
```

**사용자 입력**: 네 / 아니오

아니오 시 → STEP 2로 돌아가기

---

### **STEP 7: 백업 실행**

AI가 PowerShell 스크립트 실행:

**경우 A: 전체 백업**
```powershell
.\@Scripts\backup-session.ps1 -BackupPath $path -Format $format -Verify:$verify
```

**경우 B: 선택 세션 백업**
```powershell
.\@Scripts\interactive-backup.ps1 `
  -SessionIds @("session_id_1", "session_id_3") `
  -Format $format `
  -BackupPath $path `
  -Export:$export `
  -Verify:$verify
```

**경우 C: 내보내기 포함**
```powershell
.\@Scripts\backup-session.ps1 -BackupPath $path -Format tar -Verify
foreach ($sessionId in $sessionIds) {
    .\@Scripts\export-session.ps1 -SessionId $sessionId -Format markdown
}
```

---

### **STEP 8: 진행 상황 표시**

AI가 실시간으로 스크립트 출력 표시:

```markdown
🔄 **백업 실행 중...**

✓ 임시 디렉토리 생성
✓ 세션 데이터 복사
  - 수칙 추가 (1732)
  - 2D 맵 생성-메인 (18904)
✓ 매니페스트 생성
✓ TAR 압축 (60%)
...
```

---

### **STEP 9: 완료**

```markdown
✅ **백업 완료!**

**백업 정보:**
- 파일: C:\Users\<현재 사용자명>\Documents\LHS\claude_backup_2sessions_2026-10-12_16-54.tar.gz
- 크기: 285 MB
- 세션: 2개 (150 + 1,200 메시지)
- 소요 시간: 2분 30초

**검증 결과**: ✅ 통과 (99.8%)

**다음 단계:**
1. 외장 드라이브에 복사
2. 정기 백업 설정
3. 3개월마다 검증

**복원 명령어:**
\`\`\`powershell
.\@Scripts\restore-session.ps1 -BackupFile "C:\Users\...\claude_backup_2sessions_2026-10-12_16-54.tar.gz"
\`\`\`
```

---

## 3. 오류 처리

| 오류 | 메시지 | 처리 |
|------|--------|------|
| 세션 없음 | "세션을 찾을 수 없습니다" | STEP 2 반복 |
| 디스크 부족 | "디스크 공간 부족 (필요: 500MB)" | 경로 변경 제안 |
| 권한 없음 | "쓰기 권한 없음" | 다른 경로 제안 |
| 스크립트 실패 | "[오류 메시지]" | 오류 원인 설명 + 수동 실행 방법 제시 |

---

## 4. 예외 상황

### 외장 드라이브 자동 감지

```powershell
$drives = Get-Volume | Where-Object {$_.DriveType -eq 'Removable'}
if ($drives) {
    Write-Host "감지된 외장 드라이브:"
    foreach ($drive in $drives) {
        Write-Host "  - $($drive.DriveLetter): $($drive.FileSystemLabel) ($([math]::Round($drive.SizeRemaining/1GB))GB 가용)"
    }
}
```

### 클라우드 경로 감지

```powershell
$cloudPaths = @{
    "OneDrive" = "$env:USERPROFILE\OneDrive"
    "Google Drive" = "$env:USERPROFILE\Google Drive"
    "Dropbox" = "$env:USERPROFILE\Dropbox"
}
```

---

## 5. 플로우 다이어그램

```
시작
  ↓
[환영 메시지]
  ↓
[백업 방식 선택] ←→ [1. 전체] [2. 선택] [3. 내보내기]
  ↓
(각 경로별 처리)
  ↓
[경로/형식 선택]
  ↓
[추가 옵션]
  ↓
[최종 확인] → [아니오] → [STEP 2로 돌아가기]
  ↓ [네]
[백업 실행]
  ↓
[진행 상황 표시]
  ↓
[완료 + 검증]
  ↓
[복원 명령어 제시]
  ↓
종료
```

---

## 6. 구현 체크리스트

- [ ] `backup.md` 명령어 등록
- [ ] BACKUP_PROCESS.md 작성
- [ ] interactive-backup.ps1 스크립트 완성
- [ ] 모든 backup-*.ps1 스크립트 테스트
- [ ] AI 스킬 정의 문서 작성 (이 파일)
- [ ] 사용자 테스트
- [ ] 오류 처리 검증

---

## 7. 참고

- `.claude/commands/backup.md` - 명령어 정의
- `@Edit/BACKUP_PROCESS.md` - 프로세스 상세
- `@Scripts/interactive-backup.ps1` - 대화형 스크립트
- `@Scripts/backup-session.ps1` - 전체 백업
- `SESSION_BACKUP_DESIGN.md` - 아키텍처


