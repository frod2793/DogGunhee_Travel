using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DogGuns_Games
{
    public class ChoosegamePopup : MonoBehaviour
    {
        //private Button CgameBtn;
        [SerializeField] private List<Button> CgameBtn = new List<Button>();
        [SerializeField] private Button exitBtn;
        [SerializeField] private MessageManager messageManager;


        private void Start()
        {
            for (int i = 0; i < CgameBtn.Count; i++)
            {
                int index = i;

                //모두마지막 값으로 초기화 되는 현상 방지 를 위해 따로 변수 할당

                CgameBtn[i].onClick.AddListener(delegate
                {
                    cGameBtnSetting(index);
                    Debug.Log("가능 삽입" + i);
                });
            }

            exitBtn.onClick.AddListener(exitBtn_Func);
        }

        private void cGameBtnSetting(int FuncNum)
        {
            Debug.Log("가능 활성화" + FuncNum);
            switch (FuncNum)
            {
                case 0:
                    Vamsirlike();
                    break;
                case 1:
                    groundGame();
                    break;
                case 2:
                    shootingGame();
                    break;
                default:
                    messageManager.OnEmptyGameMessage();
                    break;
            }
        }

        private void exitBtn_Func()
        {
            gameObject.SetActive(false);
        }

        private void Vamsirlike()
        {
            SceneLoader.Instance.LoadVamSerLikeScene();
            Debug.Log("런게임");
        }

        private void groundGame()
        {
            messageManager.OnEmptyGameMessage();
            Debug.Log("땅따먹기");
        }

        private void shootingGame()
        {
            messageManager.OnEmptyGameMessage();
            Debug.Log("슈팅게임");
        }
    }
}