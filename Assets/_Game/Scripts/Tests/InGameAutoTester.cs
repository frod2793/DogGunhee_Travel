#if UNITY_EDITOR
using Tests;
using InGame.ObjectPool;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using InGame;
using InGame.Core.Interfaces;
using InGame.Managers;
using InGame.Player.Player_Base;
using InGame.UI.Views;
using InGame.vamsir;
using InGame.Weapon.Base;
using InGame.Services;
using TMPro;

namespace Tests
{
    /// <summary>
    /// [설명]: 인게임 전투 및 UI 흐름을 자동화하여 테스트하는 컴포넌트입니다.
    /// 스킬 선택, 사망/재시작 루프, 그리고 모든 무기에 대한 순차적 기능 테스트를 수행합니다.
    /// </summary>
    public class InGameAutoTester : MonoBehaviour
    {
        #region 에디터 설정
        [Header("테스트 모드 설정")]
        [SerializeField, Tooltip("테스트 모드 활성화 여부")] private bool m_isTestMode = false;
        [SerializeField, Tooltip("테스트용 무적 더미 프리팹")] private TestDummyMob m_dummyPrefab;
        [SerializeField, Tooltip("체크 간격")] private float m_checkInterval = 1.0f;

        [Header("무기 시퀀스 테스트 설정")]
        [SerializeField, Tooltip("무기 하나당 테스트 유지 시간(초) - 자동 레벨업 완료 후 대기 시간")]
        private float m_weaponTestDuration = 3.0f;

        [Header("경제 테스트 설정")]
        [SerializeField, Tooltip("추가할 코인 기본 수량")]
        private int m_defaultAddCoinAmount = 1000;
        #endregion

        #region 내부 필드
        private GameManager m_gameManager;
        private UIManager m_uiManager;
        private SkillDatabase m_skillDatabase;
        private bool m_isSequenceTesting = false;
        private IPlayerDataService m_playerDataService;
        private TestDummyMob m_spawnedDummy;
        #endregion

