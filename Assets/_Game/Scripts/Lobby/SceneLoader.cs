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
    /// 씬 전환과 로딩 연출을 총괄하는 싱글톤 클래스입니다.
    /// <br/>UniTask를 활용한 비동기 로딩과 DOTween 페이드 효과를 제공합니다.
    /// </summary>
    public class SceneLoader : MonoBehaviour
    {
        #region 1. 상수 및 정적 필드

        private static readonly int k_AnimHashOnFinish = Animator.StringToHash("onFinish");
        private static SceneLoader s_instance;

        /// <summary>
        /// SceneLoader의 전역 인스턴스입니다.
        /// </summary>
        public static SceneLoader Instance
        {
            get
            {
                if (s_instance == null)
                {
                    s_instance = FindFirstObjectByType<SceneLoader>();
                    if (s_instance == null)
                    {
                        s_instance = Create();
                    }
                }

                return s_instance;
            }
        }

        #endregion

        #region 2. 에디터 설정 (Inspector)

        [Header("<color=green>UI 참조</color>")]
        [SerializeField, Tooltip("로딩 화면 페이드용 캔버스 그룹"), FormerlySerializedAs("sceneLoadferCanvasGroup")]
        private CanvasGroup m_canvasGroup;

        [SerializeField, Tooltip("로딩 진행률 슬라이더"), FormerlySerializedAs("progressbar")]
        private Slider m_progressBar;

        [Header("<color=green>연출 설정</color>")] [SerializeField, Tooltip("로딩 아이콘/바 애니메이터")]
        private Animator m_animator;

        [Header("<color=green>씬 참조 목록</color>")] [SerializeField, FormerlySerializedAs("sceneReferences")]
        private SceneReference[] m_sceneReferences;

        #endregion

        #region 3. 내부 클래스 및 구조체

        /// <summary>
        /// 씬 이름과 에디터 에셋을 연결하는 참조 구조체입니다.
        /// </summary>
        [System.Serializable]
        public class SceneReference
        {
            [FormerlySerializedAs("sceneName")] public string SceneName;

#if UNITY_EDITOR
            [SerializeField, FormerlySerializedAs("sceneAsset")]
            private UnityEditor.SceneAsset m_sceneAsset;

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

        #region 4. 초기화 및 생성

        /// <summary>
        /// SceneLoader를 동적으로 생성하거나 프리팹으로부터 인스턴스화합니다.
        /// </summary>
        public static SceneLoader Create(SceneLoader prefab = null)
        {
            var prefabToLoad = prefab != null ? prefab : Resources.Load<SceneLoader>("SceneLoader");
            if (prefabToLoad == null)
            {
                LogManager.LogError("[SceneLoader] 프리팹을 찾을 수 없습니다. 기본 오브젝트를 생성합니다.",
                    LogManager.LogCategory.SceneLoader);
                return new GameObject("SceneLoader").AddComponent<SceneLoader>();
            }

            return Instantiate(prefabToLoad);
        }

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeUI();
        }

        /// <summary>
        /// 로딩 UI의 초기 레이아웃 상태를 설정합니다.
        /// </summary>
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

        #region 5. 공개 API (씬 이동)

        public UniTask LoadLobbySceneAsync() => LoadSceneAsync(SceneNames.Lobby);
        public UniTask LoadGameSceneAsync() => LoadSceneAsync(SceneNames.RunGame);
        public UniTask LoadVamSerLikeSceneAsync() => LoadSceneAsync(SceneNames.VamSerLike);
        public UniTask LoadIntroSceneAsync() => LoadSceneAsync(SceneNames.Intro);

        // --- void 기반의 비동기 실행 (Forget 사용) ---
        public void LoadLobbyScene() => LoadLobbySceneAsync().Forget();
        public void LoadGameScene() => LoadGameSceneAsync().Forget();
        public void LoadVamSerLikeScene() => LoadVamSerLikeSceneAsync().Forget();
        public void LoadIntroScene() => LoadIntroSceneAsync().Forget();
        public void LoadScene(string sceneName) => LoadSceneAsync(sceneName).Forget();
        public void LoadScene(SceneReference sceneRef) => LoadSceneAsync(sceneRef).Forget();

        /// <summary>
        /// SceneReference 객체를 사용하여 비동기로 씬을 로드합니다.
        /// </summary>
        public UniTask LoadSceneAsync(SceneReference sceneRef)
        {
            if (sceneRef != null && !string.IsNullOrEmpty(sceneRef.SceneName))
            {
                return LoadSceneAsync(sceneRef.SceneName);
            }

            LogManager.LogError("[SceneLoader] SceneReference가 유효하지 않습니다.", LogManager.LogCategory.SceneLoader);
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 씬 이름을 문자열로 받아 비동기로 로드 절차를 시작합니다.
        /// </summary>
        public async UniTask LoadSceneAsync(string sceneName)
        {
            if (!IsSceneInBuild(sceneName))
            {
                LogManager.LogError($"[SceneLoader] 씬 '{sceneName}'이 빌드 설정에 없습니다!", LogManager.LogCategory.SceneLoader);
                return;
            }

            if (gameObject.activeSelf)
            {
                LogManager.LogWarning($"[SceneLoader] 이미 로딩이 진행 중입니다: {sceneName}", LogManager.LogCategory.SceneLoader);
                return;
            }

            await ProcessSceneLoadAsync(sceneName);
        }

        #endregion

        #region 6. 비동기 시퀀스 (Core Logic)

        /// <summary>
        /// 페이드 인 -> 씬 로드 -> 연출 대기 -> 페이드 아웃의 표준 로딩 시퀀스를 실행합니다.
        /// </summary>
        private async UniTask ProcessSceneLoadAsync(string sceneName)
        {
            PrepareLoading();

            LogManager.Log($"[SceneLoader] 씬 전환 시작: {sceneName}", LogManager.LogCategory.SceneLoader);

            // 1. 페이드 인 (화면 가리기)
            await FadeAsync(true);

            // 2. 실제 비동기 씬 로드 및 진행 바 갱신
            await LoadSceneInternalAsync(sceneName);

            // 3. 로딩 완료 연출 대기
            await WaitForFinishAnimationAsync();

            // 4. 페이드 아웃 (화면 보이기)
            await FadeAsync(false);

            FinishLoading();
        }

        /// <summary>
        /// 로딩 시작 전 애니메이터와 UI 상태를 초기화합니다.
        /// </summary>
        private void PrepareLoading()
        {
            gameObject.SetActive(true);

            if (m_animator != null)
            {
                m_animator.gameObject.SetActive(true);
                m_animator.Rebind();
                m_animator.Update(0f);
            }

            if (m_progressBar != null)
            {
                m_progressBar.value = 0f;
            }
        }

        /// <summary>
        /// Unity AsyncOperation을 사용하여 씬을 로드하고 프로그레스바를 부드럽게 갱신합니다.
        /// </summary>
        private async UniTask LoadSceneInternalAsync(string sceneName)
        {
            var op = SceneManager.LoadSceneAsync(sceneName);
            op.allowSceneActivation = false;

            float timer = 0f;

            while (!op.isDone)
            {
                await UniTask.Yield();
                timer += Time.unscaledDeltaTime;

                // 부드러운 로딩바 연출을 위한 가짜 진행률 계산
                if (op.progress < 0.9f)
                {
                    if (m_progressBar != null)
                    {
                        m_progressBar.value = Mathf.Lerp(m_progressBar.value, op.progress, timer);
                    }

                    if (m_progressBar != null && m_progressBar.value >= op.progress)
                    {
                        timer = 0f;
                    }
                }
                else
                {
                    if (m_progressBar != null)
                    {
                        m_progressBar.value = Mathf.Lerp(m_progressBar.value, 1f, timer);
                    }

                    if (m_progressBar == null || m_progressBar.value >= 0.99f)
                    {
                        op.allowSceneActivation = true;
                    }
                }
            }

            await op;
        }

        /// <summary>
        /// 로딩이 끝난 후 마침 애니메이션이 완료될 때까지 대기합니다.
        /// </summary>
        private async UniTask WaitForFinishAnimationAsync()
        {
            if (m_animator != null)
            {
                m_animator.SetTrigger(k_AnimHashOnFinish);

                // 상태 전이 완료 시까지 대기
                await UniTask.Yield(PlayerLoopTiming.Update);
                while (m_animator.IsInTransition(0))
                {
                    await UniTask.Yield(PlayerLoopTiming.Update);
                }

                // 애니메이션 클립 재생 시간만큼 대기
                var stateInfo = m_animator.GetCurrentAnimatorStateInfo(0);
                float delay = stateInfo.length;
                if (stateInfo.speed > 0)
                {
                    delay /= stateInfo.speed;
                }

                await UniTask.Delay(TimeSpan.FromSeconds(delay), ignoreTimeScale: true);
            }
        }

        /// <summary>
        /// 모든 로딩 절차가 완료된 후 상태를 정리합니다.
        /// </summary>
        private void FinishLoading()
        {
            if (m_animator != null)
            {
                m_animator.gameObject.SetActive(false);
            }

            gameObject.SetActive(false);
        }

        /// <summary>
        /// DOTween을 사용하여 화면 전체를 페이드 처리합니다.
        /// </summary>
        private async UniTask FadeAsync(bool isFadeIn)
        {
            if (m_canvasGroup == null) return;

            float endAlpha = isFadeIn ? 1f : 0f;
            float duration = 0.5f;

            m_canvasGroup.blocksRaycasts = true;

            await m_canvasGroup.DOFade(endAlpha, duration)
                .SetUpdate(true)
                .ToUniTask();

            if (!isFadeIn)
            {
                m_canvasGroup.blocksRaycasts = false;
            }
        }

        #endregion

        #region 7. 유틸리티 로직

        /// <summary>
        /// 제공된 씬 이름이 Build Settings에 포함되어 있는지 확인합니다.
        /// </summary>
        private bool IsSceneInBuild(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return false;

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

        /// <summary>
        /// 설정된 씬 참조 목록에서 프로젝트 내 실데이터를 찾습니다.
        /// </summary>
        public SceneReference FindSceneReference(string sceneName)
        {
            if (m_sceneReferences == null) return null;

            foreach (var sceneRef in m_sceneReferences)
            {
                if (sceneRef.SceneName == sceneName)
                {
                    return sceneRef;
                }
            }

            return null;
        }

        #endregion
    }
}