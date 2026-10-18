# Claude AI 인계 — 현재 작업 상태 (2026-10-17 갱신)

<!-- AI work record: Claude Sonnet 5.5, 2026-10-17. Replaced the 2026-10-12 backup-tool handoff (backup scripts exist since 2026-10-13: @Scripts/backup-session.ps1, session-backup.py, restore-session.ps1). Current work list lives in @Edit/TODO_Progress_20261018.md. -->

## 먼저 읽을 문서
- 할 일 전체: `@Edit/TODO_Progress_20261018.md` (이전 ToDo 3개를 대체)
- 작업 수칙: `@Edit/LHS_Custumer.md`(Unity 작업은 명시 지시 후, 커밋은 사용자가 직접), `@Edit/MapEdit.md`(맵 규칙 1~23)
- 최근 작업 기록: `@Edit/Stage11_53_54_Implementation_20261016.md`, `@Edit/Stage21_55_Implementation_20261016.md`, `@Edit/SystemDesign_FallRecovery.md`, `@Edit/MapDesign_Stage5-1.md`(7·8절)

## 현재 상태 한 줄 요약
미구현 영역은 없다. 1-1, 1-4, 2-1, 2-5, 5-1 v2, 5-3, 5-4, 5-5(보스5 포함)가 Unity에 적용되어 있고, 낙사 복귀 시스템(체력 35 손실 + 마지막 안전 발판 복귀)이 구현되어 있다. 대부분 임시 씬 검증까지이며 직접 조작 완주·보스 전투는 검증 전이다.

## 지금 가장 중요한 것
1. WarpManager 테스트 시작 값이 Stage1=`1-5`, Stage2=`2-5`, Stage5=`5-2`로 남아 있다(재생하면 그 영역에서 시작, 배포 전 비움).
2. Stage5 발판을 스파이크 포함 블록 타일로 바꾼 변경은 파일로만 수정했다. Unity에서 임포트·렌더링 확인이 필요하다.
3. 5-1 `PlatformCycleManager` NullReference 보고(10-16)가 미해결이다.
4. 2-4 수정 설계는 10-18에 Unity에 적용됐다(직접 완주 전).

## 환경 주의
- 이 프로젝트는 여러 PC가 OneDrive로 공유한다. 씬을 파일로 고치면 다른 쪽 Unity가 "외부 변경" 확인을 띄운다.
- `Assets/@ExternalAssets`는 Git 제외 폴더다(프리팹·이미지·음원은 Git에 없음).
- Unity MCP 설정은 프로젝트 루트 `.mcp.json`에 있다(임시 파일 아님).
- 백업: `@Scripts/backup-session.ps1`, `@Scripts/session-backup.py`, `@Scripts/restore-session.ps1`. 절차는 `@Edit/BACKUP_PROCESS.md`.

