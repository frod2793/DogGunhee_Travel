using Cysharp.Threading.Tasks;
using DG.Tweening;
using DogGuns_Games.vamsir;
using System.Threading;
using UnityEngine;

public class BoneBullet : Weaphon_base
{
    #region 필드 및 변수

    [Tooltip("이 총알을 생성하고 관리하는 오브젝트 풀 스포너입니다.")]
    public WeaphonBone ObjectPoolSpawner { get; set; }

    [Tooltip("총알의 이동 속도입니다. WeaphonBone에 의해 설정됩니다.")]
    public float BulletSpeed { get; set; } = 0;

    [Header("폭발 설정")]
    [Tooltip("업그레이드 시 폭발 반경입니다.")]
    [SerializeField] private float m_explosionRadius = 1.5f;
    
    [Tooltip("업그레이드 시 폭발로 인한 추가 광역 대미지입니다.")]
    [SerializeField] private float m_explosionDamage = 10f;

    [Header("감지 설정")]
    [Tooltip("폭발 시 감지할 Mob의 레이어를 설정합니다.")]
    [SerializeField] private LayerMask m_mobLayerMask;

    // Private 필드들은 m_ 접두사를 사용하여 명확성을 높였습니다.
    private Vector3 m_attackAngle;
    private const float k_RotateSpeed = 5f; // Z축 회전 속도
    private bool m_isActive;
    private CancellationTokenSource m_cancellationTokenSource;

    #endregion

    // 폭발 시 OverlapCircleNonAlloc을 위한 최적화 필드
    private const int k_MaxOverlapColliders = 10; // 폭발 시 감지할 최대 콜라이더 수
    private Collider2D[] m_overlapResults = new Collider2D[k_MaxOverlapColliders];
    private ContactFilter2D m_contactFilter;

    #region Unity 라이프사이클 메서드

    private void Awake()
    {
        m_contactFilter.useTriggers = true; // Mob의 isTrigger 콜라이더를 감지해야 하므로 true로 설정
        m_contactFilter.SetLayerMask(m_mobLayerMask); // 설정된 Mob 레이어만 감지
        m_contactFilter.useLayerMask = true;
    }

    private void OnEnable()
    {
        m_isActive = true;
        SoundManager.PlaySound(Sound.SFX, SoundKeys.Throwbone);
    }

    private void OnDisable()
    {
        m_isActive = false;
        // 비동기 작업 취소 및 리소스 정리
        if (m_cancellationTokenSource != null)
        {   
            m_cancellationTokenSource.Cancel();
            m_cancellationTokenSource.Dispose();
            m_cancellationTokenSource = null;
        }
        // DOTween.Kill은 ResetState에서 처리하므로 중복 호출을 피합니다.
    }

