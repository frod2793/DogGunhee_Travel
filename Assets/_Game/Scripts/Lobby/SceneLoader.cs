using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace InGame
{
    /// <summary>
    /// [설명]: 씬 전환과 로딩 연출을 총괄하는 싱글톤 클래스입니다.
    /// 비동기 씬 로딩, 페이드 효과, 로딩 프로그레스바 및 애니메이션 제어를 담당합니다.
    /// </summary>
    public class SceneLoader : MonoBehaviour, ISceneLoader
    {
        #region 에디터 설정

        [Header("<color=green>UI 참조</color>")]
        [SerializeField, Tooltip("로딩 화면 페이드용 캔버스 그룹"), FormerlySerializedAs("sceneLoadferCanvasGroup")]
        private CanvasGroup m_canvasGroup;

        [SerializeField, Tooltip("로딩 진행률 슬라이더"), FormerlySerializedAs("progressbar")]
        private Slider m_progressBar;

        [Header("<color=green>연출 설정</color>")]
        [SerializeField, Tooltip("로딩 아이콘/바 애니메이터")]
        private Animator m_animator;

        private bool m_isFadedOut = true; // [추가]: 페이드 아웃 완료 여부 플래그

        [Header("<color=green>씬 참조 목록</color>")]
        [SerializeField, FormerlySerializedAs("sceneReferences")]
        private SceneReference[] m_sceneReferences;

        #endregion

        #region 내부 필드

        private static readonly int k_AnimHashOnFinish = Animator.StringToHash("onFinish");
        private static SceneLoader s_instance;

        private CancellationTokenSource m_cts;

        #endregion

        #region 싱글톤

        /// <summary>
        /// [설명]: SceneLoader의 전역 인스턴스입니다.
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

        #region 유니티 생명주기

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

        private void OnDestroy()
        {
            if (m_cts != null)
            {
                m_cts.Cancel();
                m_cts.Dispose();
            }

            if (s_instance == this)
            {
                s_instance = null;
            }
        }

        #endregion

        #region 초기화

        /// <summary>
        /// [설명]: SceneLoader를 동적으로 생성하거나 프리팹으로부터 인스턴스화합니다.
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

        /// <summary>
        /// [설명]: 로딩 UI의 초기 레이아웃 상태를 설정합니다.
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

        /// <summary>
        /// [설명]: 씬 전환 후, 페이드 아웃 연출이 완전히 끝날 때까지 대기합니다.
        /// </summary>
        public async UniTask WaitUntilFadedOutAsync()
        {
            await UniTask.WaitUntil(() => m_isFadedOut);
        }

        #endregion

        #region 공개 API

        public UniTask LoadLobbySceneAsync(object payload = null) => LoadSceneAsync(SceneNames.Lobby, payload);
        public UniTask LoadGameSceneAsync(object payload = null) => LoadSceneAsync(SceneNames.RunGame, payload);
        public UniTask LoadVamSerLikeSceneAsync(object payload = null) => LoadSceneAsync(SceneNames.VamSerLike, payload);
        public UniTask LoadIntroSceneAsync(object payload = null) => LoadSceneAsync(SceneNames.Intro, payload);

        public void LoadLobbyScene(object payload = null) => LoadLobbySceneAsync(payload).Forget();
        public void LoadGameScene(object payload = null) => LoadGameSceneAsync(payload).Forget();
        public void LoadVamSerLikeScene(object payload = null) => LoadVamSerLikeSceneAsync(payload).Forget();
        public void LoadIntroScene(object payload = null) => LoadIntroSceneAsync(payload).Forget();
        public void LoadScene(string sceneName, object payload = null) => LoadSceneAsync(sceneName, payload).Forget();
        public void LoadScene(SceneReference sceneRef, object payload = null) => LoadSceneAsync(sceneRef, payload).Forget();

        /// <summary>
        /// [설명]: SceneReference 객체를 사용하여 비동기로 씬을 로드합니다.
        /// </summary>
        public UniTask LoadSceneAsync(SceneReference sceneRef, object payload = null)
        {
            if (sceneRef != null && !string.IsNullOrEmpty(sceneRef.SceneName))
            {
                return LoadSceneAsync(sceneRef.SceneName, payload);
            }

            LogManager.LogError("[SceneLoader] SceneReference가 유효하지 않습니다.", LogManager.LogCategory.SceneLoader);
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// [설명]: 씬 이름을 문자열로 받아 비동기로 로드 절차를 시작합니다.
        /// </summary>
        public async UniTask LoadSceneAsync(string sceneName, object payload = null)
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

            // 이전 태스크 취소 후 새로 생성
            if (m_cts != null)
            {
                m_cts.Cancel();
                m_cts.Dispose();
            }

            m_cts = new CancellationTokenSource();

            try
            {
                await ProcessSceneLoadAsync(sceneName, payload, m_cts.Token);
            }
            catch (OperationCanceledException)
            {
                LogManager.Log("[SceneLoader] 씬 로딩이 취소되었습니다.", LogManager.LogCategory.SceneLoader);
            }
            catch (Exception e)
            {
                LogManager.LogError($"[SceneLoader] 씬 로딩 중 에러 발생: {e.Message}", LogManager.LogCategory.SceneLoader);
            }
        }

        #endregion

        #region 내부 비즈니스 로직

        /// <summary>
        /// [설명]: 페이드 인 -> 씬 로드 -> 연출 대기 -> 페이드 아웃의 표준 로딩 시퀀스를 실행합니다.
        /// </summary>
        private async UniTask ProcessSceneLoadAsync(string sceneName, object payload, CancellationToken ct)
        {
            m_isFadedOut = false; // [수정]: 페이드 아웃 상태 리셋
            PrepareLoading();

            LogManager.Log($"[SceneLoader] 씬 전환 시작: {sceneName}", LogManager.LogCategory.SceneLoader);

            // 1. 페이드 인 (화면 가리기)
            await FadeAsync(true, ct);

            // 2. 실제 비동기 씬 로드 및 진행 바 갱신
            await LoadSceneInternalAsync(sceneName, payload, ct);

            // 3. 로딩 완료 연출 대기
            await WaitForFinishAnimationAsync(ct);

            // 4. 페이드 아웃 (화면 보이기)
            await FadeAsync(false, ct);

            FinishLoading();
            m_isFadedOut = true; // [수정]: 페이드 아웃 완료 표시
            LogManager.Log($"[SceneLoader] 씬 전환 완료: {sceneName}", LogManager.LogCategory.SceneLoader);
        }

        /// <summary>
        /// [설명]: 로딩 시작 전 애니메이터와 UI 상태를 초기화합니다.
        /// </summary>
        private void PrepareLoading()
        {
            // 씬 전환 시 시간축이 멈춰있으면 애니메이션 등이 작동하지 않으므로 1.0으로 복구
            if (Time.timeScale < 1f)
            {
                Time.timeScale = 1f;
            }

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
        /// [설명]: Unity AsyncOperation을 사용하여 씬을 로드하고 프로그레스바를 부드럽게 갱신합니다.
        /// </summary>
        private async UniTask LoadSceneInternalAsync(string sceneName, object payload, CancellationToken ct)
        {
            var op = SceneManager.LoadSceneAsync(sceneName);
            op.allowSceneActivation = false;

            float timer = 0f;

            while (!op.isDone)
            {
                ct.ThrowIfCancellationRequested();

                await UniTask.Yield(ct);
                timer += Time.unscaledDeltaTime;

                // 부드러운 로딩바 연출을 위한 가짜 진행률 계산
                if (op.progress < 0.9f)
                {
                    if (m_progressBar != null)
                    {
                        m_progressBar.value = Mathf.Lerp(m_progressBar.value, op.progress, timer * 2f);
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
                        m_progressBar.value = Mathf.Lerp(m_progressBar.value, 1f, timer * 2f);
                    }

                    if (m_progressBar == null || m_progressBar.value >= 0.99f)
                    {
                        op.allowSceneActivation = true;
                    }
                }
            }

            // 씬 로드 완료 후 Initializer 찾기 및 초기화 대기
            var initializers = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            var initTasks = new System.Collections.Generic.List<UniTask>();

            foreach (var mono in initializers)
            {
                if (mono is Core.ISceneInitializer initializer)
                {
                    initTasks.Add(initializer.OnInitialize(payload));
                }
            }

            if (initTasks.Count > 0)
            {
                await UniTask.WhenAll(initTasks).AttachExternalCancellation(ct);
            }

            await op.ToUniTask(cancellationToken: ct);
        }

        /// <summary>
        /// [설명]: 로딩이 끝난 후 마침 애니메이션이 완료될 때까지 대기합니다.
        /// </summary>
        private async UniTask WaitForFinishAnimationAsync(CancellationToken ct)
        {
            if (m_animator == null) return;

            m_animator.SetTrigger(k_AnimHashOnFinish);

            // 상태 전이 시작 시까지 대기
            await UniTask.Yield(PlayerLoopTiming.Update, ct);

            float startTime = Time.realtimeSinceStartup;

            // 전이가 진행 중인 동안 대기 (타임아웃 3초)
            while (m_animator.IsInTransition(0))
            {
                if (Time.realtimeSinceStartup - startTime > 3f)
                {
                    LogManager.LogWarning("[SceneLoader] 전이 대기 시간 초과 (3초).", LogManager.LogCategory.SceneLoader);
                    break;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            // 애니메이션 클립 재생 시간만큼 대기
            var stateInfo = m_animator.GetCurrentAnimatorStateInfo(0);
            float delay = stateInfo.length;
            if (stateInfo.speed > 0)
            {
                delay /= stateInfo.speed;
            }

            // 비정상적으로 긴 지연 방지
            delay = Mathf.Min(delay, 2f);

            if (delay > 0)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(delay), ignoreTimeScale: true, cancellationToken: ct);
            }
        }

        /// <summary>
        /// [설명]: 모든 로딩 절차가 완료된 후 상태를 정리합니다.
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
        /// [설명]: DOTween을 사용하여 화면 전체를 페이드 처리합니다.
        /// </summary>
        private async UniTask FadeAsync(bool isFadeIn, CancellationToken ct)
        {
            if (m_canvasGroup == null) return;

            float endAlpha = isFadeIn ? 1f : 0f;
            float duration = 0.5f;

            m_canvasGroup.blocksRaycasts = true;

            await m_canvasGroup.DOFade(endAlpha, duration)
                .SetUpdate(true)
                .ToUniTask(cancellationToken: ct);

            if (!isFadeIn)
            {
                m_canvasGroup.blocksRaycasts = false;
            }
        }

        /// <summary>
        /// [설명]: 제공된 씬 이름이 Build Settings에 포함되어 있는지 확인합니다.
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
        /// [설명]: 설정된 씬 참조 목록에서 프로젝트 내 실데이터를 찾습니다.
        /// </summary>
        public SceneReference FindSceneReference(string sceneName)
        {
            if (m_sceneReferences == null) return null;

            foreach (var sceneRef in m_sceneReferences)
            {
                if (sceneRef != null && sceneRef.SceneName == sceneName)
                {
                    return sceneRef;
                }
            }

            return null;
        }

        #endregion

        #region 내부 클래스 및 구조체

        /// <summary>
        /// [설명]: 씬 이름과 에디터 에셋을 연결하는 참조 구조체입니다.
        /// </summary>
        [System.Serializable]
        public class SceneReference
        {
            [FormerlySerializedAs("sceneName")]
            public string SceneName;

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
    }
}