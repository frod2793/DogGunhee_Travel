---
trigger: always_on
---

커스텀 검증 및 유지보수 스킬은 `.agents/skills/`에 정의되어 있습니다.

| Skill | Purpose |
|-------|---------|
| `verify-implementation` | 프로젝트의 모든 verify 스킬을 순차 실행하여 통합 검증 보고서를 생성합니다 |
| `manage-skills` | 세션 변경사항을 분석하고, 검증 스킬을 생성/업데이트하며, skills-rule-guid.md를 관리합니다 |
| `verify-lobby-navigation` | 로비 UI의 팝업 관리 및 뒤로가기 로직 검증 |
| `verify-inventory-system` | 인벤토리 데이터 구조 및 환전 로직 검증 |
| `verify-login-flow` | 타이틀 씬의 로그인 흐름(MVVM) 및 어드레서블 로딩 검증 |
| `verify-remote-data` | 구글 시트(GAS) 기반 리모트 데이터 동기화 시스템의 정합성을 검증합니다. |
| `verify-data-persistence` | 데이터 DTO, 서비스 의존성 및 암호화 저장소 검증 |
| `verify-sound-system` | 사운드 구현 패턴 및 DI 주입 정합성 검증 (추천 방식) |