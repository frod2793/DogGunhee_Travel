using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using InGame.UI;

namespace InGame.Lobby
{
    /// <summary>
    /// [설명]: 캐릭터 선택 UI의 전반적인 상태와 시각적 요소를 제어하는 관리 클래스입니다.
    /// 캐릭터 리스트 생성, 스킨 선택, 패널 전환 및 데이터 동기화를 담당합니다.
    /// </summary>
    public class CharacterSelectUIManager : MonoBehaviour
    {
        #region 에디터 설정

        [Header("<color=green>UI 패널 설정</color>")]
        [SerializeField, Tooltip("캐릭터 선택 메인 패널"), FormerlySerializedAs("characterSelectPanel")]
        private GameObject m_characterSelectPanel;

        [SerializeField, Tooltip("캐릭터 정보 확장(스킨) 패널"), FormerlySerializedAs("characterExpendViewPanel")]
        private GameObject m_characterExpendViewPanel;

        [SerializeField, Tooltip("캐릭터 리스트 표시 패널"), FormerlySerializedAs("characterListPanel")]
        private GameObject m_characterListPanel;

        [Header("<color=green>캐릭터 인덱스 설정</color>")]
        [SerializeField, Tooltip("캐릭터 선택 항목 프리팹"), FormerlySerializedAs("characterSelectIndexPrefab")]
        private CharacterSelectIndex m_characterSelectIndexPrefab;

        [SerializeField, Tooltip("캐릭터 항목이 생성될 부모 트랜스폼"), FormerlySerializedAs("characterSelectIndexParent")]
        private Transform m_characterSelectIndexParent;

        [Header("<color=green>스킨 인덱스 설정</color>")]
        [SerializeField, Tooltip("스킨 선택 항목 프리팹"), FormerlySerializedAs("characterSkinIndexPrefab")]
        private CharacterSkinIndex m_characterSkinIndexPrefab;

        [SerializeField, Tooltip("스킨 항목이 생성될 부모 트랜스폼"), FormerlySerializedAs("characterSkinIndexParent")]
        private Transform m_characterSkinIndexParent;

        #endregion

        #region 내부 변수

        // 캐릭터 및 스킨 데이터 리스트
        private List<CharacterData> m_characterDataList = new List<CharacterData>();
        private List<CharacterSkinData> m_skinDataList = new List<CharacterSkinData>();

        // 현재 선택 상태 캐싱
        private int m_currentCharacterIndex = 0;
        private int m_currentSkinIndex = 0;

        // 동적으로 생성된 UI 요소 참조
        private List<CharacterSelectIndex> m_characterIndexItems = new List<CharacterSelectIndex>();
        private List<CharacterSkinIndex> m_skinIndexItems = new List<CharacterSkinIndex>();

        #endregion

        #region 유니티 생명주기

        /// <summary>
        /// [설명]: 컴포넌트 시작 시 데이터를 로드하고 UI를 초기화합니다.
        /// </summary>
        private void Start()
        {
            LoadCharacterData();
            InitializeCharacterUI();
        }

        #endregion

        #region 초기화 로직

        /// <summary>
        /// [설명]: 패널들의 초기 활성 상태를 설정합니다.
        /// </summary>
        private void InitializePanels()
        {
            SetGameObjectActive(m_characterSelectPanel, false);
            SetGameObjectActive(m_characterExpendViewPanel, false);
            SetGameObjectActive(m_characterListPanel, false);
        }

        /// <summary>
        /// [설명]: 외부 저장소나 매니저로부터 캐릭터 및 스킨 데이터를 불러옵니다.
        /// </summary>
        private void LoadCharacterData()
        {
            LogManager.Log("[CharacterSelectUIManager] 캐릭터 데이터 로드 시작", LogManager.LogCategory.CharacterManager);

            // 기존에 선택되었던 인덱스 정보 복원
            m_currentCharacterIndex = PlayerDataManager.Instance != null ? PlayerDataManager.Instance.SelectCharacterIndex : 0;
            m_currentSkinIndex = PlayerPrefs.GetInt("SelectedSkinIndex", 0);
        }

