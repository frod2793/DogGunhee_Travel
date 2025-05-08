using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BackEnd;
using DogGuns_Games.Lobby;
using LitJson;
using UnityEngine;

namespace DogGuns_Games
{
    public class ServerManager : MonoBehaviour
    {
        #region 필드 및 프로퍼티
        
        public string uuid;
        public string nickName;
        private string _gameDataRowInDate = string.Empty;

        private PlayerData _playerData;
        private Item_Data _itemData;
        private Inventory_Data _inventoryData;

        public Dictionary<string, int> Inventory = new();
        
        #endregion

        #region 싱글톤 패턴 (DontDestroyOnLoad)

        //파괴되지않는 오브젝트
        private static ServerManager instance;

        public static ServerManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<ServerManager>();
                    if (instance == null)
                    {
                        var container = new GameObject("ServerManager");
                        instance = container.AddComponent<ServerManager>();
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
        }

        private void Start()
        {
            BackEndInIt();
        }

        #endregion

        #region 초기화 메서드

        /// <summary>
        ///     뒤끝 서버 초기화
        /// </summary>
        private void BackEndInIt()
        {
            var bro = Backend.Initialize(); // 뒤끝 초기화

            // 뒤끝 초기화에 대한 응답값
            if (bro.IsSuccess())
                Debug.Log("초기화 성공 : " + bro); // 성공일 경우 statusCode 204 Success
            else
                Debug.LogError("초기화 실패 : " + bro); // 실패일 경우 statusCode 400대 에러 발생
        }

        #endregion

        #region 로그인 및 회원 가입

        /// <summary>
        ///     회원 가입
        /// </summary>
        /// <param name="id"></param>
        /// <param name="pw"></param>
        /// <param name="nickname"></param>
        /// <param name="ac"></param>
        public void SignUp(string id, string pw, string nickname, Action ac)
        {
            var bro = Backend.BMember.CustomSignUp(id, pw);
            if (bro.IsSuccess())
            {
                Debug.Log("회원가입 성공: " + bro);
                bro = Backend.BMember.UpdateNickname(nickname);
                if (bro.IsSuccess())
                {
                    Debug.Log("닉네임 변경 성공: " + bro);
                    ac.Invoke();
                }
                else
                {
                    Debug.Log("닉네임 변경 실패: " + bro);
                    ErroDebug(bro);
                }
            }
            else
            {
                Debug.Log("회원가입 실패: " + bro);
                ErroDebug(bro);
            }
        }

        /// <summary>
        ///     로그인
        /// </summary>
        /// <param name="id"></param>
        /// <param name="pw"></param>
        /// <param name="ac">로그인 성공시 실행할액션 </param>
        public void Login(string id, string pw, Action ac)
        {
            var bro = Backend.BMember.CustomLogin(id, pw);
            if (bro.IsSuccess())
            {
                Debug.Log("로그인 성공");
                Debug.Log(bro);

                uuid = Backend.UID;
                nickName = Backend.UserNickName;
                Debug.Log("uuid: " + uuid);
                Debug.Log("nickName: " + nickName);
                bro = Backend.BMember.IsAccessTokenAlive();
                if (bro.IsSuccess())
                {
                    Debug.Log("액세스 토큰이 살아있습니다");
                    Backend.BMember.RefreshTheBackendToken();
                }


                ac.Invoke();
            }
            else
            {
                Debug.Log("로그인 실패: " + bro);
                ErroDebug(bro);
            }
        }

        /// <summary>
        ///     게스트 로그인
        /// </summary>
        /// <param name="action">게스트 로그인이 성공 할때 실행할 엑션</param>
        public void GuestLogin(Action action)
        {
            var bro = Backend.BMember.GuestLogin("게스트 로그인으로 로그인함");
            if (bro.IsSuccess())
            {
                Debug.Log("게스트 로그인에 성공했습니다: " + bro);
                uuid = Backend.UID;
                nickName = Backend.UserNickName;
                Debug.Log("uuid: " + uuid);
                Debug.Log("nickName: " + nickName);
                action.Invoke();
                bro = Backend.BMember.IsAccessTokenAlive();
                if (bro.IsSuccess())
                {
                    Debug.Log("액세스 토큰이 살아있습니다");
                    Backend.BMember.RefreshTheBackendToken();
                }
            }
            else
            {
                Debug.Log("게스트 로그인에 실패했습니다: " + bro);
                ErroDebug(bro);
            }
        }

