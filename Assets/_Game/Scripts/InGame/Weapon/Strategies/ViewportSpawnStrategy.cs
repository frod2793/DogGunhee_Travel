using UnityEngine;

namespace InGame.Weapon.Strategies
{
    /// <summary>
    /// 카메라 뷰포트 영역 내의 랜덤한 스폰 좌표를 계산하는 전략 클래스입니다.
    /// </summary>
    public class ViewportSpawnStrategy : ISpawnPositionStrategy
    {
        #region 내부 상태 및 변수

        private readonly float m_minViewportX;
        private readonly float m_maxViewportX;
        private readonly float m_minViewportY;
        private readonly float m_maxViewportY;
        private readonly float m_distanceFromCamera;

        #endregion

        #region 생성자

        /// <summary>
        /// 뷰포트 범위와 깊이(Z) 값을 설정하여 생성합니다.
        /// </summary>
        /// <param name="minX">최소 뷰포트 X (0.0~1.0)</param>
        /// <param name="maxX">최대 뷰포트 X (0.0~1.0)</param>
        /// <param name="minY">최소 뷰포트 Y (0.0~1.0)</param>
        /// <param name="maxY">최대 뷰포트 Y (0.0~1.0)</param>
        /// <param name="distance">카메라로부터의 기본 거리</param>
        public ViewportSpawnStrategy(float minX = 0.1f, float maxX = 0.9f, float minY = 0.1f, float maxY = 0.9f, float distance = 10f)
        {
            m_minViewportX = minX;
            m_maxViewportX = maxX;
            m_minViewportY = minY;
            m_maxViewportY = maxY;
            m_distanceFromCamera = distance;
        }

        #endregion

        #region ISpawnPositionStrategy 구현

        public Vector3 GetSpawnPosition(Transform owner)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return Vector3.zero;
            }

            float randomX = UnityEngine.Random.Range(m_minViewportX, m_maxViewportX);
            float randomY = UnityEngine.Random.Range(m_minViewportY, m_maxViewportY);

            // 뷰포트 좌표를 월드 좌표로 변환하여 반환
            Vector3 viewportPos = new Vector3(randomX, randomY, m_distanceFromCamera);
            return camera.ViewportToWorldPoint(viewportPos);
        }

        #endregion
    }
}
