using UnityEngine;
using UnityEngine.EventSystems;

namespace InGame.UI.Joystick
{
    /// <summary>
    /// [설명]: 조이스틱 UI를 드래그하여 이동시키는 핸들러 클래스입니다.
    /// </summary>
    public class JoystickDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        #region 내부 필드 

        [Header("설정")]
        [SerializeField, Tooltip("드래그할 대상 RectTransform (비워둘 경우 자기 자신)")]
        private RectTransform m_targetRect;

        [SerializeField, Tooltip("기준이 될 캔버스 (비워둘 경우 부모에서 탐색)")]
        private Canvas m_canvas;

        // 내부 상태 변수
        private Vector2 m_dragOffset;
        private Vector2 m_minBoundary;
        private Vector2 m_maxBoundary;

        #endregion

        #region 유니티 생명주기 

        private void Awake()
        {
            InitializeComponents();
        }

        #endregion

        #region 초기화 

        /// <summary>
        /// [설명]: 필요 참조를 초기화합니다.
        /// </summary>
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
        /// [설명]: 드래그 가능한 영역을 부모 RectTransform 내부로 제한하기 위한 경계를 계산합니다.
        /// </summary>
        private void CalculateBoundaries()
        {
            if (m_targetRect == null) return;

            // 조이스틱의 직계 부모 (조이스틱의 anchoredPosition이 이 부모 기준임)
            RectTransform parentRect = m_targetRect.parent as RectTransform;
            if (parentRect == null) return;

            // 1. 부모의 Rect 정보 확보 (로컬 좌표계 기준)
            Rect pRect = parentRect.rect;

            // 2. 조이스틱의 앵커 위치를 부모 로컬 좌표계 기준으로 계산
            // anchoredPosition은 이 앵커 지점으로부터의 오프셋이므로, 이를 빼주어야 함
            Vector2 anchorPosInParent = new Vector2(
                Mathf.Lerp(pRect.xMin, pRect.xMax, m_targetRect.anchorMin.x),
                Mathf.Lerp(pRect.yMin, pRect.yMax, m_targetRect.anchorMin.y)
            );

            // 3. 조이스틱의 현재 크기(스케일 포함)와 피벗을 고려하여 Clamp 범위 계산
            float targetWidth = m_targetRect.rect.width * m_targetRect.localScale.x;
            float targetHeight = m_targetRect.rect.height * m_targetRect.localScale.y;

            // [계산식]: (부모 경계 - 앵커 위치) + (피벗에 따른 최소/최대 오프셋)
            // pRect.xMin/xMax는 부모의 피벗 위치에 따른 상대 좌표를 이미 포함하고 있음
            float minX = pRect.xMin - anchorPosInParent.x + (targetWidth * m_targetRect.pivot.x);
            float minY = pRect.yMin - anchorPosInParent.y + (targetHeight * m_targetRect.pivot.y);
            float maxX = pRect.xMax - anchorPosInParent.x - (targetWidth * (1 - m_targetRect.pivot.x));
            float maxY = pRect.yMax - anchorPosInParent.y - (targetHeight * (1 - m_targetRect.pivot.y));

            m_minBoundary = new Vector2(minX, minY);
            m_maxBoundary = new Vector2(maxX, maxY);
        }

        #endregion

        #region 드래그 인터페이스 

        /// <summary>
        /// [설명]: 드래그가 시작될 때 1회 호출됩니다.
        /// </summary>
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (m_targetRect == null || m_canvas == null) return;

            // 1. 이동 범위 재계산 (화면 해상도 변경 및 앵커 설정 대응)
            CalculateBoundaries();

            // 2. 터치 지점과 UI 중심점 사이의 오프셋 계산 (부모 로컬 좌표계 기준)
            RectTransform parentRect = m_targetRect.parent as RectTransform;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 localPoint))
            {
                // anchoredPosition space와 localPoint space는 동일한 스케일/방향을 공유함 (앵커 위치만 다름)
                m_dragOffset = m_targetRect.anchoredPosition - localPoint;
            }
        }

        /// <summary>
        /// [설명]: 드래그 중 매 프레임 호출됩니다.
        /// </summary>
        public void OnDrag(PointerEventData eventData)
        {
            if (m_targetRect == null || m_canvas == null) return;

            RectTransform parentRect = m_targetRect.parent as RectTransform;

            // 터치 위치를 부모 로컬 좌표로 변환
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 localPoint))
            {
                // 드래그 시작 시 계산한 오프셋을 적용하여 목표 anchoredPosition 도출
                Vector2 desiredPosition = localPoint + m_dragOffset;

                // 캔버스(화면) 영역 밖으로 나가지 않도록 Clamp
                desiredPosition.x = Mathf.Clamp(desiredPosition.x, m_minBoundary.x, m_maxBoundary.x);
                desiredPosition.y = Mathf.Clamp(desiredPosition.y, m_minBoundary.y, m_maxBoundary.y);

                m_targetRect.anchoredPosition = desiredPosition;
            }
        }

        /// <summary>
        /// [설명]: 드래그 종료 시 호출됩니다.
        /// </summary>
        public void OnEndDrag(PointerEventData eventData)
        {
            // 필요 시 조이스틱을 원래 위치로 되돌리는 로직 등을 여기에 추가
        }

        #endregion

        #region 디버그 (Gizmos) 

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