using UnityEngine;

namespace InGame
{
    public enum ModificationMode
    {
        Add,
        Multiply
    }

    [System.Serializable]
    public class StatModification
    {
        [Tooltip("변경할 스탯의 이름입니다. (예: Damage, Cooldown)")]
        public string StatName;

        [Tooltip("변경할 수치입니다.")]
        public float Value;

        [Tooltip("수치 적용 방식입니다. (더하기/곱하기)")]
        public ModificationMode Mode;
    }
}