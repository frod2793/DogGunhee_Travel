using System.Collections.Generic;
using UnityEngine;

namespace DogGuns_Games.Lobby
{
    /// <summary>
    /// 캐릭터 선택 UI 관리 클래스
    /// </summary>
    public class CharacterSelectUIManager : MonoBehaviour
    {
        #region 필드 및 변수

        [Header("<color=green>캐릭터 선택창</color>")] 
        [SerializeField] private GameObject characterSelectPanel;
        
        [Header("<color=green>캐릭터 정보 확장창</color>")] 
        [SerializeField] private GameObject characterExpendViewPanel;
        
        [Header("<color=green>캐릭터 리스트</color>")] 
        [SerializeField] private GameObject characterListPanel;

        [Header("<color=green>캐릭터선택 관련</color>")] 
        [SerializeField] private CharactorSelectIndex characterSelectIndexPrefab;
        [SerializeField] private Transform characterSelectIndexParent;
        
        [Header("<color=green>캐릭터스킨 관련</color>")] 
        [SerializeField] private CharactorSkinIndex characterSkinIndexPrefab;
        [SerializeField] private Transform characterSkinIndexParent;

        // 캐릭터 및 스킨 데이터 캐싱
        private List<CharacterData> _characterDataList = new List<CharacterData>();
        private List<CharacterSkinData> _skinDataList = new List<CharacterSkinData>();
        
        // 현재 선택된 캐릭터/스킨 인덱스
        private int _currentCharacterIndex = 0;
        private int _currentSkinIndex = 0;
        
        // 생성된 UI 요소 참조 저장
        private List<CharactorSelectIndex> _characterIndexItems = new List<CharactorSelectIndex>();
        private List<CharactorSkinIndex> _skinIndexItems = new List<CharactorSkinIndex>();
        
        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
          //  InitializePanels();
        }

        private void Start()
        {
            LoadCharacterData();
            InitializeCharacterUI();
        }

        #endregion

        #region 초기화 메서드

        /// <summary>
        /// 패널 초기 상태 설정
        /// </summary>
        private void InitializePanels()
        {
            SetGameObjectActive(characterSelectPanel, false);
            SetGameObjectActive(characterExpendViewPanel, false);
            SetGameObjectActive(characterListPanel, false);
        }
        
        /// <summary>
        /// 캐릭터 데이터 로드
        /// </summary>
        private void LoadCharacterData()
        {
            // TODO: 캐릭터 및 스킨 데이터 로드 구현
            // 예: ScriptableObject, PlayerPrefs, 서버 등에서 불러오기
            Debug.Log("캐릭터 데이터 로드");
            
            // 이전에 선택된 캐릭터/스킨 인덱스 복원
            _currentCharacterIndex = PlayerDataManagerDontdesytoy.Instance?.SelectCharacterIndex ?? 0;
            _currentSkinIndex = PlayerPrefs.GetInt("SelectedSkinIndex", 0);
        }
        
        /// <summary>
        /// 캐릭터 선택 UI 초기화
        /// </summary>
        private void InitializeCharacterUI()
        {
            // 기존 UI 요소 정리
            ClearUIItems();
            
            // 캐릭터 선택 UI 생성
            CreateCharacterSelectItems();
            
            // 스킨 선택 UI 생성
            CreateSkinSelectItems(_currentCharacterIndex);
            
            Debug.Log($"캐릭터 UI 초기화 완료: 선택된 캐릭터 {_currentCharacterIndex}, 선택된 스킨 {_currentSkinIndex}");
        }
        
        /// <summary>
        /// UI 요소 정리
        /// </summary>
        private void ClearUIItems()
        {
            // 생성된 캐릭터 선택 UI 항목 제거
            foreach (var item in _characterIndexItems)
            {
                if (item != null) Destroy(item.gameObject);
            }
            _characterIndexItems.Clear();
            
            // 생성된 스킨 선택 UI 항목 제거
            foreach (var item in _skinIndexItems)
            {
                if (item != null) Destroy(item.gameObject);
            }
            _skinIndexItems.Clear();
        }
        
        /// <summary>
        /// 캐릭터 선택 항목 생성
        /// </summary>
        private void CreateCharacterSelectItems()
        {
            if (characterSelectIndexPrefab == null || characterSelectIndexParent == null)
            {
                Debug.LogError("캐릭터 선택 프리팹 또는 부모 트랜스폼이 설정되지 않았습니다.");
                return;
            }

            for (int i = 0; i < 5; i++)
            {
                var characterItem = Instantiate(characterSelectIndexPrefab, characterSelectIndexParent);
                if (characterItem.charactorName != null)
                {
                    characterItem.charactorName.text = $"캐릭터 {i}";
                }

                // 클릭 이벤트 설정
                int index = i; // 클로저를 위한 인덱스 변수
                var button = characterItem.GetComponent<UnityEngine.UI.Button>();
                if (button != null)
                {
                    button.onClick.AddListener(() => {
                        SelectCharacter(index);
                        OpenCharacterSkinViewPanel(); // 캐릭터 정보 확장창 열기
                    });
                }

                _characterIndexItems.Add(characterItem);
            }
        }
        
