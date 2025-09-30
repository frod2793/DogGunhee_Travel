using UnityEngine;

namespace DogGuns_Games.vamsir
{
    public class Player_Doggun : PlayerBase
    {
        SpriteRenderer _playerSpriteRenderer;

        private void Awake()
        {
            _playerSpriteRenderer = GetComponent<SpriteRenderer>();
        }

        public override void OnCollisionStay2D(Collision2D other)
        {
            base.OnCollisionStay2D(other);
        }

        public override void Player_attack(Vector3 attackAngle)
        {
            base.Player_attack(attackAngle);

            WeaphonBase.Weaphon_Attack(attackAngle);
            //   Debug.Log("Player_attack : " + AttackAngle);
        }

        public override void Player_Die()
        {
            base.Player_Die();
            SoundManager.PlaySound(Sound.SFX, SoundKeys.PlayerDeth, false);
        }

        public override void Player_Idle()
        {
            base.Player_Idle();
        }

        protected override void PlayHitEffect()
        {
            if (_playerSpriteRenderer == null)
            {
                _playerSpriteRenderer = GetComponent<SpriteRenderer>();
            }

            EffectManager.Instance.PlayImmediateFlashEffect(_playerSpriteRenderer);
            SoundManager.PlaySound(Sound.SFX, SoundKeys.playerHit, false);
        }
    }
}