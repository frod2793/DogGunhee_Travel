using InGame.ObjectPool;
using InGame.Mob.Systems;
using UnityEngine;

namespace InGame.Core.Interfaces
{
    /// <summary>
    /// [설명]: 몬스터 스포너, 타겟팅 시스템, 맵 경계 데이터 등 전투 로직에 필요한 컨텍스트 정보를 제공합니다.
    /// </summary>
    public interface ICombatContext
    {
        ObjectPoolSpawner ObjectPoolSpawner { get; }
        MobManager MobManager { get; }
        Bounds MapBounds { get; }
        int ActiveMobCount { get; }
    }
}
