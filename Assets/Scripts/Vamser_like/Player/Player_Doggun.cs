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

            // 피격 시 붉은색 점멸 효과
            _playerSpriteRenderer.DOKill(); // 이전 이펙트가 진행중일 수 있으므로 중지
            _playerSpriteRenderer.color = Color.white;
            DOTween.Sequence()
                .Append(_playerSpriteRenderer.DOColor(Color.red, 0.1f))
                .Append(_playerSpriteRenderer.DOColor(Color.white, 0.1f));
            
            SoundManager.PlaySound(Sound.SFX, SoundKeys.playerHit, false);
        }
    }
}