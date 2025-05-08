using System.IO;
using UnityEngine;

namespace DogGuns_Games
{
    public class PlayerDataManagerDontdesytoy : MonoBehaviour
    {
        #region 변수 및 필드
        
        public PlayerData scritpableobjPlayerData;

        public int SelectWeaponIndex
        {
            get => scritpableobjPlayerData.selelcWeaponIndex;
            set => scritpableobjPlayerData.selelcWeaponIndex = value;
        }

        public int SelectCharacterIndex
        {
            get => scritpableobjPlayerData.selectCharacterIndex;
            set => scritpableobjPlayerData.selectCharacterIndex = value;
        }
        
        #endregion

        #region DontDestroyOnLoad

        //파괴되지않는 오브젝트
        private static PlayerDataManagerDontdesytoy instance;

        public static PlayerDataManagerDontdesytoy Instance
        {
            get
            {
                if (IsNullOrEmpty(instance))
                {
                    instance = FindAnyObjectByType<PlayerDataManagerDontdesytoy>();
                    if (IsNullOrEmpty(instance))
                    {
                        var container = new GameObject("Player_Data");
                        instance = container.AddComponent<PlayerDataManagerDontdesytoy>();
                        DontDestroyOnLoad(container);
                    }
                }

                return instance;
            }
        }

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            DontDestroyOnLoad(gameObject);

            if (scritpableobjPlayerData == null)
                scritpableobjPlayerData = ScriptableObject.CreateInstance<PlayerData>();
        }
        
        #endregion

        #region 데이터 관리 메서드

        /// <summary>
        ///     플레이어 데이터 로컬 저장
        /// </summary>
        public void SavePlayerData()
        {
            var savePath = Path.Combine(Application.persistentDataPath, "playerData.json");
            var jsonData = JsonUtility.ToJson(scritpableobjPlayerData, true);
            File.WriteAllText(savePath, jsonData);
        }

        /// <summary>
        ///     플레이어 데이터 업데이트
        /// </summary>
        /// <param name="playerData"></param>
        public void UpdatePlayerData(PlayerData playerData)
        {
            scritpableobjPlayerData = playerData;
        }
        
        #endregion

        #region 유틸리티 메서드

        private static bool IsNullOrEmpty(Object value)
        {
            return ReferenceEquals(value, null);
        }
        
        #endregion
    }
}