using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using InGame.Mob.MobBase;
using InGame.vamsir;
using UnityEngine;

namespace InGame.Player.Player_Base
{
    /// <summary>
    /// [설명]: 플레이어의 물리적 충돌 이벤트를 감지하고 처리하는 컴포넌트입니다.
    /// 몬스터에 의한 피격(데미지 판정 및 무적 시간 부여)과 필드 아이템(경험치 소환수, 코인 등) 습득 로직을 담당합니다.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class PlayerCollisionHandler : MonoBehaviour
    {
        #region 에디터 설정

        [Header("충돌 설정")]
        [SerializeField, Tooltip("피격 후 다시 데미지를 입을 때까지의 무적 시간 (초)")]
        private float m_invincibilityDuration = 0.5f;

        #endregion

        #region 내부 필드

        /// <summary> 현재 피격 후 무적 상태인지 여부 </summary>
        private bool m_isHit = false;

        /// <summary> 시스템적으로 충돌 판정을 수행할지 여부 </summary>
        private bool m_isColliderActive = true;

        /// <summary> 무적 시간 타이머 제어를 위한 비동기 토큰 </summary>
        private CancellationTokenSource m_hitCts;

        #endregion

        #region 이벤트

        /// <summary> [설명]: 적에게 데미지를 입었을 때 발생하며, 입은 원시 데미지 양을 전달합니다. </summary>
        public event Action<float> OnDamageReceived;

        /// <summary> [설명]: 경험치 보석을 습득했을 때 발생하며, 획득한 경험치 값을 전달합니다. </summary>
        public event Action<float> OnExpCollected;

        /// <summary> [설명]: 코인을 습득했을 때 발생하며, 획득한 코인 가치를 전달합니다. </summary>
        public event Action<int> OnCoinCollected;

        #endregion

        #region 초기화

        /// <summary>
        /// [설명]: 충돌 핸들러의 내부 상태를 초기화합니다.
        /// </summary>
        public void Init()
        {
            ResetState();
        }

        /// <summary>
        /// [설명]: 모든 가변 상태(무적 여부, 비동기 토큰 등)를 기본값으로 되돌립니다.
        /// </summary>
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
        /// [설명]: 런타임 중에 충돌 판정 가동 여부를 동적으로 설정합니다.
        /// </summary>
        /// <param name="active">활성화 여부</param>
        public void SetColliderActive(bool active)
        {
            m_isColliderActive = active;
        }

        /// <summary>
        /// [설명]: 비활성화 시 진행 중인 무적 시간 타이머를 취소합니다.
        /// </summary>
        private void OnDisable()
        {
            if (m_hitCts != null)
            {
                m_hitCts.Cancel();
                m_hitCts.Dispose();
                m_hitCts = null;
            }
        }

        #endregion

        #region 유니티 물리 이벤트

        /// <summary>
        /// [설명]: 트리거 충돌을 통해 아이템(경험치, 코인) 습득 여부를 확인합니다.
        /// </summary>
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!m_isColliderActive)
            {
                return;
            }

            if (other.CompareTag("Exp"))
            {
                HandleExpCollision(other.gameObject);
            }
            else if (other.CompareTag("Coin"))
            {
                HandleCoinCollision(other.gameObject);
            }
        }

        /// <summary>
        /// [설명]: 일반 물리 충돌을 통해 몬스터와 닿았을 때 피격 처리를 시작합니다.
        /// </summary>
        private void OnCollisionEnter2D(Collision2D other)
        {
            if (!m_isColliderActive)
            {
                return;
            }

            if (other.gameObject.CompareTag("Mob"))
            {
                HandleMobCollision(other.gameObject);
            }
        }

        /// <summary>
        /// [설명]: 몬스터와 지속적으로 접촉 중일 때 무적 상태가 해제되면 즉시 다시 데미지를 입힙니다.
        /// </summary>
        private void OnCollisionStay2D(Collision2D other)
        {
            if (!m_isColliderActive)
            {
                return;
            }

            if (!m_isHit && other.gameObject.CompareTag("Mob"))
            {
                HandleMobCollision(other.gameObject);
            }
        }

        #endregion

        #region 충돌 처리 비즈니스 로직

        /// <summary>
        /// [설명]: 몬스터와의 충돌이 유효할 경우 데미지 이벤트를 알리고 무적 타이머를 작동시킵니다.
        /// </summary>
        private void HandleMobCollision(GameObject mobObject)
        {
            if (m_isHit)
            {
                return;
            }

            if (mobObject.TryGetComponent(out MobBase mob))
            {
                OnDamageReceived?.Invoke(mob.AttackDamage);
                StartInvincibilityAsync(m_invincibilityDuration).Forget();
            }
        }

        /// <summary>
        /// [설명]: 수집한 경험치 아이템의 가치를 계산하여 시스템에 반영하고 풀로 반납합니다.
        /// </summary>
        private void HandleExpCollision(GameObject expObject)
        {
            if (expObject.TryGetComponent(out EXP_Obj expObj))
            {
                OnExpCollected?.Invoke(expObj.ExpValue);

                if (SoundManager.Instance != null)
                {
                    SoundManager.PlaySound(Sound.SFX, SoundKeys.GetExp, false);
                }

                if (expObj.ObjectPoolSpawner != null && expObj.ObjectPoolSpawner.ExpObjectPool != null)
                {
                    expObj.ObjectPoolSpawner.ExpObjectPool.Release(expObj);
                }
                else
                {
                    expObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// [설명]: 수집한 코인 아이템의 가치를 합산하고 사운드를 재생하며 풀로 반납합니다.
        /// </summary>
        private void HandleCoinCollision(GameObject coinObject)
        {
            if (coinObject.TryGetComponent(out Coin_Obj coinObj))
            {
                OnCoinCollected?.Invoke(coinObj.CoinValue);

                if (SoundManager.Instance != null)
                {
                    SoundManager.PlaySound(Sound.SFX, SoundKeys.GetCoin, false);
                }

                if (coinObj.ObjectPoolSpawner != null && coinObj.ObjectPoolSpawner.CoinObjectPool != null)
                {
                    coinObj.ObjectPoolSpawner.CoinObjectPool.Release(coinObj);
                }
                else
                {
                    coinObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// [설명]: 피격 직후 지정된 시간 동안 무적 상태를 유지하게 하는 비동기 루틴입니다.
        /// </summary>
        private async UniTaskVoid StartInvincibilityAsync(float duration)
        {
            m_isHit = true;

            if (m_hitCts != null)
            {
                m_hitCts.Cancel();
                m_hitCts.Dispose();
            }
            m_hitCts = new CancellationTokenSource();

            var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(m_hitCts.Token, this.GetCancellationTokenOnDestroy()).Token;

            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: linkedToken);
            }
            catch (OperationCanceledException)
            {
                // 타이머 중단
            }
            finally
            {
                m_isHit = false;
            }
        }

        #endregion
    }
}