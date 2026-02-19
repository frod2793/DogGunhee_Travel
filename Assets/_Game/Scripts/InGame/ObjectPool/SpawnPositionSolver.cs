using UnityEngine;

namespace InGame.ObjectPool
{
    /// <summary>
    /// [설명]: 몬스터 스폰 위치를 계산하는 순수 로직 클래스입니다.
    /// 카메라 뷰포트 밖(도넛 모양 범위)이면서 맵 경계 내부에 있는 유효 좌표를 탐색하여 반환합니다.
    /// </summary>
    public class SpawnPositionSolver
    {
        #region 내부 설정 데이터

        /// <summary> 몬스터가 생성될 수 있는 전체 맵 범위 </summary>
        private readonly Bounds m_mapBounds;

        /// <summary> 카메라 모서리로부터 떨어진 최소 스폰 거리 </summary>
        private readonly float m_minSpawnDistance;

        /// <summary> 카메라 모서리로부터 떨어진 최대 스폰 거리 </summary>
        private readonly float m_maxSpawnDistance;

        /// <summary> 유효 위치 탐색을 위한 최대 시도 횟수 </summary>
        private readonly int m_maxAttempts;

        #endregion

        #region 초기화

        /// <summary>
        /// [설명]: 스폰 위치 계산기를 초기화하고 필수 파라미터를 주입받습니다.
        /// </summary>
        /// <param name="mapBounds">전체 맵의 경계</param>
        /// <param name="minSpawnDistance">카메라 '모서리'로부터 떨어진 최소 스폰 거리</param>
        /// <param name="maxSpawnDistance">카메라 '모서리'로부터 떨어진 최대 스폰 거리</param>
        /// <param name="maxAttempts">유효 위치 탐색 최대 시도 횟수</param>
        public SpawnPositionSolver(
            Bounds mapBounds,
            float minSpawnDistance = 2f,
            float maxSpawnDistance = 10f,
            int maxAttempts = 30)
        {
            m_mapBounds = mapBounds;
            m_minSpawnDistance = minSpawnDistance;
            m_maxSpawnDistance = maxSpawnDistance;
            m_maxAttempts = maxAttempts;
        }

        #endregion

        #region 위치 계산 로직

        /// <summary>
        /// [설명]: 제공된 카메라 정보를 기반으로 현재 화면 밖의 유효한 스폰 위치를 계산합니다.
        /// </summary>
        /// <param name="camera">기준이 될 카메라</param>
        /// <returns>월드 좌표계 상의 계산된 스폰 위치</returns>
        public Vector3 CalculateSpawnPosition(Camera camera)
        {
            if (camera == null)
            {
                return GetRandomPositionInBounds();
            }

            // 카메라의 가시 영역(Viewport) 계산
            float height = camera.orthographicSize * 2f;
            float width = height * camera.aspect;

            // 여유 범위를 포함한 카메라 영역(Bounds) 생성
            Vector3 camPos = camera.transform.position;
            camPos.z = 0;

            Bounds viewportBounds = new Bounds(camPos, new Vector3(width + (m_minSpawnDistance * 2f), height + (m_minSpawnDistance * 2f), 100f));

            return CalculateSpawnPositionInternal(viewportBounds);
        }

        /// <summary>
        /// [설명]: 카메라 가시 영역을 제외한 맵 내부의 유효 위치를 다단계 알고리즘으로 계산합니다.
        /// </summary>
        private Vector3 CalculateSpawnPositionInternal(Bounds viewportBounds)
        {
            // 1차 시도: 맵 전체 범위에서 랜덤하게 샘플링하되 가시 영역 제외
            for (int i = 0; i < m_maxAttempts; i++)
            {
                Vector3 candidate = GetRandomPositionInBounds();

                if (!IsInsideViewport(candidate, viewportBounds))
                {
                    if (IsPositionValid(candidate))
                    {
                        return candidate;
                    }
                }
            }

            // 2차 시도: 카메라 주변 도넛 영역에서 탐색 (맵 경계 근처일 때 유용)
            Vector3 camPos = viewportBounds.center;
            float viewportRadius = Mathf.Max(viewportBounds.size.x, viewportBounds.size.y) * 0.5f;
            float minRadius = viewportRadius;
            float maxRadius = minRadius + m_maxSpawnDistance;

            for (int i = 0; i < 20; i++)
            {
                Vector2 randomDir = Random.insideUnitCircle.normalized;
                float distance = Random.Range(minRadius, maxRadius);
                Vector3 candidate = camPos + (Vector3)(randomDir * distance);

                if (!IsInsideViewport(candidate, viewportBounds) && IsPositionValid(candidate))
                {
                    return candidate;
                }
            }

            // 3차 시도: 최후의 수단으로 가시 영역 밖인 곳 중 맵 랜덤 위치 검색
            for (int i = 0; i < 10; i++)
            {
                Vector3 candidate = GetRandomPositionInBounds();
                if (!IsInsideViewport(candidate, viewportBounds))
                {
                    return candidate;
                }
            }

            // 모든 수단 실패 시 맵 내 완전 랜덤 위치 반환
            Vector3 finalFallback = GetRandomPositionInBounds();
            Debug.LogWarning($"[SpawnSolver] 모든 화면 밖 스폰 시도 실패. 맵 랜덤 위치 반환: {finalFallback}");
            return finalFallback;
        }

        /// <summary>
        /// [설명]: 특정 좌표가 확장된 카메라 가시 영역 내부에 위치하는지 판정합니다.
        /// </summary>
        private bool IsInsideViewport(Vector3 position, Bounds viewportBounds)
        {
            float halfWidth = viewportBounds.size.x * 0.5f;
            float halfHeight = viewportBounds.size.y * 0.5f;

            return (position.x >= viewportBounds.center.x - halfWidth &&
                    position.x <= viewportBounds.center.x + halfWidth &&
                    position.y >= viewportBounds.center.y - halfHeight &&
                    position.y <= viewportBounds.center.y + halfHeight);
        }

        #endregion

        #region 내부 헬퍼 메서드

        /// <summary>
        /// [설명]: 해당 좌표가 맵 데이터상의 유효한 경계 내부에 포함되는지 검사합니다.
        /// </summary>
        private bool IsPositionValid(Vector3 position)
        {
            Vector3 checkPos = position;
            checkPos.z = m_mapBounds.center.z;
            return m_mapBounds.Contains(checkPos);
        }

        /// <summary>
        /// [설명]: 맵 경계 내의 임의의 랜덤 좌표를 생성하여 반환합니다.
        /// </summary>
        private Vector3 GetRandomPositionInBounds()
        {
            float x = Random.Range(m_mapBounds.min.x, m_mapBounds.max.x);
            float y = Random.Range(m_mapBounds.min.y, m_mapBounds.max.y);
            return new Vector3(x, y, 0f);
        }

        #endregion
    }
}