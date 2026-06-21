# 🗺️ TEAMPPT 개발 진행 보드

> 이 파일 하나만 열어두면 "지금 어디서 뭘 하는지" 보입니다. Claude가 매 세션 함께 유지.
> **기록용 아카이브가 아니라 "지금 여기" 작업 보드.** 끝난 잎(Task)은 지우고 교체, 숲·나무 단위는 끝날 때까지 유지. (규칙: CLAUDE.md)
> 최종 갱신: 2026-06-21 · 현재 작업: **패널 버튼화 + 중복 본질 해결 (설계 승인·spec 완료 → 다음: writing-plans, 새 세션·별도 브랜치)**

---

## 🌲 숲 — 제품 로드맵 (전체 단계)

```
[A 데이터스키마]──▶[E LLM연동]──▶[ B 에셋 인제스트 ]──▶[D 리디자인]
      ✅              ✅            ◀── 진행 중              ⬜ 예정
   에셋 그릇       AI 말하기      에셋 자동 수집·정리      한방 리디자인(킬러)
```

목표: 사용자 초안 슬라이드를 **AI가 한방에 리디자인**. 품질의 핵심 = 좋은 에셋 데이터.

- **B 진행 상황:** "로컬 인제스트 코어"(묶음 pptx → 낱장 에셋 split + 768px PNG) ✅ **완료·main 머지** (Task 1~7, 수동검증 35/35 PASS).
- **B 남은 것(인제스트 나무 재개 시):** LLM 이해 어댑터(Gemini/Claude) → 임베딩 + Supabase 업로드 + 관리자 게이트 → 추천·삽입 읽기 경로.

---

## 🌳 나무 — 지금 plan: "패널 버튼화 + 중복 본질 해결" (B 재개 전 곁가지 버그 task)

> ⚠️ Connect.cs/TaskPaneHost 수정 → 인제스트와 분리, **별도 브랜치**에서 진행.

**문제:** 패널이 시작 시 자동으로 뜨고, 창이 여럿이면 누적 중복/엉뚱한 창에 붙음.
**방향(접근 A, 사용자 승인 대기):** 리본 **전용 TEAMPPT 탭 + 토글 버튼**으로 전환(자동 열기 폐기) + `Dictionary<창, CTP>`로 **창마다 1개 소유·추적·닫을 때 해제** → 중복이 구조적으로 불가능.

```
로드 시 CTP 1개 자동생성(덮어쓰기·해제없음)   ──▶   버튼 토글 + 창별 CTP 추적/해제
        = 누적 중복 (현재)                              = 창마다 1개 (목표)
```

---

## 🍃 잎 — Task 현황

> 브랜치 `panel-button-per-window` · [구현계획](docs/superpowers/plans/2026-06-21-panel-button-per-window.md) 작성 완료.

| # | Task | 무엇을 | 상태 |
|---|------|--------|------|
| — | 설계·spec·계획 | brainstorming 접근법 A → [spec](docs/superpowers/specs/2026-06-21-panel-button-per-window-design.md) → [plan](docs/superpowers/plans/2026-06-21-panel-button-per-window.md) | ✅ 완료 |
| 0 | systematic-debugging | `debug.log`로 `CTPFactoryAvailable`/`WindowActivate` 실측 → §2 진단 확정 | ⬜ 다음 |
| 1 | WindowSweep (TDD) | 회수판단 순수함수 + 단위테스트 4 GREEN | ⬜ |
| 2 | TaskPaneManager | 창별 CTP 추적·해제 3종 세트 (신규 모듈) | ⬜ |
| 3 | Connect 버튼화 | 리본 토글 버튼 + 위임, 자동 생성 제거 (수동검증 1~4) | ⬜ |
| 4 | 동기화 견고화 | 창 전환/닫기/종료 회수 검증 (수동검증 5~10) | ⬜ |
| 5 | 마무리 | 보드/인계 갱신 + main 통합 | ⬜ |

> Task 0이 구현 선행. 순수함수만 TDD GREEN, 나머지는 PowerPoint 수동검증 PASS로 확정.
