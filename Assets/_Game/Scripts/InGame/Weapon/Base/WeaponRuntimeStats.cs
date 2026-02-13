using R3;
using UnityEngine;

namespace InGame.Weapon.Base
{
    /// <summary>
    /// 무기의 런타임 스탯(레벨, 데미지, 쿨타임 등)을 관리하는 순수 데이터 클래스입니다.
    /// <br/> WeaponDataSO의 정적 데이터를 기반으로 초기화되며, 게임 진행 중 변경되는 수치를 관리합니다.
    /// <br/> R3 ReactiveProperty를 통해 주요 스탯 변화를 구독할 수 있습니다.
    /// </summary>
    public class WeaponRuntimeStats
    {
        #region 1. 내부 상태 및 변수 (Fields)

        // 주요 스탯은 반응형으로 관리하여 UI나 로직에서 구독 가능하게 함
        private readonly ReactiveProperty<float> m_attackPowerRP = new(0f);
        private readonly ReactiveProperty<int> m_currentLevelRP = new(1);

        #endregion

        #region 2. 공개 프로퍼티 (Properties)

        /// <summary>
        /// 무기의 정적 데이터 원본 (ScriptableObject)
        /// </summary>
        public WeaponDataSO Data { get; private set; }

        /// <summary>
        /// 현재 무기 레벨 (변경 시 ReactiveProperty 알림 발생)
        /// </summary>
        public int CurrentLevel
        {
            get => m_currentLevelRP.Value;
            private set => m_currentLevelRP.Value = value;
        }

        /// <summary>
        /// 최종 계산된 공격력 (변경 시 ReactiveProperty 알림 발생)
        /// </summary>
        public float AttackPower
        {
            get => m_attackPowerRP.Value;
            set => m_attackPowerRP.Value = value;
        }

        /// <summary>
        /// 최종 계산된 재사용 대기시간 (초)
        /// </summary>
        public float CoolTime { get; set; }

        /// <summary>
        /// 최종 계산된 공격 속도 배율
        /// </summary>
        public float AttackSpeed { get; set; }

        /// <summary>
        /// 최종 계산된 공격 사거리
        /// </summary>
        public float AttackRange { get; set; }

        /// <summary>
        /// 효과 지속 시간
        /// </summary>
        public float Duration { get; set; }

        /// <summary>
        /// 발사체 개수
        /// </summary>
        public int ProjectileCount { get; set; }

        /// <summary>
        /// 적 타격 시 기절 시간
        /// </summary>
        public float MobStunTime { get; set; }

        /// <summary>
        /// 무기 진화 여부
        /// </summary>
        public bool IsEvolved { get; set; }

        #endregion

        #region 3. 하위 호환성 프로퍼티 (Legacy Aliases)

        // 기존 코드와의 호환성을 위해 유지 (가능하다면 점진적으로 위 프로퍼티로 대체 권장)
        public float CurrentAttackPower => AttackPower;
        public float CurrentCoolTime => CoolTime;
        public float CurrentAttackSpeed => AttackSpeed;
        public float CurrentAttackRange => AttackRange;
        public int CurrentProjectileCount => ProjectileCount;
        public float CurrentDuration => Duration;

        #endregion

        #region 4. 반응형 프로퍼티 (Reactive Streams)

        /// <summary>
        /// 공격력 변화를 감지하는 읽기 전용 스트림
        /// </summary>
        public ReadOnlyReactiveProperty<float> AttackPowerRP => m_attackPowerRP;

        /// <summary>
        /// 레벨 변화를 감지하는 읽기 전용 스트림
        /// </summary>
        public ReadOnlyReactiveProperty<int> CurrentLevelRP => m_currentLevelRP;

        #endregion

        #region 5. 초기화 및 로직 (Initialization & Logic)

        /// <summary>
        /// 무기 데이터를 기반으로 런타임 스탯을 초기화합니다.
        /// </summary>
        /// <param name="data">기초 무기 데이터</param>
        public WeaponRuntimeStats(WeaponDataSO data)
        {
            Data = data;
            ResetStats();
        }

        /// <summary>
        /// 모든 스탯을 데이터 원본(Level 1) 기준으로 재설정합니다.
        /// </summary>
        public void ResetStats()
        {
            if (Data == null)
            {
                Debug.LogWarning("[WeaponRuntimeStats] Data가 없어 초기화할 수 없습니다.");
                return;
            }

            // 반응형 프로퍼티 값 설정
            CurrentLevel = 1;
            AttackPower = Data.BaseAttackPower;

            // 일반 프로퍼티 값 설정
            CoolTime = Data.BaseCoolTime;
            AttackSpeed = Data.BaseAttackSpeed;
            AttackRange = Data.BaseAttackRange;
            Duration = Data.BaseDuration;
            ProjectileCount = Data.BaseProjectileCount;
            
            // 고정값 및 상태 초기화
            MobStunTime = Data.BaseStunDuration;
            IsEvolved = false;
        }

        /// <summary>
        /// 무기 레벨을 갱신하고, 필요 시 진화 상태를 업데이트합니다.
        /// <br/> (추후 스탯 재계산 로직이 이곳에 추가되어야 합니다.)
        /// </summary>
        /// <param name="newLevel">변경할 목표 레벨</param>
        public void LevelUp(int newLevel)
        {
            CurrentLevel = newLevel;

            // 6레벨 이상일 경우 진화 처리 (기획에 따라 조건 변경 가능)
            if (CurrentLevel >= 6)
            {
                IsEvolved = true;
            }

            // TODO: WeaponDataSO의 성장 테이블(Growth Table)을 참조하여 
            // AttackPower, CoolTime 등의 수치를 레벨에 맞게 재계산하는 로직이 필요합니다.
            // 예: AttackPower = Data.BaseAttackPower + (Data.GrowthPerLevel * (CurrentLevel - 1));
        }

        #endregion
    }
}