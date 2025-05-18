using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DogGuns_Games.Lobby
{


    public class Quest_Index : MonoBehaviour
    {
       [SerializeField] private Toggle questToggle;
       [SerializeField] private TMP_Text questName;
       [SerializeField] private Button  questButton;
       
       public Button QuestButton => questButton;
       
         public void SetQuestIndex(string name)
         {
              if (questName != null)
              {
                questName.text = name;
              }
         }
    }
}