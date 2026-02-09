using UnityEngine;
using InGame.Weapon.Base;
using InGame.ObjectPool;

namespace InGame.Weapon
{
    /// <summary>
    /// 플레이어 주위를 회전하는 투사체 컴포넌트입니다.
    /// '공놀이(Ball)' 무기에 주로 사용됩니다.
    /// </summary>
    public class OrbitProjectile : MonoBehaviour
    {
        #region 내부 상태 및 필드

        private Transform m_owner;
        private float m_currentAngle;
        private float m_radius;
        private float m_rotationSpeed;
        private BallDamageDealer m_damageDealer;
        private bool m_isInitialized = false;

        // 추가된 회전 제어 변수
        private float m_rotationOffset;
        private bool m_rotateWithOrbit;

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            m_damageDealer = GetComponent<BallDamageDealer>() ?? gameObject.AddComponent<BallDamageDealer>();
        }

        private void Update()
        {
            if (!m_isInitialized || m_owner == null) return;

            // 회전 로직 (Time.deltaTime 기반)
            m_currentAngle += m_rotationSpeed * Time.deltaTime;
            UpdatePositionAndRotation();
        }

        #endregion

        #region 초기화 및 제어

        /// <summary>
        /// 투사체를 초기화하고 회전을 시작합니다.
        /// </summary>
        /// <param name="owner">회전 중심이 될 트랜스폼</param>
        /// <param name="radius">회전 반경</param>
        /// <param name="speed">회전 속도 (도/초)</param>
        /// <param name="startAngle">시작 각도</param>
        /// <param name="stats">무기 런타임 스탯</param>
        /// <param name="rotationOffset">Z축 회전 보정값</param>
        /// <param name="rotateWithOrbit">궤도 방향에 맞춰 회전할지 여부</param>
        public void Initialize(
            Transform owner, 
            float radius, 
            float speed, 
            float startAngle, 
            WeaponRuntimeStats stats,
            float rotationOffset = 0f,
            bool rotateWithOrbit = true)
        {
            m_owner = owner;
            m_radius = radius;
            m_rotationSpeed = speed;
            m_currentAngle = startAngle;
            m_rotationOffset = rotationOffset;
            m_rotateWithOrbit = rotateWithOrbit;
            
            if (m_damageDealer != null)
            {
                m_damageDealer.Initialize(stats.AttackPower, stats.MobStunTime);
            }
            
            m_isInitialized = true;
            UpdatePositionAndRotation();
        }

        private void UpdatePositionAndRotation()
        {
            if (m_owner == null) return;

            // 1. 위치 계산 (삼각함수 원형 궤도)
            float rad = m_currentAngle * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * m_radius;
            transform.position = m_owner.position + offset;

            // 2. 회전 계산
            if (m_rotateWithOrbit)
            {
                // 궤도 방향(접선 방향)을 바라보도록 설정
                // 각도에 90도를 더하면 진행 방향을 향하게 됨 (반시계 방향 기준)
                float targetRotation = m_currentAngle + 90f + m_rotationOffset;
                transform.rotation = Quaternion.Euler(0, 0, targetRotation);
            }
            else if (m_rotationOffset != 0)
            {
                // 고정된 보정값만 적용
                transform.rotation = Quaternion.Euler(0, 0, m_rotationOffset);
            }
        }

        #endregion
    }
}
