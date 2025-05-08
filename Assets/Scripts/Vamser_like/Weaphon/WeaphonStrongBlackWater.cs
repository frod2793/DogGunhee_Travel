using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DogGuns_Games.vamsir
{
    public class WeaphonStrongBlackWater : Weaphon_base
    {
        #region 필드 및 변수

        private bool _isAttacking; // 중복 호출 방지 플래그
        private Collider2D _collider2D; 

        #endregion

        #region Unity 라이프사이클

        public override void OnEnable()
        {
            base.OnEnable();
            mobStunTime = 0.5f;
        }

        #endregion

        #region 무기 동작 관리

        public override void Weaphon_Idle()
        {
            base.Weaphon_Idle();
        }

        public override void Weaphon_Attack(Vector3 attackAngle)
        {
            if (_collider2D == null)
            {
                _collider2D = GetComponent<Collider2D>();
            }
               
            base.Weaphon_Attack(attackAngle);

            if (!_isAttacking)
            {
                _isAttacking = true;
                ActiveBlackWater().Forget();
            }
        }

        public override void Weaphon_Reload()
        {
            base.Weaphon_Reload();
        }

        #endregion

        #region 공격 구현

        private async UniTask ActiveBlackWater()
        {
            // 플레이어 공격 함수가 호출되는 동안 실행
            float originalAttackPower = attackPower; // 원래 공격력을 저장
  
            await UniTask.Delay(TimeSpan.FromSeconds(coolTime)); // coolTime 동안 대기
            _collider2D.enabled = true;   
            attackPower = originalAttackPower; // 원래 공격력으로 복원
            await UniTask.Delay(TimeSpan.FromSeconds(coolTime)); // coolTime 동안 대기
            _collider2D.enabled = false;
            Debug.Log("BlackWater Attack: " + attackPower);
            _isAttacking = false; // 공격 완료 후 플래그 리셋
        }

        #endregion
    }
}