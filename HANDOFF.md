# 작업 핸드오프

**작성 시각**: 2026-09-16 (자동 트리거 — 5시간 사용 한도 잔여 3%에서 작성됨)

## 현재 목표

Claude Code 세션이 토큰 부족으로 중단돼도 다른 PC에서 이어서 작업할 수 있도록 "핸드오프" 체계를 구축 중.
구체적으로는 (1) 진행상황 요약을 저장/불러오는 슬래시 커맨드, (2) 5시간 사용 한도가 10% 이하로 남으면 자동으로 (1)을 실행시키는 훅을 만드는 작업.

## 진행 상황

- [.claude/commands/handoff.md](.claude/commands/handoff.md): `/handoff` 커맨드. 실행하면 진행상황/다음 할 일을 이 파일(HANDOFF.md)에 정리. git commit은 하지 않음(사용자가 원할 때 직접 커밋).
- [.claude/commands/resume.md](.claude/commands/resume.md): `/resume` 커맨드. 다른 PC에서 HANDOFF.md를 읽고 git 상태와 대조해 이어서 작업하도록 안내.
- [.claude/scripts/statusline.ps1](.claude/scripts/statusline.ps1): statusLine 훅. 5시간 사용률을 `.claude/.usage-state.json`에 기록. **단, Claude Desktop 앱의 Code 탭에서는 statusLine이 아예 호출되지 않는 것을 실측으로 확인함** (터미널 CLI 세션에서는 작동할 가능성이 있어 남겨둠).
- [.claude/scripts/check-usage-stop.ps1](.claude/scripts/check-usage-stop.ps1): Stop 훅. 매 턴 종료 시 실행됨(실측 확인됨 — `.claude/.stophook-debug.log`에 다회 기록 확인 후 정리함).
  - 1차: `.claude/.usage-state.json`이 20분 이내로 신선하면 그 값을 사용 (CLI/statusLine 작동 환경용, 비용 없음).
  - 2차(현재 데스크톱 앱에서 실제로 타는 경로): 로컬 상태가 없으면 Claude에게 `mcp__ccd_session_mgmt__get_usage` 도구를 직접 호출해서 5시간 사용률을 확인하고, 90% 이상이면 `/handoff` 절차를 수행하도록 지시를 주입(block).
  - 중복 실행 방지: `.claude/.usage-handoff-flag` 파일에 현재 윈도우의 resetsAt 시각을 저장해두고, 그 시각이 지나야 다시 트리거되게 함.
  - 무한루프 방지: 훅 입력의 `stop_hook_active`가 true면 즉시 종료.
- `.claude/settings.json`에 위 statusLine/Stop 훅 등록 완료, 커밋됨.
- `.gitignore`에 `.claude/.usage-state.json`, `.claude/.usage-handoff-flag`, `.claude/settings.local.json` 추가함(개인/런타임 상태라 커밋 대상 아님).
- **이번에 실제로 5시간 한도 97% 사용(잔여 3%) 상황에서 자동 트리거가 걸려 이 HANDOFF.md가 작성됨 — 데스크톱 앱 경로(get_usage 폴백)가 실제로 동작함을 확인.**

## 다음 할 일

1. `.claude/scripts/check-usage-stop.ps1`, `.claude/scripts/statusline.ps1`의 방금 수정사항(디버그 로그 제거, 폴백 로직 추가)을 git add/commit할지 사용자에게 확인 후 커밋.
2. 데스크톱 앱 폴백 경로는 **매 턴마다** `get_usage` 도구를 호출하게 되어 있어 약간의 오버헤드가 있음 — 사용자가 원하면 N턴마다 한 번만 체크하도록 샘플링 로직 추가 검토.
3. 실제 터미널(CLI) `claude` 세션에서 statusLine 경로가 진짜로 작동하는지는 아직 미검증 (데스크톱 앱에서만 "작동 안 함"을 확인함).
4. `.claude/.usage-handoff-flag`가 정상적으로 5시간 윈도우 리셋 후 재무장되는지 실제 사용 중 재확인.

## 중요 결정/맥락

- 사용자가 "구성만 제작, 커밋은 내가 원할 때"라고 명시해서, 이 세션은 관련 파일들을 만들기만 하고 git add/commit은 하지 않음.
- statusLine이 Claude Desktop 앱에서 호출되지 않는다는 사실은 문서에 없고, 디버그 로그(`.claude/.statusline-debug.log`가 끝내 생기지 않음, `.claude/.stophook-debug.log`는 생김)로 직접 실측 확인한 것. 향후 Claude Code 업데이트로 바뀔 수 있음.

## 관련 파일

- `.claude/commands/handoff.md`, `.claude/commands/resume.md`
- `.claude/scripts/statusline.ps1`, `.claude/scripts/check-usage-stop.ps1`
- `.claude/settings.json`
- `.gitignore` (상태 파일 무시 규칙 추가)

## 커밋되지 않은 변경사항

`git status` 기준, 아래 파일만 수정된 상태이고 아직 커밋 안 됨:
- `.claude/scripts/check-usage-stop.ps1`
- `.claude/scripts/statusline.ps1`

(그 외 `.claude/commands/`, `.claude/settings.json` 등은 이전에 이미 커밋됨 — 커밋 9207852 "260916-오전 작업분" 기준)
