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
        [Header("기본 자동화 설정")]
        [SerializeField] private bool m_autoSkillSelection = true;
        [SerializeField] private bool m_autoRestartOnDeath = false;
        [SerializeField] private bool m_forceAutoAttack = true;
        [SerializeField] private float m_checkInterval = 1.0f;

        [Header("무기 시퀀스 테스트 설정")]
        [SerializeField, Tooltip("무기 하나당 테스트 유지 시간(초) - 자동 레벨업 완료 후 대기 시간")]
        private float m_weaponTestDuration = 3.0f;
        #endregion

        #region 내부 필드
        private GameManager m_gameManager;
        private UIManager m_uiManager;
        private SkillDatabase m_skillDatabase;
        private bool m_isSequenceTesting = false;
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

            if (m_gameManager == null || m_uiManager == null)
            {
                Debug.LogWarning("[InGameAutoTester] 매니저를 찾을 수 없습니다. 인게임 씬이 맞는지 확인하세요.");
                return;
            }

            StartAutomationLoop().Forget();
        }

        private void Update()
        {
            if (m_gameManager == null || m_gameManager.SpawnedPlayer == null) return;

            // 1. 강제 자동 공격 활성화 제어
            if (m_forceAutoAttack && m_gameManager.PlayerController != null)
            {
                if (!m_gameManager.PlayerController.AutoAttackEnabledByToggle)
                {
                    m_gameManager.PlayerController.AutoAttackEnabledByToggle = true;
                }
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

        #region 자동화 로직
        private async UniTaskVoid StartAutomationLoop()
        {
            while (this != null)
            {
                try
                {
                    // 스킬 선택 팝업 자동화
                    if (m_autoSkillSelection && !m_isSequenceTesting) // 시퀀스 테스트 중에는 방해 금지
                    {
                        await CheckAndProcessSkillSelection();
                    }

                    // 사망 후 재시작 자동화
                    if (m_autoRestartOnDeath)
                    {
                        await CheckAndProcessGameOver();
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }

                await UniTask.Delay(TimeSpan.FromSeconds(m_checkInterval), cancellationToken: this.GetCancellationTokenOnDestroy());
            }
        }

        /// <summary>
        /// [설명]: 스킬 선택 팝업이 활성화되었는지 확인하고 첫 번째 스킬을 자동으로 선택합니다.
        /// </summary>
        private async UniTask CheckAndProcessSkillSelection()
        {
            var skillView = FindFirstObjectByType<InGameSkillView>();
            if (skillView == null || !skillView.gameObject.activeInHierarchy) return;

            var buttons = skillView.GetComponentsInChildren<SelectSkillBtnPrefab>(false);
            if (buttons != null && buttons.Length > 0)
            {
                Debug.Log($"[InGameAutoTester] 스킬 자동 선택 중... ({buttons.Length}개 후보)");
                var btn = buttons[0].GetComponent<Button>();
                if (btn != null && btn.interactable)
                {
                    btn.onClick.Invoke();
                    await UniTask.Delay(500);
                }
            }
        }

        /// <summary>
        /// [설명]: 게임 오버 팝업이 활성화되었는지 확인하고 재시작을 시도합니다.
        /// </summary>
        private async UniTask CheckAndProcessGameOver()
        {
            var gameOverPopup = FindFirstObjectByType<GameOverPopup>();
            if (gameOverPopup == null || !gameOverPopup.gameObject.activeInHierarchy)
            {
                await UniTask.Yield();
                return;
            }

            // [Refine]: 리플렉션을 통해 private 필드인 m_restartButton에 직접 접근 (경로 의존성 제거)
            var buttonField = typeof(GameOverPopup).GetField("m_restartButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (buttonField != null)
            {
                var restartBtn = buttonField.GetValue(gameOverPopup) as Button;
                if (restartBtn != null && restartBtn.gameObject.activeInHierarchy && restartBtn.interactable)
                {
                    Debug.Log("[InGameAutoTester] 게임 오버 감지 - 자동 재시작 버튼 클릭");
                    restartBtn.onClick.Invoke();
                    
                    // 재시작 처리 중 중복 방지를 위해 잠시 대기
                    await UniTask.Delay(2000, cancellationToken: this.GetCancellationTokenOnDestroy());
                }
            }
        }

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

            m_isSequenceTesting = true;
            Debug.Log("<color=magenta>[InGameAutoTester] 무기 정밀 테스트(Precision Log) 시작</color>");

            // 1. 테스트용 더미 소환
            GameObject dummyObj = new GameObject("Test_Precision_Dummy");
            dummyObj.transform.position = m_gameManager.SpawnedPlayer.transform.position + Vector3.right * 3f;
            
            // 기존 몬스터들의 레이어 설정 (필요 시)
            dummyObj.layer = LayerMask.NameToLayer("Enemy"); // 몬스터 레이어 할당 (Enemy로 변경)

            // 컴포넌트 추가
            var collider = dummyObj.AddComponent<CircleCollider2D>();
            collider.radius = 0.5f;
            collider.isTrigger = true;

            var renderer = dummyObj.AddComponent<SpriteRenderer>();
            // 임시로 하얀 사각형 표시
            renderer.color = Color.gray; 

            var dummy = dummyObj.AddComponent<TestDummyMob>();
            
            // 의존성 주입 (GameManager에서 서비스 조회)
            var gameStateField = m_gameManager.GetType().GetField("m_gameState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var gameState = gameStateField?.GetValue(m_gameManager) as IGameStateService;
            
            var combatCtxField = m_gameManager.GetType().GetField("m_combatContext", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var combatCtx = combatCtxField?.GetValue(m_gameManager) as ICombatContext;

            dummy.Init(null, null, null, gameState, combatCtx);

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

            // 3. 더미 제거 및 종료
            Destroy(dummyObj);
            m_isSequenceTesting = false;
            Debug.Log("<color=magenta>[InGameAutoTester] 무기 정밀 테스트 종료</color>");
        }
        #endregion
    }
}
