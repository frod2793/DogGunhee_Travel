using Cysharp.Threading.Tasks;

namespace InGame.Mob.Systems
{
    /// <summary>
    /// 몬스터의 '지능'을 담당하는 추상 포코(POCO) 클래스입니다.
    /// <br/> 어떤 행동을 할지 결정(Decision Making)하며, 비헤이비어 트리나 FSM을 소유합니다.
    /// </summary>
    public abstract class MobBrain
    {
        #region 핵심 참조
        
        protected readonly MobLogic m_logic;
        protected readonly MobView m_view;
        
        #endregion

        protected MobBrain(MobLogic logic, MobView view)
        {
            m_logic = logic;
            m_view = view;
        }

        /// <summary>
        /// 브레인을 초기화합니다. BT 구성 등이 여기서 이루어집니다.
        /// </summary>
        public abstract void Initialize();

        /// <summary>
        /// 매 프레임 AI 사고 루틴을 실행합니다.
        /// </summary>
        public abstract UniTask EvaluateAsync();
        
        /// <summary>
        /// 브레인이 활성화될 때 호출됩니다.
        /// </summary>
        public virtual void OnEnable() { }
        
        /// <summary>
        /// 브레인이 비활성화될 때 호출됩니다.
        /// </summary>
        public virtual void OnDisable() { }
    }
}
