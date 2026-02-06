using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class JoyStickPosDragandDrop : MonoBehaviour, IDragHandler, IEndDragHandler
{
    private RectTransform rectTransform;
    private Canvas canvas;
    private Vector2 dragOffset;
    private bool m_isDragging = false;
    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (canvas == null || rectTransform == null) return;

        if (!m_isDragging)
        {
            m_isDragging = true;
            Vector2 localPointerPositionOnDragStart;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform,
                eventData.pressPosition, // 드래그 시작 위치 사용
                eventData.pressEventCamera,
                out localPointerPositionOnDragStart))
            {
                dragOffset = rectTransform.anchoredPosition - localPointerPositionOnDragStart;
            }
        }
        
        Vector2 localPointerPosition;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out localPointerPosition)) return;

        rectTransform.anchoredPosition = ClampToCanvas(localPointerPosition + dragOffset);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (canvas == null || rectTransform == null) return;
        rectTransform.anchoredPosition = ClampToCanvas(rectTransform.anchoredPosition);
        m_isDragging = false;
    }

    private Vector2 ClampToCanvas(Vector2 position)
    {
        if (canvas == null || rectTransform == null) return position;
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        if (canvasRect == null) return position;

        float minX = -canvasRect.rect.width * canvasRect.pivot.x + rectTransform.rect.width * rectTransform.pivot.x;
        float maxX = canvasRect.rect.width * (1 - canvasRect.pivot.x) - rectTransform.rect.width * (1 - rectTransform.pivot.x);
        float minY = -canvasRect.rect.height * canvasRect.pivot.y + rectTransform.rect.height * rectTransform.pivot.y;
        float maxY = canvasRect.rect.height * (1 - canvasRect.pivot.y) - rectTransform.rect.height * (1 - rectTransform.pivot.y);
        position.x = Mathf.Clamp(position.x, minX, maxX);
        position.y = Mathf.Clamp(position.y, minY, maxY);
        return position;
    }

    private void OnDrawGizmos()
    {
        if (canvas == null) return;
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        if (canvasRect == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(canvasRect.transform.position, canvasRect.rect.size);
    }

}