        /// <summary>
        ///     토큰 로그인
        /// </summary>
        /// <param name="action">로그인 성공할때 액션</param>
        /// <param name="action2">로그인 실패할때 액션</param>
        public void TokenLogin(Action onSuccess, Action onFailure)
        {
            var bro = Backend.BMember.LoginWithTheBackendToken();
            if (bro.IsSuccess())
            {
                Debug.Log("자동 로그인에 성공했습니다");
                Debug.Log(bro);

                bro = Backend.BMember.IsAccessTokenAlive();
                if (bro.IsSuccess())
                {
                    Debug.Log("액세스 토큰이 살아있습니다");
                    Backend.BMember.RefreshTheBackendToken();
                    onSuccess.Invoke();
                }
            }
            else
            {
                Debug.Log("자동 로그인에 실패했습니다");
                ErroDebug(bro);
                onFailure.Invoke();
            }
        }

        #endregion

        #region 게임 데이터 저장 및 불러오기

        /// <summary>
        ///     플레이어 데이터 세팅
        /// </summary>
        /// <param name="playerData"></param>
        public void GameDataInsert(PlayerData playerData)
        {
            var param = new Param();
            param.Add("nickname", playerData.nickname);
            param.Add("uid", playerData.UID);
            param.Add("Money1", playerData.currency1);
            param.Add("Money2", playerData.currency2);
            param.Add("experience", playerData.experience);
            param.Add("level", playerData.level);


            var bro = Backend.GameData.Insert("User_Data", param);
            if (bro.IsSuccess())
            {
                _gameDataRowInDate = bro.GetInDate();
                Debug.Log("게임 데이터 저장 성공");
            }
            else
            {
                Debug.Log("게임 데이터 저장 실패");
            }
        }

        /// <summary>
        ///     게임 정보 조회
        /// </summary>
        /// <param name="action">게임 데이터가 존재하지않을떄 실행할 액션</param>
        public void GameDataGet(Action action)
        {
            Debug.Log("게임 정보 조회 함수를 호출합니다.");

            var bro = Backend.GameData.GetMyData("User_Data", new Where());

            if (bro.IsSuccess())
            {
                Debug.Log("게임 정보 조회에 성공했습니다. : " + bro);


                var gameDataJson = bro.FlattenRows(); // Json으로 리턴된 데이터를 받아옵니다.  

                // 받아온 데이터의 갯수가 0이라면 데이터가 존재하지 않는 것입니다.  
                if (gameDataJson.Count <= 0)
                {
                    Debug.LogWarning("데이터가 존재하지 않습니다.: " + bro);
                    action.Invoke();
                }
                else
                {
                    _gameDataRowInDate = gameDataJson[0]["inDate"].ToString(); //불러온 게임 정보의 고유값입니다.  

                    _playerData = ScriptableObject.CreateInstance<PlayerData>();

                    _playerData.level = int.Parse(gameDataJson[0]["level"].ToString());
                    _playerData.currency1 = int.Parse(gameDataJson[0]["Money1"].ToString());
                    _playerData.currency2 = int.Parse(gameDataJson[0]["Money2"].ToString());
                    _playerData.experience = float.Parse(gameDataJson[0]["experience"].ToString());
                    _playerData.UID = gameDataJson[0]["uid"].ToString();
                    // _playerData.nickname = gameDataJson[0]["nickname"].ToString();


                    PlayerDataManagerDontdesytoy.Instance.UpdatePlayerData(_playerData);

                    Debug.Log(_playerData.ToString());

                    Debug.Log(_playerData.ToString());
                }
            }
            else
            {
                Debug.LogError("게임 정보 조회에 실패했습니다. : " + bro);
                ErroDebug(bro);

                if (bro.GetStatusCode() == "404") action.Invoke();
            }
        }


