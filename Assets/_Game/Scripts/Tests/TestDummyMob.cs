#if UNITY_EDITOR
using InGame.Core.Interfaces;
using UnityEngine;
using InGame.Mob.MobBase;
using InGame.Managers;
using InGame.Mob.Systems;
using InGame.Player.Player_Base;

namespace Tests
{
    /// <summary>
    /// [설명]: 무기 정밀 테스트를 위한 불사신 샌드백 몬스터 클래스입니다.
    /// 공격을 하지 않으며, 체력이 줄어들지 않고 피격 로그를 출력합니다.
    /// </summary>
    public class TestDummyMob : MobBase
    {
        #region 초기화
        public override void Init(MobManager mobManager, InGame.Data.PlayerDataDTO playerData = null, InGame.Services.ISoundManager soundManager = null,
            IGameStateService gameState = null, ICombatContext combatContext = null)
        {
            base.Init(mobManager, playerData, soundManager, gameState, combatContext);
            
            // 더미용 최소 로직 설정 (Null 방지)
            var stats = new MobStats(999999f, 0f, 0f, 0f, 0f, 1f);
            m_logic = new MobLogic(stats, transform.position, null, new Bounds(Vector3.zero, Vector3.one * 100f));
            
            // ✅ [추가]: 물리 감지를 위해 Collider2D가 없다면 추가 (IsTrigger 필수)
            var col = GetComponent<Collider2D>();
            if (col == null)
            {
                col = gameObject.AddComponent<CircleCollider2D>();
                ((CircleCollider2D)col).radius = 0.5f;
            }
            col.isTrigger = true;
            
            // 레이어 강제 재설정 (Enemy 레이어)
            gameObject.layer = LayerMask.NameToLayer("Enemy");

            m_currentState = MobState.Idle;
            IsDead = false;
            
            Debug.Log("<color=yellow>[TestDummyMob] 초기화 완료 (Immortal Mode + Physics Enabled)</color>");
        }
        #endregion

        #region 전투 및 피격 처리
        /// <summary>
        /// [설명]: 피격 시 데미지를 입지 않고 로그만 출력합니다.
        /// </summary>
        public override void TakeDamage(float damage, float stunTime = 0f)
        {
            // 실제 체력 감소 없이 로그 출력
            Debug.Log($"<color=orange>[Weapon Test] Hit! Damage: {damage:F2}, Stun: {stunTime:F2}</color>");
            
            // 피격 연출 (흰색 점멸) 재생
            PlayDamageEffect();
        }

        protected override void TakeDotDamage(float damage)
        {
            Debug.Log($"<color=orange>[Weapon Test] DoT Hit! Damage: {damage:F2}</color>");
            PlayDamageEffect();
        }

        public override void PlayDamageEffect(Color? color = null)
        {
            // EffectManager를 통한 점멸 연출 (주입되었을 경우)
            var effectService = m_gameState?.EffectService;
            var spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            
            if (effectService != null && spriteRenderer != null)
            {
                effectService.PlayMobHitEffect(spriteRenderer);
            }
        }

        protected override void OnDie()
        {
            // 더미는 죽지 않음
        }

        public override void SetTarget(PlayerBase target)
        {
            // 추적 대상 설정만 하고 행동(AI)은 하지 않음
            m_player = target;
        }
        #endregion
    }
}
#endif
