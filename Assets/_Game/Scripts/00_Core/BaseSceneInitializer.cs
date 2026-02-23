using UnityEngine;
using Cysharp.Threading.Tasks;

namespace InGame.Core
{
    #region 초기화 인터페이스
    /// <summary>
    /// [설명]: 씬 진입 시 초기화 로직을 수행하는 인터페이스입니다.
    /// SceneLoader를 통해 씬 전환 시 데이터를 전달받을 수 있습니다.
    /// </summary>
    public interface ISceneInitializer
    {
        /// <summary>
        /// [설명]: 씬 로드 직후 호출되며, 전달받은 데이터를 주입받습니다.
        /// </summary>
        /// <param name="payload">이전 씬으로부터 전달된 데이터 DTO</param>
        UniTask OnInitialize(object payload);
    }
    #endregion

    #region 기본 추상 클래스
    /// <summary>
    /// [설명]: 모든 씬 이니셜라이저의 기반이 되는 추상 클래스입니다.
    /// </summary>
    public abstract class BaseSceneInitializer : MonoBehaviour, ISceneInitializer
    {
        public abstract UniTask OnInitialize(object payload);

        protected virtual void Awake()
        {
            // 직접 씬을 실행했을 때를 대비한 기본 초기화 로직이 필요할 수 있음
        }
    }
    #endregion
}
