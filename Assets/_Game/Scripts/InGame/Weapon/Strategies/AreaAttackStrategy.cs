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
            if (data == null || data.ProjectilePrefab == null)
            {
                LogManager.LogError($"[AreaAttackStrategy] 데이터 또는 프리팹이 유효하지 않습니다. (Weapon: {data?.WeaponName})");
                return;
            }

            // 제네릭 TEffect가 MonoBehaviour인지 확인
            var prefab = data.ProjectilePrefab.GetComponent<TEffect>();
            if (prefab == null)
            {
                LogManager.LogError($"[AreaAttackStrategy] 프리팹에 {typeof(TEffect).Name} 컴포넌트가 없습니다.");
                return;
            }

            // 풀 등록 (이미 등록된 경우 무시됨)
            WeaponPoolManager.Instance.GetOrAddPool<TEffect>(
                () => Object.Instantiate(prefab),
                actionOnGet: (e) => e.gameObject.SetActive(true),
                actionOnRelease: (e) => e.gameObject.SetActive(false),
                actionOnDestroy: (e) => Object.Destroy(e.gameObject),
                maxSize: 20 // 적절한 풀 사이즈 설정
            );
        }

        public void Attack(WeaponRuntimeStats stats, Transform owner, Vector3 direction)
        {
            if (m_camera == null) m_camera = Camera.main;

            // 1. 위치 계산 (Owner 위치 또는 카메라 기반)
            Vector3 spawnPos = m_positionStrategy.GetSpawnPosition(owner, m_camera);

            // 2. 오브젝트 풀에서 가져오기
            var effect = WeaponPoolManager.Instance.Get<TEffect>();
            
            if (effect != null)
            {
                effect.transform.position = spawnPos;
                // Get 호출 시 pool의 actionOnGet에서 SetActive(true)가 호출되므로 여기서는 생략 가능하나 명시적으로 확인
                if (!effect.gameObject.activeSelf) effect.gameObject.SetActive(true);
                
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
