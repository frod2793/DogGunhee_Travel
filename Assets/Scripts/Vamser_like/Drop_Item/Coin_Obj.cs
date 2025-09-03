using UnityEngine;

namespace DogGuns_Games.vamsir
{
    public class Coin_Obj : DropItemBase,IObjectPoolSpawnerSettable 
    {
        [Header("고유 설정")]
        [field: SerializeField] public int CoinValue { get; private set; } = 1; // 코인 값

        public ObjectPoolSpawner objectPoolSpawner { get; set; }
    }
}