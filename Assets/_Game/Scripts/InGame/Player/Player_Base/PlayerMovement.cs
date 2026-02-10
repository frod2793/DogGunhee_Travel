using UnityEngine;

namespace InGame.Player.Player_Base
{
    /// <summary>
    /// 플레이어의 물리적 이동(Transform Position)과 시각적 회전(Model Rotation)을 담당하는 순수 로직 클래스입니다.
    /// <br/> PlayerController의 Update 사이클에서 호출됩니다.
    /// </summary>
    public class PlayerMovement
    {
        #region 1. 내부 상태 및 캐시 (State & Cache)

        // 데이터 제공자 및 타겟 트랜스폼
        private readonly PlayerBase m_playerStat;
        private readonly Transform m_rootTransform;   // 실제 이동할 최상위 부모
        private readonly Transform m_modelTransform;  // 회전할 비주얼 자식 객체
        private readonly SpriteRenderer m_mapBoundary; // 이동 제한 구역

        #endregion

        #region 2. 생성자 (Constructor)

        /// <summary>
        /// 이동 시스템을 초기화합니다.
        /// </summary>
        /// <param name="playerBase">이동 속도 등 스탯 정보를 가진 컴포넌트</param>
        /// <param name="rootTransform">이동시킬 플레이어의 최상위 Transform</param>
        /// <param name="characterTransform">회전시킬 캐릭터 모델(스프라이트)의 Transform</param>
        /// <param name="mapRange">이동 제한 맵 스프라이트 (없으면 null)</param>
        public PlayerMovement(PlayerBase playerBase, Transform rootTransform, Transform characterTransform, SpriteRenderer mapRange)
        {
            m_playerStat = playerBase;
            m_rootTransform = rootTransform;
            m_modelTransform = characterTransform;
            m_mapBoundary = mapRange;
        }

        #endregion

        #region 3. 이동 제어 (Movement Control)

        /// <summary>
        /// 입력된 방향벡터를 기반으로 캐릭터를 이동시키고 맵 안으로 위치를 보정합니다.
        /// </summary>
        /// <param name="direction">정규화된 이동 방향 벡터 (Vector2)</param>
        public void Move(Vector2 direction)
        {
            // 필수 컴포넌트가 없거나 이동 입력이 미미하면 리턴
            if (m_playerStat == null || m_rootTransform == null || direction.sqrMagnitude < 0.001f) return;

            // 1. 이동할 변위(Delta) 계산
            // 거리 = 속력 * 시간
            float moveDistance = m_playerStat.MoveSpeed * Time.deltaTime;
            Vector3 moveDelta = (Vector3)direction * moveDistance;

            // 2. 예상 목표 위치 계산
            Vector3 targetPosition = m_rootTransform.position + moveDelta;

            // 3. 맵 경계 클램핑 (이동 제한)
            // Z축 값은 기존 위치를 유지해야 함 (2D 레이어 이슈 방지)
            targetPosition.z = m_rootTransform.position.z; 
            Vector3 clampedPosition = GetClampedPosition(targetPosition);

            // 4. 최종 위치 적용
            m_rootTransform.position = clampedPosition;
            
            // 5. 바라보는 방향(회전) 업데이트
            UpdateFacingDirection(direction.x);
        }

        #endregion

        #region 4. 내부 유틸리티 (Internal Logic)

        /// <summary>
        /// 목표 위치가 맵 경계(Bounds)를 벗어나지 않도록 보정된 좌표를 반환합니다.
        /// </summary>
        private Vector3 GetClampedPosition(Vector3 targetPos)
        {
            if (m_mapBoundary == null) return targetPos;

            Bounds bounds = m_mapBoundary.bounds;

            // 맵 영역 안으로 좌표 제한 (Clamp)
            float clampedX = Mathf.Clamp(targetPos.x, bounds.min.x, bounds.max.x);
            float clampedY = Mathf.Clamp(targetPos.y, bounds.min.y, bounds.max.y);
            
            // Z축은 입력받은 targetPos의 Z를 그대로 유지
            return new Vector3(clampedX, clampedY, targetPos.z);
        }

        /// <summary>
        /// X축 이동 방향에 따라 캐릭터 스프라이트를 좌우 반전(회전)시킵니다.
        /// </summary>
        private void UpdateFacingDirection(float xDir)
        {
            if (m_modelTransform == null) return;

            // X축 입력이 거의 없으면(수직 이동 중이면) 회전하지 않음 -> 기존 방향 유지
            if (Mathf.Abs(xDir) <= 0.01f) return;

            // 기존 로직 유지:
            // x < 0 (왼쪽 입력) -> Y축 0도
            // x > 0 (오른쪽 입력) -> Y축 180도
            // (참고: 이 로직은 원본 스프라이트가 왼쪽을 보고 있다고 가정합니다. 
            //  일반적으로는 오른쪽이 0도, 왼쪽이 180도인 경우가 많으므로 에셋에 따라 조정 필요)
            float yRotation = xDir < 0 ? 0f : 180f;

            // 불필요한 연산 방지를 위해 현재 회전값과 다를 때만 적용
            if (Mathf.Abs(m_modelTransform.localEulerAngles.y - yRotation) > 0.1f)
            {
                m_modelTransform.rotation = Quaternion.Euler(0, yRotation, 0);
            }
        }

        #endregion
    }
}