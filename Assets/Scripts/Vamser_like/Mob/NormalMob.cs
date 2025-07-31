using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace DogGuns_Games.vamsir
{
    public class NormalMob : VamserMobBase
    {
        [Header("<color=green>플레이어 무기")] 
        private Weaphon_base player_Weaphon;

        //피격 물체가 발사체인지 구분
        private bool _isHitByShoot;

        [Header("몹 스탯")]
        [SerializeField] private float initialHp = 100f;
        [SerializeField] private float initialSpeed = 1f;
        [SerializeField] private float initialAttackDamage = 10f;
        [SerializeField] private float initialAttackSpeed = 1f;
        [SerializeField] private float initialAttackRange = 1f;
        [SerializeField] private float initialStunTime = 0.1f;
        
        private void Awake()
        {
            DOTween.SetTweensCapacity(500, 50);
        }

        private void Start()
        {
            // Init() 호출을 SetTarget으로 이동하여 player 참조가 보장되도록 합니다.
        }

        private void Init()
        {
            if (player != null)
            {
                player_Weaphon = player.WeaphonBase;
                if (player_Weaphon != null)
                {
                    _isHitByShoot = player_Weaphon.isShooting;
                }
            }
            
            Mob_Hp = initialHp;
            Mob_Speed = initialSpeed;
            Mob_AttackDamage = initialAttackDamage;
            Mob_AttackSpeed = initialAttackSpeed;
            Mob_AttackRange = initialAttackRange;
            Mob_StunTime = initialStunTime;
            
            Mob_IsDie = false;
            Mob_IsHit = false;
        }

        public override void SetTarget(PlayerBase target)
        {
            base.SetTarget(target);
            Init();
        }
   
        public override void OnEnable()
        {
            base.OnEnable();
            // Init() 호출을 SetTarget으로 이동하여 player 참조가 보장되도록 합니다.
            SetMobState(MobState.Move);
        }


        private void FixedUpdate()
        {
            if (!ismove || player == null) // player가 null이면 이동 로직을 실행하지 않습니다.
            {
                if (ismove) transform.DOKill();
                return;
            }
            
            // 플레이어를 추적하는 로직
            Vector3 direction = (player.transform.position - transform.position).normalized;
            transform.position += direction * Mob_Speed * Time.fixedDeltaTime;

            // 플레이어 방향으로 몹 회전 (좌우 반전)
            if (direction.x != 0)
            {
                // direction.x > 0 이면 오른쪽을 보도록 180도 회전 (스프라이트가 왼쪽을 보고 있을 경우)
                // direction.x < 0 이면 왼쪽을 보도록 0도 회전
                float yRotation = direction.x > 0 ? 180f : 0f;
                transform.rotation = Quaternion.Euler(0, yRotation, 0);
            }
        }

        private void OnCollisionEnter2D(Collision2D other)
        {
            if (_isHitByShoot)
            {
                HandleCollision(other);
            }
        }

        private void OnCollisionStay2D(Collision2D other)
        {
            if (!_isHitByShoot)
            {
                HandleCollision(other);
            }
        }

        private void HandleCollision(Collision2D other)
        {
            if (!Mob_IsHit && other.gameObject.CompareTag("Player_Attack"))
            {
                HitCooltime(other).Forget();
                LogManager.Log("_isHitByShoot: "+_isHitByShoot,LogManager.LogCategory.NormalMob);
            }
        }

        private async UniTask HitCooltime(Collision2D other)
        {
            Mob_IsHit = true;

            float attackPower = player_Weaphon.attackPower;
            float stunTime = player_Weaphon.mobStunTime;

            await UniTask.Yield();
            Mob_Hp -= attackPower;

            if (Mob_Hp <= 0)
            {
                SetMobState(MobState.Die);
            }
            else
            {
                Mob_StunTime = stunTime;
                SetMobState(MobState.Stun);
            }

            Mob_IsHit = false;
        }

        protected override void Mob_Idle()
        {
            LogManager.Log("Idle", LogManager.LogCategory.NormalMob);
        }

        protected override void Mob_Move()
        {
            ismove = true;
        }

        protected override void Mob_Stun()
        {
            LogManager.Log("Stun", LogManager.LogCategory.NormalMob);
            ismove = false;
            DOVirtual.DelayedCall(Mob_StunTime, () => { SetMobState(MobState.Move); });
        }

        protected override void Mob_hit()
        {
            base.Mob_hit();
        }

        protected override void Mob_Attack()
        {
            LogManager.Log("Attack", LogManager.LogCategory.NormalMob);
        }

        protected override void Mob_Die()
        {
            base.Mob_Die();
            transform.DOKill();
            LogManager.Log("Die", LogManager.LogCategory.NormalMob);
        }
    }
}