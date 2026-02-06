using UnityEngine;

namespace InGame.ObjectPool
{
    /// <summary>
    /// 스폰 위치 계산 로직을 담당하는 POCO 클래스입니다.
    /// 카메라 뷰포트 외부, 맵 경계 내부에서 유효한 스폰 위치를 계산합니다.
    /// </summary>
    public class SpawnPositionSolver
    {
        #region 설정 데이터

        private readonly Bounds m_mapBounds;
        private readonly float m_minSpawnDistance;
        private readonly float m_maxSpawnDistance;
        private readonly int m_maxAttempts;

        #endregion

        #region 생성자

        /// <summary>
        /// SpawnPositionSolver를 초기화합니다.
        /// </summary>
        /// <param name="mapBounds">맵 경계 (Bounds)</param>
        /// <param name="minSpawnDistance">최소 스폰 거리 (카메라 중심 기준)</param>
        /// <param name="maxSpawnDistance">최대 스폰 거리</param>
        /// <param name="maxAttempts">스폰 위치 탐색 최대 시도 횟수</param>
        public SpawnPositionSolver(
            Bounds mapBounds,
            float minSpawnDistance = 15f,
            float maxSpawnDistance = 25f,
            int maxAttempts = 30)
        {
            m_mapBounds = mapBounds;
            m_minSpawnDistance = minSpawnDistance;
            m_maxSpawnDistance = maxSpawnDistance;
            m_maxAttempts = maxAttempts;
        }

        #endregion

        #region 공개 메서드

        /// <summary>
        /// 유효한 스폰 위치를 계산합니다.
        /// </summary>
        /// <param name="cameraPosition">카메라 중심 위치 (z=0 보정됨)</param>
        /// <param name="cameraHalfHeight">카메라 orthographicSize</param>
        /// <param name="cameraAspect">카메라 aspect ratio</param>
        /// <returns>맵 내부, 카메라 외부에 있는 유효한 스폰 위치</returns>
        public Vector3 CalculateSpawnPosition(Vector3 cameraPosition, float cameraHalfHeight, float cameraAspect)
        {
            cameraPosition.z = 0f;

            float cameraHalfWidth = cameraHalfHeight * cameraAspect;
            float diagonalDistance = Mathf.Sqrt(cameraHalfWidth * cameraHalfWidth + cameraHalfHeight * cameraHalfHeight);

            float minDist = diagonalDistance + m_minSpawnDistance;
            float maxDist = diagonalDistance + m_maxSpawnDistance;

            // 1차 시도: 카메라 주변 도넛 형태 랜덤 위치
            for (int i = 0; i < m_maxAttempts; i++)
            {
                Vector2 randomDir = Random.insideUnitCircle.normalized;
                float distance = Random.Range(minDist, maxDist);

                Vector3 candidatePos = cameraPosition + (Vector3)(randomDir * distance);

                if (m_mapBounds.Contains(candidatePos))
                {
                    return candidatePos;
                }
            }

            // 2차 시도: 맵 전체 랜덤 중 카메라와 충분히 먼 곳
            for (int i = 0; i < 20; i++)
            {
                Vector3 randomMapPos = GetRandomPositionInBounds();
                if (Vector3.Distance(cameraPosition, randomMapPos) >= minDist)
                {
                    return randomMapPos;
                }
            }

            // 최후의 수단: 맵 내 랜덤 위치
            return GetRandomPositionInBounds();
        }

        /// <summary>
        /// 카메라 데이터를 직접 받아 스폰 위치를 계산합니다 (편의 메서드).
        /// </summary>
        public Vector3 CalculateSpawnPosition(Camera camera)
        {
            if (camera == null)
            {
                return GetRandomPositionInBounds();
            }

            Vector3 camPos = camera.transform.position;
            return CalculateSpawnPosition(camPos, camera.orthographicSize, camera.aspect);
        }

        #endregion

        #region 내부 메서드

        private Vector3 GetRandomPositionInBounds()
        {
            float x = Random.Range(m_mapBounds.min.x, m_mapBounds.max.x);
            float y = Random.Range(m_mapBounds.min.y, m_mapBounds.max.y);
            return new Vector3(x, y, 0f);
        }

        #endregion
    }
}
