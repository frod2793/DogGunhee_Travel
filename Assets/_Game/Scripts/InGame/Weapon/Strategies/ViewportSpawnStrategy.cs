using UnityEngine;

namespace InGame.Weapon.Strategies
{
    /// <summary>
    /// 카메라 뷰포트 내의 랜덤한 위치를 계산하는 전략입니다.
    /// </summary>
    public class ViewportSpawnStrategy : ISpawnPositionStrategy
    {
        private readonly float m_minViewportX;
        private readonly float m_maxViewportX;
        private readonly float m_minViewportY;
        private readonly float m_maxViewportY;
        private readonly float m_distanceFromCamera;

        /// <summary>
        /// 생성자
        /// </summary>
        /// <param name="minX">뷰포트 X 최소값 (0~1)</param>
        /// <param name="maxX">뷰포트 X 최대값 (0~1)</param>
        /// <param name="minY">뷰포트 Y 최소값 (0~1)</param>
        /// <param name="maxY">뷰포트 Y 최대값 (0~1)</param>
        /// <param name="distance">카메라와의 거리</param>
        public ViewportSpawnStrategy(float minX = 0.1f, float maxX = 0.9f, float minY = 0.1f, float maxY = 0.9f, float distance = 10f)
        {
            m_minViewportX = minX;
            m_maxViewportX = maxX;
            m_minViewportY = minY;
            m_maxViewportY = maxY;
            m_distanceFromCamera = distance;
        }

        public Vector3 GetSpawnPosition(Camera camera)
        {
            if (camera == null) return Vector3.zero;

            float randomX = UnityEngine.Random.Range(m_minViewportX, m_maxViewportX);
            float randomY = UnityEngine.Random.Range(m_minViewportY, m_maxViewportY);

            Vector3 viewportPos = new Vector3(randomX, randomY, m_distanceFromCamera);
            return camera.ViewportToWorldPoint(viewportPos);
        }
    }
}
