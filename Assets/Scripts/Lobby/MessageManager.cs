using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DogGuns_Games
{
    public class MessageManager : MonoBehaviour
    {
        [SerializeField] private Canvas messageCanvas;

        [SerializeField] private Image message_BG;

        [SerializeField] private Text message_Text;

        void OnMessage()
        {
            messageCanvas.gameObject.SetActive(true);
            message_BG.color = Color.white;

            message_BG.color = new Color(0, 0, 0, 1);
            message_Text.color = new Color(1, 1, 1, 1);
            StartCoroutine(FadeOut());
        }

        IEnumerator FadeOut()
        {
            float fadeCount = 1;
            while (fadeCount > 0.0f)
            {
                fadeCount -= 0.01f;
                yield return new WaitForSeconds(0.01f);
                message_BG.color = new Color(0, 0, 0, fadeCount);
                message_Text.color = new Color(1, 1, 1, fadeCount);
            }

            if (fadeCount == 0 || fadeCount < 0)
            {
                message_Text.color = new Color(1, 1, 1, 1);
                message_BG.color = new Color(0, 0, 0, 1);
                messageCanvas.gameObject.SetActive(false);
            }
        }

        public void OnEmptyGameMessage()
        {
            message_Text.text = "구상중인 항목입니다.";
            OnMessage();
        }

        public void Func_Continue()
        {
            SceneLoader.Instace.LoadScene("LobbyScene");
        }
    }
}