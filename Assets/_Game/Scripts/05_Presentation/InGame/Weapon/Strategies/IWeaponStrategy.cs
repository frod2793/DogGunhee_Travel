using UnityEngine;
using InGame.ObjectPool;
using InGame.Weapon.Base;

using InGame.Core.Interfaces;

namespace InGame.Weapon.Strategies
{
    /// <summary>
    /// [설명]: 무기의 공격 동작을 정의하는 핵심 전략 인터페이스입니다.
    /// 전략 패턴(Strategy Pattern)을 사용하여 무기별 고유 동작(근접, 투사체, 오라 등)을 캡슐화합니다.
    /// </summary>
    public interface IWeaponStrategy
    {
        /// <summary>
        /// 무기 데이터와 오브젝트 풀을 사용하여 전략을 초기화합니다.
        /// </summary>
        /// <param name="data">무기 설정 데이터</param>
        /// <param name="poolManager">투사체 생성을 위한 풀 매니저</param>
        /// <param name="gameState">게임 상태 서비스</param>
        /// <param name="combatContext">전투 컨텍스트</param>
        /// <param name="playerContext">플레이어 컨텍스트</param>
        void Init(
            WeaponDataSO data, 
            WeaponPoolManager poolManager,
            IGameStateService gameState,
            ICombatContext combatContext,
            IPlayerContext playerContext);

        /// <summary>
        /// 실제 공격 로직을 수행합니다. (쿨타임이 끝났을 때 호출됨)
        /// </summary>
        /// <param name="stats">현재 무기 스탯</param>
        /// <param name="owner">무기 소유자 Transform</param>
        /// <param name="direction">공격 방향</param>
        void Attack(WeaponRuntimeStats stats, Transform owner, Vector3 direction);

        /// <summary>
        /// 매 프레임 업데이트가 필요한 로직을 처리합니다. (예: 오라 위치 동기화)
        /// </summary>
        /// <param name="stats">현재 무기 스탯</param>
        /// <param name="deltaTime">프레임 경과 시간</param>
        void OnUpdate(WeaponRuntimeStats stats, float deltaTime);
    }
}