# docs/ 디자인 시스템 — 단일 진실(Single Source of Truth)

웹사이트(`docs/*.html`)를 수정·추가할 때 **반드시 이 규칙을 지킨다.** 목적: 세션이 바뀌어도 스타일이 틀어지지 않게.

## 절대 규칙
1. **`docs/shared.css`가 유일한 진실이다.** 색·여백·라운드·그림자·폰트를 새로 지어내지 마라.
2. **hex 하드코딩 금지.** 항상 아래 CSS 변수를 쓴다. (예: `#3182f6` ❌ → `var(--accent)` ✅)
3. **새 컴포넌트가 필요하면** 인라인으로 박지 말고 **`shared.css`에 클래스/토큰을 먼저 추가**한 뒤 그 클래스를 쓴다.
4. 새 페이지는 기존 형제 페이지(예: `features.html`)의 구조를 복제해서 시작한다 — `<nav>` → `.page-hero` → `.abar` → `.section-wrap` 골격 유지.
5. 폰트는 Pretendard Variable. 코드/모노는 `'SF Mono','Fira Mono',monospace`.

## 컬러 토큰
| 용도 | 변수 | 값 |
|---|---|---|
| 배경 | `--bg` | #f0f2f5 |
| 카드/표면 | `--white` | #fff |
| 다크 히어로 | `--hero` | #0a0b0f |
| 메인 액센트(파랑) | `--accent` / `--accent-light` | #3182f6 / #e8f0fe |
| 성공·완료(초록) | `--green` / `--green-light` | #00c471 / #e6faf3 |
| 대기·주의(주황) | `--orange` / `--orange-light` | #f5a623 / #fff8ec |
| 진행보드(보라) | `--purple` / `--purple-light` | #7c3aed / #f5f3ff |
| 에러 | `--red` | #ef4444 |
| 본문/보조/흐림 | `--text` / `--text2` / `--text3` | #111216 / #6b7280 / #9ca3af |
| 보더 | `--border` | #e5e7eb |

## 형태 토큰
- 라운드: `--r` (14px) / `--r-sm` (10px)
- 그림자: `--shadow` (기본 카드) / `--shadow-lg` (강조 카드)

## 컴포넌트 어휘 (이미 정의됨 — 재사용)
- 네비: `nav` / `.logo` / `.nav-links` (현재 페이지는 `a.active`)
- 메인 히어로: `.hero` / `.hero-tag` / `.hero h1 em`(그라데이션) / `.hero-chips .hchip`
- 서브 히어로: `.page-hero` / `.page-hero-tag` / `.page-hero h1 em`
- 대상 표시 바: `.abar` / `.atag`(`atag-exec` 경영, `atag-dev` 개발) / `.abar-text`
- 섹션: `.section-wrap` / `.section-label` / `h2.stitle` / `.sdesc`
- 로드맵 카드: `.route-row` / `.rc`(`.active/.next/.later`) / `.rc-badge`(`rcb-now/next/later`)
- 파이프라인: `.pipeline-card` / `.flow` / `.fnode` / `.farrow` / `.branch-note`
- 보안: `.sec-grid` / `.sec-card` / `.sec-node`(`user/admin/blocked/allowed/rls`)
- 모델표: `.model-table` / `.model-badge`
- 데이터 스키마: `.schema-relation` / `.schema-box` / `.db-table` / `.why-card`
- 푸터: `footer`
- 유틸: `.divider` / `.mt32` / `.mt48` / `.two-col`

## 진행률·버전 표기 일관성
- 진행 상태 색 규칙: 완료=초록(`--green`), 진행 중=파랑(`--accent`), 대기=회색(`--text3`/`--border`).
- `progress.html`의 `.abar-text` "최종 갱신 YYYY-MM-DD"와 `download.html`의 버전 표기를 업데이트 때마다 함께 맞춘다.

## 알려진 부채(드리프트 위험)
- 현재 `progress.html` 등에 인라인 `style="..."` + 일부 hex 하드코딩(#a7f3d0 등)이 남아있다. 손댈 일이 생기면 그 김에 위 토큰/클래스로 흡수해 부채를 줄인다.
