using Cysharp.Threading.Tasks;

namespace InGame.Mob.Systems
{
    /// <summary>
    /// [설명]: 몬스터의 '지능'을 담당하는 추상 기반 클래스입니다.
    /// 어떤 행동을 할지 결정(Decision Making)하며, 비헤이비어 트리나 FSM의 실행 주체가 됩니다.
    /// </summary>
    public abstract class MobBrain
    {
        #region 내부 상속 필드

        /// <summary> 몬스터 비즈니스 로직 참조 </summary>
        protected readonly MobLogic m_logic;

        /// <summary> 몬스터 뷰(애니메이션 등 시각화) 참조 </summary>
        protected readonly MobView m_view;

        #endregion

        #region 초기화

        /// <summary>
        /// [설명]: 브레인 객체를 초기화하고 필수 의존성을 주입받습니다.
        /// </summary>
        protected MobBrain(MobLogic logic, MobView view)
        {
            m_logic = logic;
            m_view = view;
        }

        #endregion

        #region 추상 메서드

        /// <summary>
        /// [설명]: 브레인을 초기화합니다. 비헤이비어 트리(BT) 구성 등이 여기서 이루어집니다.
        /// </summary>
        public abstract void Initialize();

        /// <summary>
        /// [설명]: 매 사고 주기마다 AI 루틴을 비동기로 평가합니다.
        /// </summary>
        public abstract UniTask EvaluateAsync();

        #endregion

        #region 가상 메서드 (생명주기)

        /// <summary>
        /// [설명]: 브레인이 활성화될 때(몬스터 스폰 시 등) 호출됩니다.
        /// </summary>
        public virtual void OnEnable()
        {
        }

        /// <summary>
        /// [설명]: 브레인이 비활성화될 때(몬스터 사망/언풀링 등) 호출됩니다.
        /// </summary>
        public virtual void OnDisable()
        {
        }

        #endregion
    }
}