    // 콜라이더를 트리거로 사용하므로 OnTriggerEnter2D로 변경합니다.
    // 중요: BoneBullet 프리팹의 Collider2D에서 'Is Trigger'가 반드시 체크되어야 합니다.
    private void OnTriggerEnter2D(Collider2D other)
    {
        // 총알이 활성 상태일 때만 충돌 로직을 처리합니다.
        if (m_isActive && other.CompareTag("Mob"))
        {
            // 몹에게 데미지와 스턴 효과를 직접 전달합니다.
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
    }

    #endregion

    #region 총알 이동 및 회전

    /// <summary>
    /// 총알을 지정된 방향으로 이동시키고 회전시킵니다.
    /// </summary>
    private async UniTaskVoid MoveAndRotateBullet(CancellationToken cancellationToken)
    {
        // 총알 이동 설정 - 현재 위치에서 진행 방향으로 계속 이동
        Vector3 targetPosition = transform.position + m_attackAngle * 100f; // 충분히 먼 거리
        transform.DOMove(targetPosition, 100f / BulletSpeed)
            .SetSpeedBased(true) // 속도 기반으로 이동 시간 계산
            .SetEase(Ease.Linear) // 선형 이동
            .SetLink(gameObject); // GameObject가 비활성화/파괴될 때 트윈 자동 종료

        // Z축 회전 설정
        transform.DORotate(new Vector3(0, 0, 360f), 1f / k_RotateSpeed, RotateMode.LocalAxisAdd)
            .SetLoops(-1, LoopType.Restart)
            .SetEase(Ease.Linear) // 선형 회전
            .SetLink(gameObject); // GameObject가 비활성화/파괴될 때 트윈 자동 종료

        // 화면 밖으로 나가는지 주기적으로 체크
        while (!cancellationToken.IsCancellationRequested)
        {
            CheckBounds();
            // Delay가 취소될 때 예외를 던지지 않도록 설정
            await UniTask.Delay(50, ignoreTimeScale: false, PlayerLoopTiming.Update, cancellationToken).SuppressCancellationThrow();
        }
    }

    #endregion

    #region 총알 동작 관리

    /// <summary>
    /// 총알이 화면 밖으로 나갔는지 확인하고, 나갔다면 풀에 반환합니다.
    /// </summary>
    private void CheckBounds()
    {
        if (Camera.main == null) return;

        Vector3 viewPos = Camera.main.WorldToViewportPoint(transform.position);
        // 화면 경계를 약간 벗어나는 여유를 줍니다.
        if (viewPos.x < -0.2f || viewPos.x > 1.2f || viewPos.y < -0.2f || viewPos.y > 1.2f)
        {
            ReleaseToPool();
        }
    }

    /// <summary>
    /// 총알의 발사 방향을 설정합니다.
    /// </summary>
    public void ThrowBullet(Vector3 direction)
    {
        // CancellationTokenSource를 이동 시작 직전에 생성합니다.
        m_cancellationTokenSource = new CancellationTokenSource();
        m_attackAngle = direction.normalized;
        // 방향이 설정된 직후에 이동 및 회전을 시작합니다.
        MoveAndRotateBullet(m_cancellationTokenSource.Token).Forget();
    }

    /// <summary>
    /// 총알 폭발 효과를 생성하고 주변 적에게 광역 피해를 줍니다.
    /// </summary>
    private void BulletExplosion()
    {
        // 1. 폭발 이펙트 생성
        EffectManager.Instance.PlayEffect(EffectType.BoneExplosion, transform.position);

        // 2. 주변 적 감지 및 피해 처리 (최신 API인 OverlapCircle 사용)
        int numColliders = Physics2D.OverlapCircle(transform.position, m_explosionRadius, m_contactFilter, m_overlapResults);
        for (int i = 0; i < numColliders; i++)
        {
            var hitCollider = m_overlapResults[i];
            if (hitCollider != null && hitCollider.CompareTag("Mob") && hitCollider.TryGetComponent(out VamserMobBase mob))
            {
                // 폭발 대미지를 입힙니다. (스턴은 선택적으로 추가 가능)
                mob.TakeDamage(m_explosionDamage, 0);
            }
        }
        // 배열을 재활용하기 위해 사용 후 null로 초기화 (선택 사항이지만 안전성을 높임)
        System.Array.Clear(m_overlapResults, 0, numColliders);
        // 3. 오브젝트 풀로 반환
        ReleaseToPool();
    }

    /// <summary>
    /// 총알을 비활성화하고 오브젝트 풀로 반환합니다.
    /// </summary>
    private void ReleaseToPool()
    {
        if (!m_isActive) return;
        m_isActive = false;

        ObjectPoolSpawner?.WeaphonBoneObjectPool.Release(this);
    }

    #endregion

    /// <summary>
    /// 부모 무기(WeaphonBone)의 스탯으로 투사체를 초기화합니다.
    /// </summary>
    public void Initialize(Weaphon_base parentWeapon)
    {
        isUpgradelv2 = parentWeapon.isUpgradelv2;
        attackPower = parentWeapon.attackPower;
        mobStunTime = parentWeapon.mobStunTime;
        BulletSpeed = parentWeapon.attackSpeed;
    }

    /// <summary>
    /// 오브젝트 풀에서 재사용될 때 상태를 초기화합니다.
    /// </summary>
    public void ResetState()
    {
        m_isActive = true;
        // 오브젝트 풀에서 재사용될 때 모든 DOTween 애니메이션을 확실히 정리합니다.
        DOTween.Kill(transform);
    }
}