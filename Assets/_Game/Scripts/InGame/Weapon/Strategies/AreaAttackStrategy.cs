using UnityEngine;
using InGame.ObjectPool;
using InGame.Weapon.Base;

namespace InGame.Weapon.Strategies
{
    /// <summary>
    /// 범위 공격(Area of Effect)을 수행하는 전략입니다.
    /// ISpawnPositionStrategy를 사용하여 소환 위치를 결정하고,
    /// IAreaAttackEffect 인터페이스를 통해 이펙트를 초기화합니다.
    /// </summary>
    /// <typeparam name="TEffect">생성할 이펙트/오브젝트의 타입 (MonoBehaviour, IAreaAttackEffect)</typeparam>
    public class AreaAttackStrategy<TEffect> : IWeaponStrategy 
        where TEffect : MonoBehaviour, IAreaAttackEffect
    {
        private readonly ISpawnPositionStrategy m_positionStrategy;
        private Camera m_camera;

        public AreaAttackStrategy(ISpawnPositionStrategy positionStrategy)
        {
            m_positionStrategy = positionStrategy;
            m_camera = Camera.main;
        }

        public void Initialize(WeaponDataSO data)
        {
            // 데이터 기반 초기화가 필요하다면 여기서 수행
        }

        public void Attack(WeaponRuntimeStats stats, Transform owner, Vector3 direction)
        {
            if (m_camera == null) m_camera = Camera.main;

            // 1. 위치 계산
            Vector3 spawnPos = m_positionStrategy.GetSpawnPosition(m_camera);

            // 2. 오브젝트 풀에서 가져오기
            var effect = WeaponPoolManager.Instance.Get<TEffect>();
            if (effect != null)
            {
                effect.transform.position = spawnPos;
                effect.gameObject.SetActive(true);
                
                // 3. 초기화 (데미지, 지속시간 등)
                effect.Initialize(stats);
            }
        }

        public void OnUpdate(WeaponRuntimeStats stats, float deltaTime)
        {
            // 이펙트 자체의 업데이트 로직이 있다면 여기서 처리 가능하나, 
            // 보통 Prefab의 MonoBehaviour가 처리함.
        }
    }
}
