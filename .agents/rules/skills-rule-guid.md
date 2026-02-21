---
trigger: always_on
---

커스텀 검증 및 유지보수 스킬은 `.agents/skills/`에 정의되어 있습니다.

| Skill | Purpose |
|-------|---------|
| `verify-implementation` | 프로젝트의 모든 verify 스킬을 순차 실행하여 통합 검증 보고서를 생성합니다 |
| `manage-skills` | 세션 변경사항을 분석하고, 검증 스킬을 생성/업데이트하며, skills-rule-guid.md를 관리합니다 |
| `verify-lobby-navigation` | 로비 UI의 팝업 관리, 뒤로가기(ESC) 및 중복 로딩 가드 검증 |
| `verify-inventory-system` | 인벤토리 데이터 구조 및 환전 로직 검증 |
| `verify-login-flow` | 타이틀 씬의 로그인 흐름(MVVM), 어드레서블 로딩 및 리모트 동기화 검증 |
| `verify-remote-data` | 구글 시트(GAS) 기반 리모트 데이터 강제 동기화 및 에디터 폴백 검증 |
| `verify-data-persistence` | 데이터 DTO, 서비스 의존성, 암호화 저장소 및 절대값 동기화 무결성 검증 |
| `verify-sound-system` | 사운드 구현 패턴 및 DI 주입 정합성 검증 (추천 방식) |
| `verify-weapon-system` | 무기 시스템의 의존성 주입(DI), POCO 전략 패턴, 팩토리 생성을 검증합니다. |
| `verify-ingame-mob` | 인게임 몹(Mob) 풀링 최적화 및 화면 밖 이탈 방지 검증 |
| `verify-player-system` | 플레이어 컨트롤러, 자동 공격(AI) 루프, 카이팅(Kiting) 이동 및 토글 연동 무결성 검증 |
| `verify-ingame-core` | 인게임 핵심 시스템, 초기화 순서 안전성, UI 데이터 바인딩 및 조이스틱 경계 로직 검증 |