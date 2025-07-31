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
        private Dictionary<string, string> _tableInDate = new Dictionary<string, string>();

        #endregion

        #region 싱글톤 패턴 (DontDestroyOnLoad)

        private static ServerManager instance;
        private static readonly object padlock = new object();

        public static ServerManager Instance
        {
            get
            {
                lock (padlock)
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
        }

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
            Init ();
        }

        #endregion

        #region 초기화 메서드

        /// <summary>
        ///     뒤끝 서버 초기화
        /// </summary>
        private void Init()
        {
            var bro = Backend.Initialize();
            if (bro.IsSuccess())
            {
                LogManager.Log("뒤끝 초기화 성공");
            }
            else
            {
                LogManager.LogError("뒤끝 초기화 실패: " + bro);
            }
        }

        #endregion

        #region 로그인 및 회원 가입

        /// <summary>
        ///     로그인
        /// </summary>
        /// <param name="id"></param>
        /// <param name="pw"></param>
        /// <param name="action">로그인 성공시 실행할액션 </param>
        public void Login(string id, string pw, Action action)
        {
            Backend.BMember.CustomLogin(id, pw, bro =>
            {
                if (bro.IsSuccess())
                {
                    LogManager.Log("로그인 성공");
                    LogManager.Log(bro.ToString());

                    uuid = Backend.UID;
                    nickName = Backend.UserNickName;
                    LogManager.Log("uuid: " + uuid);
                    LogManager.Log("nickName: " + nickName);
                    bro = Backend.BMember.IsAccessTokenAlive();
                    if (bro.IsSuccess())
                    {
                        LogManager.Log("액세스 토큰이 살아있습니다");
                        Backend.BMember.RefreshTheBackendToken();
                    }


                    action.Invoke();
                }
                else
                {
                    LogManager.LogError("로그인 실패: " + bro);
                }
            });
        }

        /// <summary>
        ///     게스트 로그인
        /// </summary>
        /// <param name="action">게스트 로그인이 성공 할때 실행할 엑션</param>
        public void GuestLogin(Action action)
        {
            Backend.BMember.GuestLogin(bro =>
            {
                if (bro.IsSuccess())
                {
                    LogManager.Log("게스트 로그인에 성공했습니다: " + bro);
                    uuid = Backend.UID;
                    nickName = Backend.UserNickName;
                    LogManager.Log("uuid: " + uuid);
                    LogManager.Log("nickName: " + nickName);
                    action.Invoke();
                    bro = Backend.BMember.IsAccessTokenAlive();
                    if (bro.IsSuccess())
                    {
                        LogManager.Log("액세스 토큰이 살아있습니다");
                        Backend.BMember.RefreshTheBackendToken();
                    }
                }
                else
                {
                    LogManager.LogError("게스트 로그인 실패: " + bro);
                    Backend.BMember.DeleteGuestInfo();
                }
            });
        }

        /// <summary>
        ///     토큰 로그인
        /// </summary>
        /// <param name="action">로그인 성공할때 액션</param>
        public void TokenLogin(Action onSuccess, Action onFailure)
        {
            var bro = Backend.BMember.LoginWithTheBackendToken();
            if (bro.IsSuccess())
            {
                LogManager.Log("자동 로그인에 성공했습니다");
                LogManager.Log(bro.ToString());
                
                uuid = Backend.UID;
                nickName = Backend.UserNickName;

                bro = Backend.BMember.IsAccessTokenAlive();
                if (bro.IsSuccess())
                {
                    LogManager.Log("액세스 토큰이 살아있습니다");
                    Backend.BMember.RefreshTheBackendToken();
                    onSuccess.Invoke();
                }
            }
            else
            {
                LogManager.LogError("자동 로그인에 실패했습니다");
                ErroDebug(bro);
                onFailure.Invoke();
            }
        }

        /// <summary>
        ///     회원 가입
        /// </summary>
        /// <param name="id"></param>
        /// <param name="pw"></param>
        /// <param name="nickname"></param>
        /// <param name="action">회원가입 성공할때 액션</param>
        public void SignUp(string id, string pw, string nickname, Action action)
        {
            Backend.BMember.CustomSignUp(id, pw, bro =>
            {
                if (bro.IsSuccess())
                {
                    LogManager.Log("회원가입 성공: " + bro);
                    bro = Backend.BMember.UpdateNickname(nickname);
                    if (bro.IsSuccess())
                    {
                        LogManager.Log("닉네임 변경 성공: " + bro);
                        action.Invoke();
                    }
                    else
                    {
                        LogManager.LogError("닉네임 변경 실패: " + bro);
                        ErroDebug(bro);
                    }
                }
                else
                {
                    LogManager.LogError("회원가입 실패: " + bro);
                    ErroDebug(bro);
                }
            });
        }

        #endregion

        #region 게임 데이터 저장 및 불러오기 (범용)

        /// <summary>
        /// 지정된 테이블의 데이터를 서버에 업로드합니다. (Insert or Update)
        /// </summary>
        /// <param name="tableName">테이블 이름</param>
        /// <param name="param">업로드할 데이터</param>
        /// <param name="callback">완료 시 콜백</param>
        public void UploadData(string tableName, Param param, Action<BackendReturnObject> callback = null)
        {
            // 이전에 해당 테이블의 데이터를 불러온 적이 있는지 확인
            if (_tableInDate.ContainsKey(tableName))
            {
                // 데이터 수정
                string inDate = _tableInDate[tableName];
                LogManager.Log($"{tableName} 테이블의 데이터 수정을 요청합니다. (inDate: {inDate})");
                Backend.GameData.UpdateV2(tableName, inDate, Backend.UserInDate, param, bro => callback?.Invoke(bro));
            }
            else
            {
                // 데이터 삽입
                LogManager.Log($"{tableName} 테이블에 새 데이터 삽입을 요청합니다.");
                Backend.GameData.Insert(tableName, param, bro =>
                {
                    if (bro.IsSuccess())
                    {
                        // 삽입 성공 시, 다음부터는 Update를 할 수 있도록 inDate 저장
                        _tableInDate[tableName] = bro.GetInDate();
                    }
                    callback?.Invoke(bro);
                });
            }
        }

        /// <summary>
        /// 지정된 테이블에서 내 데이터를 다운로드합니다.
        /// </summary>
        /// <param name="tableName">테이블 이름</param>
        /// <param name="callback">완료 시 콜백</param>
        public void DownloadData(string tableName, Action<BackendReturnObject> callback)
        {
            LogManager.Log($"{tableName} 테이블의 데이터 조회를 요청합니다.");
            Backend.GameData.GetMyData(tableName, new Where(), bro =>
            {
                if (bro.IsSuccess())
                {
                    var gameDataJson = bro.FlattenRows();
                    if (gameDataJson.Count > 0)
                    {
                        // 데이터 조회 성공 시, 다음부터는 Update를 할 수 있도록 inDate 저장
                        _tableInDate[tableName] = gameDataJson[0]["inDate"].ToString();
                    }
                }
                callback?.Invoke(bro);
            });
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
                LogManager.Log("제목 : " + json[i]["title"]);
                LogManager.Log("inDate : " + json[i]["inDate"]);
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
                        LogManager.Log("제목 : " + json[i]["title"]);
                        LogManager.Log("inDate : " + json[i]["inDate"]);
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
                LogManager.LogError($"우편 모두 수령하기 중 에러가 발생하였습니다. : {receiveBro}");
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
            LogManager.LogError($"StatusCode: {bro.GetStatusCode()}");
            LogManager.LogError($"ErrorCode: {bro.GetErrorCode()}");
            LogManager.LogError($"Message: {bro.GetMessage()}");
        }

        #endregion
    }
}
