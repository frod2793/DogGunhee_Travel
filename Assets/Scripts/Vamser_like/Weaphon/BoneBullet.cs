using Cysharp.Threading.Tasks;
using DogGuns_Games.vamsir;
using UnityEngine;

public class BoneBullet : MonoBehaviour
{
    #region 필드 및 변수

    // 총알 오브젝트 풀 관리 객체
    public WeaphonBone objectPoolSpawner;

    // 공격 방향을 저장하는 벡터
    private Vector3 _attackAngle;

    // 총알의 이동 속도 총알 대미지는 WeaphonBone 에서처리
    public float bulletSpeed = 5;

    private readonly float _rotateSpeed = 1f;

    private bool _isActive;
    private readonly bool _isNecclassary = false;

    #endregion

    #region Unity 라이프사이클

    private void OnEnable()
    {
        _isActive = true;
        MoveAndRotateBullet().Forget();
    }

    private void OnCollisionEnter2D(Collision2D other)
    {
        if (other.gameObject.CompareTag("Mob"))
        {
            if (_isNecclassary)
            {
                BulletExplosion();
            }
            else if (_isActive) // 총알이 활성 상태인지 확인
            {
                _isActive = false;
                objectPoolSpawner.WeaphonBoneObjectPool.Release(this);
            }
        }
    }

    #endregion

    #region 총알 이동 및 회전

    // 총알 이동과 회전 함수 (UniTask 사용)
    private async UniTaskVoid MoveAndRotateBullet()
    {
        while (_isActive)
        {
            // 총알 이동
            transform.Translate(_attackAngle * bulletSpeed * Time.deltaTime, Space.World);

            // Z 축으로 오브젝트 회전 (이동 방향에 영향을 주지 않음)
            transform.Rotate(0, 0, 360 * Time.deltaTime * _rotateSpeed);

            CheckBounds();

            await UniTask.Yield();
        }
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
                objectPoolSpawner.WeaphonBoneObjectPool.Release(this);
            }
    }

    // 총알 발사 방향 설정 함수
    public void Thow_Bullet(Vector3 direction)
    {
        _attackAngle = direction.normalized;
    }

    private void BulletExplosion()
    {
        // 총알 폭발 이펙트 생성

        // 콜라이더 범위를 2배로 순간적으로 늘렸다가 원상 복귀 시킨다

        // 총알 오브젝트 풀로 반환
        _isActive = false; // 반환 전 비활성화
        objectPoolSpawner.WeaphonBoneObjectPool.Release(this);
    }

    #endregion
}