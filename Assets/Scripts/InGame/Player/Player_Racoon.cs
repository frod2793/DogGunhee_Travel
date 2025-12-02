using InGame.Player.Player_Base;
using UnityEngine;

namespace InGame.Player
{
    public class Player_Racoon : PlayerBase
    {
        private SpriteRenderer m_playerSpriteRenderer;

        private void Awake()
        {
            m_playerSpriteRenderer = GetComponent<SpriteRenderer>();
        }

        // 부모 클래스의 OnCollisionEnter2D를 오버라이드합니다.
        public override void OnCollisionEnter2D(Collision2D other)
        {
            base.OnCollisionEnter2D(other);
        }

        public override void Player_attack(Vector3 attackAngle)
        {
            base.Player_attack(attackAngle);
            // 이 로직은 PlayerControll에서 모든 무기를 대상으로 처리하므로 더 이상 필요하지 않습니다.
            // if (WeaphonBase != null)
            // {
            //     WeaphonBase.Weaphon_Attack(attackAngle);
            // }
        }

        public override void Player_Die()
        {
            base.Player_Die();
            // TODO: 너구리 캐릭터 전용 사망 사운드로 교체 필요
            SoundManager.PlaySound(Sound.SFX, SoundKeys.PlayerDeth, false);
        }
        
        protected override void PlayHitEffect()
        {
            // TODO: 너구리 캐릭터 전용 피격 효과 및 사운드로 교체 필요
            // EffectManager.Instance.PlayImmediateFlashEffect(m_playerSpriteRenderer);
            SoundManager.PlaySound(Sound.SFX, SoundKeys.playerHit, false);
        }
    }
}
