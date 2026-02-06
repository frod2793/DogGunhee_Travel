using InGame.Player.Player_Base;
using UnityEngine;

namespace InGame.Player

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
            // 이 라인은 PlayerControll에서 모든 무기를 순회하며 공격을 호출하므로 더 이상 필요하지 않습니다.
            // WeaphonBase.Weaphon_Attack(attackAngle); 
        }

        public override void Player_Die()
        {
            base.Player_Die();
            SoundManager.PlaySound(Sound.SFX, SoundKeys.PlayerDeth, false);
        }
        
        protected override void PlayHitEffect()
        {
         //   EffectManager.Instance.PlayImmediateFlashEffect(m_playerSpriteRenderer);
            SoundManager.PlaySound(Sound.SFX, SoundKeys.playerHit, false);
        }
    }
}