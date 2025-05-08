using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Manager : MonoBehaviour
{

    static Manager s_instance;
    static Manager Instance { get { Init(); return s_instance; } }
    SoundManager _sound = new SoundManager();
    public static SoundManager Sound { get { return Instance._sound; } }
    ResourceManager _resource = new ResourceManager();

    public static ResourceManager Resource { get { return Instance._resource; } }
  

    static void Init()
    {
        if (s_instance == null)
        {
            GameObject go = GameObject.Find("@Manager");
            if (go == null)
            {
                go = new GameObject { name = "@Managers" };
                go.AddComponent<Manager>();
            }
            DontDestroyOnLoad(go);
            s_instance = go.GetComponent<Manager>();
            s_instance._sound.Init();
        }
    }

    public static void Clear()
    {
        Sound.Clear();
    }

}
