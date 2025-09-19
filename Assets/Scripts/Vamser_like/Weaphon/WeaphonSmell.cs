using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace DogGuns_Games.vamsir
{
    /// <summary>
    /// 라인 렌더러를 이용해 플레이어의 이동 경로를 따라 냄새 흔적을 남겨,
    /// 해당 흔적에 닿는 적에게 지속적인 피해를 주는 무기입니다.
    /// </summary>
    [RequireComponent(typeof(LineRenderer), typeof(EdgeCollider2D))]
    public class WeaphonSmell : Weaphon_base
    {
        [Header("냄새 흔적 설정")]
        [Tooltip("흔적의 최대 길이 (포인트 수)")]
        [SerializeField] private int maxTrailPoints = 50;

        [Tooltip("새로운 포인트를 생성하기 위해 이동해야 하는 최소 거리")]
        [SerializeField] private float pointSpacing = 0.5f;

        [Tooltip("흔적의 시각적 두께")]
        [SerializeField] private float trailWidth = 0.5f;

        [Header("렌더링 설정")]
        [Tooltip("흔적의 렌더링 순서. 맵(-10), 플레이어(0)일 경우 -1로 설정하면 플레이어 뒤, 맵 앞에 보입니다.")]
        [SerializeField] private int trailSortingOrder = -1;

        private LineRenderer _trailRenderer;
        private EdgeCollider2D _trailCollider;
        private Transform _playerTransform;
        private LinkedList<Vector3> _points;

        // 피해를 입은 적과 다음 피해 가능 시간을 추적합니다. (Key: InstanceID, Value: Time.time + coolTime)
        private Dictionary<int, float> _damageCooldowns;

        private void Awake()
        {
            _trailRenderer = GetComponent<LineRenderer>();
            _trailCollider = GetComponent<EdgeCollider2D>();
            _points = new LinkedList<Vector3>();
            _damageCooldowns = new Dictionary<int, float>();

            SetupLineRenderer();
        }

        public override void OnEnable()
        {
            base.OnEnable();
            
            // 이 무기 오브젝트의 최상위 부모를 플레이어로 가정하고 Transform을 가져옵니다.
            _playerTransform = transform.root;

            // LineRenderer 및 EdgeCollider 초기 설정
            // Awake에서 초기 설정이 완료되었으므로, 여기서는 게임 상태에 따라 변할 수 있는 값만 업데이트합니다.
            _trailRenderer.startWidth = trailWidth; // 레벨업 등으로 두께가 변경될 수 있으므로 유지
            _trailRenderer.endWidth = trailWidth;   // 레벨업 등으로 두께가 변경될 수 있으므로 유지
            _trailRenderer.positionCount = 0;

            // EdgeCollider는 많은 포인트를 가질 수 있으므로, 불필요한 충돌 계산을 방지하기 위해 비활성화로 시작합니다.
            _trailCollider.enabled = false;
            
            // 활성화 시 기존 흔적 및 쿨다운 데이터 초기화
            _points.Clear();
            _damageCooldowns.Clear();
            UpdateTrail();
        }

        /// <summary>
        /// LineRenderer의 초기 시각적 속성을 설정합니다.
        /// 이 메서드는 Awake에서 한 번만 호출됩니다.
        /// </summary>
        private void SetupLineRenderer()
        {
            // --- 좌표계 설정 ---
            // 흔적을 월드 공간에 남기므로 true로 설정합니다.
            // 이 오브젝트가 플레이어를 따라다녀도 라인은 월드에 고정됩니다.
            _trailRenderer.useWorldSpace = true;

            // --- 렌더링 순서 및 모양 설정 ---
            // 렌더링 순서를 조절하여 다른 스프라이트와 겹칠 때 올바르게 보이도록 합니다.
            _trailRenderer.sortingOrder = trailSortingOrder;

            // 픽셀 아트 스타일에서는 라인의 끝과 모서리가 각지게 보이는 것이 자연스럽습니다.
            // 이 값을 0으로 설정하여 부드러운 처리를 비활성화합니다.
            _trailRenderer.numCapVertices = 0;
            _trailRenderer.numCornerVertices = 0;

            // --- 색상 설정 (그라데이션) ---
            // 냄새가 시간에 따라 사라지는 효과를 줍니다.
            // 시작(최신)은 불투명, 끝(오래됨)은 투명하게 설정합니다.
            var colorGradient = new Gradient();
            colorGradient.SetKeys(
                // Color keys (RGB) - 머티리얼의 Tint 색상을 그대로 사용하도록 흰색으로 설정
                new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
                // Alpha keys (Transparency) - 끝으로 갈수록 투명해짐
                new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.5f, 0.8f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            _trailRenderer.colorGradient = colorGradient;

            // --- 기타 설정 ---
            _trailCollider.isTrigger = true;

            // 머티리얼이 할당되지 않았다면 경고를 출력하여 설정하도록 유도합니다.
            if (_trailRenderer.sharedMaterial == null)
            {
                Debug.LogWarning($"'{gameObject.name}'의 LineRenderer에 머티리얼이 할당되지 않았습니다. 'PixelLineTransparent' 셰이더를 사용하는 머티리얼을 할당해주세요.", this);
            }
        }

        public override void OnDisable()
        {
            base.OnDisable();
            
            // 비활성화 시 흔적을 제거하고 데이터를 초기화합니다.
            if (_points != null)
            {
                _points.Clear();
                UpdateTrail();
            }
            if (_damageCooldowns != null)
            {
                _damageCooldowns.Clear();
            }
        }

        private void FixedUpdate()
        {
            if (_playerTransform == null) return;

            Vector3 currentPosition = _playerTransform.position;
            
            // 마지막 포인트에서 일정 거리 이상 움직였을 때만 새 포인트를 추가합니다.
            // 플레이어가 움직이지 않으면 흔적이 더 길어지지 않습니다.
            if (_points.Count == 0 || Vector3.Distance(_points.Last.Value, currentPosition) > pointSpacing)
            {
                // LineRenderer는 월드 좌표를 사용하도록 설정되어 있다고 가정합니다.
                _points.AddLast(currentPosition);

                // 최대 포인트 수를 초과하면 가장 오래된 포인트를 제거하여 흔적 길이를 유지합니다.
                while (_points.Count > maxTrailPoints)
                {
                    _points.RemoveFirst();
                }

                UpdateTrail();
            }
        }

        /// <summary>
        /// LineRenderer와 EdgeCollider를 포인트 목록에 따라 업데이트합니다.
        /// </summary>
        private void UpdateTrail()
        {
            // 포인트가 2개 미만이면 선을 그릴 수 없으므로 렌더러와 콜라이더를 비웁니다.
            if (_points.Count < 2)
            {
                _trailRenderer.positionCount = 0;
                _trailCollider.enabled = false;
                return;
            }

            _trailCollider.enabled = true;
            _trailRenderer.positionCount = _points.Count;
            // LinkedList는 ToArray() 확장 메서드를 통해 배열로 변환할 수 있습니다. (System.Linq)
            _trailRenderer.SetPositions(_points.ToArray());

            // EdgeCollider의 포인트는 트랜스폼의 로컬 좌표계 기준입니다.
            // _points에 저장된 월드 좌표를 이 오브젝트의 로컬 좌표로 변환해야 합니다.
            Vector2[] localPoints = new Vector2[_points.Count];
            int i = 0;
            foreach (var worldPoint in _points)
            {
                localPoints[i] = transform.InverseTransformPoint(worldPoint);
                i++;
            }
            _trailCollider.points = localPoints;
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            // VamserMobBase 컴포넌트를 가진 객체(적)인지 먼저 확인합니다.
            if (!other.TryGetComponent<VamserMobBase>(out var enemy))
            {
                return; // 적이 아니면 무시합니다.
            }

            int enemyId = other.GetInstanceID();

            // 쿨다운을 확인하여 피해를 줄 수 있는지 결정합니다.
            if (_damageCooldowns.TryGetValue(enemyId, out float nextDamageTime))
            {
                if (Time.time < nextDamageTime)
                {
                    return; // 아직 쿨다운 중입니다.
                }
            }

            // 적에게 피해를 줍니다.
            enemy.TakeDamage(attackPower);

            // 다음 피해 가능 시간을 업데이트합니다. (coolTime은 Weaphon_base에서 상속)
            _damageCooldowns[enemyId] = Time.time + coolTime;
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            // 적이 흔적을 벗어나면 쿨다운 목록에서 제거하여 메모리를 관리합니다.
            // VamserMobBase 컴포넌트가 있는지 확인합니다.
            if (other.TryGetComponent<VamserMobBase>(out _))
            {
                _damageCooldowns.Remove(other.GetInstanceID());
            }
        }
    }
}