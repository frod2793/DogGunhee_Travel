using InGame.Core.Interfaces;
using UnityEngine;

namespace InGame.Test
{
    /// <summary>
    /// [설명]: 맵의 이동 가능 경계(MapBounds)를 게임 뷰에서 시각적으로 보여주는 디버그용 컴포넌트입니다.
    /// </summary>
    public class MapBoundsVisualizer : MonoBehaviour
    {
        #region 에디터 설정
        [SerializeField] private Color m_lineColor = Color.cyan;
        [SerializeField] private float m_lineThickness = 2.0f;
        #endregion

        private ICombatContext m_combatCtx;
        private bool m_isVisible = false;
        private LineRenderer m_lineRenderer;

        public void Initialize(ICombatContext combatCtx)
        {
            m_combatCtx = combatCtx;
            CreateLineRenderer();
            UpdateVisibility(false);
        }

        private void CreateLineRenderer()
        {
            m_lineRenderer = gameObject.AddComponent<LineRenderer>();
            m_lineRenderer.startWidth = m_lineThickness;
            m_lineRenderer.endWidth = m_lineThickness;
            m_lineRenderer.useWorldSpace = true;
            m_lineRenderer.loop = true;
            m_lineRenderer.positionCount = 4;
            m_lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            m_lineRenderer.startColor = m_lineColor;
            m_lineRenderer.endColor = m_lineColor;
        }

        public void UpdateVisibility(bool visible)
        {
            m_isVisible = visible;
            if (m_lineRenderer != null)
            {
                m_lineRenderer.enabled = visible;
            }
        }

        private void Update()
        {
            if (!m_isVisible || m_combatCtx == null || m_lineRenderer == null) return;

            Bounds bounds = m_combatCtx.MapBounds;
            if (bounds == default) return;

            Vector3[] positions = new Vector3[4];
            positions[0] = new Vector3(bounds.min.x, bounds.min.y, 0f);
            positions[1] = new Vector3(bounds.max.x, bounds.min.y, 0f);
            positions[2] = new Vector3(bounds.max.x, bounds.max.y, 0f);
            positions[3] = new Vector3(bounds.min.x, bounds.max.y, 0f);

            m_lineRenderer.SetPositions(positions);
        }
    }
}
