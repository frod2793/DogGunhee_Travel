using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace DogGuns_Games.Lobby
{
    public class PostManager : MonoBehaviour
    {
        #region 필드 및 변수

        struct Postmessage
        {
            string _message;
            string _sender;
            string _date;
        }

        [Header("<color=green> 우편함 관리")] [SerializeField]
        private GameObject postBoxPanel;

        [SerializeField] private GameObject postboxcontainer;
        [SerializeField] private Post_Index postboxPrefab;
        [SerializeField] private Button postBoxPanelCloseBtn;

        [Header("<color=green> 확장 패널")] [SerializeField]
        GameObject postBoxPanelExtension;

        [SerializeField] TMP_Text postBoxPanelExtensionText; // 상세 내용
        [SerializeField] TMP_Text postBoxSendernameText;
        [SerializeField] TMP_Text rewardItemNameText;
        [SerializeField] private Button postBoxPanelExtensionCloseBtn;

        #endregion

        #region Unity 라이프사이클

        private void Start()
        {
            postBoxPanelCloseBtn.onClick.AddListener(() => postBoxPanel.SetActive(false));
            postBoxPanelExtensionCloseBtn.onClick.AddListener(() => postBoxPanelExtension.SetActive(false));
            addPost_index();
        }

        #endregion

        #region UI 패널 관리

        public void OpenPostBoxPanel()
        {
            postBoxPanel.SetActive(true);
        }

        public void OpenPostBoxPanel_Extension(string message, string sender, string rewardItemName, int itemCode)
        {
            postBoxPanelExtension.SetActive(true);
            postBoxPanelExtensionText.text = message;
            postBoxSendernameText.text = sender;
            rewardItemNameText.text = rewardItemName;
            postBoxPanelExtensionCloseBtn.onClick.AddListener(() => ConfiromReword(itemCode));
        }

        public void ClosePostBoxPanel_Extension()
        {
            postBoxPanelExtension.SetActive(false);
        }

        #endregion

        #region 우편 데이터 관리

        // todo: 프리펩에 메시지 와 물품 아이템 코드를 저장하고 받을수 있는 클래스를 생성하여 사용 
        //todo: 메시지를 받을시 메시지를 어떻게 처리 할것인가 

        private void addPost_index()
        {
            if (postboxPrefab == null || postboxcontainer == null)
            {
                Debug.LogError("Postbox prefab or container is null!");
                return;
            }

            Post_Index postIndex = Instantiate(postboxPrefab, postboxcontainer.transform);

            if (postIndex == null)
            {
                Debug.LogError("Failed to instantiate postbox prefab!");
                return;
            }

            // 샘플 데이터
            string message = "Sample message";
            string sender = "Sender name";
            string rewardItemName = "Gold";
            int itemCode = 1001;

            UnityEngine.Events.UnityEvent clickEvent = new UnityEngine.Events.UnityEvent();
            clickEvent.AddListener(() => OpenPostBoxPanel_Extension(message, sender, rewardItemName, itemCode));

            postIndex.SetPostIndex(sender, message, "date", clickEvent);
        }

        private void ConfiromReword(int itemCode)
        {
            InventoryDataManagerDontdestory.Instance.GetItemByItemCode(itemCode);
        }

        #endregion
    }
}