using Cysharp.Threading.Tasks;
using DG.Tweening;
using DogGuns_Games.vamsir;
using UnityEngine;

public class BoneBullet : Weaphon_base
{
    #region 필드 및 변수

    // 총알 오브젝트 풀 관리 객체
    public WeaphonBone objectPoolSpawner;

    // 공격 방향을 저장하는 벡터
    private Vector3 _attackAngle;

    [HideInInspector]
    // 총알의 이동 속도 총알 대미지는 WeaphonBone 에서처리
    public float bulletSpeed = 0;

    private readonly float _rotateSpeed = 5f;

    private bool _isActive;

    #endregion

    #region Unity 라이프사이클

    private void OnEnable()
    {
        _isActive = true;
        MoveAndRotateBullet().Forget();
        SoundManager.PlaySound(Sound.SFX, SoundKeys.Throwbone);
    }
    private void OnDisable()
    {
        _isActive = false;
        DOTween.Kill(transform);
    }
    private void OnCollisionEnter2D(Collision2D other)
    {
        if (other.gameObject.CompareTag("Mob"))
        {
            // 몹에게 데미지와 스턴 효과를 직접 전달합니다.
            if (other.gameObject.TryGetComponent<VamserMobBase>(out var mob))
            {
                mob.TakeDamage(attackPower, mobStunTime);
            }
            
            if (isUpgradelv2)
            {
                BulletExplosion();
            }
            else if (_isActive) // 총알이 활성 상태인지 확인
            {
                _isActive = false;
                objectPoolSpawner?.WeaphonBoneObjectPool.Release(this);
            }
        }
    }

    #endregion

    #region 총알 이동 및 회전

    // 총알 이동과 회전 함수 (UniTask 사용)
    private async UniTaskVoid MoveAndRotateBullet()
    {
        // 기존 트윈이 실행 중이면 종료
        DOTween.Kill(transform);

        // 총알 이동 설정 - 현재 위치에서 진행 방향으로 계속 이동
        Vector3 targetPosition = transform.position + _attackAngle * 100f; // 충분히 먼 거리
        transform.DOMove(targetPosition, 100f / bulletSpeed)
            .SetSpeedBased(true)
            .SetEase(Ease.Linear);

        // Z축 회전 설정
        transform.DORotate(new Vector3(0, 0, 360f), 1f / _rotateSpeed, RotateMode.LocalAxisAdd)
            .SetLoops(-1, LoopType.Restart)
            .SetEase(Ease.Linear);

        // 화면 밖으로 나가는지 주기적으로 체크
        while (_isActive)
        {
            CheckBounds();
            await UniTask.Delay(50, cancellationToken: this.GetCancellationTokenOnDestroy());
        }
    
        // 비활성화될 때 트윈 정리
        DOTween.Kill(transform);
    }


    #endregion

    #region 총알 동작 관리

    // 화면 밖으로 나가면 오브젝트 비활성화 함수
    private void CheckBounds()
    {
        var viewPos = Camera.main.WorldToViewportPoint(transform.position);
        if (viewPos.x < 0 || viewPos.x > 1 || viewPos.y < 0 || viewPos.y > 1)
            if (objectPoolSpawner != null && _isActive) // 총알이 활성 상태인지 확인
            {
                _isActive = false;
                objectPoolSpawner?.WeaphonBoneObjectPool.Release(this);
            }
    }

    // 총알 발사 방향 설정 함수
    public void Throw_Bullet(Vector3 direction)
    {
        _attackAngle = direction.normalized;
    }

    private void BulletExplosion()
    {
        // 총알 폭발 이펙트 생성

        // 콜라이더 범위를 2배로 순간적으로 늘렸다가 원상 복귀 시킨다

        // 총알 오브젝트 풀로 반환
        _isActive = false; // 반환 전 비활성화
        objectPoolSpawner?.WeaphonBoneObjectPool.Release(this);
    }

    #endregion

    /// <summary>
    /// 부모 무기의 스탯으로 투사체를 초기화합니다.
    /// </summary>
    public void Initialize(Weaphon_base parentWeapon)
    {
        isUpgradelv2 = parentWeapon.isUpgradelv2;
        attackPower = parentWeapon.attackPower;
        mobStunTime = parentWeapon.mobStunTime;
    }
    
    public void ResetState()
    {
        // 필요한 상태 초기화
        _isActive = true;
        
        // 기타 속성 초기화
    }
}