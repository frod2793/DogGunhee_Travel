using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DogGuns_Games.vamsir
{
    /// <summary>
    /// 플레이어 캐릭터의 기본 동작과 속성을 정의하는 기본 클래스
    /// </summary>
    public class PlayerBase : MonoBehaviour
    {
        #region 플레이어 스탯

        [Header("공격 관련 스탯")]
        public float AttackPower { get; set; }
        public float CoolTime { get; set; }
        public float AttackSpeed { get; set; }
        public float WeaponSize { get; set; }
        public float ProjectileCount { get; set; }
        public float CriticalChance { get; set; }
        public float CriticalDamage { get; set; }

        [Header("방어 및 생존 관련 스탯")]
        public float Health { get; set; }
        public float HealthRegen { get; set; }
        public float Defense { get; set; }
        public float MoveSpeed { get; set; }

        [Header("자원 획득 관련 스탯")]
        public float ExpGain { get; set; }
        public float GoldGain { get; set; }
        public float ItemGainRange { get; set; }
        public float Reroll { get; set; }

        [Header("캐릭터 정보")]
        public float Level { get; set; }
        public Vector3 AttackAngle { get; set; }
        public int characterIndex; // 현재 캐릭터 인덱스

        #endregion

        #region 플레이어 상태 관리

        /// <summary>
        /// 플레이어의 상태를 정의하는 열거형
        /// </summary>
        public enum playerState
        {
            Idle,
            Move,
            Hit,
            Attack
        }

        private playerState _playState;
        public bool ishit = false;
        public Weaphon_base WeaphonBase { get; set; }

        /// <summary>
        /// 플레이어 상태 프로퍼티 - 상태 변경시 SetPlayerState 메서드 호출
        /// </summary>
        public playerState PlayState
        {
            get => _playState;
            set
            {
                _playState = value;
                SetPlayerState(_playState);
            }
        }

        /// <summary>
        /// 플레이어의 상태에 따른 동작 분기 처리
        /// </summary>
        private void SetPlayerState(playerState state)
        {
            switch (state)
            {
                case playerState.Idle:
                    Player_Idle();
                    break;
                case playerState.Move:
                    PlayerMovement();
                    break;
                case playerState.Hit:
                    Player_Hit();
                    break;
                case playerState.Attack:
                    Player_attack(AttackAngle);
                    break;
            }
        }

        #endregion

        #region 초기화

        /// <summary>
        /// 오브젝트가 활성화될 때 호출되는 메서드
        /// </summary>
        public virtual void OnEnable()
        {
            InitializeWeapon();
        }

        /// <summary>
        /// 무기 초기화 및 위치 설정
        /// </summary>
        private void InitializeWeapon()
        {
            WeaphonBase = FindFirstObjectByType<Weaphon_base>();
            if (WeaphonBase != null)
            {
                WeaphonBase.transform.SetParent(transform);
                WeaphonBase.transform.localPosition = Vector3.zero;
            }
            else
            {
                Debug.LogWarning("무기 베이스를 찾을 수 없습니다.");
            }
        }

        #endregion

        #region 충돌 처리

        /// <summary>
        /// 플레이어와 다른 오브젝트 간의 충돌 처리
        /// </summary>
        public virtual void OnCollisionStay2D(Collision2D other)
        {
            GameObject colliderObject = other.gameObject;
            string objectTag = colliderObject.tag;

            switch (objectTag)
            {
                case "Mob":
                    HandleMobCollision(colliderObject);
                    break;
                case "Exp":
                    HandleExpCollision(colliderObject);
                    break;
                case "Coin":
                    HandleCoinCollision(colliderObject);
                    break;
            }
        }

        /// <summary>
        /// 몹과의 충돌 처리 및 피해 계산
        /// </summary>
        /// <param name="mobObject">충돌한 몹 게임오브젝트</param>
        private void HandleMobCollision(GameObject mobObject)
        {
            // 이미 피격 상태면 추가 처리 없음
            if (ishit) return;

            // 피격 상태 설정
            ishit = true;
    
            // 무적 시간 후 피격 상태 해제 (캔슬레이션 토큰 추가)
            CancellationTokenSource cts = new CancellationTokenSource();
            DelayAction(1f, () => ishit = false, cts.Token).Forget();
    
            // 피격 상태로 변경
            PlayState = playerState.Hit;
    
            // 몹으로부터 피해 계산
            VamserMobBase mob = mobObject.GetComponent<VamserMobBase>();
            if (mob != null)
            {
                float damageAmount = CalculateDamage(mob.Mob_AttackDamage);
                ApplyDamage(damageAmount);
            }
        }

        /// <summary>
        /// 방어력을 고려한 최종 피해량 계산
        /// </summary>
        private float CalculateDamage(float rawDamage)
        {
            // 방어력 공식 적용 (방어력이 높을수록 피해 감소)
            return Mathf.Max(1, rawDamage * (100 / (100 + Defense)));
        }

        /// <summary>
        /// 플레이어에게 피해 적용 및 효과 처리
        /// </summary>
        private void ApplyDamage(float damageAmount)
        {
            Health -= damageAmount;
            
            // 피해량 디버그 로그
            Debug.Log($"플레이어가 {damageAmount:F1} 데미지를 받음 (남은 체력: {Health:F1})");
    
            // 피격 효과 재생
            PlayHitEffect();
        }

        /// <summary>
        /// 경험치 아이템과의 충돌 처리
        /// </summary>
        private void HandleExpCollision(GameObject expObject)
        {
            EXP_Obj expObj = expObject.GetComponent<EXP_Obj>();
            if (expObj != null && expObj.objectPoolSpawner != null)
            {
                // 경험치 획득 처리 추가
                float expAmount = 1 * ExpGain; // 기본 경험치에 획득 보너스 적용
                Debug.Log($"경험치 {expAmount} 획득");
                
                // 오브젝트 풀로 반환
                expObj.objectPoolSpawner.ExpObjectPool.Release(expObj);
            }
        }

        /// <summary>
        /// 코인 아이템과의 충돌 처리
        /// </summary>
        private void HandleCoinCollision(GameObject coinObject)
        {
            Coin_Obj coinObj = coinObject.GetComponent<Coin_Obj>();
            if (coinObj != null && coinObj.objectPoolSpawner != null)
            {
                // 코인 획득량에 골드 획득 보너스 적용 가능
                float goldBonus = GoldGain > 0 ? GoldGain : 1;
                int coinsToAdd = Mathf.RoundToInt(1 * goldBonus);
                
                // 실제 코인 증가
                PlayerDataManagerDontdesytoy.Instance.scritpableobjPlayerData.currency1 += coinsToAdd;
                Debug.Log($"코인 {coinsToAdd}개 획득");
                
                // 오브젝트 풀로 반환
                coinObj.objectPoolSpawner.CoinObjectPool.Release(coinObj);
            }
        }

        #endregion

        #region 플레이어 액션

        /// <summary>
        /// 플레이어 공격 동작
        /// </summary>
        public virtual void Player_attack(Vector3 attackAngle)
        {
            attackAngle = this.AttackAngle;
            // 자식 클래스에서 구현
        }

        /// <summary>
        /// 플레이어 사망 처리
        /// </summary>
        public virtual void Player_Die()
        {
            Debug.Log("플레이어 사망");
            // 자식 클래스에서 구현
        }

        /// <summary>
        /// 플레이어 피격 동작
        /// </summary>
        public virtual void Player_Hit()
        {
            if (Health <= 0)
            {
                Player_Die();
            }
        }

        /// <summary>
        /// 피격 효과 재생
        /// </summary>
        protected virtual void PlayHitEffect()
        {
            // 애니메이션 효과, 사운드 효과 등 구현
            // AudioManager.Instance.PlaySound("PlayerHit");
            Debug.Log("피격 효과 재생");
        }

        /// <summary>
        /// 플레이어 대기 동작
        /// </summary>
        public virtual void Player_Idle()
        {
            // 자식 클래스에서 구현
        }
        
        /// <summary>
        /// 플레이어 이동 동작
        /// </summary>
        public virtual void PlayerMovement()
        {
            // 자식 클래스에서 구현
        }

        #endregion
        
        #region 유틸리티 메서드

        /// <summary>
        /// 지정된 시간 후에 액션을 실행합니다.
        /// </summary>
        /// <param name="delay">지연 시간(초)</param>
        /// <param name="action">실행할 액션</param>
        /// <param name="cancellationToken">취소 토큰(선택 사항)</param>
        /// <returns>UniTask</returns>
        public UniTask DelayAction(float delay, Action action, CancellationToken cancellationToken = default)
        {
            return UniTask.Delay(
                TimeSpan.FromSeconds(delay), 
                cancellationToken: cancellationToken
            ).ContinueWith(() => {
                if (!cancellationToken.IsCancellationRequested)
                {
                    action?.Invoke();
                }
            });
        }

        #endregion
    }
}