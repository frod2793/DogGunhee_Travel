using UnityEngine;

namespace InGame.Player.Player_Base
{
    /// <summary>
    /// [설명]: 플레이어의 물리적 이동(Transform Position)과 시각적 방향 전환(Model Rotation)을 담당하는 순수 로직 클래스입니다.
    /// PlayerController의 업데이트 주기에 맞춰 동작하며, 실시간 맵 경계 체크를 통한 위치 보정을 수행합니다.
    /// </summary>
    public class PlayerMovement
    {
        #region 내부 상태 및 캐시

        /// <summary> 이동 속도 등 상태 데이터를 제공하는 플레이어 인스턴스 </summary>
        private readonly PlayerBase m_playerStat;

        /// <summary> 실제 월드 좌표가 이동할 최상위 트랜스폼 </summary>
        private readonly Transform m_rootTransform;

        /// <summary> 시각적으로 회전(Flip)될 캐릭터 모델 트랜스폼 </summary>
        private readonly Transform m_modelTransform;

        /// <summary> 이동 가능 범위를 제한하는 맵의 경계 데이터 </summary>
        private readonly SpriteRenderer m_mapBoundary;

        #endregion

        #region 초기화

        /// <summary>
        /// [설명]: 이동 시스템에 필요한 필수 트랜스폼과 데이터 참조를 주입받아 초기화합니다.
        /// </summary>
        /// <param name="playerBase">플레이어 스탯 데이터 인스턴스</param>
        /// <param name="rootTransform">이동 대상 루트 트랜스폼</param>
        /// <param name="characterTransform">회전 대상 모델 트랜스폼</param>
        /// <param name="mapRange">이동 제한 구역 스프라이트</param>
        public PlayerMovement(PlayerBase playerBase, Transform rootTransform, Transform characterTransform, SpriteRenderer mapRange)
        {
            m_playerStat = playerBase;
            m_rootTransform = rootTransform;
            m_modelTransform = characterTransform;
            m_mapBoundary = mapRange;
        }

        #endregion

        #region 이동 제어

        /// <summary>
        /// [설명]: 입력 방향과 현재 속도를 기반으로 캐릭터를 이동시키고 맵 경계 내로 보정합니다.
        /// </summary>
        /// <param name="direction">이동할 정규화된 방향 벡터</param>
        public void Move(Vector2 direction)
        {
            if (m_playerStat == null || m_rootTransform == null || direction.sqrMagnitude < 0.001f)
            {
                return;
            }

            // 프레임 보정된 이동 변위 계산
            float moveDistance = m_playerStat.MoveSpeed * Time.deltaTime;
            Vector3 moveDelta = (Vector3)direction * moveDistance;

            // 예상 경로 계산
            Vector3 targetPosition = m_rootTransform.position + moveDelta;

            // 2D 게임 레이어 보존을 위해 Z축 고정 및 맵 경계 클램핑
            targetPosition.z = m_rootTransform.position.z;
            Vector3 clampedPosition = GetClampedPosition(targetPosition);

            m_rootTransform.position = clampedPosition;

            // 이동 방향에 맞춰 모델의 좌우 반전 처리
            UpdateFacingDirection(direction.x);
        }

        #endregion

        #region 내부 유틸리티 로직

        /// <summary>
        /// [설명]: 계산된 목표 위치가 맵의 가시 영역 Bounds를 벗어나지 않도록 좌표를 필터링합니다.
        /// </summary>
        private Vector3 GetClampedPosition(Vector3 targetPos)
        {
            if (m_mapBoundary == null)
            {
                return targetPos;
            }

            Bounds bounds = m_mapBoundary.bounds;

            float clampedX = Mathf.Clamp(targetPos.x, bounds.min.x, bounds.max.x);
            float clampedY = Mathf.Clamp(targetPos.y, bounds.min.y, bounds.max.y);

            return new Vector3(clampedX, clampedY, targetPos.z);
        }

        /// <summary>
        /// [설명]: X축 입력 방향에 따라 스프라이트의 Y축 회전값을 조정하여 좌우를 바라보게 합니다.
        /// </summary>
        private void UpdateFacingDirection(float xDir)
        {
            if (m_modelTransform == null)
            {
                return;
            }

            // 미세한 입력값(수직 이동 등)은 기존 방향을 유지
            if (Mathf.Abs(xDir) <= 0.01f)
            {
                return;
            }

            // 에셋 기준에 맞춰 왼쪽(0도), 오른쪽(180도)으로 회전값 설정
            float yRotation = xDir < 0 ? 0f : 180f;

            if (Mathf.Abs(m_modelTransform.localEulerAngles.y - yRotation) > 0.1f)
            {
                m_modelTransform.rotation = Quaternion.Euler(0, yRotation, 0);
            }
        }

        #endregion
    }
}