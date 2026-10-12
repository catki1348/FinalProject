# AI 세션 백업/내보내기 도구 설계

**작성**: 2026-10-12  
**목표**: 포맷/디스크 사고 후 AI 세션 데이터만 복구 (Unity 프로젝트 제외)

**범위:**
- ✅ Claude 세션 메타데이터, 대화 기록, 파일 히스토리
- ❌ Unity 프로젝트 파일, 코드, Assets (사용자가 별도 백업)

---

## 1. Claude 세션 저장 구조 분석

### 1.1 전역 저장소 (`~/.claude/`)

| 위치 | 용도 | 특징 |
|------|------|------|
| `sessions/` | 세션 메타데이터 | `{pid}.json` 형식 |
| `sessions/` | 암호화 키 | `{pid}.{hash}.key` 형식 |
| `projects/` | 프로젝트별 데이터 | 디렉토리명 = URL 인코딩된 경로 |
| `file-history/` | 파일 수정 기록 | 세션별 폴더 |
| `backups/` | 자동 백업 | 타임스탬프 기반 |
| `shell-snapshots/` | 쉘 스냅샷 | 구조 미상세 |

### 1.2 세션 메타데이터 구조

**파일**: `~/.claude/sessions/{pid}.json`

```json
{
  "pid": 1732,
  "sessionId": "2812579a-ecee-485f-bc45-60f1e059f3bb",
  "cwd": "C:\\Users\\MBC-501-21\\Documents\\FinalProject",
  "startedAt": 1791784915534,
  "procStart": "134362585147530755",
  "version": "2.1.295",
  "peerProtocol": 1,
  "peerFeatures": ["notify_idle", "artifact_yield"],
  "kind": "interactive",
  "entrypoint": "claude-desktop",
  "hostSessionId": "local_a445ff56-19ca-4e55-b0b0-23c05bcfd837",
  "pidDomain": "win32:desktop-p9pnkb2",
  "name": "수칙 추가",
  "nameSince": 1791786034985,
  "updatedAt": 1791792238972,
  "status": "idle",
  "statusUpdatedAt": 1791792238972,
  "nameSource": "user",
  "formerNames": [
    {"name": "맵 배치 수칙 추가", "until": 1791786034985}
  ]
}
```

### 1.3 프로젝트별 저장소 구조

**경로**: `~/.claude/projects/{url-encoded-project-path}/`

```
{project}/
  ├── {sessionId}/              (세션별 폴더, 1-2개 파일)
  ├── {sessionId}.jsonl         (메시지 로그, JSONL 형식)
  ├── memory/                   (메모리 저장소, 현재 비어있음)
  └── {sessionId}.jsonl         (여러 세션의 로그)
```

### 1.4 메시지 로그 형식 (JSONL)

**파일**: `{project}/{sessionId}.jsonl`  
**형식**: JSON Lines (각 줄 = JSON 객체)

```json
{"type":"queue-operation","timestamp":"2026-10-12T05:25:23.054Z"}
{"type":"user","timestamp":"2026-10-12T05:25:23.115Z"}
{"type":"attachment","timestamp":"2026-10-12T05:25:23.114Z"}
```

**이벤트 타입**:
- `queue-operation` - 큐 작업
- `user` - 사용자 메시지
- `attachment` - 첨부 파일
- `artifact` - 아티팩트
- (기타 타입)

### 1.5 파일 히스토리

**경로**: `~/.claude/file-history/{sessionId}/`  
**내용**: 각 세션에서 편집한 파일의 버전 기록

### 1.6 백업

**경로**: `~/.claude/backups/`  
**파일**: `.claude.json.backup.{timestamp}`  
**크기**: ~60KB  
**주기**: 정기적 (타임스탐프 기반)

---

## 2. 암호화 및 보안

### 2.1 암호화 키 저장

- 파일: `~/.claude/sessions/{pid}.{hash}.key` (119 bytes)
- 형식: 암호화된 이진 데이터 또는 키 참조
- **주의**: 암호화 알고리즘 미상세 (Claude 앱 내부 구현)

### 2.2 보안 고려사항

1. **암호화된 데이터**: 메시지 로그가 암호화되어 있을 가능성
2. **키 관리**: 백업 시 키도 함께 보관 필요
3. **민감정보**: 인증토큰, API 키 등이 로그에 포함될 수 있음

---

## 3. 백업 전략

### 3.1 2-단계 백업 방식

#### A. 원본 보존 백업 (Binary Backup)
**목표**: 포맷 후 완전 복구

```
C:\Users\MBC-501-21\Documents\LHS\
├── claude_backup_YYYYMMDD_HHMMSS.tar.gz
│   ├── .claude/
│   │   ├── sessions/
│   │   ├── projects/
│   │   ├── file-history/
│   │   └── MANIFEST.json
└── ... (다른 백업)
```

**특징**:
- 모든 파일을 그대로 복사
- 디렉토리 구조 유지
- 암호화된 상태 유지
- 복구 시 `~/.claude/` 전체 복원

**장점**: 완전 복구 가능, 데이터 손실 없음  
**단점**: 용량 큼, 휴대 불편

#### B. 세션별 내보내기 (Markdown/JSON Export)
**목표**: 앱 없이 세션 내용 읽기 가능

```
session_export_{sessionId}/
├── metadata.json           (세션 메타데이터)
├── messages.jsonl          (메시지 로그 사본)
├── messages.md             (Markdown 형식 대화)
├── files.md                (편집된 파일 목록)
└── timeline.html           (대화 타임라인)
```

**특징**:
- 암호화 없음 (평문)
- 읽을 수 있는 형식
- 세션별 독립적
- 선택적 내보내기 가능

