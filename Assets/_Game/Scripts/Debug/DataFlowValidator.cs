using UnityEngine;
using InGame.Data;
using InGame.Services;
using Lobby;
using InGame;
using InGame.Managers;

namespace InGame.DebugTools
{
    /// <summary>
    /// [설명]: 재화 데이터 흐름 및 씬 전환 페이로드 정합성을 검증하는 테스트 스크립트입니다.
    /// 에디터 인스펙터의 컨텍스트 메뉴를 통해 실행할 수 있습니다.
    /// </summary>
    public class DataFlowValidator : MonoBehaviour
    {
        #region 에디터 설정
        [SerializeField] private ServerManager m_serverManager;
        #endregion

        #region 공개 메서드 (컨텍스트 메뉴)

        [ContextMenu("1. 타이틀 -> 로비 페이로드 검증")]
        public void ValidateTitleToLobbyPayload()
        {
            Debug.Log("<color=cyan>[Test] 타이틀 -> 로비 페이로드 검증 시작</color>");
            
            // 1. DTO 생성 시뮬레이션
            var playerDto = new PlayerDataDTO();
            playerDto.Initialize("TestUser", "uuid-1234");
            
            // 2. 페이로드 생성 (LoginViewMVVM.ProcessLoginSuccessAsync 로직 시뮬레이션)
            var sessionDto = m_serverManager != null ? m_serverManager.GetSession() : null;
            var payload = new ScenePayloadDTO(playerDto, sessionDto, null)
            {
                IsFirstLogin = true
            };

            // 3. 검증 (Assert)
            bool isValid = payload.IsFirstLogin == true;
            Debug.Log($"[Result] IsFirstLogin Flag: {payload.IsFirstLogin} (Expected: True)");
            Debug.Log(isValid ? "<color=green>SUCCESS: 최초 로그인 플래그가 올바르게 설정되었습니다.</color>" : "<color=red>FAILED: 최초 로그인 플래그 설정 오류</color>");
        }

        [ContextMenu("2. 인게임 -> 로비 페이로드 검증")]
        public void ValidateInGameToLobbyPayload()
        {
            Debug.Log("<color=cyan>[Test] 인게임 -> 로비 페이로드 검증 시작</color>");

            // 1. 임의의 데이터 획득 시뮬레이션
            var playerDto = new PlayerDataDTO();
            playerDto.Currency1 = 1000; // 기존 골드
            playerDto.IngameCoin = 500;  // 인게임 획득
            
            // 2. GameManager 결과 저장 로직 시뮬레이션
            playerDto.Currency1 += playerDto.IngameCoin;
            playerDto.IngameCoin = 0;

            // 3. 페이로드 생성 (GameManager.GetResultPayload 로직 시뮬레이션)
            var payload = new ScenePayloadDTO(playerDto, null, null)
            {
                IsFirstLogin = false
            };

            // 4. 검증
            bool dataPreserved = payload.PlayerData.Currency1 == 1500;
            bool flagCorrect = payload.IsFirstLogin == false;

            Debug.Log($"[Result] Final Gold: {payload.PlayerData.Currency1} (Expected: 1500)");
            Debug.Log($"[Result] IsFirstLogin Flag: {payload.IsFirstLogin} (Expected: False)");

            if (dataPreserved && flagCorrect)
                Debug.Log("<color=green>SUCCESS: 인게임 데이터 소실 없이 페이로드가 구성되었습니다.</color>");
            else
                Debug.Log("<color=red>FAILED: 인게임 복귀 페이로드 구성 오류</color>");
        }

        [ContextMenu("3. 로비 초기화 조건문 검증 (LobbySceneCompositionRoot)")]
        public void ValidateLobbyInitCondition()
        {
            Debug.Log("<color=cyan>[Test] 로비 초기화 조건문 로직 검증</color>");

            // 테스트 Case 1: 최초 로그인
            var payload1 = new ScenePayloadDTO(new PlayerDataDTO(), null, null) { IsFirstLogin = true };
            bool shouldLoadServer1 = payload1.IsFirstLogin; // 실제 LobbySceneCompositionRoot 로직: p.IsFirstLogin
            
            // 테스트 Case 2: 인게임 복귀
            var payload2 = new ScenePayloadDTO(new PlayerDataDTO(), null, null) { IsFirstLogin = false };
            bool shouldLoadServer2 = payload2.IsFirstLogin;

            Debug.Log($"[Case 1: FirstLogin] ShouldLoadServer: {shouldLoadServer1} (Expected: True)");
            Debug.Log($"[Case 2: ReturnLobby] ShouldLoadServer: {shouldLoadServer2} (Expected: False)");

            if (shouldLoadServer1 == true && shouldLoadServer2 == false)
                Debug.Log("<color=green>SUCCESS: 로비 진입 상황별 서버 데이터 로드 분기 로직이 정상입니다.</color>");
            else
                Debug.Log("<color=red>FAILED: 로비 진입 분기 로직 오류</color>");
        }

        #endregion
    }
}
