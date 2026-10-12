# Claude 세션 백업 시스템 — 완전 가이드

**버전**: 1.0  
**작성**: 2026-10-12  
**상태**: ✅ 완성 및 구현 가능

---

## 🎯 시스템 개요

**AI의 세션 데이터만** 자동으로 백업하고 복구하는 완전한 시스템입니다.

### 📌 목적
- AI의 대화 기록과 파일 히스토리를 보존
- 포맷 후 AI 세션 복구 (복구 가능)
- AI 작업 세션 마이그레이션

### ⚠️ 범위
- ✅ **포함**: AI 세션 메타데이터, 대화 기록, 파일 히스토리
- ❌ **미포함**: Unity 프로젝트 파일, 외부 리소스 (사용자가 별도 백업)

**핵심 기능:**
- ✅ 전체/선택 세션 백업 (AI 세션만)
- ✅ Markdown 형식 내보내기
- ✅ 자동 무결성 검증
- ✅ 포맷 후 복구 (경로 변경 처리)
- ✅ 사용자 친화적 대화형 인터페이스

---

## 🎯 백업 위치

```
C:\Users\MBC-501-21\Documents\LHS\
```

모든 백업은 위 경로에 저장됩니다.

---

## 📁 파일 구조

### **명령어 & 프로세스**
```
.claude/commands/
└── backup.md                          # /backup 명령어 정의

@Edit/
├── BACKUP_SYSTEM_README.md            # 이 파일
├── BACKUP_PROCESS.md                  # AI 실행 절차 (단계별)
├── BACKUP_SKILL_DEFINITION.md         # AI 스킬 상세 정의
├── SESSION_BACKUP_DESIGN.md           # 아키텍처 & 설계
└── HANDOFF.md                         # 인계 문서
```

### **PowerShell 스크립트**
```
@Scripts/
├── backup-session.ps1                 # 전체 세션 백업
├── interactive-backup.ps1             # 선택 세션 백업 (대화형)
├── export-session.ps1                 # Markdown/JSON 내보내기
├── restore-session.ps1                # 백업 복원
└── validate-backup.ps1                # 무결성 검증
```

---

## 🚀 빠른 시작

### **사용자 방법: /backup 명령어**

```
1️⃣  이 세션에서 입력:
    /backup

2️⃣  AI가 백업 옵션을 제시합니다:
    - 전체 세션 백업
    - 선택 세션 백업
    - 세션 + Markdown 내보내기

3️⃣  선택 후 자동으로 백업 진행

4️⃣  완료 후 복원 명령어 제시
```

### **개발자 방법: 직접 스크립트 실행**

```powershell
# 전체 백업
.\@Scripts\backup-session.ps1 -BackupPath "D:\Backup" -Format tar -Verify

# 선택 세션 백업
.\@Scripts\interactive-backup.ps1 -SessionIds @("id1", "id2") -Export

# 내보내기
.\@Scripts\export-session.ps1 -SessionId "sessionId" -Format markdown

# 검증
.\@Scripts\validate-backup.ps1 -BackupFile "path\to\backup.tar.gz" -Verbose

# 복원
.\@Scripts\restore-session.ps1 -BackupFile "path\to\backup.tar.gz" -UpdatePaths `
  -OldPath "C:\Users\old" -NewPath "C:\Users\new"
```

---

## 📋 각 파일 설명

### **명령어 정의**

#### `.claude/commands/backup.md`
- **용도**: `/backup` 명령어 정의
- **내용**: 명령어 설명, 옵션, 예시
- **역할**: Claude Code 명령어 시스템에 등록

### **프로세스 문서**

#### `@Edit/BACKUP_PROCESS.md` ⭐ 핵심
- **용도**: AI가 실행할 단계별 프로세스
- **내용**: 사용자 상호작용 흐름 (7단계)
- **예시**: 각 단계의 UI/메시지
- **읽을 대상**: AI 개발자, AI

#### `@Edit/BACKUP_SKILL_DEFINITION.md`
- **용도**: AI 스킬 상세 정의
- **내용**: 각 단계별 코드 (의사코드)
- **예시**: PowerShell 명령어
- **읽을 대상**: AI 개발자, Skill 구현자

#### `@Edit/SESSION_BACKUP_DESIGN.md`
- **용도**: 시스템 아키텍처 설계
- **내용**: Claude 저장 구조, 암호화, 복원 전략
- **읽을 대상**: 기술 이해 필요한 사람

### **실행 스크립트**

#### `@Scripts/backup-session.ps1`
- **용도**: 전체 세션 백업
- **기능**: TAR/7z/복사 형식, 매니페스트 생성, 검증
- **사용**: 자동 백업 또는 수동 실행
- **출력**: `claude_backup_YYYYMMDD_HHMMSS.tar.gz`

#### `@Scripts/interactive-backup.ps1`
- **용도**: 선택 세션만 백업
- **기능**: 세션 선택, 메타데이터 추출, 병렬 처리
- **사용**: `/backup` 명령어 → 선택 옵션
- **출력**: `claude_backup_Nsessions_YYYYMMDD_HHMMSS.tar.gz`

#### `@Scripts/export-session.ps1`
- **용도**: 세션을 읽을 수 있는 형식으로 변환
- **기능**: JSONL → Markdown, JSON 추출
- **출력**: 각 세션별 폴더
  ```
  session_export_xxxxx/
  ├── metadata.json
  ├── messages.md
  └── messages.json
  ```

#### `@Scripts/restore-session.ps1`
- **용도**: 백업에서 복원
- **기능**: 압축 해제, 기존 데이터 백업, 경로 변경 처리
- **옵션**: DryRun (미리보기), UpdatePaths (경로 변경)
- **검증**: MANIFEST.json 해시 확인

#### `@Scripts/validate-backup.ps1`
- **용도**: 백업 무결성 검증
- **기능**: 압축 파일 검증, 해시 확인, 누락 감지
- **출력**: 백업 품질 점수 (%)

---

## 📊 워크플로우

### **1. AI 세션 백업 생성**

```
사용자 또는 AI
    ↓
