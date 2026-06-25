# TEAMPPT Add-in — 변경 기록

## 2026-06-25

### 판단 파이프라인 전체 구현 (Task 1~10)
- `RecommendationTrace` + `DesignCritique` 모델 구현 (TDD)
- 이해·구성 단계 reasoning 필드 추가
- Gemini 다중 이미지 `GenerateJsonAsync` 오버로드 — 구성기가 후보 썸네일을 **실제로 보고** 조합 선택
- 검수자 루브릭(6차원 100점) 스키마 + 파서 + 오케스트레이터 (`DesignCritiqueService`)
- `RecommendationService`가 Trace 조립·반환 + 온디맨드 검수 진입점
- "🔍 판단 과정" 접이식 패널 UI + "🔍 디자이너 검수 받기" 버튼
- 배치 후 결과 슬라이드 렌더 캡처 → 검수자에 입력

### 독백 + 병목 진단 정밀화 (설계·플랜 확정)
- reasoning을 한국어 존대말 독백으로 실시간 버블에 흘리는 설계
- 검색 유사도 trace 노출 + 검수 병목 4분류(`기능/데이터추출/에셋부족/에셋품질`)
- thinkingBudget 파라미터화(이해·구성 768, 검수 2048)

### 배포 버그 수정 (R2)
- `install.bat` robocopy 트레일링 백슬래시 버그 — 신규 설치 시 0개 복사 차단
- v1.0.0 자산 교체 재업로드 완료

### 문서·인프라
- `docs/STYLE.md` 디자인 시스템 문서화 (CSS 변수·컴포넌트 어휘 정리)
- Claude Code 커맨드 (handoff/ship) 추가

## 2026-06-24

### 자동 업데이트 (R1)
- `UpdateService` + `updater.bat` + 패널 배너 + 고정경로 install/uninstall
- GitHub Release v1.0.0 생성, 릴리스 패키징 스크립트 `scripts/package-release.ps1`
- `docs/download.html` + `docs/version.json` (GitHub Pages 라이브)

### 에셋 조합 추천 (Route A 핵심)
- 추천 조합을 새 슬라이드에 배치 (`feat/place`)
- 조합 추천 오케스트레이터 `RecommendationService` (비파괴·추천만)
- LLM 조합 선택 + cover/end slide 단축 recommender
- kind별 벡터 후보 풀 provider + 캐시 폴백 분류
- 에셋 kind 4분류 (slide/header/layout/component) + capacity·material_kinds
- match_assets kind 필터 인자 + source_deck 반환

### 버그 수정
- 인제스트 재시도 완료 시 카운터 이중계산 버그 수정
- 재시도 진행 애니메이션 복구
- capacity·material_kinds를 required로 변경 (Gemini 생략 방지)
