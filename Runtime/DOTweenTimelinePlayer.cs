using DG.Tweening;
using UnityEngine;

namespace Dott
{
    [AddComponentMenu("DOTween/DOTween Timeline Player")]
    [RequireComponent(typeof(DOTweenTimeline))]
    public class DOTweenTimelinePlayer : MonoBehaviour
    {
        public enum PlayDirection
        {
            Forward,
            // Jumps to the end state and plays the timeline in reverse
            Backwards
        }

        [SerializeField] private bool playOnEnable = true;
        [SerializeField] private PlayDirection direction = PlayDirection.Forward;
        [SerializeField] private int loops = 1;

        private DOTweenTimeline timeline;

        private void Awake() => timeline = GetComponent<DOTweenTimeline>();

        private void OnEnable()
        {
            if (!playOnEnable)
            {
                return;
            }

            var sequence = direction == PlayDirection.Forward ? timeline.Play() : timeline.PlayBackwardsFromEnd();
            sequence.SetLoops(loops);
        }
    }
}
