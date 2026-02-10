using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using InGame.Player.Player_Base;
using InGame.Test; // PlayerBase 네임스페이스 명시

namespace InGame.Manager
{
    /// <summary>
    /// 인게임 캐릭터 및 무기 테스트를 위한 디버그/치트 패널 관리 클래스입니다.
    /// <br/> 캐릭터 교체, 무기 추가/레벨업/삭제 등의 기능을 제공합니다.
    /// </summary>
    public class TestManager : MonoBehaviour
    {
        #region 1. 에디터 설정 (Inspector)

        [Header("UI 참조 - 캐릭터")] [SerializeField, Tooltip("캐릭터 선택 드롭다운")]
        private TMP_Dropdown m_characterDropdown;

        [SerializeField, Tooltip("캐릭터 변경 버튼")] private Button m_changeCharacterButton;

        [Header("UI 참조 - 무기")] [SerializeField, Tooltip("추가할 무기 선택 드롭다운")]
        private TMP_Dropdown m_weaponDropdown;

        [SerializeField, Tooltip("무기 추가 버튼")] private Button m_addWeaponButton;

        [SerializeField, Tooltip("보유 무기 목록 컨테이너")]
        private RectTransform m_ownedWeaponsContainer;

        [SerializeField, Tooltip("보유 무기 아이템 프리팹 (TestWeaponItem)")]
        private TestWeaponItem m_ownedWeaponItemPrefab; // TestWeaponItem 클래스 존재 가정

        [Header("무기 생성 옵션")] [SerializeField, Tooltip("시작 레벨 입력 필드")]
        private TMP_InputField m_startLevelInput;

        [SerializeField, Tooltip("진화 상태로 시작 여부 토글")]
        private Toggle m_startEvolvedToggle;

        [Header("패널 애니메이션")] [SerializeField, Tooltip("패널 열기/닫기 토글 버튼")]
        private Button m_toggleButton;

        [SerializeField, Tooltip("애니메이션 대상 패널 트랜스폼")]
        private Transform m_panelTransform;

        [SerializeField, Tooltip("패널이 숨겨질 위치")]
        private Transform m_hiddenPosition;

        [SerializeField, Tooltip("패널이 보여질 위치")]
        private Transform m_shownPosition;

        [SerializeField, Tooltip("애니메이션 지속 시간")]
        private float m_animationDuration = 0.3f;

        [Header("데이터 참조")] [SerializeField, Tooltip("전체 스킬 데이터베이스")]
        private SkillDatabase m_skillDatabase;

        #endregion

        #region 2. 내부 변수 및 데이터 구조

        // 외부 참조
        private GameManager m_gameManager;

        // [추가] 에디터 및 외부 접근용 프로퍼티
        public Transform PanelTransform => m_panelTransform;
        public Transform HiddenPosition => m_hiddenPosition;
        public Transform ShownPosition => m_shownPosition;

        // 데이터 캐시
        private List<SkillData> m_allWeaponSkills;
        private readonly List<GameObject> m_spawnedWeaponItems = new List<GameObject>();
        private readonly List<CharacterInfo> m_loadedCharacters = new List<CharacterInfo>();

        // 상태 플래그
        private bool m_isPanelOpen = false;
        private bool m_isAnimating = false;

        // 내부 클래스: 캐릭터 정보
        private class CharacterInfo
        {
            public int Index;
            public string Name;
        }

        #endregion

        #region 3. 유니티 생명주기

        private void Awake()
        {
            m_gameManager = GameManager.Instance;
            if (m_gameManager == null)
            {
                Debug.LogError("[TestManager] GameManager를 찾을 수 없어 패널을 비활성화합니다.");
                gameObject.SetActive(false);
                return;
            }

            InitializeUI();
        }

        private void Start()
        {
            // 데이터 비동기 로드 시작 (Fire-and-Forget)
            InitializeDataAsync().Forget();

            // 초기 상태 설정
            InitializePanelPosition();

            // 이벤트 구독
            GameManager.OnPlayerChanged += HandlePlayerChanged;
        }

        private void OnEnable()
        {
            if (m_gameManager != null && m_gameManager.SpawnedPlayer != null)
            {
                RefreshOwnedWeaponList();
            }
        }

        private void OnDestroy()
        {
            // 이벤트 구독 해제
            GameManager.OnPlayerChanged -= HandlePlayerChanged;

            // DOTween 정리
            if (m_panelTransform != null)
            {
                m_panelTransform.DOKill();
            }
        }