        /// <summary>
        /// [설명]: 전체적인 캐릭터 선택 화면의 UI 요소들을 생성하고 초기화합니다.
        /// </summary>
        private void InitializeCharacterUI()
        {
            // 1. 기존 UI 요소 정리
            ClearUIItems();

            // 2. 캐릭터 리스트 생성
            CreateCharacterSelectItems();

            // 3. 현재 캐릭터에 맞는 스킨 리스트 생성
            CreateSkinSelectItems(m_currentCharacterIndex);

            LogManager.Log(
                $"[CharacterSelectUIManager] UI 초기화 완료 (캐릭터: {m_currentCharacterIndex}, 스킨: {m_currentSkinIndex})",
                LogManager.LogCategory.CharacterManager);
        }

        /// <summary>
        /// [설명]: 동적으로 호출되었던 모든 UI 인스턴스들을 파괴하고 리스트를 비웁니다.
        /// </summary>
        private void ClearUIItems()
        {
            // 캐릭터 항목 정리
            foreach (var item in m_characterIndexItems)
            {
                if (item != null)
                {
                    Destroy(item.gameObject);
                }
            }

            m_characterIndexItems.Clear();

            // 스킨 항목 정리
            foreach (var item in m_skinIndexItems)
            {
                if (item != null)
                {
                    Destroy(item.gameObject);
                }
            }

            m_skinIndexItems.Clear();
        }

        #endregion

        #region UI 생성 및 갱신

        /// <summary>
        /// [설명]: 설정된 데이터를 기반으로 캐릭터 선택 버튼들을 생성합니다.
        /// </summary>
        private void CreateCharacterSelectItems()
        {
            if (m_characterSelectIndexPrefab == null || m_characterSelectIndexParent == null)
            {
                LogManager.LogError("[CharacterSelectUIManager] 프리팹 또는 부모 트랜스폼이 설정되지 않았습니다.",
                    LogManager.LogCategory.CharacterManager);
                return;
            }

            // 임시로 5개의 캐릭터 생성 (향후 실데이터 기반 연동 필요)
            for (int i = 0; i < 5; i++)
            {
                var characterItem = Instantiate(m_characterSelectIndexPrefab, m_characterSelectIndexParent);

                if (characterItem.CharacterName != null)
                {
                    characterItem.CharacterName.text = $"캐릭터 {i}";
                }

                int index = i;
                var button = characterItem.GetComponent<UnityEngine.UI.Button>();
                if (button != null)
                {
                    button.onClick.AddListener(() =>
                    {
                        SelectCharacter(index);
                        OpenCharacterSkinViewPanel(); // 선택 시 확장 정보창 표시
                    });
                }

                m_characterIndexItems.Add(characterItem);
            }
        }

        /// <summary>
        /// [설명]: 특정 캐릭터에 할당된 스킨 버튼들을 생성합니다.
        /// </summary>
        /// <param name="characterIndex">대상 캐릭터 인덱스</param>
        private void CreateSkinSelectItems(int characterIndex)
        {
            if (m_characterSkinIndexPrefab == null || m_characterSkinIndexParent == null)
            {
                LogManager.LogError("[CharacterSelectUIManager] 스킨 프리팹 또는 부모 트랜스폼이 설정되지 않았습니다.",
                    LogManager.LogCategory.CharacterManager);
                return;
            }

            // 임시로 3개의 스킨 생성 (향후 실데이터 기반 연동 필요)
            for (int i = 0; i < 3; i++)
            {
                var skinItem = Instantiate(m_characterSkinIndexPrefab, m_characterSkinIndexParent);

                if (skinItem.CharacterName != null)
                {
                    skinItem.CharacterName.text = $"스킨 {i}";
                }

                int index = i;
                var button = skinItem.GetComponent<UnityEngine.UI.Button>();
                if (button != null)
                {
                    button.onClick.AddListener(() => SelectSkin(index));
                }

                m_skinIndexItems.Add(skinItem);
            }
        }

        /// <summary>
        /// [설명]: 캐릭터 변경 시 연결된 스킨 UI 리스트를 새로고침합니다.
        /// </summary>
        private void UpdateSkinUI(int characterIndex)
        {
            // 기존 스킨 항목만 부분 정리
            foreach (var item in m_skinIndexItems)
            {
                if (item != null)
                {
                    Destroy(item.gameObject);
                }
            }

            m_skinIndexItems.Clear();

            CreateSkinSelectItems(characterIndex);
        }

        #endregion

        #region 선택 로직

