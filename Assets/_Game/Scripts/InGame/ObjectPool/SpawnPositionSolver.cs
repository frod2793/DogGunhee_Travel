using UnityEngine;

namespace InGame.ObjectPool
{
    /// <summary>
    /// 몬스터 스폰 위치를 계산하는 순수 로직 클래스입니다.
    /// <br/> 카메라 뷰포트 밖(도넛 모양 범위)이면서 맵 경계 내부에 있는 유효 좌표를 반환합니다.
    /// </summary>
    public class SpawnPositionSolver
    {
        #region 1. 설정 데이터 (Settings)

        private readonly Bounds m_mapBounds;
        private readonly float m_minSpawnDistance; // 카메라 외곽으로부터의 거리
        private readonly float m_maxSpawnDistance; // 카메라 외곽으로부터의 최대 거리
        private readonly int m_maxAttempts;

        #endregion

        #region 2. 생성자 (Constructor)

        /// <summary>
        /// 스폰 위치 계산기를 초기화합니다.
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

        #region 3. 공개 메서드 (Public Methods)

        /// <summary>
        /// 카메라 정보를 기반으로 유효한 스폰 위치를 계산합니다.
        /// </summary>
        /// <param name="camera">기준이 될 카메라</param>
        /// <returns>스폰 좌표 (World Position)</returns>
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
            // 최소 스폰 거리만큼 확장하여 화면 바로 끝에서 나타나는 것을 방지
            Vector3 camPos = camera.transform.position;
            camPos.z = 0;
            
            Bounds viewportBounds = new Bounds(camPos, new Vector3(width + (m_minSpawnDistance * 2f), height + (m_minSpawnDistance * 2f), 100f));

            return CalculateSpawnPositionInternal(viewportBounds);
        }

        /// <summary>
        /// 카메라 가시 영역을 제외한 맵 내부의 유효 위치를 계산합니다.
        /// </summary>
        private Vector3 CalculateSpawnPositionInternal(Bounds viewportBounds)
        {
            // 1차 시도: 맵 전체 범위에서 랜덤하게 샘플링하되 가시 영역 제외
            // 시도 횟수를 늘려 정밀도를 높임
            for (int i = 0; i < m_maxAttempts; i++)
            {
                Vector3 candidate = GetRandomPositionInBounds();

                // 가시 영역 밖에 있고 맵 내부에 있는지 확인
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

            // 3차 시도: 최후의 수단으로 맵 전체 랜덤 위치 중 가시 영역 밖인 곳 검색
            for (int i = 0; i < 10; i++)
            {
                Vector3 candidate = GetRandomPositionInBounds();
                if (!IsInsideViewport(candidate, viewportBounds))
                {
                    return candidate;
                }
            }

            // 진짜 모든 수단이 실패했을 때만 맵 전체 랜덤 위치 반환
            Vector3 finalFallback = GetRandomPositionInBounds();
            Debug.LogWarning($"[SpawnSolver] 모든 화면 밖 스폰 시도 실패. 맵 랜덤 위치 반환: {finalFallback}");
            return finalFallback;
        }

        /// <summary>
        /// 좌표가 카메라 가시 영역(viewportBounds) 내부에 있는지 판정합니다.
        /// </summary>
        private bool IsInsideViewport(Vector3 position, Bounds viewportBounds)
        {
            // Z축을 무시한 2D 평면 판정
            float halfWidth = viewportBounds.size.x * 0.5f;
            float halfHeight = viewportBounds.size.y * 0.5f;

            return (position.x >= viewportBounds.center.x - halfWidth &&
                    position.x <= viewportBounds.center.x + halfWidth &&
                    position.y >= viewportBounds.center.y - halfHeight &&
                    position.y <= viewportBounds.center.y + halfHeight);
        }

        #endregion

        #region 4. 내부 헬퍼 메서드 (Helpers)

        /// <summary>
        /// 해당 위치가 맵 경계 내부에 포함되는지 검사합니다.
        /// </summary>
        private bool IsPositionValid(Vector3 position)
        {
            Vector3 checkPos = position;
            checkPos.z = m_mapBounds.center.z;
            return m_mapBounds.Contains(checkPos);
        }

        /// <summary>
        /// 맵 경계 내부의 랜덤한 좌표를 반환합니다.
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