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
        #region 내부 상태 및 변수

        private Transform m_owner;
        private float m_currentAngle;
        private float m_radius;
        private float m_rotationSpeed;
        private BallDamageDealer m_damageDealer;
        private bool m_isInitialized = false;

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
            if (!m_isInitialized || m_owner == null)
            {
                return;
            }

            // 프레임당 회전 각도 갱신
            m_currentAngle += m_rotationSpeed * Time.deltaTime;
            UpdatePositionAndRotation();
        }

        #endregion

        #region 초기화 및 제어

        /// <summary>
        /// 투사체를 초기화하고 궤도 회전을 시작합니다.
        /// </summary>
        public void Init(
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
                m_damageDealer.Init(stats.AttackPower, stats.MobStunTime);
            }
            
            m_isInitialized = true;
            UpdatePositionAndRotation();
        }

        /// <summary>
        /// 현재 각도와 반경에 맞춰 물리적인 위치와 회전값을 동기화합니다.
        /// </summary>
        private void UpdatePositionAndRotation()
        {
            if (m_owner == null)
            {
                return;
            }

            // 1. 삼각함수를 이용한 궤도 좌표 계산
            float rad = m_currentAngle * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * m_radius;
            transform.position = m_owner.position + offset;

            // 2. 자전 회전 계산
            if (m_rotateWithOrbit)
            {
                // 궤도 진행 방향(접선)을 바라보도록 설정
                float targetRotation = m_currentAngle + 90f + m_rotationOffset;
                transform.rotation = Quaternion.Euler(0, 0, targetRotation);
            }
            else if (m_rotationOffset != 0)
            {
                // 고정된 오프셋 회전 적용
                transform.rotation = Quaternion.Euler(0, 0, m_rotationOffset);
            }
        }

        #endregion
    }
}
