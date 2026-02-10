using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using InGame.Mob.MobBase;
using InGame.vamsir;
using UnityEngine;

namespace InGame.Player.Player_Base
{
    /// <summary>
    /// 플레이어의 물리적 충돌 이벤트를 처리하는 컴포넌트입니다.
    /// <br/> 몬스터 피격(데미지 및 무적 시간 관리)과 아이템(경험치, 코인) 습득을 담당합니다.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class PlayerCollisionHandler : MonoBehaviour
    {
        #region 1. 에디터 설정 (Inspector)

        [Header("충돌 설정")]
        [SerializeField, Tooltip("피격 후 무적 시간 (초)")] 
        private float m_invincibilityDuration = 0.5f;

        #endregion

        #region 2. 내부 상태 및 캐시


        // 상태 플래그
        private bool m_isHit = false;          // 현재 피격(무적) 상태인지
        private bool m_isColliderActive = true; // 충돌 처리 활성화 여부
        
        // 비동기 제어
        private CancellationTokenSource m_hitCts;

        #endregion

        #region 3. 이벤트 (Events)

        /// <summary>데미지를 입었을 때 발생 (데미지 양 전달)</summary>
        public event Action<float> OnDamageReceived;

        /// <summary>경험치 아이템 습득 시 발생 (경험치 양 전달)</summary>
        public event Action<float> OnExpCollected;

        /// <summary>코인 아이템 습득 시 발생 (코인 양 전달)</summary>
        public event Action<int> OnCoinCollected;

        #endregion

        #region 4. 초기화 및 제어

        public void Init()
        {
            ResetState();
        }

        public void ResetState()
        {
            m_isHit = false;
            m_isColliderActive = true;
            
            if (m_hitCts != null)
            {
                m_hitCts.Cancel();
                m_hitCts.Dispose();
                m_hitCts = null;
            }
        }

        /// <summary>
        /// 충돌 판정 가동 여부를 설정합니다. (연출 중 무적 처리 등)
        /// </summary>
        public void SetColliderActive(bool active)
        {
            m_isColliderActive = active;
        }

        private void OnDisable()
        {
            // 비활성화 시 비동기 작업 취소
            if (m_hitCts != null)
            {
                m_hitCts.Cancel();
                m_hitCts.Dispose();
                m_hitCts = null;
            }
        }

        #endregion

        #region 5. Unity 물리 이벤트 (Physics Events)

        // 아이템 습득 (Trigger)
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!m_isColliderActive) return;

            if (other.CompareTag("Exp"))
            {
                HandleExpCollision(other.gameObject);
            }
            else if (other.CompareTag("Coin"))
            {
                HandleCoinCollision(other.gameObject);
            }
        }

        // 몬스터 충돌 (Collision - 진입)
        private void OnCollisionEnter2D(Collision2D other)
        {
            if (!m_isColliderActive) return;

            if (other.gameObject.CompareTag("Mob"))
            {
                HandleMobCollision(other.gameObject);
            }
        }

        // 몬스터 충돌 (Collision - 유지)
        private void OnCollisionStay2D(Collision2D other)
        {
            if (!m_isColliderActive) return;

            // 무적 시간이 끝났는데도 계속 몬스터와 닿아있다면 다시 데미지 처리
            if (!m_isHit && other.gameObject.CompareTag("Mob"))
            {
                HandleMobCollision(other.gameObject);
            }
        }

        #endregion

        #region 6. 충돌 처리 로직 (Logic)

        /// <summary>
        /// 몬스터와 충돌했을 때의 로직입니다. 데미지를 입히고 무적 시간을 부여합니다.
        /// </summary>
        private void HandleMobCollision(GameObject mobObject)
        {
            // 이미 피격 상태(무적)라면 무시
            if (m_isHit) return;

            if (mobObject.TryGetComponent(out MobBase mob))
            {
                // 데미지 이벤트 발생
                OnDamageReceived?.Invoke(mob.AttackDamage);
                
                // 무적 시간 시작
                StartInvincibilityAsync(m_invincibilityDuration).Forget();
            }
        }

        /// <summary>
        /// 경험치 아이템 충돌 처리
        /// </summary>
        private void HandleExpCollision(GameObject expObject)
        {
            if (expObject.TryGetComponent(out EXP_Obj expObj))
            {
                OnExpCollected?.Invoke(expObj.ExpValue);
                
                // 사운드 재생
                if (SoundManager.Instance != null)
                {
                    SoundManager.PlaySound(Sound.SFX, SoundKeys.GetExp, false);
                }

                // 풀 반환
                if (expObj.ObjectPoolSpawner != null && expObj.ObjectPoolSpawner.ExpObjectPool != null)
                {
                    expObj.ObjectPoolSpawner.ExpObjectPool.Release(expObj);
                }
                else
                {
                    expObject.SetActive(false); // 예외 처리
                }
            }
        }

        /// <summary>
        /// 코인 아이템 충돌 처리
        /// </summary>
        private void HandleCoinCollision(GameObject coinObject)
        {
            if (coinObject.TryGetComponent(out Coin_Obj coinObj))
            {
                OnCoinCollected?.Invoke(coinObj.CoinValue);

                // 사운드 재생
                if (SoundManager.Instance != null)
                {
                    SoundManager.PlaySound(Sound.SFX, SoundKeys.GetCoin, false);
                }

                // 풀 반환
                if (coinObj.ObjectPoolSpawner != null && coinObj.ObjectPoolSpawner.CoinObjectPool != null)
                {
                    coinObj.ObjectPoolSpawner.CoinObjectPool.Release(coinObj);
                }
                else
                {
                    coinObject.SetActive(false); // 예외 처리
                }
            }
        }

        /// <summary>
        /// 피격 후 지정된 시간 동안 무적 상태를 유지합니다.
        /// </summary>
        private async UniTaskVoid StartInvincibilityAsync(float duration)
        {
            m_isHit = true;
            
            // 기존 토큰 정리 후 재생성
            if (m_hitCts != null)
            {
                m_hitCts.Cancel();
                m_hitCts.Dispose();
            }
            m_hitCts = new CancellationTokenSource();
            
            // 파괴 시 토큰과 결합하여 안전성 확보
            var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(m_hitCts.Token, this.GetCancellationTokenOnDestroy()).Token;

            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: linkedToken);
            }
            catch (OperationCanceledException)
            {
                // 취소됨 (오브젝트 파괴 등)
            }
            finally
            {
                // 시간이 지나거나 취소되어도 무적 상태 해제 (다음 충돌을 위해)
                // 단, 오브젝트가 파괴된 경우에는 의미 없음
                m_isHit = false;
            }
        }

        #endregion
    }
}