namespace Vamser_like
{
    public enum ModificationMode
    {
        Add,
        Multiply
    }

    [System.Serializable]
    public class StatModification
    {
        public string StatName;
        public float Value;
        public ModificationMode Mode;
    }
}