using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DogGuns_Games.Lobby
{
    public class PostIndex : MonoBehaviour
    {
        [SerializeField] private Image postprofile;
        [SerializeField] private Image itemprofile;
        [SerializeField] private TMP_Text postname;
        [SerializeField] private TMP_Text posttitle;
        [SerializeField] private TMP_Text postdate;
        [SerializeField] private Button getReiwordbutton;
        [SerializeField] private Button postexpendbutton;

        public void SetPostIndex(string name, string title, string date,UnityEvent getreward ,UnityEvent action)
        {
            if (postname != null)
            {
                postname.text = name;
            }

            if (posttitle != null)
            {
                posttitle.text = title;
            }

            if (postdate != null)
            {
                postdate.text = date;
            }

            if (getReiwordbutton != null && getreward != null)
            {
                getReiwordbutton.onClick.AddListener(()=>getreward.Invoke());
            }
            
            if (postexpendbutton != null && action != null)
            {
                postexpendbutton.onClick.AddListener(() => action.Invoke());
            }
            
            
        }
    }
}