using UnityEngine;

namespace InGame.vamsir
{
    public class Coin_Obj : DropItemBase 
    {
        [Header("고유 설정")]
        [field: SerializeField] public int CoinValue { get; private set; } = 1; // 코인 값

    }
}