        public void GameDataUpdate(PlayerData playerData)
        {
            var param = new Param();
            param.Add("nickname", playerData.nickname);
            param.Add("uid", playerData.UID);
            param.Add("Money1", playerData.currency1);
            param.Add("Money2", playerData.currency2);
            param.Add("experience", playerData.experience);
            param.Add("level", playerData.level);

            BackendReturnObject bro = null;


            if (string.IsNullOrEmpty(_gameDataRowInDate))
            {
                Debug.Log("내 제일 최신 게임 정보 데이터 수정을 요청합니다.");

                bro = Backend.GameData.Update("User_Data", new Where(), param);
            }
            else
            {
                Debug.Log($"{_gameDataRowInDate}의 게임 정보 데이터 수정을 요청합니다.");

                bro = Backend.GameData.UpdateV2("User_Data", _gameDataRowInDate, Backend.UserInDate, param);
            }

            if (bro.IsSuccess())
                Debug.Log("게임 정보 데이터 수정에 성공했습니다. : " + bro);
            else
                Debug.LogError("게임 정보 데이터 수정에 실패했습니다. : " + bro);
        }

        #endregion

        #region 인벤토리 데이터 저장 및 불러오기

        /// <summary>
        ///     인벤토리 데이터 삽입
        /// </summary>
        public void InventoryDataInsert()
        {
            var param = new Param();
            param.Add("Inventory", InventoryDataManagerDontdestory.Instance.inventorydataString);
            //  param.Add("equipment", _inventoryData.equipment);

            var bro = Backend.GameData.Insert("Inventory_Data", param);
            if (bro.IsSuccess())
                Debug.Log("인벤토리 데이터 삽입 성공: " + InventoryDataManagerDontdestory.Instance.inventorydataString);
            else
                Debug.Log("인벤토리 데이터 저장 실패");
        }

        /// <summary>
        ///     인벤토리 데이터 불러오기
        /// </summary>
        /// <param name="Fail"></param>
        public void Get_Inventory_Data(Action Fail)
        {
            // Backend 호출
            var bro = Backend.GameData.Get("Inventory_Data", new Where());
            if (bro.IsSuccess())
            {
                // JSON 데이터 로드
                var gameDataJson = bro.FlattenRows();

                // 데이터가 없는 경우
                if (gameDataJson.Count <= 0)
                {
                    Debug.LogWarning("데이터가 존재하지 않습니다.: " + bro);
                    Fail.Invoke();
                    return;
                }

                // 최상위 JSON 확인
                var inventoryContainerJson = gameDataJson[0];

                // Inventory 키 확인
                if (!inventoryContainerJson.Keys.Contains("Inventory"))
                {
                    Debug.LogError($"Inventory 키가 없습니다: {inventoryContainerJson.ToJson()}");
                    Fail.Invoke();
                    return;
                }

                // Inventory 문자열 디코딩
                var inventoryJsonString = inventoryContainerJson["Inventory"].ToString();
                var inventoryDecodedJson = JsonMapper.ToObject(inventoryJsonString);

                // inventoryDecodedJson에서 "Inventory" 배열 가져오기
                if (!inventoryDecodedJson.Keys.Contains("Inventory") || !inventoryDecodedJson["Inventory"].IsArray)
                {
                    Debug.LogError($"inventoryDecodedJson에 Inventory 배열이 없습니다: {inventoryDecodedJson.ToJson()}");
                    Fail.Invoke();
                    return;
                }

                var inventoryArray = inventoryDecodedJson["Inventory"];

                // Inventory_Data 생성 및 초기화
                _inventoryData = ScriptableObject.CreateInstance<Inventory_Data>();

                foreach (JsonData entry in inventoryArray)
                    try
                    {
                        // item과 count 확인
                        if (entry.Keys.Contains("item") && entry.Keys.Contains("count"))
                        {
                            var itemDataJson = entry["item"];
                            var count = int.Parse(entry["count"].ToString());

                            // Item_Data 생성
                            var item = ScriptableObject.CreateInstance<Item_Data>();
                            item.itemName = itemDataJson["itemName"].ToString();
                            item.itemCode = int.Parse(itemDataJson["itemCode"].ToString());
                            item.itemtype = itemDataJson["itemtype"].ToString();
                            item.itemCount = int.Parse(itemDataJson["itemCount"].ToString());

                            // 인벤토리에 아이템 추가
                            for (var i = 0; i < count; i++) _inventoryData.AddItem(item);
                        }
                        else
                        {
                            Debug.LogWarning("item 또는 count 키가 없는 항목이 발견되었습니다: " + entry.ToJson());
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"아이템 데이터 처리 중 오류 발생: {ex.Message}\n{entry}");
                    }

                // 로드 완료 로그
                Debug.Log($"<color=green>{_inventoryData.inventory.Count}개의 아이템이 인벤토리에 로드되었습니다.</color>");

                // Inventory_Data_Manager에 데이터 업데이트
                InventoryDataManagerDontdestory.Instance.Update_Inventory_Data(_inventoryData);
            }
            else
            {
                // 실패 처리
                Debug.LogError("인벤토리 데이터 조회 실패: " + bro.GetErrorCode());
                Fail.Invoke();
            }
        }

