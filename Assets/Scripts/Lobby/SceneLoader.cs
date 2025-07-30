using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DogGuns_Games
{
    public class SceneLoader : MonoBehaviour
    {
        protected static SceneLoader instance;

        // 씬 이름 상수들
        public const string INTRO_SCENE = "IntroScene";
        public const string LOBBY_SCENE = "LobbyScene";
        public const string RUN_GAME_SCENE = "RunGame";
        public const string VAMSER_LIKE_SCENE = "VamSerlike";

        public static SceneLoader Instance // 오타 수정: Instace -> Instance
        {
            get
            {
                if (instance == null)
                {
                    var obj = FindAnyObjectByType<SceneLoader>();
                    if (obj != null)
                    {
                        instance = obj;
                    }
                    else
                    {
                        instance = Create();
                    }
                }

                return instance;
            }
            private set { instance = value; }
        }

        [SerializeField] private CanvasGroup sceneLoadferCanvasGroup;
        [SerializeField] private Slider progressbar;
        
        [Header("Scene References")]
        [SerializeField] private SceneReference[] sceneReferences;
        
        [System.Serializable]
        public class SceneReference
        {
            public string sceneName;
            
            #if UNITY_EDITOR
            [SerializeField] private UnityEditor.SceneAsset sceneAsset;
            
            public UnityEditor.SceneAsset SceneAsset
            {
                get => sceneAsset;
                set
                {
                    sceneAsset = value;
                    sceneName = sceneAsset != null ? sceneAsset.name : "";
                }
            }
            #endif
        }
        

        private string loadSceneName;

        public static SceneLoader Create(SceneLoader prefab = null)
        {
            var SeneLoadprefeb = Resources.Load<SceneLoader>("SceneLoader");
            return Instantiate(SeneLoadprefeb);
        }

        private void Awake()
        {
            if (instance != this)
            {
                Destroy(gameObject);
                return;
            }

            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// 로비 씬으로 이동합니다.
        /// </summary>
        public void LoadLobbyScene()
        {
            LoadScene(LOBBY_SCENE);
        }

        /// <summary>
        /// 게임 씬으로 이동합니다.
        /// </summary>
        public void LoadGameScene()
        {
            LoadScene(RUN_GAME_SCENE);
        }

        /// <summary>
        /// VamSerLike 씬으로 이동합니다.
        /// </summary>
        public void LoadVamSerLikeScene()
        {
            LoadScene(VAMSER_LIKE_SCENE);
        }

        /// <summary>
        /// 인트로 씬으로 이동합니다.
        /// </summary>
        public void LoadIntroScene()
        {
            LoadScene(INTRO_SCENE);
        }

        public void LoadScene(string SceneName)
        {
            // 씬이 빌드에 포함되어 있는지 확인
            if (!IsSceneInBuild(SceneName))
            {
                Debug.LogError($"씬 '{SceneName}'이 빌드 설정에 포함되어 있지 않습니다!");
                return;
            }

            gameObject.SetActive(true);
            SceneManager.sceneLoaded += LoadSceneEnd;
            loadSceneName = SceneName;
            StartCoroutine(Load(SceneName));
            Debug.Log($"씬 변경 시작: {SceneName}");
        }

        /// <summary>
        /// 씬이 빌드에 포함되어 있는지 확인합니다.
        /// </summary>
        private bool IsSceneInBuild(string sceneName)
        {
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
                string sceneNameInBuild = System.IO.Path.GetFileNameWithoutExtension(scenePath);
                if (sceneNameInBuild == sceneName)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// SceneReference를 통해 씬을 로드합니다.
        /// </summary>
        public void LoadScene(SceneReference sceneRef)
        {
            if (sceneRef != null && !string.IsNullOrEmpty(sceneRef.sceneName))
            {
                LoadScene(sceneRef.sceneName);
            }
            else
            {
                Debug.LogError("SceneReference가 유효하지 않습니다!");
            }
        }


        /// <summary>
        /// 씬 이름으로 SceneReference를 찾습니다.
        /// </summary>
        public SceneReference FindSceneReference(string sceneName)
        {
            if (sceneReferences != null)
            {
                foreach (var sceneRef in sceneReferences)
                {
                    if (sceneRef.sceneName == sceneName)
                        return sceneRef;
                }
            }
            return null;
        }
        
        private IEnumerator Load(string sceneName)
        {
            progressbar.value = 0f;
            yield return StartCoroutine(Fade(true));

            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
            op.allowSceneActivation = false;

            float timer = 0.0f;
            while (!op.isDone)
            {
                yield return null;
                timer += Time.unscaledDeltaTime;
                if (op.progress < 0.9f)
                {
                    float targetProgress = Mathf.Lerp(progressbar.value, op.progress, timer);
                    progressbar.value = targetProgress;
                    if (progressbar.value >= op.progress)
                    {
                        timer = 0f;
                    }
                }
                else
                {
                    float targetProgress = Mathf.Lerp(progressbar.value, 1f, timer);
                    progressbar.value = targetProgress;
                    if (progressbar.value >= 0.9999f)
                    {
                        op.allowSceneActivation = true;
                        yield break;
                    }
                }
            }
        }


        private void LoadSceneEnd(Scene scene, LoadSceneMode loadSceneMode)
        {
            if (scene.name == loadSceneName)
            {
                StartCoroutine(Fade(false));
                SceneManager.sceneLoaded -= LoadSceneEnd;
            }
        }

        private IEnumerator Fade(bool isFadeIn)
        {
            float timer = 0f;

            while (timer <= 1f)
            {
                yield return null;
                timer += Time.unscaledDeltaTime * 2f;
                sceneLoadferCanvasGroup.alpha = Mathf.Lerp(isFadeIn ? 0 : 1, isFadeIn ? 1 : 0, timer);
            }

            if (!isFadeIn)
            {
                gameObject.SetActive(false);
            }
        }
    }
}