/backup 명령어
    ↓
⚠️  "AI 세션만 백업됩니다" 안내
    ↓
[옵션 선택] → 전체/선택/내보내기
    ↓
[세션 확인] → 메타데이터 로드
    ↓
[경로/형식 선택]
    ↓
backup-session.ps1 또는 interactive-backup.ps1 실행
    ↓
(Unity 파일 제외) AI 데이터만 수집
    ↓
압축 + 매니페스트 생성
    ↓
✅ 완료 (검증 선택사항)
```

### **2. 내보내기**

```
export-session.ps1 실행
    ↓
JSONL 파일 읽기
    ↓
Markdown 변환
    ↓
메타데이터 추출
    ↓
session_export_xxxxx/ 폴더 생성
```

### **3. 복원**

```
restore-session.ps1 실행
    ↓
[기존 데이터 백업]
    ↓
[압축 해제 또는 복사]
    ↓
[경로 변경 (필요시)]
    ↓
[MANIFEST 검증]
    ↓
✅ 복원 완료
```

---

## 🔒 보안 & 암호화

### **자동 제외 항목**
- `auth.json` (인증 파일)
- API 토큰
- 개인 자격증명

### **암호화**
- 백업 파일 자체는 암호화 안 함
- 외장 드라이브에 저장 권장
- 민감정보는 내보내기 전 수동 제거

### **무결성**
- SHA256 매니페스트 생성
- 백업 후 자동 검증 옵션
- 해시 불일치 감지

---

## 📈 사용 시나리오

### **시나리오 1: 일일 백업**

```powershell
# 매일 자동 실행 (Task Scheduler)
.\@Scripts\backup-session.ps1 `
  -BackupPath "C:\Users\MBC-501-21\Documents\LHS\$(Get-Date -Format 'yyyy-MM-dd')" `
  -Format tar `
  -Verify
```

### **시나리오 2: 주간 선택 백업**

```
사용자: /backup
→ 옵션: 2 (선택 세션)
→ 선택: 중요한 세션 3개만
→ 내보내기: Yes
→ 백업 + Markdown 생성
```

### **시나리오 3: 포맷 후 복구**

```powershell
# 새 PC에서
.\@Scripts\restore-session.ps1 `
  -BackupFile "E:\Backup\claude_backup_2026-10-12_16-54.tar.gz" `
  -UpdatePaths `
  -OldPath "C:\Users\old-user\Documents\FinalProject" `
  -NewPath "C:\Users\new-user\Documents\FinalProject"
```

### **시나리오 4: 세션 마이그레이션**

```
1. export-session.ps1로 Markdown 내보내기
2. 내보낸 Markdown 아카이빙
3. 새 프로젝트에서 참고
```

---

## 🛠️ 트러블슈팅

| 문제 | 해결책 |
|------|--------|
| 디스크 부족 | 다른 경로 선택 (외장 드라이브 권장) |
| 권한 없음 | 관리자 권한으로 실행 또는 다른 경로 |
| 백업 검증 실패 | `validate-backup.ps1 -Verbose`로 상세 확인 |
| 복원 중 오류 | `-DryRun`으로 미리보기 후 실행 |
| TAR 미지원 | 7z 또는 copy 형식 사용 |

---

## 📚 관련 문서

| 문서 | 대상 | 목적 |
|------|------|------|
| BACKUP_PROCESS.md | AI | 단계별 실행 프로세스 |
| BACKUP_SKILL_DEFINITION.md | AI 개발자 | 상세 구현 정의 |
| SESSION_BACKUP_DESIGN.md | 기술팀 | 아키텍처 이해 |
| 이 파일 (README) | 모든 사용자 | 전체 개요 |

---

## ✅ 구현 체크리스트

- [x] 설계 문서 작성
- [x] PowerShell 스크립트 구현
- [x] 프로세스 정의
- [x] AI 스킬 정의
- [x] 명령어 등록
- [ ] 사용자 테스트
- [ ] 자동 스케줄링 설정 (Task Scheduler)
- [ ] 클라우드 동기화 설정 (OneDrive/Google Drive)

---

## 🎓 학습 경로

1. **초급**: 이 README 읽기
2. **중급**: BACKUP_PROCESS.md 읽기
3. **상급**: SESSION_BACKUP_DESIGN.md 읽기
4. **개발자**: BACKUP_SKILL_DEFINITION.md 읽기

---

## 📞 지원

- **명령어 사용 문제**: `/backup` 입력 후 AI에게 문의
- **스크립트 오류**: PowerShell 콘솔 에러 메시지 확인
- **설계 문제**: SESSION_BACKUP_DESIGN.md 참고

---

## 🔄 버전 히스토리

### **v1.0 (2026-10-12)**
- 초기 구현
- 모든 핵심 기능 완성
- AI 스킬 정의 완료

---

## 📝 라이선스

이 시스템은 프로젝트 내부용입니다.

---

**🎉 백업 시스템이 완성되었습니다!**

이제 `/backup` 명령어를 사용하여 세션을 백업할 수 있습니다.
