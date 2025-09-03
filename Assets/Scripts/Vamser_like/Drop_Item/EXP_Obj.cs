using UnityEngine;

namespace DogGuns_Games.vamsir
{
    public class EXP_Obj : DropItemBase ,IObjectPoolSpawnerSettable 
    {
        [Header("고유 설정")]
        [field: SerializeField] public float ExpValue { get; private set; } = 10f; // 경험치 값

        public ObjectPoolSpawner objectPoolSpawner { get; set; }
    }
}