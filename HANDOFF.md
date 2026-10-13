# Claude AI 인계 — AI 세션 재해 복구 백업

## 작성 시각
2026-10-12 16:54 KST

## 현재 목표
포맷·디스크 사고 후 Unity 작업물 외에 AI 세션의 대화, 도구 실행 기록, 작업 맥락을 되살릴 백업 수단을 마련한다. 사용자는 프로젝트 체크와 handon만으로는 불충분했다고 명시했다.
마지막 지시: “지금 작업 중단하고 클로드AI 이어서 작업하게 넘겨줘라”. Codex 구현은 중단했고 Claude AI가 이어서 진행해야 한다.

## 진행 상황
- 기존 HANDOFF.md, .gitignore, handoff 명령과 사용자 홈 저장 위치를 조사했다.
- 백업/복원 도구는 아직 작성하지 않았으며 실제 백업도 실행하지 않았다. 이번 세션의 파일 변경은 이 인계 문서뿐이다.
- 기존 handoff는 요약과 Git 커밋을 제공하지만 사용자 홈에 있는 세션 원본/DB를 백업하지 않는다.
- 아래 경로는 존재를 확인했다. 대화 파일 내부나 인증정보는 읽지 않았다.
- 공식 문서 https://learn.chatgpt.com/docs/config-file/config-advanced 를 열었으나 앱 전체 세션 복구를 보장하는 공식 절차는 아직 확인하지 못했다.

## 다음 할 일
1. Codex/Claude의 세션 저장 형식과 범위를 조사한다. 활성/보관 세션, 프로젝트별 기록, 메모리, 명령/스킬, 인덱스/DB를 검토한다. Claude Desktop의 별도 앱 저장소는 아직 조사하지 않았다.
2. 원본 보존 백업과 앱 없이 읽을 수 있는 세션별 Markdown/JSON 내보내기를 함께 설계한다. 사용자/AI 대화와 도구 실행 결과, 결정사항을 가능한 범위에서 보존하고 세션 목록/ID/프로젝트 경로를 제공한다.
3. 백업 생성, 해시/누락 검증, 복원 절차를 구현한다. 실행 중 SQLite는 일관된 스냅샷 방식을 사용하거나 앱 종료 후 백업한다. 실행 중 대화 로그도 일관성을 고려한다.
4. 포맷 후 사용자명/프로젝트 경로 변경을 처리한다. 기존 데이터를 덮어쓰기 전에 별도 보존한다. 앱 UI 복구와 새 세션에서 기록을 읽고 이어가는 복구를 구분하고 지원되지 않은 앱 복원을 보장하지 않는다.
5. 실제 백업과 복원 연습으로 검증한다. 같은 디스크에만 저장하면 포맷 대비가 아니므로 외장 저장소나 별도 저장소 목적지를 사용자와 정한다. 목적지는 아직 미정이다.

## 중요 결정/맥락
- handoff/handon은 인계이며 전체 세션 백업이 아니다. 이 문서 역시 백업 완성물이 아니다.
- 백업 범위/목적지/주기/복원 방식은 아직 확정되지 않았다. 예약 작업은 만들지 않았다.
- 기존 인계 기록에는 “handoff는 명시적 명령이 있을 때만 체크”라는 사용자 선호가 있었다. 사용량 기반 자동 실행을 재도입하지 않는다.
- auth.json, 토큰, 브라우저 인증 저장소를 무심코 포함하거나 Git에 넣지 않는다. 실제 대화 백업도 공개 저장소에 올리지 않는다.
- CODEX_HOME 설정을 확인해야 한다. 아래는 이 PC에서 관측한 기본 경로다.
- 작업 트리에는 다른 세션의 Unity 변경이 다수 있다. 이번 세션은 이를 작성하거나 검증하지 않았다. 기존 handoff 스킬의 전체 작업 트리 커밋 절차에 따라 함께 보존하며 Unity 테스트는 수행하지 않았다.

## 관련 파일 경로
- 프로젝트: C:\Users\<현재 사용자명>\Documents\FinalProject
- Codex 홈: C:\Users\<현재 사용자명>\.codex
  - sessions/, archived_sessions/, session_index.jsonl
  - state_5.sqlite, thread_history_1.sqlite와 WAL/SHM 파일
  - goals_1.sqlite, memories_1.sqlite, queue_1.sqlite, sqlite/
  - .codex-global-state.json, config.toml, skills/, visualizations/, plugins/
  - auth.json이 존재함: 인증파일은 별도 취급
- Claude 홈: C:\Users\<현재 사용자명>\.claude
  - projects/, sessions/, file-history/, backups/, skills/ 등이 존재
- .claude/commands/handoff.md, handon.md, resume.md
- .agents/skills/source-command-handoff/SKILL.md
- .claude/settings.json, .claude/scripts/statusline.ps1, .agents/skills/
- 이전 HANDOFF.md 내용은 Git 이력에서 확인 가능

## 커밋 상태
인계 절차대로 HANDOFF.md와 전체 작업 트리를 커밋한다. 제외 대상은 지정하지 않았다. 다른 세션의 스테이지/트랩/보스/오프닝 작업도 포함하며 완성도는 미검증이다. push는 하지 않는다. 성공 여부는 최종 응답의 커밋 해시와 git status로 확인한다.
