using UnityEngine;
using InGame.Weapon.Base;

namespace InGame.Weapon.Controllers
{
    /// <summary>
    /// 플레이어(Owner) 주위를 공전하는 투사체 컨트롤러입니다.
    /// <br/> 공놀이(Ball) 무기 등에서 사용되며, 물리 충돌 처리는 BallDamageDealer에게 위임합니다.
    /// </summary>
    public class OrbitProjectile : MonoBehaviour
    {
        #region 1. 내부 변수 및 상태 (Internal State)

        // 소유자 및 위치 정보
        private Transform m_owner;
        private float m_currentAngle;
        private float m_radius;
        private float m_rotationSpeed;

        // 비주얼 설정
        private float m_rotationOffset;
        private bool m_rotateWithOrbit;

        // 상태 플래그
        private bool m_isInitialized = false;

        // 컴포넌트 참조
        private BallDamageDealer m_damageDealer;

        #endregion

        #region 2. Unity 라이프사이클 (Lifecycle)

        private void Awake()
        {
            // 데미지 딜러 컴포넌트 캐싱 또는 추가
            m_damageDealer = GetComponent<BallDamageDealer>();
            if (m_damageDealer == null)
            {
                m_damageDealer = gameObject.AddComponent<BallDamageDealer>();
            }
        }

        private void Update()
        {
            if (!m_isInitialized || m_owner == null)
            {
                // 소유자가 사라지면(사망 등) 비활성화
                if (m_isInitialized && m_owner == null)
                {
                    gameObject.SetActive(false);
                }
                return;
            }

            // 프레임당 각도 갱신
            m_currentAngle += m_rotationSpeed * Time.deltaTime;

            // 360도 넘어가면 리셋 (부동소수점 오차 방지)
            if (m_currentAngle >= 360f) m_currentAngle -= 360f;
            else if (m_currentAngle < 0f) m_currentAngle += 360f;

            UpdateTransform();
        }

        #endregion

        #region 3. 초기화 및 제어 (Init & Control)

        /// <summary>
        /// 투사체를 초기화하고 궤도 회전을 시작합니다.
        /// </summary>
        /// <param name="owner">공전의 중심이 되는 트랜스폼</param>
        /// <param name="radius">공전 반경</param>
        /// <param name="speed">회전 속도 (도/초)</param>
        /// <param name="startAngle">시작 각도</param>
        /// <param name="stats">무기 스탯 (공격력 등)</param>
        /// <param name="rotationOffset">자전 오프셋 각도</param>
        /// <param name="rotateWithOrbit">공전 방향에 맞춰 자전할지 여부</param>
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
            
            // 데미지 딜러 설정
            if (m_damageDealer != null)
            {
                m_damageDealer.Init(stats.AttackPower, stats.MobStunTime);
                // SetActiveDamage(true) 호출 제거 (기본적으로 활성화됨 가정)
            }
            
            m_isInitialized = true;

            // 초기 위치 즉시 설정
            UpdateTransform();
        }

        /// <summary>
        /// 현재 각도(Theta)와 반경(R)을 기반으로 투사체의 월드 좌표를 계산합니다.
        /// </summary>
        private void UpdateTransform()
        {
            if (m_owner == null) return;

            // 1. 삼각함수를 이용한 궤도 좌표 계산 (극좌표계 -> 직교좌표계 변환)
            float rad = m_currentAngle * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * m_radius;
            
            transform.position = m_owner.position + offset;

            // 2. 자전(Rotation) 계산
            if (m_rotateWithOrbit)
            {
                // 공전 궤도의 접선 방향을 바라보도록 회전 (+90도)
                // Sprite가 위쪽(Up)을 향한다고 가정할 때의 보정값
                float targetRotation = m_currentAngle - 90f + m_rotationOffset;
                transform.rotation = Quaternion.Euler(0, 0, targetRotation);
            }
            else if (Mathf.Abs(m_rotationOffset) > Mathf.Epsilon)
            {
                // 고정된 각도로 회전
                transform.rotation = Quaternion.Euler(0, 0, m_rotationOffset);
            }
            else
            {
                // 회전 없음
                transform.rotation = Quaternion.identity;
            }
        }

        #endregion
    }
}