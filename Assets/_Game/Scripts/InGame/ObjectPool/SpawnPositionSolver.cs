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

            // [추가]: 맵이 뷰포트보다 작을 경우, 뷰포트 제외 영역을 맵 크기에 맞춰 보정 (최소한의 가용 공간 확보)
            float safeWidth = Mathf.Min(viewportBounds.size.x, m_mapBounds.size.x * 0.9f);
            float safeHeight = Mathf.Min(viewportBounds.size.y, m_mapBounds.size.y * 0.9f);
            viewportBounds.size = new Vector3(safeWidth, safeHeight, viewportBounds.size.z);

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

            // 3차 시도: 맵의 상/하/좌/우 끝단(Edge) 중 카메라 밖에 있는 곳 탐색
            for (int i = 0; i < 20; i++)
            {
                Vector3 candidate = GetRandomPositionInBounds();
                // X축이 좁다면 Y축 끝단 위주로, Y축이 좁다면 X축 끝단 위주로 검색
                if (m_mapBounds.size.x < viewportBounds.size.x)
                {
                    candidate.y = Random.value > 0.5f ? m_mapBounds.min.y : m_mapBounds.max.y;
                }
                else if (m_mapBounds.size.y < viewportBounds.size.y)
                {
                    candidate.x = Random.value > 0.5f ? m_mapBounds.min.x : m_mapBounds.max.x;
                }

                if (!IsInsideViewport(candidate, viewportBounds) && IsPositionValid(candidate))
                {
                    return candidate;
                }
            }

            // 모든 수단 실패 시 맵 내 완전 랜덤 위치를 찾되, 반드시 경계 내로 제한
            Vector3 finalFallback = GetRandomPositionInBounds();
            
            // [수정]: 최종 좌표 강제 클램핑 (안전장치)
            finalFallback.x = Mathf.Clamp(finalFallback.x, m_mapBounds.min.x, m_mapBounds.max.x);
            finalFallback.y = Mathf.Clamp(finalFallback.y, m_mapBounds.min.y, m_mapBounds.max.y);

            LogManager.LogWarning($"[SpawnSolver] 모든 화면 밖 스폰 시도 실패. 맵 영역으로 강제 조정: {finalFallback}", LogManager.LogCategory.System);
            return finalFallback;
        }

        /// <summary>
        /// [설명]: 특정 좌표가 확장된 카메라 가시 영역 내부에 위치하는지 판정합니다.
        /// </summary>
        private bool IsInsideViewport(Vector3 position, Bounds viewportBounds)
        {
            return (position.x >= viewportBounds.min.x &&
                    position.x <= viewportBounds.max.x &&
                    position.y >= viewportBounds.min.y &&
                    position.y <= viewportBounds.max.y);
        }

        #endregion

        #region 내부 헬퍼 메서드

        /// <summary>
        /// [설명]: 해당 좌표가 맵 데이터상의 유효한 경계 내부에 포함되는지 검사합니다.
        /// </summary>
        private bool IsPositionValid(Vector3 position)
        {
            // [수정]: 2D 평면 공간에서의 경계 체크 (Z축 차이로 인한 Contains 실패 방지)
            return position.x >= m_mapBounds.min.x && position.x <= m_mapBounds.max.x &&
                   position.y >= m_mapBounds.min.y && position.y <= m_mapBounds.max.y;
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