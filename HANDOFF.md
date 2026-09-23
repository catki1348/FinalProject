# 작업 핸드오프

**작성 시각**: 2026-09-23

## 현재 목표

이 세션 자체의 목표는 "토큰/사용량 부족으로 세션이 중단돼도 다른 PC에서 이어서 작업할 수 있게 하는 도구"(`/handoff`, `/resume`, `/handon` 슬래시 커맨드)를 만드는 것. 이 도구 자체는 기능적으로 완성된 상태.
단, 이 저장소에는 이 세션과 무관하게 **다른 세션들에서 진행된 대규모 미커밋 Unity 작업**이 쌓여 있어(아래 참고), 그 부분은 이 세션이 세부 맥락을 모른다는 점을 분명히 밝혀둔다.

## 진행 상황

### 이 세션이 실제로 완료한 것 (핸드오프 도구 자체)
- [.claude/commands/handoff.md](.claude/commands/handoff.md) — `/handoff`: HANDOFF.md 작성 + 마지막에 `git add -A` + 고정 메시지로 자동 커밋(push는 안 함). 사용자가 명시적으로 자동 커밋 유지를 확인함.
- [.claude/commands/resume.md](.claude/commands/resume.md) — `/resume`: HANDOFF.md를 읽고 git 상태와 대조해서 "다음 할 일"을 보여주고 사용자 확인 후 시작 (보고 후 대기형).
- [.claude/commands/handon.md](.claude/commands/handon.md) — `/handon`: `/resume`과 달리 확인 없이 바로 "다음 할 일"을 실행. 파괴적/되돌리기 힘든 작업만 예외적으로 확인받음. 다른 PC에서 이전 대화 기록이 전혀 없어도 HANDOFF.md만으로 이어서 작업하도록 설계됨.
- [.claude/settings.json](.claude/settings.json), [.claude/scripts/statusline.ps1](.claude/scripts/statusline.ps1) — statusLine에 "5h left N%" 표시 (참고용 표시일 뿐, 아무것도 자동 실행하지 않음).
- **폐기된 시도**: 5시간 사용량 임계치(잔여 10% 이하)에서 Stop 훅으로 `/handoff`를 자동 실행하려 했으나, Claude Desktop 앱 Code 탭에서는 statusLine이 아예 호출되지 않고 Stop 훅 JSON에도 사용량 필드가 없어 로컬로는 감지 불가능함을 실측으로 확인. `mcp__ccd_session_mgmt__get_usage` 도구로 우회해 한 번은 성공시켰으나, 사용자가 "자동 체크 금지, 명령이 있을 때만 처리"를 명시적으로 지시(메모리 `feedback_explicit_handoff_trigger`에 기록됨) → Stop 훅 등록과 `check-usage-stop.ps1`을 완전히 제거하고 수동 명령(`/handoff`, `/handon`)만 남김.

### 이 세션이 관여하지 않은, 저장소에 쌓인 변경 (다른 세션 작업 — 세부 맥락 모름)
`git log` 최근 커밋: `af11cec 260922-오후 작업`, `fae018f 230921-오후 작업분`, `3a04b70 자동 핸드오프 커밋 (사용량 임계치 도달)`, `746944c 260918-오전 작업` 등.
현재 **미커밋 상태**로 다음이 쌓여 있음 (`git status`/`git diff --stat` 기준, 이 세션은 왜 이렇게 바뀌었는지 모름):
- 수정: `@Edit/LHS_Custumer.md`, `@Edit/MapEdit.md`, `@Edit/README.md`, `Assets/@ExternalAssets/@Prefab/Player_Warrior.prefab`, `Assets/Scenes/Stage1~5.unity`(전체), `Assets/Scripts/Managers/Boss.cs`, `Assets/Scripts/Managers/StageManager.cs`, `Assets/Sprites/Warrior/Transparent/dash_right.png`, `Project.md`(+186줄)
- 신규(미추적): `@Edit/MapDesign_AllStages_Layout.md`, `@Edit/MapDesign_Stage1-3.md`, `Assets/Scripts/Managers/BossZoneEndPoint.cs`, `Assets/Scripts/Managers/EndingManager.cs`, `Assets/Scripts/Managers/StageClearManager.cs`(+각 .meta), `Assets/Sprites/Bosses/`(+.meta)

이름으로 미루어 보스전 종료 지점, 엔딩 처리, 스테이지 클리어 처리 시스템을 추가하는 작업으로 보이나, 이 세션은 설계 의도·시행착오·테스트 여부를 전혀 모른다.

## 다음 할 일

1. **(중요) 위 미커밋 Unity 변경사항 검토** — 이번 `/handoff` 실행 시 8번 절차대로 이 변경들이 전부 `git add -A`로 함께 커밋된다. 실험적이거나 깨진 상태의 코드가 섞여 있다면, 다음 세션에서 커밋 로그(`git show`)를 보고 필요시 별도로 되돌릴 것.
2. Boss/EndingManager/StageClearManager 관련 작업을 진행하던 세션이 있다면 그 세션에서 이어서 테스트 진행.
3. 핸드오프 도구(`/handoff`, `/resume`, `/handon`) 자체는 추가 요구사항이 없는 한 현재 상태로 유지.

## 중요 결정/맥락

- `/handoff`의 자동 커밋(8번)은 사용자가 명시적으로 유지를 지시함 — git push는 하지 않음(사용자가 직접).
- 5시간 사용량 자동 감지는 이 host 환경(Claude Desktop Code 탭)에서 기술적으로 불가능하다고 판단하고 포기함 — 향후 Claude Code가 관련 필드를 훅/statusLine에 노출하면 재검토 가능.
- 자동 트리거를 걷어낸 이유는 사용자 피드백(메모리 `feedback_explicit_handoff_trigger`): "handoff는 명시적 명령이 있을 때만 체크".

## 관련 파일 경로

- `.claude/commands/handoff.md`, `.claude/commands/resume.md`, `.claude/commands/handon.md`
- `.claude/settings.json`, `.claude/scripts/statusline.ps1`
- (다른 세션 관할, 참고만) `Assets/Scripts/Managers/Boss.cs`, `BossZoneEndPoint.cs`, `EndingManager.cs`, `StageClearManager.cs`, `Assets/Scenes/Stage1~5.unity`, `@Edit/MapDesign_AllStages_Layout.md`, `@Edit/MapDesign_Stage1-3.md`

## 커밋 상태

미커밋 변경이 많지만(위 목록), 이 세션이 의도적으로 제외해야 할 근거를 가진 파일은 없다. 8번 절차대로 전부 `git add -A` 후 커밋한다. 다만 위 "다른 세션 작업" 항목은 이 세션이 검증하지 않은 상태로 함께 커밋됨에 유의.
