using UnityEngine;
using InGame.Manager;
using InGame.Mob.MobBase;
using InGame.vamsir;
using Cysharp.Threading.Tasks;
using System;

namespace InGame.Player.Player_Base
{
    /// <summary>
    /// 플레이어의 물리적 충돌(적 피격, 아이템 습득 등)을 처리하는 컴포넌트입니다.
    /// </summary>
    public class PlayerCollisionHandler : MonoBehaviour
    {
        #region 내부 상태 및 캐시

        private const float k_ContactDamageInterval = 1.0f;

        private PlayerBase m_playerBase;
        private bool m_isHit = false;
        private bool m_isColliderActive = true;
        private float m_damageTickTimer = 0f;

        #endregion

        #region 이벤트

        public event Action<float> OnDamageReceived;
        public event Action<float> OnExpCollected;
        public event Action<int> OnCoinCollected;

        #endregion

        #region 초기화 및 제어

        public void Init(PlayerBase playerBase)
        {
            m_playerBase = playerBase;
            m_isHit = false;
            m_isColliderActive = true;
            m_damageTickTimer = 0f;
        }

        /// <summary>
        /// 충돌 판정 가동 여부를 설정합니다.
        /// </summary>
        public void SetColliderActive(bool active) => m_isColliderActive = active;

        #endregion

        #region Unity 라이프사이클 (충돌)

        private void OnCollisionEnter2D(Collision2D other)
        {
            if (!m_isColliderActive) return;

            if (other.gameObject.CompareTag("Mob"))
            {
                HandleMobCollision(other.gameObject);
                m_damageTickTimer = 0f;
            }
            else if (other.gameObject.CompareTag("Exp"))
            {
                HandleExpCollision(other.gameObject);
            }
            else if (other.gameObject.CompareTag("Coin"))
            {
                HandleCoinCollision(other.gameObject);
            }
        }

        private void OnCollisionStay2D(Collision2D other)
        {
            if (!m_isColliderActive || !other.gameObject.CompareTag("Mob")) return;
            
            // 지속 충돌 시 일정 인터벌마다 데미지 적용
            m_damageTickTimer += Time.fixedDeltaTime;
            if (m_damageTickTimer >= k_ContactDamageInterval)
            {
                HandleMobCollision(other.gameObject);
                m_damageTickTimer = 0f;
            }
        }

        private void OnCollisionExit2D(Collision2D other)
        {
            if (other.gameObject.CompareTag("Mob"))
            {
                m_damageTickTimer = 0f;
            }
        }

        #endregion

        #region 충돌 로직 상세

        private void HandleMobCollision(GameObject mobObject)
        {
            if (m_isHit) return;
            
            if (mobObject.TryGetComponent(out MobBase mob))
            {
                OnDamageReceived?.Invoke(mob.AttackDamage);
                // 피격 후 무적 시간 적용
                EnableHitCooldown(0.5f).Forget();
            }
        }

        private void HandleExpCollision(GameObject expObject)
        {
            if (expObject.TryGetComponent(out EXP_Obj expObj) && expObj.ObjectPoolSpawner != null)
            {
                OnExpCollected?.Invoke(expObj.ExpValue);
                expObj.ObjectPoolSpawner.ExpObjectPool.Release(expObj);
                SoundManager.PlaySound(Sound.SFX, SoundKeys.GetExp, false);
            }
        }

        private void HandleCoinCollision(GameObject coinObject)
        {
            if (coinObject.TryGetComponent(out Coin_Obj coinObj) && coinObj.ObjectPoolSpawner != null)
            {
                OnCoinCollected?.Invoke(coinObj.CoinValue);
                coinObj.ObjectPoolSpawner.CoinObjectPool.Release(coinObj);
                SoundManager.PlaySound(Sound.SFX, SoundKeys.GetCoin, false);
            }
        }

        /// <summary>
        /// 피격 후 무적 상태를 유지하기 위한 비동기 쿨다운입니다.
        /// </summary>
        private async UniTaskVoid EnableHitCooldown(float duration)
        {
            m_isHit = true;
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: this.GetCancellationTokenOnDestroy());
            }
            finally
            {
                m_isHit = false;
            }
        }

        #endregion
    }
}
