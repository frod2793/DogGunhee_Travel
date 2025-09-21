using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace DogGuns_Games.vamsir
{
    [RequireComponent(typeof(LineRenderer), typeof(EdgeCollider2D))]
    public class WeaphonSmell : Weaphon_base
    {
        /// <summary>
        /// 흔적의 각 포인트를 표현하는 구조체. 위치와 생성 시간을 저장합니다.
        /// </summary>
        private struct TrailPoint
        {
            public Vector3 position;
            public float creationTime;
        }

        [Header("냄새 흔적 설정")]
        [Tooltip("흔적의 최대 길이 (포인트 수)")]
        [SerializeField] private int maxTrailPoints = 50;
        [Tooltip("새로운 포인트를 생성하기 위해 이동해야 하는 최소 거리")]
        [SerializeField] private float pointSpacing = 0.5f;
        [Tooltip("흔적의 시각적 두께")]
        [SerializeField] private float trailWidth = 0.5f;
        [Tooltip("흔적이 완전히 사라지기까지의 시간 (초)")]
        [SerializeField] private float trailLifetime = 10f;

        [Header("렌더링 설정")]
        [Tooltip("라인 렌더러에 적용할 머티리얼. 비어있을 경우 기본 머티리얼이 자동 생성됩니다.")]
        [SerializeField] private Material trailMaterial;
        [Tooltip("흔적의 렌더링 순서. 다른 스프라이트와 겹칠 때 값을 조절합니다.")]
        [SerializeField] private int trailSortingOrder = -1;

        private LineRenderer _trailRenderer;
        private EdgeCollider2D _trailCollider;
        private Transform _playerTransform;
        private LinkedList<TrailPoint> _points;
        private Dictionary<int, float> _damageCooldowns;

        private void Awake()
        {
            _trailRenderer = GetComponent<LineRenderer>();
            _trailCollider = GetComponent<EdgeCollider2D>();
            _points = new LinkedList<TrailPoint>();
            _damageCooldowns = new Dictionary<int, float>();

            SetupLineRenderer();
        }

        public override void OnEnable()
        {
            base.OnEnable();
            
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                _playerTransform = playerObject.transform;
            }
            else
            {
                Debug.LogError("WeaphonSmell: 'Player' 태그를 가진 게임 오브젝트를 찾을 수 없습니다!", this);
                _playerTransform = null;
            }

            _trailRenderer.startWidth = trailWidth;
            _trailRenderer.endWidth = trailWidth;
            
            // 활성화 시 기존 흔적 및 데이터 초기화
            _points.Clear();
            _damageCooldowns.Clear();
            UpdateTrail();
        }

        private void SetupLineRenderer()
        {
            _trailRenderer.useWorldSpace = true;
            _trailRenderer.sortingOrder = trailSortingOrder;
            _trailRenderer.numCapVertices = 0;
            _trailRenderer.numCornerVertices = 0;

            // 그라데이션 설정: 최신 부분(오른쪽)이 불투명하고, 오래된 부분(왼쪽)으로 갈수록 투명해집니다.
            var colorGradient = new Gradient();
            colorGradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.0f, 0.0f), new GradientAlphaKey(1.0f, 0.8f), new GradientAlphaKey(1.0f, 1.0f) }
            );
            _trailRenderer.colorGradient = colorGradient;

            _trailCollider.isTrigger = true;

            if (trailMaterial != null)
            {
                _trailRenderer.material = trailMaterial;
            }
            else
            {
                Debug.LogWarning($"'{gameObject.name}'의 'Trail Material'이(가) 비어있어 기본 머티리얼을 생성합니다.", this);
                Shader pixelShader = Shader.Find("Unlit/PixelLineEffect");
                if (pixelShader != null) { _trailRenderer.material = new Material(pixelShader); }
                else
                {
                    Debug.LogError("'Unlit/PixelLineEffect' 셰이더를 찾을 수 없습니다. 'Sprites/Default'로 대체합니다.");
                    Shader defaultShader = Shader.Find("Sprites/Default");
                    if (defaultShader != null) { _trailRenderer.material = new Material(defaultShader); }
                    else { Debug.LogError("대체 셰이더 'Sprites/Default'도 찾을 수 없습니다."); }
                }
            }
        }

        public override void OnDisable()
        {
            base.OnDisable();
            if (_points != null) { _points.Clear(); UpdateTrail(); }
            if (_damageCooldowns != null) { _damageCooldowns.Clear(); }
        }

        private void Update()
        {
            // 수명이 다한 포인트를 리스트의 앞에서부터 제거합니다.
            bool trailUpdated = false;
            while (_points.Count > 0 && Time.time - _points.First.Value.creationTime > trailLifetime)
            {
                _points.RemoveFirst();
                trailUpdated = true;
            }

            // 포인트가 제거되었다면, LineRenderer와 Collider를 업데이트합니다.
            if (trailUpdated)
            {
                UpdateTrail();
            }
        }

        private void FixedUpdate()
        {
            if (_playerTransform == null) return;

            Vector3 currentPosition = _playerTransform.position;
            
            // 마지막 포인트에서 일정 거리 이상 움직였을 때 새 포인트를 추가합니다.
            bool shouldAddPoint = _points.Count == 0 || Vector3.Distance(_points.Last.Value.position, currentPosition) > pointSpacing;

            if (shouldAddPoint)
            {
                // 위치와 현재 시간을 함께 저장하는 새로운 TrailPoint를 추가합니다.
                _points.AddLast(new TrailPoint { position = currentPosition, creationTime = Time.time });

                // 최대 포인트 수를 초과하면 가장 오래된 포인트를 제거합니다.
                while (_points.Count > maxTrailPoints)
                {
                    _points.RemoveFirst();
                }

                UpdateTrail();
            }
        }

        private void UpdateTrail()
        {
            if (_points.Count < 2)
            {
                _trailRenderer.positionCount = 0;
                _trailCollider.enabled = false;
                return;
            }

            _trailCollider.enabled = true;
            _trailRenderer.positionCount = _points.Count;
            
            // TrailPoint 리스트에서 위치(Vector3) 정보만 추출하여 LineRenderer에 설정합니다.
            _trailRenderer.SetPositions(_points.Select(p => p.position).ToArray());

            // EdgeCollider의 포인트들을 업데이트합니다.
            Vector2[] localPoints = new Vector2[_points.Count];
            int i = 0;
            foreach (var trailPoint in _points)
            {
                localPoints[i] = transform.InverseTransformPoint(trailPoint.position);
                i++;
            }
            _trailCollider.points = localPoints;
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (!other.TryGetComponent<VamserMobBase>(out var enemy)) return;
            int enemyId = other.GetInstanceID();
            if (_damageCooldowns.TryGetValue(enemyId, out float nextDamageTime) && Time.time < nextDamageTime) return;
            enemy.TakeDamage(attackPower);
            _damageCooldowns[enemyId] = Time.time + coolTime;
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.TryGetComponent<VamserMobBase>(out _)) { _damageCooldowns.Remove(other.GetInstanceID()); }
        }
    }
}