        #endregion

        #region 4. 초기화 및 데이터 로드

        /// <summary>
        /// Addressables를 통해 사용 가능한 캐릭터 목록을 로드하고 드롭다운을 구성합니다.
        /// </summary>
        private async UniTaskVoid InitializeDataAsync()
        {
            if (m_skillDatabase == null)
            {
                Debug.LogWarning("[TestManager] SkillDatabase가 할당되지 않았습니다.");
                return;
            }

            // UI 잠금
            if (m_characterDropdown != null) m_characterDropdown.interactable = false;

            m_loadedCharacters.Clear();
            var characterNames = new List<string>();

            int charIndex = 0;
            while (true)
            {
                // 캐릭터 키 패턴: Player_Character_0, Player_Character_1 ...
                string key = $"Player_Character_{charIndex}";

                // 1. 리소스 위치 확인 (존재 여부 체크)
                var locationsHandle = Addressables.LoadResourceLocationsAsync(key);
                await locationsHandle;

                if (locationsHandle.Status == AsyncOperationStatus.Succeeded && locationsHandle.Result.Count > 0)
                {
                    // 2. 에셋 로드 (이름 추출용)
                    // 주의: 실제 게임에서는 메타데이터나 별도 테이블을 사용하는 것이 효율적임
                    var assetHandle = Addressables.LoadAssetAsync<GameObject>(key);
                    await assetHandle;

                    if (assetHandle.Status == AsyncOperationStatus.Succeeded && assetHandle.Result != null)
                    {
                        GameObject prefab = assetHandle.Result;
                        string charName = prefab.name.Replace("Player_Character_", ""); // 이름 파싱

                        m_loadedCharacters.Add(new CharacterInfo { Index = charIndex, Name = charName });
                        characterNames.Add($"[{charIndex}] {charName}");

                        // 사용 후 즉시 해제 (메모리 절약)
                        Addressables.Release(assetHandle);
                    }

                    Addressables.Release(locationsHandle);
                    charIndex++;
                }
                else
                {
                    // 더 이상 캐릭터가 없으면 루프 종료
                    Addressables.Release(locationsHandle);
                    break;
                }
            }

            // 캐릭터 드롭다운 갱신
            if (m_characterDropdown != null)
            {
                m_characterDropdown.ClearOptions();
                m_characterDropdown.AddOptions(characterNames);
                m_characterDropdown.interactable = true;
            }

            // 무기 목록 필터링 및 드롭다운 구성
            m_allWeaponSkills = m_skillDatabase.allSkills
                .Where(s => s.skillType == SkillType.Weapon)
                .ToList();

            if (m_weaponDropdown != null)
            {
                m_weaponDropdown.ClearOptions();
                m_weaponDropdown.AddOptions(m_allWeaponSkills.Select(s => s.skillName).ToList());
            }
        }

        private void InitializeUI()
        {
            if (m_changeCharacterButton != null)
                m_changeCharacterButton.onClick.AddListener(OnChangeCharacterClicked);

            if (m_addWeaponButton != null)
                m_addWeaponButton.onClick.AddListener(OnAddWeaponClicked);

            if (m_toggleButton != null)
                m_toggleButton.onClick.AddListener(() => TogglePanelAsync().Forget());
        }

        private void InitializePanelPosition()
        {
            if (m_panelTransform != null && m_hiddenPosition != null)
            {
                m_panelTransform.position = m_hiddenPosition.position;
                m_isPanelOpen = false;
            }
        }

        #endregion

        #region 5. UI 상호작용 및 로직

        /// <summary>
        /// 패널 열기/닫기 애니메이션을 수행합니다.
        /// </summary>
        private async UniTaskVoid TogglePanelAsync()
        {
            if (m_isAnimating || m_panelTransform == null || m_hiddenPosition == null ||
                m_shownPosition == null) return;

            m_isAnimating = true;
            m_isPanelOpen = !m_isPanelOpen;

            Vector3 targetPosition = m_isPanelOpen ? m_shownPosition.position : m_hiddenPosition.position;
            Ease ease = m_isPanelOpen ? Ease.OutCubic : Ease.InCubic;

            // DOTween 이동
            await m_panelTransform.DOMove(targetPosition, m_animationDuration)
                .SetEase(ease)
                .ToUniTask(cancellationToken: this.GetCancellationTokenOnDestroy());

            m_isAnimating = false;
        }

