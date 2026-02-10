using UnityEngine;
using UnityEngine.EventSystems;

namespace InGame.UI.Joystick
{
    /// <summary>
    /// 조이스틱 UI를 드래그하여 이동시키는 핸들러 클래스입니다.
    /// <br/> 캔버스 영역 밖으로 나가지 않도록 좌표를 보정(Clamp)합니다.
    /// </summary>
    public class JoystickDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        #region 1. 에디터 설정 및 내부 참조

        [Header("설정")] 
        [SerializeField, Tooltip("드래그할 대상 RectTransform (비워둘 경우 자기 자신)")] 
        private RectTransform m_targetRect;

        [SerializeField, Tooltip("기준이 될 캔버스 (비워둘 경우 부모에서 탐색)")] 
        private Canvas m_canvas;

        #endregion

        #region 2. 내부 상태 변수

        private Vector2 m_dragOffset;
        private Vector2 m_minBoundary;
        private Vector2 m_maxBoundary;

        #endregion

        #region 3. 유니티 생명주기

        private void Awake()
        {
            InitializeComponents();
        }

        #endregion

        #region 4. 초기화 로직

        private void InitializeComponents()
        {
            if (m_targetRect == null)
            {
                m_targetRect = GetComponent<RectTransform>();
            }

            if (m_canvas == null)
            {
                m_canvas = GetComponentInParent<Canvas>();
            }
        }

        /// <summary>
        /// 드래그 가능한 영역(Canvas 혹은 부모)의 경계를 계산합니다.
        /// 드래그 시작 시 한 번만 호출하여 성능을 최적화합니다.
        /// </summary>
        private void CalculateBoundaries()
        {
            if (m_canvas == null || m_targetRect == null) return;

            // 캔버스(혹은 부모)의 RectTransform 가져오기
            RectTransform parentRect = m_canvas.transform as RectTransform;
            if (m_targetRect.parent != null)
            {
                // 조이스틱의 부모가 캔버스가 아닐 수도 있으므로, 직계 부모를 기준으로 계산하는 것이 안전함
                parentRect = m_targetRect.parent as RectTransform;
            }

            if (parentRect == null) return;

            // 부모의 크기 및 피벗 고려
            float parentWidth = parentRect.rect.width;
            float parentHeight = parentRect.rect.height;

            // 타겟(조이스틱)의 크기 및 피벗 고려
            float targetWidth = m_targetRect.rect.width;
            float targetHeight = m_targetRect.rect.height;

            // 이동 가능한 최소/최대 좌표 계산 (부모 기준 로컬 좌표)
            // Min = (부모 왼쪽 끝) + (타겟 피벗 보정)
            float minX = -parentWidth * parentRect.pivot.x + targetWidth * m_targetRect.pivot.x;
            float minY = -parentHeight * parentRect.pivot.y + targetHeight * m_targetRect.pivot.y;

            // Max = (부모 오른쪽 끝) - (타겟 피벗 보정)
            float maxX = parentWidth * (1 - parentRect.pivot.x) - targetWidth * (1 - m_targetRect.pivot.x);
            float maxY = parentHeight * (1 - parentRect.pivot.y) - targetHeight * (1 - m_targetRect.pivot.y);

            m_minBoundary = new Vector2(minX, minY);
            m_maxBoundary = new Vector2(maxX, maxY);
        }

        #endregion

        #region 5. 드래그 인터페이스 구현

        /// <summary>
        /// 드래그가 시작될 때 1회 호출됩니다.
        /// 초기 오프셋과 이동 제한 범위를 계산합니다.
        /// </summary>
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (m_targetRect == null || m_canvas == null) return;

            // 1. 이동 범위 재계산 (화면 해상도 변경 대응)
            CalculateBoundaries();

            // 2. 터치 지점과 UI 중심점 사이의 오프셋 계산
            // 이를 통해 이미지를 잡은 위치 그대로 자연스럽게 따라오게 함
            RectTransform parentRect = m_targetRect.parent as RectTransform;
            
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect, 
                    eventData.position, 
                    eventData.pressEventCamera, 
                    out Vector2 localPoint))
            {
                m_dragOffset = m_targetRect.anchoredPosition - localPoint;
            }
        }

        /// <summary>
        /// 드래그 중 매 프레임 호출됩니다.
        /// </summary>
        public void OnDrag(PointerEventData eventData)
        {
            if (m_targetRect == null || m_canvas == null) return;

            RectTransform parentRect = m_targetRect.parent as RectTransform;

            // 터치 위치를 로컬 좌표로 변환
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 localPoint))
            {
                // 오프셋을 적용한 목표 위치
                Vector2 desiredPosition = localPoint + m_dragOffset;

                // 캔버스 영역 밖으로 나가지 않도록 Clamp
                desiredPosition.x = Mathf.Clamp(desiredPosition.x, m_minBoundary.x, m_maxBoundary.x);
                desiredPosition.y = Mathf.Clamp(desiredPosition.y, m_minBoundary.y, m_maxBoundary.y);

                m_targetRect.anchoredPosition = desiredPosition;
            }
        }

        /// <summary>
        /// 드래그 종료 시 호출됩니다.
        /// </summary>
        public void OnEndDrag(PointerEventData eventData)
        {
            // 필요 시 조이스틱을 원래 위치로 되돌리는 로직 등을 여기에 추가
            // Debug.Log("Drag Ended");
        }

        #endregion

        #region 6. 디버그 (Gizmos)

        private void OnDrawGizmosSelected()
        {
            // 캔버스의 영역을 시각적으로 표시 (선택되었을 때만)
            if (m_canvas != null)
            {
                RectTransform canvasRect = m_canvas.GetComponent<RectTransform>();
                if (canvasRect != null)
                {
                    Gizmos.color = Color.green;
                    // 실제 월드 좌표 기준 큐브 그리기 (회전/스케일 반영)
                    Matrix4x4 rotationMatrix = Matrix4x4.TRS(canvasRect.position, canvasRect.rotation, canvasRect.lossyScale);
                    Gizmos.matrix = rotationMatrix;
                    Gizmos.DrawWireCube(Vector3.zero, canvasRect.rect.size);
                }
            }
        }

        #endregion
    }
}