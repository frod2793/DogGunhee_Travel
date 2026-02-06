using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace InGame
{
    /// <summary>
    /// 씬 전환을 관리하는 싱글톤 클래스입니다.
    /// UniTask를 사용하여 비동기 로딩을 지원하며, DOTween을 활용한 페이드 효과를 제공합니다.
    /// </summary>
    public class SceneLoader : MonoBehaviour
    {
        #region 상수 및 정적 필드

        private static readonly int AnimHash_OnFinish = Animator.StringToHash("onFinish");
        private static SceneLoader m_instance;

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
                LogManager.LogError("SceneLoader 프리팹을 찾을 수 없습니다. Resources 폴더를 확인하세요.", LogManager.LogCategory.SceneLoader);
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

            InitializeUI();
        }

        private void InitializeUI()
        {
            if (m_canvasGroup != null)
            {
                m_canvasGroup.alpha = 0f;
                m_canvasGroup.blocksRaycasts = false;
            }
            gameObject.SetActive(false);
        }

        #endregion

        #region 공개 API (씬 이동)

        public UniTask LoadLobbySceneAsync() => LoadSceneAsync(SceneNames.Lobby);
        public UniTask LoadGameSceneAsync() => LoadSceneAsync(SceneNames.RunGame);
        public UniTask LoadVamSerLikeSceneAsync() => LoadSceneAsync(SceneNames.VamSerLike);
        public UniTask LoadIntroSceneAsync() => LoadSceneAsync(SceneNames.Intro);

        // 기존 void 메서드 유지 (하위 호환성, 필요한 경우)
        public void LoadLobbyScene() => LoadLobbySceneAsync().Forget();
        public void LoadGameScene() => LoadGameSceneAsync().Forget();
        public void LoadVamSerLikeScene() => LoadVamSerLikeSceneAsync().Forget();
        public void LoadIntroScene() => LoadIntroSceneAsync().Forget();

        // 기존 API 호환용 (void 반환)
        public void LoadScene(string sceneName) => LoadSceneAsync(sceneName).Forget();
        public void LoadScene(SceneReference sceneRef) => LoadSceneAsync(sceneRef).Forget();


        public UniTask LoadSceneAsync(SceneReference sceneRef)
        {
            if (sceneRef != null && !string.IsNullOrEmpty(sceneRef.SceneName))
                return LoadSceneAsync(sceneRef.SceneName);

            LogManager.LogError("SceneReference가 유효하지 않습니다!", LogManager.LogCategory.SceneLoader);
            return UniTask.CompletedTask;
        }

        public async UniTask LoadSceneAsync(string sceneName)
        {
            if (!IsSceneInBuild(sceneName))
            {
                LogManager.LogError($"씬 '{sceneName}'이 빌드 설정에 포함되어 있지 않습니다!", LogManager.LogCategory.SceneLoader);
                return;
            }

            if (gameObject.activeSelf)
            {
                LogManager.LogWarning($"이미 씬 로딩 중입니다: {sceneName}", LogManager.LogCategory.SceneLoader);
                return;
            }

            await ProcessSceneLoadAsync(sceneName);
        }

        #endregion

        #region 로딩 로직 (Core)

        private async UniTask ProcessSceneLoadAsync(string sceneName)
        {
            PrepareLoading();

            LogManager.Log($"씬 변경 시작: {sceneName}", LogManager.LogCategory.SceneLoader);

            // 1. 페이드 인
            await FadeAsync(true);

            // 2. 비동기 씬 로드
            await LoadSceneInternalAsync(sceneName);

            // 3. 로딩 종료 연출 (애니메이션 대기)
            await WaitForFinishAnimationAsync();

            // 4. 페이드 아웃 및 종료
            await FadeAsync(false);

            FinishLoading();
        }

        private void PrepareLoading()
        {
            gameObject.SetActive(true);

            // 애니메이터 초기화
            if (m_animator != null)
            {
                m_animator.gameObject.SetActive(true);
                m_animator.Rebind();
                m_animator.Update(0f);
            }

            // 프로그레스바 초기화
            if (m_progressBar != null) m_progressBar.value = 0f;
        }

        private async UniTask LoadSceneInternalAsync(string sceneName)
        {
            var op = SceneManager.LoadSceneAsync(sceneName);
            op.allowSceneActivation = false;

            float timer = 0f;

            while (!op.isDone)
            {
                await UniTask.Yield();
                timer += Time.unscaledDeltaTime;

                // [가짜 로딩] 0.9까지는 천천히, 그 이후는 빠르게
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
                        m_progressBar.value = Mathf.Lerp(m_progressBar.value, 1f, timer);

                    // 완료 조건
                    if (m_progressBar == null || m_progressBar.value >= 0.99f)
                    {
                        op.allowSceneActivation = true;
                    }
                }
            }

            await op;
        }

        private async UniTask WaitForFinishAnimationAsync()
        {
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

                await UniTask.Delay(TimeSpan.FromSeconds(delay), ignoreTimeScale: true);
            }
        }

        private void FinishLoading()
        {
            if (m_animator != null) m_animator.gameObject.SetActive(false);
            gameObject.SetActive(false);
        }

        private async UniTask FadeAsync(bool isFadeIn)
        {
            if (m_canvasGroup == null) return;

            float endAlpha = isFadeIn ? 1f : 0f;
            float duration = 0.5f;

            m_canvasGroup.blocksRaycasts = true;

            // DOTween 사용 (기존 Lerp 로직 대체)
            await m_canvasGroup.DOFade(endAlpha, duration)
                               .SetUpdate(true) // TimeScale 무시
                               .ToUniTask();

            if (!isFadeIn)
            {
                m_canvasGroup.blocksRaycasts = false;
            }
        }

        #endregion

        #region 유틸리티

        private bool IsSceneInBuild(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return false;
            
            // Build Settings에 있는지 확인
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