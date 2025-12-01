using System.Collections.Generic;
using UnityEngine;
using Vamser_like.vamsir;

namespace Vamser_like
{
    [System.Serializable]
    public class EffectMapping
    {
        public EffectType type;
        public GameObject prefab;
    }

    [CreateAssetMenu(fileName = "EffectData", menuName = "VamserLike/Effect Data", order = 0)]
    public class EffectData : ScriptableObject
    {
        public List<EffectMapping> effects;
    }
}