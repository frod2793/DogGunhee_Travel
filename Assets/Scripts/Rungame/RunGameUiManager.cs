using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace DogGuns_Games.Run
{
    public class RunGameUiManager : MonoBehaviour
    {
        public SettingsData_oBJ settingsData; // ScriptableObject 참조

        [Header("플레이어")]
        public Button jumpBtn;

        public Slider Hpbar;
        public float JumpHight;
        public float JumpSpeed;

        [Header("초당 깎이는 Hp")] public float discountHp;
        public float maxHp;
        public float currenthp;

        [Header("점수 오브젝트 ")] public float CoinSpeed;
        public List<GameObject> CoinPool = new List<GameObject>();
        public GameObject[] Coins;
        public int Coin_Objcount;
        public Vector2 C_startpoint;

        [Header("체력 회복 오브젝트")] public GameObject HealObj;
        public float PointHp; //체력 회복 아이템이 스폰되는 기준 hp
        public List<GameObject> Healpool;

        [Header("거인화 오브젝트 ")] public GameObject Biggerobj;
        public List<GameObject> Biggerpool;

        [Header("장애물")] public List<GameObject> MobPool = new List<GameObject>();
        public GameObject[] Mobs;
        public int Objcount;
        public float MobSpeed;
        public Vector2 startpoint;

        [Header("배경 땅")] public SpriteRenderer[] tiles;
        public Sprite[] GroundImage;
        public float Groundspeed;

        [Header("UI 버튼 & PopUP")] public Button PuseBtn;
        public Button PlayBtn;
        public Text Scoretext;
        public bool isPlay = false;

        [Header("Puse 팝업")] public Image PusePopup;
        public Button PopUp_playBtn;
        public Button PopUp_ResetBtn;
        public Button PopUp_OPtionBtn;
        [Header("옵션 팝업")] public OptionPopupManager OptionPopUP;

        [Header("점수창 팝업")] public GameObject ScorePopUp;
        public TMP_Text ScorePopUpText;
        public Button contineBtn;
        public Button ExitBtn;

        public Image Star1;
        public Image Star2;
        public Image Star3;

        public delegate void Onplay(bool isPlay);

        public Onplay OnPlay;
        [Header("힐 아이템 목표 점")] public int score = 0;
        public int nextHealScore; //목표점수 
        public int healScore; //더할점수

        [Header("거대화 아이템 목표 점수")] public int nextBiggerScore;
        public int biggerScore;

        public bool isDead;

        private void Func_playBtn()
        {
            jumpBtn.gameObject.SetActive(true);
            PlayBtn.gameObject.SetActive(false);
            isPlay = true;
            isDead = false;
            OnPlay.Invoke(isPlay);
            score = 0;
            nextHealScore = 0;
            Scoretext.text = score.ToString();

            StartCoroutine(Co_AddScore());
        }

        private void Puse()
        {
            isPlay = false;
            PusePopup.gameObject.SetActive(true);
        }

        private void Option()
        {
            Instantiate(OptionPopUP);
            OptionPopUP.gameObject.SetActive(true);
        }

        private void PopUp_Reset()
        {
            LoadSetting();
            Gameover();
            PusePopup.gameObject.SetActive(false);
        }

        private void Popup_Play()
        {
            LoadSetting();
            isPlay = true;
            PusePopup.gameObject.SetActive(false);
            //추후 카운트다운 기능 넣을것
        }

        // Start is called before the first frame update
        void Awake()
        {
            Event_Setter();
            PusePopup.gameObject.SetActive(false);
            ScorePopUp.gameObject.SetActive(false);
            LoadSetting();
        }

        private void Event_Setter()
        {
            PlayBtn.onClick.AddListener(Func_playBtn);
            PuseBtn.onClick.AddListener(Puse);
            PopUp_playBtn.onClick.AddListener(Popup_Play);
            PopUp_ResetBtn.onClick.AddListener(PopUp_Reset);
            PopUp_OPtionBtn.onClick.AddListener(Option);
            contineBtn.onClick.AddListener(Continue);
            ExitBtn.onClick.AddListener(Exit);
        }

        private void LoadSetting()
        {
            if (settingsData.conSmallToggle)
            {
                jumpBtn.gameObject.transform.localScale = new Vector3(0.5f, 0.5f, 1);
            }
            else if (settingsData.conNormalToggle)
            {
                jumpBtn.gameObject.transform.localScale = new Vector3(1, 1, 1);
            }
            else if (settingsData.conBigBtnToggle)
            {
                jumpBtn.gameObject.transform.localScale = new Vector3(1.5f, 1.5f, 1);
            }
        }

        IEnumerator Co_AddScore()
        {
            while (isPlay)
            {
                // score++;
                Scoretext.text = score.ToString();
                yield return new WaitForSeconds(0.1f);
            }
        }

        // Update is called once per frame
        public void Gameover()
        {
            jumpBtn.gameObject.SetActive(false);
            ScorePopUp.gameObject.SetActive(true);
            GameEnd_Show_Score();
            isPlay = false;
            isDead = true;
            OnPlay.Invoke(isPlay);
            StopCoroutine(Co_AddScore());
        }

        public void GameEnd_Show_Score()
        {
            // 점수가 0부터 시작해서 'score'까지 올라가는 애니메이션
            int displayScore = 0;

            // DOTween을 사용하여 점수 애니메이션
            DOTween.To(() => displayScore, x => displayScore = x, score, 2f) // 2초 동안 score까지 증가
                .OnUpdate(() =>
                {
                    // 애니메이션이 업데이트될 때마다 텍스트 갱신
                    ScorePopUpText.text = displayScore.ToString();
                })
                .OnComplete(() =>
                {
                    // 애니메이션이 끝난 후 최종 점수 표시
                    ScorePopUpText.text = score.ToString();
                });
        }

        private void Continue()
        {
            ScorePopUp.gameObject.SetActive(false);
            Func_playBtn();
            Debug.Log("Continue");
        }

        private void Exit()
        {
            SceneLoader.Instance.LoadScene("LobbyScene");
        }
    }
}