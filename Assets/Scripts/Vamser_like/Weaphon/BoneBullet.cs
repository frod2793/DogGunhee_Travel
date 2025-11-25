using Cysharp.Threading.Tasks;
using DG.Tweening;
using DogGuns_Games.vamsir;
using UnityEngine;

public class BoneBullet : WeaphonBase
{
    #region 필드 및 변수

    [Tooltip("이 총알을 생성하고 관리하는 오브젝트 풀 스포너입니다.")]
    public WeaphonBone ObjectPoolSpawner { get; set; }

    [Tooltip("총알의 이동 속도입니다. WeaphonBone에 의해 설정됩니다.")]
    public float BulletSpeed { get; set; } = 0;

    [Header("회전 설정")]
    [Tooltip("총알의 초당 회전 속도입니다 (도/초).")]
    [SerializeField] private float m_rotateSpeed = 360f; // [추가됨] 인스펙터에서 조절 가능

    [Header("폭발 설정")]
    [Tooltip("업그레이드 시 폭발 반경입니다.")]
    [SerializeField] private float m_explosionRadius = 1.5f;
    
    [Tooltip("업그레이드 시 폭발로 인한 추가 광역 대미지입니다.")]
    [SerializeField] private float m_explosionDamage = 10f;

    [Header("감지 설정")]
    [Tooltip("폭발 시 감지할 Mob의 레이어를 설정합니다.")]
    [SerializeField] private LayerMask m_mobLayerMask;

    // 내부 상태 변수
    private Vector3 m_attackAngle;
    private bool m_isActive;

    // 물리 연산 최적화 (NonAlloc)
    private const int k_MaxOverlapColliders = 10;
    private readonly Collider2D[] m_overlapResults = new Collider2D[k_MaxOverlapColliders];
    private ContactFilter2D m_contactFilter;

    // 카메라 캐싱
    private Camera m_mainCamera;

    #endregion

    #region Unity 라이프사이클

    private void Awake()
    {
        m_mainCamera = Camera.main;

        // ContactFilter 초기화
        m_contactFilter.useTriggers = true; 
        m_contactFilter.SetLayerMask(m_mobLayerMask);
        m_contactFilter.useLayerMask = true;
    }

    protected override void OnEnable()
    {
        base.OnEnable(); // 부모 클래스(WeaphonBase)의 OnEnable 호출
        
        m_isActive = true;
        SoundManager.PlaySound(Sound.SFX, SoundKeys.Throwbone);
        
        // 이동 및 회전 루프 시작
        MoveAndRotateBulletAsync().Forget();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        m_isActive = false;
        transform.DOKill(); // 안전장치
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!m_isActive || !other.CompareTag("Mob")) return;

        if (other.TryGetComponent(out VamserMobBase mob))
        {
            mob.TakeDamage(attackPower, mobStunTime);
        }

        if (isUpgradelv2)
        {
            BulletExplosion();
        }
        else
        {
            ReleaseToPool();
        }
    }

    #endregion

    #region 이동 및 회전 로직

    /// <summary>
    /// 총알 이동 및 회전 (최적화됨: DOTween 제거하고 직접 연산)
    /// </summary>
    private async UniTaskVoid MoveAndRotateBulletAsync()
    {
        var token = this.GetCancellationTokenOnDestroy();

        while (m_isActive)
        {
            float dt = Time.deltaTime;

            // 1. 이동 처리
            transform.position += m_attackAngle * (BulletSpeed * dt);

            // 2. [수정] 회전 처리 (변수 m_rotateSpeed 사용)
            // Z축 기준으로 회전
            transform.Rotate(0, 0, m_rotateSpeed * dt);

            // 3. 화면 밖 체크
            if (IsOutOfBounds())
            {
                ReleaseToPool();
                return;
            }

            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }
    }

    private bool IsOutOfBounds()
    {
        if (m_mainCamera == null) return false;

        Vector3 viewPos = m_mainCamera.WorldToViewportPoint(transform.position);
        return viewPos.x < -0.2f || viewPos.x > 1.2f || viewPos.y < -0.2f || viewPos.y > 1.2f;
    }

    #endregion

    #region 기능 메서드

    public void ThrowBullet(Vector3 direction)
    {
        m_attackAngle = direction.normalized;
    }

    private void BulletExplosion()
    {
        EffectManager.Instance.PlayEffect(EffectType.BoneExplosion, transform.position);

        // OverlapCircleNonAlloc 사용
        int numColliders = Physics2D.OverlapCircle(transform.position, m_explosionRadius, m_contactFilter, m_overlapResults);
        
        for (int i = 0; i < numColliders; i++)
        {
            var hitCollider = m_overlapResults[i];
            if (hitCollider != null && hitCollider.CompareTag("Mob") && hitCollider.TryGetComponent(out VamserMobBase mob))
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
        ObjectPoolSpawner?.WeaphonBoneObjectPool.Release(this);
    }

    public void Initialize(WeaphonBase parentWeapon)
    {
        isUpgradelv2 = parentWeapon.isUpgradelv2;
        attackPower = parentWeapon.attackPower;
        mobStunTime = parentWeapon.mobStunTime;
        BulletSpeed = parentWeapon.attackSpeed;
    }

    public void ResetState()
    {
        m_isActive = true;
        transform.DOKill();
        transform.rotation = Quaternion.identity; // 회전 초기화
    }

    #endregion
}