---
name: "source-command-handon"
description: "HANDOFF.md에 기록된 중단된 작업을 실제로 이어서 진행 (다른 PC에서 세션을 처음 시작했을 때 사용)"
---

# source-command-handon

Use this skill when the user asks to run the migrated source command `handon`.

## Command Template

이 명령은 이전 대화 맥락이 전혀 없는 상태(다른 PC에서 방금 새로 시작한 세션)에서도 동작해야 한다.
프로젝트 루트의 `HANDOFF.md` 파일을 읽는 것부터 시작하라.

- **HANDOFF.md가 없으면**: 이어서 할 작업이 기록되어 있지 않다고 알리고 끝낸다. `/handoff`가 먼저 실행된 적이 없다는 뜻이다.
- **HANDOFF.md가 있으면** 아래 순서로 진행한다:
  1. `git log --oneline -10`, `git status`, `git diff`로 현재 저장소 상태를 확인해서 HANDOFF.md 내용과 대조한다. HANDOFF.md에 적힌 "다음 할 일" 중 이미 다른 커밋으로 반영된 항목이 있는지, 또는 HANDOFF.md가 가리키는 파일들이 실제로 존재하는지 확인한다.
  2. `.Codex/commands/resume.md`처럼 상태만 보고하고 멈추지 말고, HANDOFF.md의 "다음 할 일" 목록을 실제 작업 순서로 채택해서 **바로 이어서 작업을 진행**한다 — 첫 번째 미완료 항목부터 순서대로 실제 구현/수정/테스트를 시작한다.
  3. 각 항목을 시작하기 전에 "진행할까요?"라고 매번 묻지 않는다. 다만 파괴적이거나 되돌리기 어려운 작업(강제 푸시, 파일/브랜치 삭제, 커밋되지 않은 남의 변경사항을 덮어쓰는 것 등)이 다음 할 일에 포함되어 있다면 그 항목만 먼저 확인을 받는다.
  4. 진행 중 HANDOFF.md에 없던 중요한 판단이 필요하면 잠깐 멈추고 물어봐도 되지만, 그 외에는 계속 이어서 작업한다.

다음 할 일을 모두 마쳤거나 더 진행할 수 없는 지점에 도달하면, 무엇을 완료했고 무엇이 남았는지 간단히 요약하라. 세션이 다시 중단될 가능성이 있다면 이 시점에서 `/handoff`를 실행해 HANDOFF.md를 최신 상태로 갱신할 것을 사용자에게 제안한다.
