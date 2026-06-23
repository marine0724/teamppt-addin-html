화면 공유 진단 기능을 구현해줘. 설계와 구현 계획이 이미 작성돼 있어.

- 스펙: docs/superpowers/specs/2026-06-23-screen-share-diagnosis-design.md
- 계획: docs/superpowers/plans/2026-06-23-screen-share-diagnosis.md

superpowers:executing-plans 스킬로 이 계획을 Task 1부터 순서대로 실행해.
각 Task의 step(테스트 작성 → 실패 확인 → 구현 → 통과 확인 → 커밋)을 그대로 따라가고,
Task가 끝날 때마다 멈춰서 결과를 보고해줘.

빌드는 반드시 CLAUDE.md 규칙대로 MSBuild 관리자 권한(Start-Process -Verb RunAs)으로 하고,
빌드 후 DLL 타임스탬프(1분 이내) + build.log 오류 0건을 검증해. (cmd 래핑/stdout redirect 금지)

Task 1·2는 xUnit 테스트가 있으니 dotnet test로 검증하고,
Task 3·4(COM/WPF)는 자동 테스트가 없으니 코드 완결 후 Task 5에서 PowerPoint로 수동 검증해.

마지막에 PROGRESS-BOARD.md와 PITCH.md를 둘 다 갱신해
(PITCH는 §4 예정 항목을 §2 완성 기능으로 비전문가 언어로 승격).
