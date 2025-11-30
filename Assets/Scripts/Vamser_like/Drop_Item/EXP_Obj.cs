using UnityEngine;

namespace Vamser_like.vamsir
{
    public class EXP_Obj : DropItemBase
    {
        [Header("고유 설정")]
        [field: SerializeField] public float ExpValue { get; private set; } = 10f; // 경험치 값
    }
}