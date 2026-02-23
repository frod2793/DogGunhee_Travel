using UnityEngine;

namespace InGame.Weapon.Strategies
{
    /// <summary>
    /// [설명]: 카메라 뷰포트(Viewport) 영역 내의 랜덤한 월드 좌표를 계산하는 전략 클래스입니다.
    /// 화면 밖이나 특정 영역 내에 소환해야 할 때 사용됩니다.
    /// </summary>
    public class ViewportSpawnStrategy : ISpawnPositionStrategy
    {
        #region 내부 변수

        private readonly float m_minViewportX;
        private readonly float m_maxViewportX;
        private readonly float m_minViewportY;
        private readonly float m_maxViewportY;
        private readonly float m_distanceFromCamera;

        #endregion

        #region 생성자

        /// <summary>
        /// 뷰포트 범위(0.0 ~ 1.0)와 카메라 거리(Z)를 설정하여 생성합니다.
        /// </summary>
        /// <param name="minX">최소 X 비율 (기본 0.1)</param>
        /// <param name="maxX">최대 X 비율 (기본 0.9)</param>
        /// <param name="minY">최소 Y 비율 (기본 0.1)</param>
        /// <param name="maxY">최대 Y 비율 (기본 0.9)</param>
        /// <param name="distance">카메라로부터의 Z축 거리 (기본 10)</param>
        public ViewportSpawnStrategy(float minX = 0.1f, float maxX = 0.9f, float minY = 0.1f, float maxY = 0.9f, float distance = 10f)
        {
            m_minViewportX = minX;
            m_maxViewportX = maxX;
            m_minViewportY = minY;
            m_maxViewportY = maxY;
            m_distanceFromCamera = distance;
        }

        #endregion

        #region 인터페이스 구현

        /// <summary>
        /// 설정된 뷰포트 범위 내에서 무작위 월드 좌표를 계산하여 반환합니다.
        /// </summary>
        /// <param name="owner">기준이 될 트랜스폼 (이 전략에서는 사용하지 않음)</param>
        public Vector3 GetSpawnPosition(Transform owner)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                // 카메라가 없으면 (0,0,0)에 안전하게 반환
                return Vector3.zero;
            }

            // 랜덤 뷰포트 좌표 생성
            float randomX = Random.Range(m_minViewportX, m_maxViewportX);
            float randomY = Random.Range(m_minViewportY, m_maxViewportY);

            // 뷰포트 좌표를 월드 좌표로 변환
            // (z값은 카메라로부터의 거리를 의미함)
            Vector3 viewportPos = new Vector3(randomX, randomY, m_distanceFromCamera);
            
            return camera.ViewportToWorldPoint(viewportPos);
        }

        #endregion
    }
}