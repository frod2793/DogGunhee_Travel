using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using Vamser_like.Mob.MobBase;
using Vamser_like.Weaphon.Base;

namespace Vamser_like.Weaphon
{
    public class BoneBullet : WeaphonBase
    {
        #region 필드 및 변수

        public WeaphonBone ObjectPoolSpawner { get; set; }
        public float BulletSpeed { get; set; }

        [Header("이동 설정")]
        [SerializeField]
        private float m_travelDistance = 20f;

        [Header("회전 설정")]
        [SerializeField]
        private float m_rotateSpeed = 360f;

        [Header("폭발 설정")]
        [SerializeField]
        private float m_explosionRadius = 1.5f;
        [SerializeField]
        private float m_explosionDamage = 10f;

        [Header("감지 설정")]
        [SerializeField]
        private LayerMask m_mobLayerMask;

        private Transform m_transform;
        private Vector3 m_attackAngle;
        private bool m_isActive;
        private Tween m_moveTween;

        private const int k_MaxOverlapColliders = 10;
        private readonly Collider2D[] m_overlapResults = new Collider2D[k_MaxOverlapColliders];
        private ContactFilter2D m_contactFilter;

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            m_transform = transform;
            
            m_contactFilter = new ContactFilter2D();
            m_contactFilter.useTriggers = true;
            m_contactFilter.SetLayerMask(m_mobLayerMask);
            m_contactFilter.useLayerMask = true;
        }

        private new void OnEnable()
        {
            SetWeaphonState(WeaphonState.Idle);
            m_isActive = true;
            SoundManager.PlaySound(Sound.SFX, SoundKeys.Throwbone);
        }

        private new void OnDisable()
        {
            m_isActive = false;
            m_moveTween?.Kill();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!m_isActive || !other.CompareTag("Mob")) return;

            if (other.TryGetComponent(out MobBase mob))
            {
                mob.TakeDamage(attackPower, mobStunTime);
            }

            if (isEvolved)
            {
                BulletExplosion();
            }
            else
            {
                ReleaseToPool();
            }
        }

        #endregion

        #region 핵심 로직

        public void ThrowBullet(Vector3 direction)
        {
            // UniTask로 구현된 비동기 로직을 실행하고, 결과룰 기다리지 않습니다 (Fire and Forget)
            ThrowAndTrackLifecycleAsync(direction).Forget();
        }

        private async UniTaskVoid ThrowAndTrackLifecycleAsync(Vector3 direction)
        {
            m_attackAngle = direction.normalized;
            Vector3 targetPosition = m_transform.position + m_attackAngle * m_travelDistance;
            float duration = m_travelDistance / BulletSpeed;

            var token = this.GetCancellationTokenOnDestroy();

            m_moveTween?.Kill();
            m_moveTween = DOTween.Sequence()
                .Append(m_transform.DOMove(targetPosition, duration).SetEase(Ease.Linear))
                .Join(m_transform.DORotate(new Vector3(0, 0, m_rotateSpeed), 1f / (m_rotateSpeed / 360f),
                        RotateMode.FastBeyond360)
                    .SetEase(Ease.Linear)
                    .SetLoops(-1, LoopType.Incremental));

            // 트윈이 끝까지 재생되거나, 외부 요인(충돌)에 의해 취소될 때까지 기다립니다.
            bool cancelled = await m_moveTween.ToUniTask(cancellationToken: token).SuppressCancellationThrow();

            if (!cancelled)
            {
                ReleaseToPool();
            }
        }

        private void BulletExplosion()
        {
            Vector3 currentPosition = m_transform.position;
            EffectManager.Instance.PlayEffect(EffectType.BoneExplosion, currentPosition);

            int numColliders =
                Physics2D.OverlapCircle(currentPosition, m_explosionRadius, m_contactFilter, m_overlapResults);

            for (int i = 0; i < numColliders; i++)
            {
                if (m_overlapResults[i].TryGetComponent(out MobBase mob))
                {
                    mob.TakeDamage(m_explosionDamage);
                }
            }

            ReleaseToPool();
        }

        private void ReleaseToPool()
        {
            if (!m_isActive) return;
            m_isActive = false;
            m_moveTween?.Kill();

            if (ObjectPoolSpawner != null)
            {
                ObjectPoolSpawner.WeaphonBoneObjectPool.Release(this);
            }
        }

        public void Initialize(WeaphonBase parentWeapon)
        {
            isEvolved = parentWeapon.isEvolved;
            attackPower = parentWeapon.attackPower;
            mobStunTime = parentWeapon.mobStunTime;
            BulletSpeed = parentWeapon.attackSpeed;
        }

        public void ResetState()
        {
            m_isActive = true;
            m_transform.DOKill();
            m_transform.rotation = Quaternion.identity;
        }

        #endregion
    }
}