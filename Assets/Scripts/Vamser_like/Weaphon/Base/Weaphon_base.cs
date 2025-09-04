using System;
using UnityEngine;
using UnityEngine.Serialization;
using Cysharp.Threading.Tasks;
namespace DogGuns_Games.vamsir
{
    public class Weaphon_base : MonoBehaviour
    {
        public float attackPower;
        public float coolTime;
        public float attackSpeed;

        public float attackRange;

        //피격 대상에게 부여할 스턴 시간
        public float mobStunTime;

        public int weaphonIndex; //현재 무기 인덱스 
        //투사체 무기인지 구분 
        public bool isShooting;
        
        public enum WeaphonState
        {
            Idle,
            Attack,
            Reload
        }

        [SerializeField] private WeaphonState weaphonState;

        public virtual void OnEnable()
        {
            SetWeaphonState(WeaphonState.Idle);
        }

        public virtual void OnDisable()
        {
            
        }

        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                // 플레이 모드에서만 SetWeaphonState 호출
                SetWeaphonState(weaphonState);
            }
        }

        public void SetWeaphonState(WeaphonState state)
        {
            switch (state)
            {
                case WeaphonState.Idle:
                    Weaphon_Idle();
                    break;
                case WeaphonState.Attack:
                    Weaphon_Attack(Vector3.zero);
                    break;
                case WeaphonState.Reload:
                    Weaphon_Reload();
                    break;
            }
        }

        public virtual void Weaphon_Idle()
        {
            // 무기가 대기 상태일 때 처리
        }

        public virtual void Weaphon_Attack(Vector3 attackAngle)
        {
            // 무기가 공격 상태일 때 처리
        }

        public virtual void Weaphon_Reload()
        {
            // 무기가 재장전 상태일 때 처리
        }
    }
}