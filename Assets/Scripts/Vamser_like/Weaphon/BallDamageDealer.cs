using UnityEngine;
using System.Collections.Generic;

namespace DogGuns_Games.vamsir
{
    /// <summary>
    /// WeaphonBallplay에 의해 생성된 공의 피해 처리를 담당합니다.
    /// 적과 충돌 시 지정된 공격력과 쿨타임에 따라 지속적인 피해를 줍니다.
    /// </summary>
    [RequireComponent(typeof(Collider2D), typeof(TrailRenderer))]
    public class BallDamageDealer : Weaphon_base
    {
        private float _attackPower;
        private float _coolTime;
        private TrailRenderer _trailRenderer;

        // 피해를 입은 적과 다음 피해 가능 시간을 추적 (Key: InstanceID, Value: Time.time + coolTime)
        private readonly Dictionary<int, float> _damageCooldowns = new Dictionary<int, float>();

        [Header("궤적 이펙트 설정")]
        [Tooltip("궤적에 적용할 머티리얼. 비어있을 경우 경고가 표시됩니다.")]
        [SerializeField] private Material trailMaterial;
        [Tooltip("궤적의 지속 시간(초)")]
        [SerializeField] private float trailTime = 0.3f;
        [Tooltip("궤적의 시작 두께")]
        [SerializeField] private float trailStartWidth = 0.2f;

        private void Awake()
        {
            var col = GetComponent<Collider2D>();
            if (!col.isTrigger)
            {
                LogManager.LogWarning($"'{name}'의 Collider2D가 Trigger로 설정되지 않았습니다. 피해 감지가 동작하지 않을 수 있습니다.", LogManager.LogCategory.Weapon, this);
            }

            _trailRenderer = GetComponent<TrailRenderer>();
            SetupTrailRenderer();
        }

        /// <summary>
        /// WeaphonBallplay에서 이 공의 스탯을 초기화합니다.
        /// </summary>
        public void Initialize(Weaphon_base parentWeapon)
        {
            isUpgradelv2 = parentWeapon.isUpgradelv2;
            attackPower = parentWeapon.attackPower;
            mobStunTime = parentWeapon.mobStunTime;

            // 무기가 활성화/비활성화 될 때 궤적이 초기화되도록 합니다.
            if (_trailRenderer != null)
            {
                _trailRenderer.Clear();
            }
        }

        private void OnDisable()
        {
            // 비활성화 시 쿨다운 목록과 궤적을 정리하여 메모리 누수 및 시각적 오류를 방지합니다.
            _damageCooldowns.Clear();
            if (_trailRenderer != null)
            {
                _trailRenderer.Clear();
            }
        }

        /// <summary>
        /// TrailRenderer의 시각적 속성을 코드에서 설정합니다.
        /// </summary>
        private void SetupTrailRenderer()
        {
            _trailRenderer.time = trailTime;
            _trailRenderer.startWidth = trailStartWidth;
            _trailRenderer.endWidth = 0f;
            _trailRenderer.autodestruct = false; // 오브젝트 풀링을 사용하므로 자동으로 파괴하지 않음

            // 그라데이션: 시작은 불투명, 끝은 투명하게 설정하여 자연스럽게 사라지는 효과를 줍니다.
            var colorGradient = new Gradient();
            colorGradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            _trailRenderer.colorGradient = colorGradient;

            // 렌더링 순서: 공 스프라이트보다 뒤에 그려지도록 설정
            if (TryGetComponent<SpriteRenderer>(out var spriteRenderer))
            {
                _trailRenderer.sortingOrder = spriteRenderer.sortingOrder - 1;
            }

            // 머티리얼 설정: 인스펙터에서 할당된 머티리얼을 우선적으로 사용합니다.
            if (trailMaterial != null)
            {
                _trailRenderer.material = trailMaterial;
            }
            else
            {
                LogManager.LogWarning($"'Trail Material'이 할당되지 않았습니다. TrailRenderer에 적절한 머티리얼을 할당해주세요.", LogManager.LogCategory.Weapon, this);
            }
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (!other.TryGetComponent<VamserMobBase>(out var enemy)) return;

            int enemyId = other.GetInstanceID();

            if (_damageCooldowns.TryGetValue(enemyId, out float nextDamageTime) && Time.time < nextDamageTime)
            {
                return; // 아직 쿨다운 중
            }

            _damageCooldowns[enemyId] = Time.time + _coolTime;
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            // 적이 충돌 범위에서 벗어나면 쿨다운 목록에서 제거하여 메모리 관리
            if (other.TryGetComponent<VamserMobBase>(out _)) _damageCooldowns.Remove(other.GetInstanceID());
        }
    }
}