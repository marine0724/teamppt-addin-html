TEAMPPT — A-1b/c 검증 완료, A-1d "벡터검색 읽기경로" 착수

너는 확정된 실행계획을 정확히 구현하는 개발 실행자다. 범위 임의확장 금지.

[먼저 정독]
1. PROGRESS-BOARD.md
2. docs/superpowers/plans/2026-06-22-a1d-vector-read-path.md ← 이번에 실행할 plan
3. 메모리: architecture_ingest_pipeline, architecture_supabase_schema,
   feedback_admin_build_log, feedback_coordinate_converter, feedback_no_keys_in_docs

[선행 완료 — 이전 세션들]
- A-1a LLM 이해 어댑터: ✅ 완료
- A-1b/c Supabase 적재경로: ✅ 완료 (커밋 0113ad0)
  - 임시 인제스트 UI (파일선택→split→이해→임베딩→Supabase 업로드)
  - 토스 스타일 진행 패널 (세로 카드, 카드폭 썸네일, 스캔 애니메이션)
  - 완료 목록 별도 접히는 카드 (최근 완료 미리보기)
  - LLM 이름 실시간 반영 (이해 완료 시 카드 이름 갱신)
  - upsert 정상 (on_conflict=file), 섹션 없는 pptx 폴백
  - Supabase 확인: assets 42행 (layout 35 + header 7), Storage 정상
- Supabase 인프라: 테이블·pgvector·match_assets RPC·RLS·Storage 버킷 모두 셋업 완료

[이번 세션에서 할 일 = A-1d]
- docs/superpowers/plans/2026-06-22-a1d-vector-read-path.md의 Task를 순서대로 실행
- 핵심: 질의→임베딩→match_assets RPC→AI탭 추천 카드 표시
- 검색 결과를 보면서 LLM 프롬프트 튜닝이 필요할 수 있음 (이름/카테고리 품질)

[보류 사항 — A-1d 진행하면서 확인]
- LLM 이름 품질: "header"인데 "layout"으로 분류되는 케이스 있음
- 이름이 너무 길고 설명적 (예: "좌측 번호 강조 상단 제목 레이아웃")
- → 검색 결과 보면서 AssetUnderstandingService 프롬프트 조정 → 재인제스트

[빌드]
- 관리자 빌드: /fileLogger 방식 사용 (shell redirect 금지)
  Start-Process -FilePath $msbuild -ArgumentList "`"$sln`" /t:Build ... /fileLogger `"/flp:logfile=$logFile`"" -Verb RunAs -Wait -WindowStyle Hidden -WorkingDirectory "c:\Projects\teamppt-addin"
- 테스트: 본프로젝트 빌드 → 테스트 빌드(BuildProjectReferences=false) → dotnet test --no-build

[건드리지 말 것]
- Core/Connect.cs/Globals.cs 수정 금지 (TaskPaneHost.cs 배선은 plan에 적힌 범위만)
- 의존성 추가 금지
- 시크릿 평문 커밋 금지
- CoordinateConverter 폴백 금지
- Gemini 키 형식 손대지 마라

[완료 후]
- 관리자 빌드 + 테스트 GREEN 확인
- PROGRESS-BOARD.md 갱신
- 사용자에게 PPT 재시작 → 검색 테스트 안내
