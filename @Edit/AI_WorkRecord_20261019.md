<!-- Internal AI work record (LHS_Custumer rule 54). Not for user-facing reports. Linked to the work by the paths listed below. -->
<!-- AI: Claude Sonnet 5.5 | completed: 2026-10-19 | scope: one long session (rule edits, document/path fixes, presentation, teaser re-shoot) -->

# AI 작업 기록 2026-10-19

작업한 AI와 모델, 완료일, 작업 요약을 남기는 내부 기록 파일이다. 개별 파일 형식(.png, .pptx, 영상 등)이나 대량 수정 때문에 파일마다 주석을 넣기 어려운 항목은 이 파일에 경로로 연결해 둔다. 이후 AI가 이전 작업자를 확인할 때 이 파일을 참조한다.

| 항목 | 작업 AI | 모델 | 완료일 | 요약 |
|---|---|---|---|---|
| `@Edit/LHS_Custumer.md` | Claude | Claude Sonnet 5.5 | 2026-10-12~19 | 수칙 추가/삭제/번호 정리(빈 27·30번 제거로 번호 연속화), 경로를 `<현재 사용자명>` 형식으로 변경, 26번 저장 위치 규칙 변경. 54·55번은 사용자 지시로 다른 형태로 갱신됨 |
| `@Edit/MapEdit.md` | Claude | Claude Sonnet 5.5 | 2026-10-12 | 14번(점프 80% 거리), 19번(영역 배치 순서) |
| `@Edit/DEPLOYMENT_2-1.md` | Claude | Claude Sonnet 5.5 | 2026-10-12 | 커밋 단계를 사용자 수행으로, Unity 적용은 사용자 요청 시에만, 간격 검증에 점프 성능 80% 기준 추가 |
| `@Edit/OpeningDesign.md`, `@Edit/PlayerAnimation_HeroKnightTest.md` | Claude | Claude Sonnet 5.5 | 2026-10-12~19 | 3MB 이상 산출물을 `Documents\LHS`로 옮긴 경로 갱신, 수칙 번호 인용 갱신 |
| `@Edit/BossDesign_Stage2_StoneSerpent.md`, `BossDesign_Stage3_VolcanicScorpion.md`, `BossDesign_Stage4_BlindCaveHorror.md`, `MapDesign_Stage1-3.md`, `MapDesign_Stage2-3.md`, `MapDesign_Stage2-4.md`, `MapDesign_Stage3-2.md`, `MapDesign_Stage3-4.md`, `Monster.md`, `PlatformDesign_InteractiveFootholds.md`, `TEST_PROTOCOL.md`, `TrapDesign_SwitchTeleport.md`, `TrapDesign_SwitchZoneMonster.md`, `UIDesign_HUD_Health.md`, `CameraDesign_FollowCamera.md`, `Stage4_Attacks/GenerationNotes.md` | Claude | Claude Sonnet 5.5 | 2026-10-19 | 수칙 번호 정리에 맞춘 인용 번호 수정, 삭제된 27번 인용 문구 정리, 사용자명 경로를 `<현재 사용자명>`으로 |
| `Assets/@Scripts/**`, `Assets/Scripts/**`의 `.cs` 주석 약 35개 | Claude | Claude Sonnet 5.5 | 2026-10-19 | 주석 안의 수칙 번호만 새 번호 체계로 수정(코드 변경 없음) |
| `@Edit/BACKUP_SKILL_DEFINITION.md`, `BACKUP_SYSTEM_README.md`, `SESSION_BACKUP_DESIGN.md`, `HANDOFF.md`, `@Scripts/export-session.ps1`, `@Edit/hk2_gen/*.ps1`(5개) | Claude | Claude Sonnet 5.5 | 2026-10-19 | 특정 사용자명 경로를 `<현재 사용자명>` 또는 `$env:USERPROFILE`/`$env:USERNAME`으로 |
| `Documents\LHS\FinalProject_@Edit\` (옮긴 3MB 이상 파일 13개) | Claude | Claude Sonnet 5.5 | 2026-10-19 | `@Edit`의 큰 산출물을 규칙에 맞게 이동 |
| `Documents\LHS\@Preview\Preview_5_GPT_Work_Room\` (이미지 24개) | Claude | Claude Sonnet 5.5 | 2026-10-19 | `Downloads`의 GPT_Work_Room/해시 이름 이미지를 이동하고 내용 키워드로 이름 붙임 (이미지 내용은 직접 확인) |
| `@Edit/Presentation_GameMaking.pptx` | Claude | Claude Sonnet 5.5 | 2026-10-19 | 게임 제작 과정 발표 자료(8장, 3~4분). 파일 속성(author/subject)에도 같은 기록을 남김 |
| `@Edit/teaser/build_teaser_reshoot.py`, `@Edit/Teaser_Production_20261018.md`(재촬영 절) | Claude | Claude Sonnet 5.5 | 2026-10-19 | 티저 재촬영 조립 스크립트와 기록 |
| `Assets/@Scripts/Teaser/TeaserBot.cs`, `TeaserCapture.cs`, `TeaserDirector.cs` | Claude | Claude Sonnet 5.5 | 2026-10-19 | 봇의 방향/걷기 모션 수정, 보스전 모드, 촬영 진행자 신설. 수칙 38/39/41에 맞게 Find 계열 조회·프레임 할당 제거. 각 파일 맨 위 주석에도 기록 |
| `Documents\LHS\컨셉\FinalDungeon_Teaser_v1_Reshoot.mp4` | Claude | Claude Sonnet 5.5 | 2026-10-19 | 재촬영 티저(2분 58.6초). 영상에는 작업자 표시를 넣지 않음 |
| `Documents\LHS\@Preview\Preview_6_Presentation\`, `Preview_7_Teaser_Reshoot\` | Claude | Claude Sonnet 5.5 | 2026-10-19 | 발표 자료/티저 미리보기 이미지(수칙 55) |

| `@Edit/Ending/MOVED_FILES.md`, `Assets/@Scripts/Ending/EndingSceneController.cs`(영상 참조 방식), `Assets/Scenes/Ending.unity`(`endingVideo` 연결), `Assets/@ExternalAssets/Ending/FinalDungeon_Ending.mp4`, `Documents\LHS\FinalProject_@Edit\Ending\` | Claude | Claude Sonnet 5.5 | 2026-10-19 | 엔딩 영상 등 큰 파일을 수칙 43/48에 맞게 이동하고 `StreamingAssets` 경로 참조를 `VideoClip` 참조로 변경 (엔딩 제작 원본 작업은 다른 세션의 것) |

모델이 확실하지 않은 항목은 없다.
