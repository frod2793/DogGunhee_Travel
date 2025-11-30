using Cysharp.Threading.Tasks;
using DG.Tweening;
using DogGuns_Games.vamsir;
using UnityEngine;

public class BoneBullet : WeaphonBase
{
    #region 필드 및 변수

    public WeaphonBone ObjectPoolSpawner { get; set; }
    public float BulletSpeed { get; set; } = 0;

    [Header("회전 설정")]
    [SerializeField] private float m_rotateSpeed = 360f;

    [Header("폭발 설정")]
    [SerializeField] private float m_explosionRadius = 1.5f;
    [SerializeField] private float m_explosionDamage = 10f;

    [Header("감지 설정")]
    [SerializeField] private LayerMask m_mobLayerMask;

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
        m_contactFilter = new ContactFilter2D();
        m_contactFilter.useTriggers = true; 
        m_contactFilter.SetLayerMask(m_mobLayerMask);
        m_contactFilter.useLayerMask = true;
    }

    private void OnEnable()
    {
        // WeaphonBase의 OnEnable 로직을 직접 수행
        SetWeaphonState(WeaphonState.Idle);
        
        m_isActive = true;
        SoundManager.PlaySound(Sound.SFX, SoundKeys.Throwbone);
    }

    private void OnDisable()
    {
        m_isActive = false;
        m_moveTween?.Kill(); // 트윈 정리
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
        m_attackAngle = direction.normalized;
        
        float travelDistance = 20f; 
        Vector3 targetPosition = transform.position + m_attackAngle * travelDistance;
        float duration = travelDistance / BulletSpeed;

        m_moveTween?.Kill();
        m_moveTween = DOTween.Sequence()
            .Append(transform.DOMove(targetPosition, duration).SetEase(Ease.Linear))
            .Join(transform.DORotate(new Vector3(0, 0, m_rotateSpeed), 1f / (m_rotateSpeed / 360f), RotateMode.FastBeyond360)
                .SetEase(Ease.Linear)
                .SetLoops(-1, LoopType.Incremental))
            .OnComplete(ReleaseToPool);
    }

    private void BulletExplosion()
    {
        EffectManager.Instance.PlayEffect(EffectType.BoneExplosion, transform.position);

        int numColliders = Physics2D.OverlapCircle(transform.position, m_explosionRadius, m_contactFilter, m_overlapResults);
        
        for (int i = 0; i < numColliders; i++)
        {
            if (m_overlapResults[i].TryGetComponent(out MobBase mob))
            {
                mob.TakeDamage(m_explosionDamage, 0);
            }
        }
        
        ReleaseToPool();
    }

    private void ReleaseToPool()
    {
        if (!m_isActive) return;
        m_isActive = false;
        m_moveTween?.Kill();
        ObjectPoolSpawner?.WeaphonBoneObjectPool.Release(this);
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
        transform.DOKill();
        transform.rotation = Quaternion.identity;
    }

    #endregion
}