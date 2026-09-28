using UnityEngine;

namespace Dott.Sample
{
    // A component that references a ScriptableObject: drop this GameObject into DOTween Property and pick
    // "DottSampleStatsHolder > stats [DottSampleStats]" in the Component dropdown, or a "stats.health" style path.
    public class DottSampleStatsHolder : MonoBehaviour
    {
        [SerializeField] private DottSampleStats stats;
        [SerializeField] private float glow;
    }
}