        /// <summary>
        /// [설명]: 사용자가 선택한 캐릭터를 현재 활성 캐릭터로 설정하고 데이터를 저장합니다.
        /// </summary>
        private void SelectCharacter(int characterIndex)
        {
            m_currentCharacterIndex = characterIndex;

            if (PlayerDataManager.Instance != null)
            {
                PlayerDataManager.Instance.SelectCharacterIndex = characterIndex;
            }

            UpdateSkinUI(characterIndex);
            LogManager.Log($"[CharacterSelectUIManager] 캐릭터 선택됨: {characterIndex}",
                LogManager.LogCategory.CharacterManager);
        }

        /// <summary>
        /// [설명]: 사용자가 선택한 스킨을 저장하고 적용합니다.
        /// </summary>
        private void SelectSkin(int skinIndex)
        {
            m_currentSkinIndex = skinIndex;

            PlayerPrefs.SetInt("SelectedSkinIndex", skinIndex);
            PlayerPrefs.Save();

            LogManager.Log($"[CharacterSelectUIManager] 스킨 선택됨: {skinIndex}", LogManager.LogCategory.CharacterManager);
        }

        #endregion

        #region 패널 제어

        /// <summary>
        /// [설명]: 캐릭터 선택 메인 화면을 엽니다.
        /// </summary>
        public void OpenCharacterSelectPanel()
        {
            SetGameObjectActive(m_characterSelectPanel, true);
            if (PopupManager.Instance != null)
            {
                PopupManager.Instance.RegisterPopup(CloseCharacterSelectPanel);
            }

            InitializeCharacterUI();
        }

        /// <summary>
        /// [설명]: 캐릭터 리스트 화면을 열고 충돌할 수 있는 다른 패널을 정리합니다.
        /// </summary>
        public void OpenCharacterListPanel()
        {
            SetGameObjectActive(m_characterListPanel, true);
            SetGameObjectActive(m_characterExpendViewPanel, false);
            if (PopupManager.Instance != null)
            {
                PopupManager.Instance.RegisterPopup(CloseCharacterListPanel);
            }
        }

        /// <summary>
        /// [설명]: 캐릭터 스킬 및 정보 확장 패널을 엽니다.
        /// </summary>
        public void OpenCharacterSkinViewPanel()
        {
            SetGameObjectActive(m_characterExpendViewPanel, true);
            SetGameObjectActive(m_characterListPanel, false);
            if (PopupManager.Instance != null)
            {
                PopupManager.Instance.RegisterPopup(CloseCharacterSkinViewPanel);
            }
        }

        /// <summary>
        /// [설명]: 캐릭터 선택 메인 패널을 닫습니다.
        /// </summary>
        public void CloseCharacterSelectPanel()
        {
            SetGameObjectActive(m_characterSelectPanel, false);
        }

        /// <summary>
        /// [설명]: 캐릭터 리스트 패널을 닫습니다.
        /// </summary>
        private void CloseCharacterListPanel()
        {
            SetGameObjectActive(m_characterListPanel, false);
        }

        /// <summary>
        /// [설명]: 캐릭터 정보 확장 패널을 닫습니다.
        /// </summary>
        private void CloseCharacterSkinViewPanel()
        {
            SetGameObjectActive(m_characterExpendViewPanel, false);
        }

        #endregion

        #region 유틸리티 및 데이터 구조

        /// <summary>
        /// [설명]: 게임 오브젝트의 활성 상태를 안전하게 제어합니다.
        /// </summary>
        private static void SetGameObjectActive(GameObject obj, bool isActive)
        {
            if (obj != null)
            {
                obj.SetActive(isActive);
            }
            else
            {
                LogManager.LogWarning("[CharacterSelectUIManager] 활성화하려는 오브젝트가 null입니다.",
                    LogManager.LogCategory.CharacterManager);
            }
        }

        /// <summary>
        /// [설명]: 캐릭터의 기본 정보를 담는 구조체입니다.
        /// </summary>
        [System.Serializable]
        private struct CharacterData
        {
            public int index;
            public string name;
            public Sprite thumbnail;
            public bool isUnlocked;
        }

        /// <summary>
        /// [설명]: 캐릭터 스킨 정보를 담는 구조체입니다.
        /// </summary>
        [System.Serializable]
        private struct CharacterSkinData
        {
            public int characterIndex;
            public int skinIndex;
            public string name;
            public Sprite thumbnail;
            public bool isUnlocked;
        }

        #endregion
    }
}