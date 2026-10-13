# /BackUp — Claude + GPT(Codex) 전체 세션 백업

`/BackUp` 또는 `/backup`을 입력하면 두 앱의 전체 로컬 세션을 함께 백업한다.
추가 대상 선택 질문 없이 기본 TAR 형식으로 실행한다.

```powershell
& '.\@Scripts\backup-session.ps1' -Verify
```

- Claude: 대화 기록, 세션 메타데이터, 파일 히스토리, 메모리 및 설정.
- GPT(Codex): `.codex/sessions`, `archived_sessions`, 세션 목록, 로컬 상태 및 메모리 SQLite 스냅샷.
- `CODEX_HOME`과 `config.toml`의 `sqlite_home` 경로를 반영한다.
- 로그인 인증 정보, Codex 브라우저/캐시/샌드박스 비밀, Unity 프로젝트 파일은 제외한다.
- ChatGPT 웹/클라우드 대화는 로컬 Codex 데이터가 아니므로 포함되지 않는다.
- 실행 중인 세션은 복사 시점까지의 기록이다.

저장: 기존 LHS 폴더를 OneDrive 문서 → Documents → Downloads 순서로 찾고,
없으면 사용자 Documents/LHS에 생성한다.
파일명: `ai_sessions_backup_yyyy-MM-dd_HH-mm-ss.tar.gz`.
Claude나 Codex 저장 폴더가 없으면 실패를 알리고 양쪽 백업 성공으로 보고하지 않는다.
저장된 파일의 SHA256 해시를 모두 확인한 뒤 경로, 용량, 양쪽 파일 수를 보고한다.
작업 폴더 외부 쓰기는 실행 환경의 권한 승인을 사용한다.

검증: `@Scripts/validate-backup.ps1 -BackupFile <파일>`.
복원: 두 앱 종료 후 `@Scripts/restore-session.ps1 -BackupFile <파일>`.
Codex 복원 위치는 기본 `.codex`이며 다른 PC의 경로는 자동 치환하지 않는다.
Python 3.12+ 필요. Codex 번들 Python 자동 탐색 또는 `SESSION_BACKUP_PYTHON` 지정.

선택 세션/Markdown 내보내기는 기존 Claude 전용 보조 기능이다.
이 보조 기능을 Claude+GPT 전체 백업의 대체로 사용하지 않는다.
