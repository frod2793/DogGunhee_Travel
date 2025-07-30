using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class JoyStickPosDragandDrop : MonoBehaviour, IDragHandler, IEndDragHandler, IPointerDownHandler
{
  [SerializeField]  float offsetY = 1000f;
    private RectTransform rectTransform;
    private Canvas canvas;
    private CanvasScaler canvasScaler;
    private Vector2 originalLocalPointerPosition;
    private Vector2 dragOffset;
    private float minX;
    private float maxX;
    private float minY;
    private float maxY;
    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        canvasScaler = canvas != null ? canvas.GetComponentInParent<CanvasScaler>() : null;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (canvas == null || rectTransform == null) return;
        Vector2 localPointerPosition;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out localPointerPosition))
        {
            dragOffset = rectTransform.anchoredPosition - localPointerPosition;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (canvas == null || rectTransform == null) return;
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
    }

    private Vector2 ClampToCanvas(Vector2 position)
    {
        if (canvas == null || rectTransform == null) return position;
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        if (canvasRect == null) return position;

        minX = -canvasRect.rect.width * canvasRect.pivot.x + rectTransform.rect.width * rectTransform.pivot.x;
        maxX = canvasRect.rect.width * canvasRect.pivot.x - rectTransform.rect.width * rectTransform.pivot.x;
        minY = -canvasRect.rect.height * canvasRect.pivot.y + rectTransform.rect.height * rectTransform.pivot.y + offsetY;
        maxY = canvasRect.rect.height * canvasRect.pivot.y - rectTransform.rect.height * rectTransform.pivot.y + offsetY;
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
