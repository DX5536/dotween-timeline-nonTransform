using UnityEngine;

namespace Dott.Sample
{
    // Example ScriptableObject: its members can be tweened with DOTween Property (directly, or through DottSampleStatsHolder)
    [CreateAssetMenu(menuName = "DOTween Timeline/Sample Stats")]
    public class DottSampleStats : ScriptableObject
    {
        public int health = 100;
        public float speed = 1f;
        public string title = "Hero";
        public Color tint = Color.white;

        // Properties with a public setter are listed too
        public float Energy { get; set; }
    }
}