        #region 유니티 생명주기
        private void Start()
        {
            m_gameManager = FindFirstObjectByType<GameManager>();
            m_uiManager = FindFirstObjectByType<UIManager>();
            
            // SkillDatabase는 GameManager에서 가져오거나 리소스 로드 시도
            if (m_gameManager != null)
            {
                // GameManager 내부 필드 접근을 위해 캐스팅 시도 (필요 시)
                var field = m_gameManager.GetType().GetField("m_skillDatabase", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null) m_skillDatabase = field.GetValue(m_gameManager) as SkillDatabase;
            }

            // ✅ 수정: GameManager 내부 PlayerDataService에 리플렉션으로 접근
            if (m_gameManager != null)
            {
                var playerServiceField = m_gameManager.GetType().GetField("m_playerService", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (playerServiceField != null)
                {
                    m_playerDataService = playerServiceField.GetValue(m_gameManager) as IPlayerDataService;
                }

                if (m_playerDataService == null)
                {
                    Debug.LogWarning("[InGameAutoTester] GameManager의 PlayerDataService를 찾을 수 없습니다.");
                }
            }

            if (m_gameManager == null || m_uiManager == null)
            {
                Debug.LogWarning("[InGameAutoTester] 매니저를 찾을 수 없습니다. 인게임 씬이 맞는지 확인하세요.");
                return;
            }

            // 초기 테스트 모드 상태 적용
            if (m_isTestMode)
            {
                ToggleTestMode(true).Forget();
            }
        }

        private void Update()
        {
            // [삭제]: 자동 공격 및 자동 선택은 게임 기본 기능이므로 여기서 강제하지 않음
        }

        /// <summary> [설명]: 테스트 모드 상태를 전환합니다. </summary>
        public async UniTask ToggleTestMode(bool active)
        {
            m_isTestMode = active;

            if (m_gameManager == null) return;

            // 1. 웨이브 시스템 제어
            m_gameManager.SetWaveSystemPause(active);

            if (active)
            {
                // 2. 기존 몬스터 제거
                var spawnerField = m_gameManager.GetType().GetField("m_objectPoolSpawner", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var spawner = spawnerField?.GetValue(m_gameManager) as InGame.ObjectPool.ObjectPoolSpawner;
                if (spawner != null)
                {
                    spawner.ReturnAllMobsForTest();
                }

                // 3. 무적 더미 소환
                await SpawnTestDummy();
            }
            else
            {
                // 4. 더미 제거
                RemoveTestDummy();
            }

            Debug.Log($"<color=yellow>[InGameAutoTester] 테스트 모드 {(active ? "활성화" : "비활성화")}</color>");
        }

        private async UniTask SpawnTestDummy()
        {
            if (m_dummyPrefab == null || m_gameManager == null || m_gameManager.SpawnedPlayer == null) return;

            RemoveTestDummy();

            Vector3 spawnPos = m_gameManager.SpawnedPlayer.transform.position + Vector3.right * 3f;
            m_spawnedDummy = Instantiate(m_dummyPrefab, spawnPos, Quaternion.identity);
            
            // 더미 초기화
            var gameStateField = m_gameManager.GetType().GetField("m_state", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var gameState = gameStateField?.GetValue(m_gameManager) as IGameStateService;
            
            var combatCtxField = m_gameManager.GetType().GetField("m_combatContext", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var combatCtx = combatCtxField?.GetValue(m_gameManager) as ICombatContext;

            var mobManagerField = m_gameManager.GetType().GetField("m_mobManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var mobManager = mobManagerField?.GetValue(m_gameManager) as InGame.Mob.Systems.MobManager;

            m_spawnedDummy.Init(mobManager, null, null, gameState, combatCtx);
            
            if (mobManager != null)
            {
                mobManager.Register(m_spawnedDummy);
            }
            m_spawnedDummy.SetTarget(m_gameManager.SpawnedPlayer);
            
            Debug.Log("[InGameAutoTester] 테스트용 무적 더미가 소환되었습니다.");
        }

        private void RemoveTestDummy()
        {
            if (m_spawnedDummy != null)
            {
                Destroy(m_spawnedDummy.gameObject);
                m_spawnedDummy = null;
            }
        }

        public void TriggerLevelUp()
        {
            if (m_gameManager != null && m_gameManager.SpawnedPlayer != null)
            {
                Debug.Log("[InGameAutoTester] 강제 레벨업 트리거 (Inspector)");
                m_gameManager.SpawnedPlayer.Debug_GainExp(m_gameManager.SpawnedPlayer.MaxExp);
            }
        }

        public void TriggerDeath()
        {
            if (m_gameManager != null && m_gameManager.SpawnedPlayer != null)
            {
                Debug.Log("[InGameAutoTester] 강제 사망 트리거 (Inspector)");
                m_gameManager.SpawnedPlayer.Debug_TakeDamage(m_gameManager.SpawnedPlayer.MaxHealth + 100);
            }
        }

        public void TriggerSequenceTest()
        {
            if (!m_isSequenceTesting)
            {
                RunWeaponSequenceTest().Forget();
            }
            else
            {
                Debug.LogWarning("[InGameAutoTester] 이미 무기 시퀀스 테스트가 진행 중입니다.");
            }
        }

        public void TriggerPrecisionTest()
        {
            if (!m_isSequenceTesting)
            {
                RunWeaponPrecisionTest().Forget();
            }
            else
            {
                Debug.LogWarning("[InGameAutoTester] 이미 테스트가 진행 중입니다.");
            }
        }
        #endregion


        /// <summary>
        /// [설명]: 데이터베이스의 모든 무기를 순차적으로 장착, 만렙 달성, 제거하며 테스트합니다.
        /// </summary>
        private async UniTaskVoid RunWeaponSequenceTest()
        {
            if (m_skillDatabase == null || m_gameManager == null)
            {
                Debug.LogError("[InGameAutoTester] 무기 시퀀스 테스트를 수행할 수 없습니다 (Database/Manager 누락).");
                return;
            }

            // 0. 테스트 모드 강제 활성화
            if (!m_isTestMode)
            {
                await ToggleTestMode(true);
            }

            m_isSequenceTesting = true;
            Debug.Log("<color=cyan>[InGameAutoTester] 전수 무기 시퀀스 테스트 시작</color>");

            var weaponSkills = m_skillDatabase.allSkills.Where(s => s.skillType == SkillType.Weapon).ToList();
            int total = weaponSkills.Count;

            for (int i = 0; i < total; i++)
            {
                var skill = weaponSkills[i];
                Debug.Log($"<color=white>[Sequence Test {i+1}/{total}]</color> 무기 테스트 중: <b>{skill.skillName}</b>");

                // 1. 기존 무기 제거 (테스트 격리를 위해)
                if (m_gameManager.SpawnedPlayer != null)
                {
                    var currentWeapons = m_gameManager.SpawnedPlayer.Weapons.ToList();
                    foreach (var w in currentWeapons)
                    {
                        m_gameManager.RemoveWeaponForTest(w.SkillCode);
                    }
                }

                // 2. 무기 장착 (Lv.1)
                await m_gameManager.EquipNewWeapon(skill, true, 1, false);
                await UniTask.Delay(1000); // 장착 연출 대기

                // 3. 만렙 달성 (Lv.8)
                var controller = m_gameManager.SpawnedPlayer?.Weapons.FirstOrDefault(w => w.SkillCode == skill.skillCode);
                if (controller != null)
                {
                    int maxLv = controller.MaxLevel;
                    Debug.Log($"[Sequence Test] {skill.skillName} 만렙(Lv.{maxLv}) 달성 시도");
                    for (int lv = 1; lv < maxLv; lv++)
                    {
                        controller.LevelUp();
                        await UniTask.Yield();
                    }
                }

                // 4. 동작 관찰을 위한 대기
                await UniTask.Delay(TimeSpan.FromSeconds(m_weaponTestDuration), cancellationToken: this.GetCancellationTokenOnDestroy());

                Debug.Log($"<color=green>[Sequence Test] {skill.skillName} 테스트 완료</color>");
            }

            m_isSequenceTesting = false;
            Debug.Log("<color=cyan>[InGameAutoTester] 모든 무기 시퀀스 테스트 종료</color>");
        }

        /// <summary>
        /// [설명]: 지정된 무기를 제외한 모든 무기를 대상으로 정밀 테스트(더미 타격 로그)를 수행합니다.
        /// </summary>
        private async UniTaskVoid RunWeaponPrecisionTest()
        {
            if (m_skillDatabase == null || m_gameManager == null)
            {
                Debug.LogError("[InGameAutoTester] 정밀 테스트를 수행할 수 없습니다.");
                return;
            }

            // 0. 테스트 모드 강제 활성화
            if (!m_isTestMode)
            {
                await ToggleTestMode(true);
            }

            m_isSequenceTesting = true;
            Debug.Log("<color=magenta>[InGameAutoTester] 무기 정밀 테스트(Precision Log) 시작</color>");

            // 1. 이미 테스트 모드에서 소환된 더미가 있으므로 별도 소환 불필요 (필요 시 위치만 조정)
            if (m_spawnedDummy != null)
            {
                m_spawnedDummy.transform.position = m_gameManager.SpawnedPlayer.transform.position + Vector3.right * 3f;
            }
            else
            {
                await SpawnTestDummy();
            }

            // 2. 무기 리스트 필터링 (잉크, 꼬순내 제외)
            var excludeCodes = new HashSet<string> { "WP_INK", "WP_SMELL" };
            var targetSkills = m_skillDatabase.allSkills
                .Where(s => s.skillType == SkillType.Weapon && !excludeCodes.Contains(s.skillCode))
                .ToList();

            foreach (var skill in targetSkills)
            {
                Debug.Log($"<color=white>[Precision Test]</color> 테스트 무기: <b>{skill.skillName}</b>");

                // 기존 무기 제거
                var currentWeapons = m_gameManager.SpawnedPlayer.Weapons.ToList();
                foreach (var w in currentWeapons) m_gameManager.RemoveWeaponForTest(w.SkillCode);

                // 무기 장착 (최고 레벨 테스트)
                await m_gameManager.EquipNewWeapon(skill, true, 8, false);
                
                // 관찰 대기
                await UniTask.Delay(TimeSpan.FromSeconds(m_weaponTestDuration), cancellationToken: this.GetCancellationTokenOnDestroy());
            }

            m_isSequenceTesting = false;
            Debug.Log("<color=magenta>[InGameAutoTester] 무기 정밀 테스트 종료</color>");
        }

        #region 공개 API (Unity Inspector에서 호출 가능)
        /// <summary>
        /// [설명]: 현재 플레이어에게 코인을 추가합니다.
        /// </summary>
        /// <param name="amount">추가할 코인 수</param>
        public void AddPlayerCoin(int amount)
        {
            if (m_playerDataService == null)
            {
                Debug.LogError("[InGameAutoTester] 코인 추가 실패: IPlayerDataService가 없습니다.");
                return;
            }

            if (amount <= 0)
            {
                Debug.LogWarning("[InGameAutoTester] 코인 추가 금액은 0보다 커야 합니다.");
                return;
            }

            m_playerDataService.AddCurrency("ingameCoin", amount);
            Debug.Log($"[InGameAutoTester] 코인 {amount}개 추가 완료. 현재 코인: {m_playerDataService.Data.IngameCoin}");
        }

        /// <summary>
        /// [설명]: 현재 플레이어의 코인을 확인합니다.
        /// </summary>
        public void CheckPlayerCoin()
        {
            if (m_playerDataService == null)
            {
                Debug.LogError("[InGameAutoTester] 코인 확인 실패: IPlayerDataService가 없습니다.");
                return;
            }

            Debug.Log($"[InGameAutoTester] 현재 코인: {m_playerDataService.Data.IngameCoin}");
        }
        #endregion
    }
}
#endif