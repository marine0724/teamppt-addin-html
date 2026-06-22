TEAMPPT — A-2 화면공유 시스템 개발

[상황]
- A-1 데이터 토대 완료 (인제스트·벡터검색·추천 전부 동작)
- 다음 단계: A-2 실시간 공유엔진
- PROGRESS-BOARD.md 먼저 읽을 것

[A-2 목표]
- 슬라이드 리더: 현재 슬라이드/전체 덱을 온디맨드로 읽기
- 화면공유 표시: [슬라이드 N 공유중] indicator
- 로드맵 위치: A-1(데이터) → **A-2(공유)** → A-3(추천·진단 UX)

[병행 작업 — 별도 세션에서]
1. 데이터 추출 품질 확인 — Supabase 실제 메타데이터 검토
2. 인제스트 시 에셋 이름 규칙 결정
3. 기존 표지 에셋 규약 맞춰 재제작 (Placeholder→일반 shape, 배경채우기→도형)
4. 대표 발표 준비 — 시스템 구조·규약·진행 상황 정리

[이전 세션 완료 사항 (06-23)]
- GhostWindow 크기 정상화 (ExportShapesComposite 방식)
- 표지 에셋 Placeholder/배경 유실 → PPT COM 근본 제약, 에셋 제작 규약으로 해결 (spec §5 #6·7)
- 검색 비용 최적화: thinkingBudget 1024→0, 카탈로그 입력 다이어트

[빌드]
- 관리자 빌드 필수 (COM 등록): CLAUDE.md 빌드 섹션 참고
- 테스트 전 PPT 완전 종료 필수 (taskkill /f /im POWERPNT.EXE)

[건드리지 말 것]
- Core/Connect.cs, Globals.cs 수정 금지
- CoordinateConverter에 폴백 로직 추가 금지
- ThumbnailGenerator.cs, GhostWindow.cs — 방금 수정 완료, 건드리지 말 것
- 의존성 추가 금지, 시크릿 평문 금지
