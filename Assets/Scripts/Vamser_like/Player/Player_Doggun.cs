using UnityEngine;

namespace DogGuns_Games.vamsir
{
    public class Player_Doggun : PlayerBase
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
            WeaphonBase.Weaphon_Attack(attackAngle);
        }

        public override void Player_Die()
        {
            base.Player_Die();
            SoundManager.PlaySound(Sound.SFX, SoundKeys.PlayerDeth, false);
        }
        
        protected override void PlayHitEffect()
        {
            EffectManager.Instance.PlayImmediateFlashEffect(m_playerSpriteRenderer);
            SoundManager.PlaySound(Sound.SFX, SoundKeys.playerHit, false);
        }
    }
}