        private void HandlePlayerChanged(PlayerBase player)
        {
            RefreshOwnedWeaponList();
        }

        /// <summary>
        /// 현재 플레이어의 보유 무기 목록 UI를 갱신합니다.
        /// </summary>
        private void RefreshOwnedWeaponList()
        {
            // 기존 아이템 제거
            foreach (var item in m_spawnedWeaponItems)
            {
                if (item != null) Destroy(item);
            }

            m_spawnedWeaponItems.Clear();

            // 유효성 검사
            if (m_gameManager == null || m_gameManager.SpawnedPlayer == null || m_ownedWeaponItemPrefab == null) return;

            // 무기 목록 순회 및 UI 생성
            foreach (var weapon in m_gameManager.SpawnedPlayer.Weapons)
            {
                if (weapon == null) continue;

                TestWeaponItem itemInstance = Instantiate(m_ownedWeaponItemPrefab, m_ownedWeaponsContainer);

                // 아이템 설정 (콜백 연결)
                // TestWeaponItem.Setup 시그니처 가정: (IWeaponController, Action<string> onLevelUp, Action<string> onRemove)
                itemInstance.Setup(weapon, OnLevelUpWeaponClicked, OnRemoveWeaponClicked);

                m_spawnedWeaponItems.Add(itemInstance.gameObject);
            }
        }

        private void OnChangeCharacterClicked()
        {
            if (m_characterDropdown == null) return;

            int selectedIndex = m_characterDropdown.value;
            if (selectedIndex < 0 || selectedIndex >= m_loadedCharacters.Count) return;

            int charIndexToSpawn = m_loadedCharacters[selectedIndex].Index;

            // 데이터 매니저에 선택 반영
            if (PlayerDataManager.Instance != null)
            {
                PlayerDataManager.Instance.SelectCharacterIndex = charIndexToSpawn;
            }

            // 캐릭터 재생성 요청
            m_gameManager.ChangeCharacterAndWeapon_Spawn().Forget();
        }

        private void OnAddWeaponClicked()
        {
            if (m_allWeaponSkills == null || m_allWeaponSkills.Count == 0) return;
            if (m_gameManager == null || m_gameManager.SpawnedPlayer == null) return;

            int selectedIndex = m_weaponDropdown != null ? m_weaponDropdown.value : 0;
            if (selectedIndex < 0 || selectedIndex >= m_allWeaponSkills.Count) return;

            SkillData selectedSkill = m_allWeaponSkills[selectedIndex];

            // 중복 보유 체크 (GameManager나 PlayerBase에서 처리할 수도 있지만 UI 피드백을 위해 선행 체크)
            if (m_gameManager.SpawnedPlayer.Weapons.Any(w => w.SkillCode == selectedSkill.skillCode))
            {
                Debug.LogWarning($"[TestManager] 이미 보유한 무기입니다: {selectedSkill.skillName}");
                return;
            }

            // 옵션 파싱
            int startLevel = 1;
            if (m_startLevelInput != null && !string.IsNullOrEmpty(m_startLevelInput.text))
            {
                if (int.TryParse(m_startLevelInput.text, out int parsedLevel))
                {
                    startLevel = Mathf.Clamp(parsedLevel, 1, 8); // 최대 레벨 제한 (가정)
                }
            }

            bool startEvolved = m_startEvolvedToggle != null && m_startEvolvedToggle.isOn;

            // 무기 장착 요청 및 UI 갱신
            EquipWeaponAsync(selectedSkill, startLevel, startEvolved).Forget();
        }

        private async UniTaskVoid EquipWeaponAsync(SkillData skill, int level, bool evolved)
        {
            await m_gameManager.EquipNewWeapon(skill, true, level, evolved);
            RefreshOwnedWeaponList();
        }

        private void OnLevelUpWeaponClicked(string skillCode)
        {
            if (m_gameManager.SpawnedPlayer == null) return;

            var weapon = m_gameManager.SpawnedPlayer.Weapons.FirstOrDefault(w => w.SkillCode == skillCode);
            if (weapon != null)
            {
                weapon.LevelUp();
                RefreshOwnedWeaponList();
            }
        }

        private void OnRemoveWeaponClicked(string skillCode)
        {
            if (m_gameManager == null) return;

            m_gameManager.RemoveWeaponForTest(skillCode);
            RefreshOwnedWeaponList();
        }

        #endregion
    }
}