**장점**: 읽기 쉬움, 재사용 용이  
**단점**: 일부 정보 손실 가능

### 3.2 백업 범위

#### 필수 백업 (항상)
```
- ~/.claude/sessions/*.json      (메타데이터)
- ~/.claude/sessions/*.key       (암호화 키)
- ~/.claude/projects/{project}/* (메시지 로그)
- ~/.claude/file-history/        (파일 히스토리)
```

#### 선택 백업 (옵션)
```
- C:\Users\MBC-501-21\Documents\LHS\  (지정된 백업 경로)
- ~/.claude/plugins/                  (플러그인)
- ~/.claude/skills/                   (스킬)
```

#### 제외 항목 (보안)
```
- auth.json          (인증정보)
- 토큰/API 키        (민감정보)
```

---

## 4. 내보내기 형식 설계

### 4.1 JSON 메타데이터 내보내기

**파일**: `session_export_{sessionId}/metadata.json`

```json
{
  "sessionId": "2812579a-ecee-485f-bc45-60f1e059f3bb",
  "pid": 1732,
  "name": "수칙 추가",
  "project": "C:\\Users\\MBC-501-21\\Documents\\FinalProject",
  "startedAt": "2026-10-12T05:25:23.054Z",
  "updatedAt": "2026-10-12T05:32:38.972Z",
  "status": "idle",
  "messageCount": 150,
  "duration_seconds": 430,
  "exportedAt": "2026-10-12T16:54:00.000Z"
}
```

### 4.2 Markdown 대화 내보내기

**파일**: `session_export_{sessionId}/messages.md`

```markdown
# 세션: 수칙 추가

**PID**: 1732  
**시작**: 2026-10-12 05:25 (UTC)  
**종료**: 2026-10-12 05:32 (UTC)  
**메시지**: 150개

---

## 메시지 로그

### 2026-10-12 05:25:23 | 사용자

> 맵 배치 수칙을 추가해줘

### 2026-10-12 05:25:50 | Claude

설계도를 바탕으로...

[계속...]
```

### 4.3 파일 히스토리 내보내기

**파일**: `session_export_{sessionId}/files.md`

```markdown
# 편집된 파일

## MapEdit.md
- 규칙 개수: 12
- 마지막 수정: 2026-10-12 05:30
- 변경 내용: 규칙 11-12 추가

## LHS_Customer.md
- 규칙 개수: 48
- 마지막 수정: 2026-10-12 05:28
- 변경 내용: 규칙 47-48 수정
```

---

## 5. 복원 전략

### 5.1 원본 백업 복원

```bash
# 1. 백업 파일 확인
tar -tzf backup_raw_2026-10-12_16-54.tar.gz

# 2. 복원 (위험: 기존 세션 덮어쓰기)
tar -xzf backup_raw_2026-10-12_16-54.tar.gz -C ~

# 3. 검증
ls -la ~/.claude/sessions/ | wc -l
```

### 5.2 선택적 복원 (세션별)

```bash
# 특정 세션만 복원
cp -r backup_raw_2026-10-12/\.claude/projects/{project}/* \
      ~/.claude/projects/{project}/
```

### 5.3 경로 변경 처리

**포맷 후 사용자명/경로 변경 시**:

```
원래:  C:\Users\old-user\Documents\FinalProject
새로:  C:\Users\new-user\Documents\FinalProject

처리:  JSON/JSONL 파일에서 경로 일괄 치환
```

---

## 6. 구현 체크리스트

### Phase 1: 원본 백업 (이진 파일)
- [ ] 백업 생성 스크립트 작성 (tar/7z)
- [ ] SHA256 해시 생성 및 검증
- [ ] 타임스탐프 포맷 정의
- [ ] 외장 저장소 경로 설정

### Phase 2: 세션 내보내기 (Markdown/JSON)
- [ ] JSONL 파서 작성
- [ ] Markdown 생성기 작성
- [ ] 파일 히스토리 처리
- [ ] 메타데이터 통합

### Phase 3: 복원 도구
- [ ] 백업 검증 스크립트
- [ ] 자동 복원 스크립트
- [ ] 경로 변경 처리
- [ ] 충돌 감지 및 해결

### Phase 4: 검증 및 테스트
- [ ] 테스트 데이터셋 준비
- [ ] 백업 → 복원 → 검증 사이클
- [ ] 경로 변경 테스트
- [ ] 권한/인코딩 테스트

---

## 7. 관련 파일 경로

```
프로젝트 루트:
C:\Users\MBC-501-21\Documents\FinalProject

Claude 홈:
C:\Users\MBC-501-21\.claude\

백업 저장 경로:
C:\Users\MBC-501-21\Documents\LHS\

이 설계 문서:
@Edit/SESSION_BACKUP_DESIGN.md

구현:
@Scripts/backup-session.ps1        (백업 생성)
@Scripts/export-session.ps1        (내보내기)
@Scripts/restore-session.ps1       (복원)
@Scripts/validate-backup.ps1       (검증)
```

---

## 8. 다음 단계

1. ✅ 세션 저장 형식 조사 (완료)
2. ✅ 백업/내보내기 설계 (완료)
3. ⏳ **구현 시작**: backup-session.ps1 작성
4. ⏳ 내보내기 도구 작성
5. ⏳ 복원 도구 작성
6. ⏳ 검증 및 테스트

---

## 참고자료

- 현재 세션 수: 12개
- 프로젝트별 세션: 6-7개
- 파일 히스토리 용량: ~50KB
- 자동 백업: 5개 (각 ~60KB)
