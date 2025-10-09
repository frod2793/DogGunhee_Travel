using UnityEngine;
using System.Collections.Generic;

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

        // 순환 큐(Circular Queue)를 위한 필드 (메모리 최적화)
        private TrailPoint[] _points;
        private int _head;
        private int _tail;
        private int _pointCount;

        // 렌더러와 콜라이더 업데이트 최적화를 위한 캐시 배열
        private Vector3[] _linePositions;
        private readonly List<Vector2> _colliderPointsList = new List<Vector2>();

        private readonly Dictionary<int, float> _damageCooldowns = new Dictionary<int, float>();

        private void Awake()
        {
            _trailRenderer = GetComponent<LineRenderer>();
            _trailCollider = GetComponent<EdgeCollider2D>();

           
        }

        public override void OnEnable()
        {
            base.OnEnable();
            SetupLineRenderer();
            _playerTransform = VamserLikeGameManager.Instance.PlayerTransfrom();
            _trailRenderer.startWidth = trailWidth;
            _trailRenderer.endWidth = trailWidth;
            
            // 순환 큐 및 데이터 초기화
            _points = new TrailPoint[maxTrailPoints];
            _linePositions = new Vector3[maxTrailPoints];
            _head = 0; _tail = 0; _pointCount = 0;
            _damageCooldowns.Clear();
            UpdateTrail();
        }

        private void SetupLineRenderer()
        {
            _trailRenderer.useWorldSpace = true;
            _trailRenderer.sortingOrder = trailSortingOrder;
            _trailRenderer.numCapVertices = 5; // 라인의 끝부분을 부드럽게 처리합니다.
            _trailRenderer.numCornerVertices = 5;
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
            // 비활성화 시 모든 포인트와 데이터를 초기화합니다.
            _pointCount = 0;
            UpdateTrail();
            _damageCooldowns.Clear();
        }

        private void Update()
        {
            if (_playerTransform == null) return;

            // 1. 수명이 다한 포인트 제거 (순환 큐의 head를 이동)
            bool trailUpdated = false;
            while (_pointCount > 0 && Time.time - _points[_head].creationTime > trailLifetime)
            {
                _head = (_head + 1) % maxTrailPoints;
                _pointCount--;
                trailUpdated = true;
            }

            // 2. 플레이어 이동에 따라 새 포인트 추가
            Vector3 currentPosition = _playerTransform.position;
            bool shouldAddPoint = _pointCount == 0 || Vector3.Distance(_points[(_tail - 1 + maxTrailPoints) % maxTrailPoints].position, currentPosition) > pointSpacing;

            if (shouldAddPoint)
            {
                // 순환 큐가 가득 찼다면, 가장 오래된 포인트를 덮어씁니다 (head를 이동).
                if (_pointCount == maxTrailPoints)
                {
                    _head = (_head + 1) % maxTrailPoints;
                    _pointCount--;
                }

                _points[_tail] = new TrailPoint { position = currentPosition, creationTime = Time.time };
                _tail = (_tail + 1) % maxTrailPoints;
                _pointCount++;
                trailUpdated = true;
            }

            // 3. 흔적에 변경이 있었다면 렌더러와 콜라이더를 업데이트합니다.
            if (trailUpdated)
            {
                UpdateTrail();
            }
        }

        private void UpdateTrail()
        {
            if (_pointCount < 2)
            {
                _trailRenderer.positionCount = 0;
                _trailCollider.enabled = false;
                return;
            }

            _trailRenderer.positionCount = _pointCount;
            _trailCollider.enabled = true;

            // 순환 큐의 데이터를 미리 할당된 배열에 복사하여 GC를 방지합니다.
            int currentPointIndex = _head;
            for (int i = 0; i < _pointCount; i++)
            {
                _linePositions[i] = _points[currentPointIndex].position;
                // _colliderPoints[i] = transform.InverseTransformPoint(_points[currentPointIndex].position); // List로 변경되므로 직접 할당하지 않음
                currentPointIndex = (currentPointIndex + 1) % maxTrailPoints;
            }

            // GC 할당을 피하기 위해 SetPositions(ToArray()) 대신, positionCount를 설정하고 각 포인트를 직접 할당합니다.
            for (int i = 0; i < _pointCount; i++)
            {
                _trailRenderer.SetPosition(i, _linePositions[i]);
            }
            
            // EdgeCollider2D는 SetPoints(List<T>)를 사용하여 GC 할당 없이 업데이트합니다.
            _colliderPointsList.Clear();
            for (int i = 0; i < _pointCount; i++)
            {
                // LineRenderer의 월드 좌표를 EdgeCollider2D의 로컬 좌표로 변환합니다.
                _colliderPointsList.Add(transform.InverseTransformPoint(_linePositions[i]));
            }
            _trailCollider.SetPoints(_colliderPointsList);
        }
       
        private void OnTriggerStay2D(Collider2D other)
        {
            if (!other.CompareTag("Mob")) return;

            // 쿨타임이 적용된 지속적인 데미지
            int mobInstanceId = other.gameObject.GetInstanceID();

            // 쿨타임이 끝났는지 확인 (또는 처음 충돌하는 몹인지 확인)
            if (!_damageCooldowns.ContainsKey(mobInstanceId) || Time.time >= _damageCooldowns[mobInstanceId])
            {
                if (other.TryGetComponent<VamserMobBase>(out var mob))
                {
                    if (!mob.IsDead)
                    {
                        // 데미지를 입히고 다음 쿨타임 시간을 기록합니다.
                        mob.TakeDamage(attackPower);
                        _damageCooldowns[mobInstanceId] = Time.time + coolTime;
                        
                    }
                }
            }
        }
    }
}