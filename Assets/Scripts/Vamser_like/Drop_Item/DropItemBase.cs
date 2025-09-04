using DG.Tweening;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using DogGuns_Games.vamsir;

public class DropItemBase : MonoBehaviour, IObjectPoolSpawnerSettable
{
    public ObjectPoolSpawner objectPoolSpawner { get; set; }
    // 몬스터를 잡으면 나오는 아이템 코인, 경험치 , 대형 경험치 등의 드랍 시의 부유 효과 등을 정의 함
    [Header("플로팅 효과 설정")]
    [Tooltip("아이템이 위아래로 떠다니는 최대 높이입니다.")]
    [SerializeField] protected float floatHeight = 0.2f;
    [Tooltip("아이템이 한 번 위아래로 왕복하는 데 걸리는 시간입니다.")]
    [SerializeField] protected float floatDuration = 1.5f;

    [Header("회전 효과 설정")]
    [Tooltip("좌우로 회전하는 최대 각도입니다 (Y축 기준).")]
    [SerializeField] protected float rotationAngle = 10f;
    [Tooltip("한 번 좌우로 왕복하는 데 걸리는 시간입니다.")]
    [SerializeField] protected float rotationDuration = 2.0f;

    [Header("생명주기 설정")]
    [Tooltip("스폰된 후, 줍지 않았을 때 자동으로 사라지기까지의 시간(초)입니다.")]
    [SerializeField] protected float lifeTime = 30f;

    private CancellationTokenSource _lifeTimeCts;

    protected virtual void OnEnable()
    {
        // 오브젝트가 활성화될 때 플로팅 효과 시작
        StartFloating();

        // 자동 복귀 타이머 시작
        StartReturnToPoolTimer();
    }

    protected virtual void OnDisable()
    {
        // 자동 복귀 타이머 중지
        _lifeTimeCts?.Cancel();
        _lifeTimeCts?.Dispose();
        _lifeTimeCts = null;

        // 오브젝트가 비활성화될 때 진행 중인 모든 트윈을 중지하여 리소스를 정리합니다.
        transform.DOKill();
    }

    protected virtual void StartFloating()
    {
        // 아이템이 스폰된 위치를 중심으로 위아래로 떠다니도록 시퀀스를 생성합니다.
        // 이는 아이템이 맵 경계 근처에 스폰되더라도 맵 밖으로 나갈 확률을 줄여줍니다.
        var sequence = DOTween.Sequence();
        
        // 1. 위로 절반 이동
        sequence.Append(transform.DOLocalMoveY(floatHeight / 2f, floatDuration / 4, false)
            .SetRelative(true).SetEase(Ease.Linear));
        // 2. 아래로 전체 높이만큼 이동 (중심점 아래까지)
        sequence.Append(transform.DOLocalMoveY(-floatHeight, floatDuration / 2, false)
            .SetRelative(true).SetEase(Ease.Linear));
        // 3. 다시 위로 절반 이동하여 중심으로 복귀
        sequence.Append(transform.DOLocalMoveY(floatHeight / 2f, floatDuration / 4, false)
            .SetRelative(true).SetEase(Ease.Linear));
        
        sequence.SetLoops(-1).SetTarget(transform); // 루프 설정 및 트윈에 타겟 연결

        // 좌우 회전 효과를 추가합니다. 이 트윈은 상하 이동과 별개로 동작합니다.
        transform.DOLocalRotate(new Vector3(0, rotationAngle, 0), rotationDuration / 2)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetTarget(transform);
    }
    
    private void StartReturnToPoolTimer()
    {
        _lifeTimeCts = new CancellationTokenSource();
        ReturnToPoolAfterDelay(_lifeTimeCts.Token).Forget();
    }

    private async UniTaskVoid ReturnToPoolAfterDelay(CancellationToken token)
    {
        try
        {
            await UniTask.Delay(TimeSpan.FromSeconds(lifeTime), cancellationToken: token);

            if (objectPoolSpawner != null)
            {
                objectPoolSpawner.ReturnObject(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
        catch (OperationCanceledException)
        {
            // 타이머가 정상적으로 취소된 경우 (예: 플레이어가 아이템을 주웠을 때)
        }
    }
}
