using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace InGame
{
    public class SceneLoader : MonoBehaviour
    {
        // ... (상단 상수 및 필드 부분은 기존과 동일) ...
        #region 상수 및 정적 필드

        public const string INTRO_SCENE = "IntroScene";
        public const string LOBBY_SCENE = "LobbyScene";
        public const string RUN_GAME_SCENE = "RunGame";
        public const string VAMSER_LIKE_SCENE = "VamSerlike";

        private static readonly int AnimHash_OnFinish = Animator.StringToHash("onFinish");

        protected static SceneLoader m_instance;

        public static SceneLoader Instance
        {
            get
            {
                if (m_instance == null)
                {
                    m_instance = FindFirstObjectByType<SceneLoader>();
                    if (m_instance == null)
                    {
                        m_instance = Create();
                    }
                }
                return m_instance;
            }
            private set => m_instance = value;
        }

        #endregion

        #region 인스펙터 연결 필드

        [Header("UI References")]
        [Tooltip("로딩 화면을 가릴 캔버스 그룹")]
        [FormerlySerializedAs("sceneLoadferCanvasGroup")]
        [SerializeField] private CanvasGroup m_canvasGroup; 

        [Tooltip("로딩 진행률 슬라이더")]
        [FormerlySerializedAs("progressbar")]
        [SerializeField] private Slider m_progressBar;
        
        [Header("Animation")]
        [Tooltip("로딩 애니메이션을 제어할 애니메이터")]
        [SerializeField] private Animator m_animator;

        [Header("Scene References")]
        [FormerlySerializedAs("sceneReferences")]
        [SerializeField] private SceneReference[] m_sceneReferences;

        #endregion

        // ... (내부 클래스 및 초기화 영역 동일) ...
        #region 내부 클래스

        [System.Serializable]
        public class SceneReference
        {
            [FormerlySerializedAs("sceneName")]
            public string SceneName;
            
            #if UNITY_EDITOR
            [FormerlySerializedAs("sceneAsset")]
            [SerializeField] private UnityEditor.SceneAsset m_sceneAsset;
            
            public UnityEditor.SceneAsset SceneAsset
            {
                get => m_sceneAsset;
                set
                {
                    m_sceneAsset = value;
                    SceneName = m_sceneAsset != null ? m_sceneAsset.name : "";
                }
            }
            #endif
        }

        #endregion

        #region 초기화

        public static SceneLoader Create(SceneLoader prefab = null)
        {
            var prefabToLoad = prefab != null ? prefab : Resources.Load<SceneLoader>("SceneLoader");
            if (prefabToLoad == null)
            {
                Debug.LogError("SceneLoader 프리팹을 찾을 수 없습니다. Resources 폴더를 확인하세요.");
                return new GameObject("SceneLoader").AddComponent<SceneLoader>();
            }
            return Instantiate(prefabToLoad);
        }

        private void Awake()
        {
            if (m_instance != null && m_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            m_instance = this;
            DontDestroyOnLoad(gameObject);

            if (m_canvasGroup != null)
            {
                m_canvasGroup.alpha = 0f;
                m_canvasGroup.blocksRaycasts = false;
            }
            gameObject.SetActive(false);
        }

        #endregion

        #region 씬 이동 메서드
        public void LoadLobbyScene() => LoadScene(LOBBY_SCENE);
        public void LoadGameScene() => LoadScene(RUN_GAME_SCENE);
        public void LoadVamSerLikeScene() => LoadScene(VAMSER_LIKE_SCENE);
        public void LoadIntroScene() => LoadScene(INTRO_SCENE);

        public void LoadScene(SceneReference sceneRef)
        {
            if (sceneRef != null && !string.IsNullOrEmpty(sceneRef.SceneName))
                LoadScene(sceneRef.SceneName);
            else
                LogManager.LogError("SceneReference가 유효하지 않습니다!", LogManager.LogCategory.SceneLoader);
        }

        public void LoadScene(string sceneName)
        {
            if (!IsSceneInBuild(sceneName))
            {
                LogManager.LogError($"씬 '{sceneName}'이 빌드 설정에 포함되어 있지 않습니다!", LogManager.LogCategory.SceneLoader);
                return;
            }
            ProcessSceneLoadAsync(sceneName).Forget();
        }
        #endregion

        #region 로딩 로직 (UniTask)

        private async UniTaskVoid ProcessSceneLoadAsync(string sceneName)
        {
            gameObject.SetActive(true);
            LogManager.Log($"씬 변경 시작: {sceneName}", LogManager.LogCategory.SceneLoader);

            // 0. 애니메이터 초기화
            if (m_animator != null)
            {
                m_animator.gameObject.SetActive(true);
                m_animator.Rebind(); 
                m_animator.Update(0f);
            }

            // 1. 페이드 인
            await FadeAsync(true);

            // 2. 로드 시작
            var op = SceneManager.LoadSceneAsync(sceneName);
            op.allowSceneActivation = false;

            float timer = 0f;
            if (m_progressBar != null) m_progressBar.value = 0f;

            // 3. 로딩 루프
            while (!op.isDone)
            {
                await UniTask.Yield();
                timer += Time.unscaledDeltaTime;

                if (op.progress < 0.9f)
                {
                    if (m_progressBar != null)
                        m_progressBar.value = Mathf.Lerp(m_progressBar.value, op.progress, timer);
                    
                    if (m_progressBar != null && m_progressBar.value >= op.progress)
                        timer = 0f;
                }
                else
                {
                    if (m_progressBar != null)
                    {
                        m_progressBar.value = Mathf.Lerp(m_progressBar.value, 1f, timer);
                        if (m_progressBar.value >= 0.99f)
                            op.allowSceneActivation = true;
                    }
                    else
                    {
                        op.allowSceneActivation = true;
                    }
                }
            }

            await op;

            // 4. 로딩 종료 애니메이션 재생
            if (m_animator != null)
            {
                m_animator.SetTrigger(AnimHash_OnFinish);

                // 상태 전이 대기
                await UniTask.Yield(PlayerLoopTiming.Update);
                while (m_animator.IsInTransition(0))
                {
                    await UniTask.Yield(PlayerLoopTiming.Update);
                }

                // 애니메이션 길이만큼 대기
                var stateInfo = m_animator.GetCurrentAnimatorStateInfo(0);
                float delay = stateInfo.length;
                if (stateInfo.speed > 0) delay /= stateInfo.speed;
                
                await UniTask.Delay(TimeSpan.FromSeconds(delay));
                
                // [중요] 여기서 애니메이션을 끄지 않습니다.
                // 그대로 두면 마지막 프레임에 멈춰있는 상태로 페이드 아웃됩니다.
            }

            // 5. 페이드 아웃 (애니메이션 + 배경이 함께 투명해짐)
            await FadeAsync(false);

            // 6. 완전히 끝난 후 비활성화
            if (m_animator != null) m_animator.gameObject.SetActive(false);
            gameObject.SetActive(false);
        }

        private async UniTask FadeAsync(bool isFadeIn)
        {
            if (m_canvasGroup == null) return;

            float startAlpha = isFadeIn ? 0f : 1f;
            float endAlpha = isFadeIn ? 1f : 0f;
            float duration = 0.5f;
            float elapsed = 0f;

            m_canvasGroup.blocksRaycasts = true;
            m_canvasGroup.alpha = startAlpha;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                m_canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, t);
                await UniTask.Yield();
            }

            m_canvasGroup.alpha = endAlpha;
            
            if (!isFadeIn)
            {
                m_canvasGroup.blocksRaycasts = false;
            }
        }

        #endregion

        // ... (유틸리티 영역 동일) ...
        #region 유틸리티

        private bool IsSceneInBuild(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return false;
            int buildIndex = SceneUtility.GetBuildIndexByScenePath(sceneName);
            if (buildIndex != -1) return true;

            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
                string sceneNameInBuild = System.IO.Path.GetFileNameWithoutExtension(scenePath);
                if (string.Equals(sceneNameInBuild, sceneName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        public SceneReference FindSceneReference(string sceneName)
        {
            if (m_sceneReferences == null) return null;

            foreach (var sceneRef in m_sceneReferences)
            {
                if (sceneRef.SceneName == sceneName)
                    return sceneRef;
            }
            return null;
        }

        #endregion
    }
}