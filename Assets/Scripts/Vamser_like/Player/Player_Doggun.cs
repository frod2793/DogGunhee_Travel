using DG.Tweening;
using UnityEngine;

namespace DogGuns_Games.vamsir
{
    public class Player_Doggun : PlayerBase
    {
        SpriteRenderer _playerSpriteRenderer;

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
        }

        public override void Player_Hit()
        {
            base.Player_Hit();
 
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

            Color originalColor = Color.white;
            _playerSpriteRenderer.DOColor(Color.red, 0.1f).OnComplete(() =>
            {
                _playerSpriteRenderer.DOColor(originalColor, 0.1f);
            });
        }
    }
}