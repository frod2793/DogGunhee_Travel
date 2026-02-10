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
                // 카메라가 없으면 맵 전체 랜덤 반환
                return GetRandomPositionInBounds();
            }

            return CalculateSpawnPosition(
                camera.transform.position, 
                camera.orthographicSize, 
                camera.aspect
            );
        }

        /// <summary>
        /// 주어진 카메라 매개변수를 사용하여 스폰 위치를 계산합니다. (핵심 로직)
        /// </summary>
        public Vector3 CalculateSpawnPosition(Vector3 cameraPosition, float cameraHalfHeight, float cameraAspect)
        {
            // 2D 게임 기준 Z축 보정
            cameraPosition.z = 0f;

            // 카메라의 대각선 길이(반지름) 계산
            float cameraHalfWidth = cameraHalfHeight * cameraAspect;
            float cameraRadius = Mathf.Sqrt((cameraHalfWidth * cameraHalfWidth) + (cameraHalfHeight * cameraHalfHeight));

            // 스폰 가능한 도넛 범위(Annulus)의 안쪽/바깥쪽 반지름 설정
            float minRadius = cameraRadius + m_minSpawnDistance;
            float maxRadius = cameraRadius + m_maxSpawnDistance;

            // 1차 시도: 카메라 주변 도넛 형태의 랜덤 위치 탐색
            for (int i = 0; i < m_maxAttempts; i++)
            {
                // 랜덤 방향 및 거리
                Vector2 randomDir = Random.insideUnitCircle.normalized;
                
                // insideUnitCircle이 (0,0)일 경우 방어 코드 (매우 희박함)
                if (randomDir == Vector2.zero) randomDir = Vector2.right;

                float distance = Random.Range(minRadius, maxRadius);
                Vector3 candidatePos = cameraPosition + (Vector3)(randomDir * distance);

                // 생성된 위치가 맵 경계 안에 있는지 확인
                if (IsPositionValid(candidatePos))
                {
                    return candidatePos;
                }
            }

            // 2차 시도: 도넛 범위 탐색 실패 시, 맵 전체에서 랜덤 샘플링하되 카메라와 먼 곳 찾기
            // (맵 구석에 몰렸을 때 몬스터가 안 나오는 현상 방지)
            for (int i = 0; i < 10; i++)
            {
                Vector3 randomMapPos = GetRandomPositionInBounds();
                
                // 카메라와 최소한의 거리는 유지되는지 확인
                if (Vector3.Distance(cameraPosition, randomMapPos) >= minRadius)
                {
                    return randomMapPos;
                }
            }

            // 최후의 수단: 조건 무시하고 맵 내 랜덤 위치 반환
            return GetRandomPositionInBounds();
        }

        #endregion

        #region 4. 내부 헬퍼 메서드 (Helpers)

        /// <summary>
        /// 해당 위치가 맵 경계 내부에 포함되는지 검사합니다.
        /// </summary>
        private bool IsPositionValid(Vector3 position)
        {
            // Bounds.Contains는 3D 검사입니다.
            // 2D 게임에서 맵의 Z축 두께가 얇거나 위치가 다르면 실패할 수 있으므로
            // Z축을 맵의 중심 Z로 맞춰서 검사합니다.
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
            
            // Z축은 0으로 고정 (2D 평면)
            return new Vector3(x, y, 0f);
        }

        #endregion
    }
}