        /// <summary>
        ///     인벤토리 정보 업데이트
        /// </summary>
        /// <param name="inventoryData"></param>
        public void Inventory_Data_Update()
        {
            var param = new Param();
            param.Add("Inventory", InventoryDataManagerDontdestory.Instance.inventorydataString);

            BackendReturnObject bro = null;

            if (string.IsNullOrEmpty(_gameDataRowInDate))
            {
                Debug.Log("내 제일 최신 인벤토리 정보 데이터 수정을 요청합니다.");

                bro = Backend.GameData.Update("Inventory_Data", new Where(), param);
            }
            else
            {
                Debug.Log($"{_gameDataRowInDate}의 인벤토리 정보 데이터 수정을 요청합니다.");

                bro = Backend.GameData.UpdateV2("Inventory_Data", _gameDataRowInDate, Backend.UserInDate, param);
            }

            if (bro.IsSuccess())
                Debug.Log("인벤토리 정보 데이터 수정에 성공했습니다. : " + bro);
            else
                Debug.LogError("인벤토리 정보 데이터 수정에 실패했습니다. : " + bro);
        }

        #endregion

        #region 메시지 관련

        /// <summary>
        ///     우편함 불러오기 (동기)
        /// </summary>
        public void LoadMessage()
        {
            var bro = Backend.UPost.GetPostList(PostType.Coupon, 10);
            var json = bro.GetReturnValuetoJSON()["postList"];

            for (var i = 0; i < json.Count; i++)
            {
                Debug.Log("제목 : " + json[i]["title"]);
                Debug.Log("inDate : " + json[i]["inDate"]);
            }
        }

        /// <summary>
        ///     우편함 불러오기 (비동기)
        /// </summary>
        public async void LoadMessage2()
        {
            await Task.Run(() =>
            {
                Backend.UPost.GetPostList(PostType.Coupon, 10, callback =>
                {
                    var json = callback.GetReturnValuetoJSON()["postList"];

                    for (var i = 0; i < json.Count; i++)
                    {
                        Debug.Log("제목 : " + json[i]["title"]);
                        Debug.Log("inDate : " + json[i]["inDate"]);
                    }
                });
            });
        }

        /// <summary>
        ///     우편 하나 수령하기
        /// </summary>
        public void GetReward()
        {
            var type = PostType.Admin;

            //우편 리스트 불러오기
            var bro = Backend.UPost.GetPostList(type, 100);
            var json = bro.GetReturnValuetoJSON()["postItems"];

            //우편 리스트중 0번째 우편의 inDate 가져오기
            var recentPostIndate = json[0]["inDate"].ToString();

            // 동일한 PostType의 우편 수령하기
            Backend.UPost.ReceivePostItem(type, recentPostIndate);
        }

        public void GetRewardAll()
        {
            var receiveBro = Backend.UPost.ReceivePostItemAll(PostType.Admin);
            if (receiveBro.IsSuccess() == false)
            {
                Debug.LogError("우편 모두 수령하기 중 에러가 발생하였습니다. : " + receiveBro);
                return;
            }

            foreach (JsonData postItemJson in receiveBro.GetReturnValuetoJSON()["postItems"])
                for (var j = 0; j < postItemJson.Count; j++)
                    if (!postItemJson[j].ContainsKey("item"))
                    {
                    }
        }

        #endregion

        #region 유틸리티 메서드

        /// <summary>
        ///     오류 디버그
        /// </summary>
        /// <param name="bro"></param>
        private void ErroDebug(BackendReturnObject bro)
        {
            // bro = Backend.BMember.CustomLogin;
            print(bro.GetStatusCode());
            print(bro.GetErrorCode());
            print(bro.GetMessage());
        }
        
        #endregion
    }
}