        /// <summary>
        /// 스킨 선택 항목 생성
        /// </summary>
        private void CreateSkinSelectItems(int characterIndex)
        {
            if (characterSkinIndexPrefab == null || characterSkinIndexParent == null)
            {
                Debug.LogError("스킨 선택 프리팹 또는 부모 트랜스폼이 설정되지 않았습니다.");
                return;
            }
            
            // TODO: 선택된 캐릭터의 스킨 데이터 기반으로 UI 생성
            // 예시: 테스트용 더미 데이터로 구현
            for (int i = 0; i < 3; i++)
            {
                var skinItem = Instantiate(characterSkinIndexPrefab, characterSkinIndexParent);
                if (skinItem.charactorName != null)
                {
                    skinItem.charactorName.text = $"스킨 {i}";
                }
                
                // 클릭 이벤트 설정
                int index = i; // 클로저를 위한 인덱스 변수
                var button = skinItem.GetComponent<UnityEngine.UI.Button>();
                if (button != null)
                {
                    button.onClick.AddListener(() => SelectSkin(index));
                }
                
                _skinIndexItems.Add(skinItem);
            }
        }

        #endregion

        #region 캐릭터 및 스킨 선택 메서드
        
        /// <summary>
        /// 캐릭터 선택 처리
        /// </summary>
        private void SelectCharacter(int characterIndex)
        {
            _currentCharacterIndex = characterIndex;
            
            // PlayerData에 선택한 캐릭터 인덱스 저장
            if (PlayerDataManagerDontdesytoy.Instance != null)
            {
                PlayerDataManagerDontdesytoy.Instance.SelectCharacterIndex = characterIndex;
            }
            
            // 스킨 UI 업데이트
            UpdateSkinUI(characterIndex);
            
            Debug.Log($"캐릭터 선택: {characterIndex}");
        }
        
        /// <summary>
        /// 스킨 선택 처리
        /// </summary>
        private void SelectSkin(int skinIndex)
        {
            _currentSkinIndex = skinIndex;
            
            // 선택한 스킨 인덱스 저장
            PlayerPrefs.SetInt("SelectedSkinIndex", skinIndex);
            PlayerPrefs.Save();
            
            Debug.Log($"스킨 선택: {skinIndex}");
        }
        
        /// <summary>
        /// 선택된 캐릭터에 따라 스킨 UI 업데이트
        /// </summary>
        private void UpdateSkinUI(int characterIndex)
        {
            // 기존 스킨 UI 항목 제거
            foreach (var item in _skinIndexItems)
            {
                if (item != null) Destroy(item.gameObject);
            }
            _skinIndexItems.Clear();
            
            // 선택된 캐릭터의 스킨 UI 다시 생성
            CreateSkinSelectItems(characterIndex);
        }
        
        #endregion

        #region UI 패널 제어 - 공개 메서드

        /// <summary>
        /// 캐릭터 선택 패널 열기
        /// </summary>
        public void OpenCharacterSelectPanel()
        {
            SetGameObjectActive(characterSelectPanel, true);
            LobbyUIManager.AddClosePopUpAction(CloseCharacterSelectPanel);
            
            // UI 초기화 - 최신 데이터로 리프레시
            InitializeCharacterUI();
        }

        /// <summary>
        /// 캐릭터 목록 패널 열기
        /// </summary>
        public void OpenCharacterListPanel()
        {
            SetGameObjectActive(characterListPanel, true);
            SetGameObjectActive(characterExpendViewPanel, false);
            LobbyUIManager.AddClosePopUpAction(CloseCharacterListPanel);
        }

        /// <summary>
        /// 캐릭터 스킬 보기 패널 열기
        /// </summary>
        public void OpenCharacterSkinViewPanel()
        {
            SetGameObjectActive(characterExpendViewPanel, true);
            SetGameObjectActive(characterListPanel, false);
            LobbyUIManager.AddClosePopUpAction(CloseCharacterSkinViewPanel);
        }

        /// <summary>
        /// 캐릭터 선택 패널 닫기
        /// </summary>
        public void CloseCharacterSelectPanel()
        {
            SetGameObjectActive(characterSelectPanel, false);
        }

        /// <summary>
        /// 캐릭터 목록 패널 닫기
        /// </summary>
        private void CloseCharacterListPanel()
        {
            SetGameObjectActive(characterListPanel, false);
        }
        
        /// <summary>
        /// 캐릭터 스킬 보기 패널 닫기
        /// </summary>
        private void CloseCharacterSkinViewPanel()
        {
            SetGameObjectActive(characterExpendViewPanel, false);
        }

        #endregion

        #region 유틸리티 메서드

        /// <summary>
        /// 게임 오브젝트 활성화/비활성화 처리
        /// </summary>
        /// <param name="obj">대상 게임 오브젝트</param>
        /// <param name="isActive">활성화 여부</param>
        private static void SetGameObjectActive(GameObject obj, bool isActive = false)
        {
            if (obj != null)
                obj.SetActive(isActive);
            else
                Debug.LogWarning("활성화하려는 게임 오브젝트가 null입니다.");
        }

        #endregion
        
        #region 데이터 클래스
        
        /// <summary>
        /// 캐릭터 데이터 구조체
        /// </summary>
        [System.Serializable]
        private struct CharacterData
        {
            public int index;         // 캐릭터 번호
            public string name;       // 캐릭터 이름
            public Sprite thumbnail;  // 캐릭터 썸네일
            public bool isUnlocked;   // 잠금 해제 여부
        }
        
        /// <summary>
        /// 캐릭터 스킨 데이터 구조체
        /// </summary>
        [System.Serializable]
        private struct CharacterSkinData
        {
            public int characterIndex; // 캐릭터 번호
            public int skinIndex;      // 스킨 번호
            public string name;        // 스킨 이름
            public Sprite thumbnail;   // 스킨 썸네일
            public bool isUnlocked;    // 잠금 해제 여부
        }
        
        #endregion
    }
}