using DG.Tweening;
using JetBrains.Annotations;
using UnityEngine;

namespace Dott
{
    [AddComponentMenu("DOTween/DOTween Timeline")]
    public class DOTweenTimeline : MonoBehaviour
    {
        [CanBeNull] public Sequence Sequence { get; private set; }

        public bool IsPlaying => Sequence != null && Sequence.IsActive() && Sequence.IsPlaying();
        public bool IsBackwards => Sequence != null && Sequence.IsActive() && Sequence.IsBackwards();
        public bool IsComplete => Sequence != null && Sequence.IsActive() && Sequence.IsComplete();

        #region Playback

        // Do not override the onKill callback because it is used internally to reset the Sequence

        /// <summary>Plays the timeline forward from its current position. Restarts it if it has already finished.</summary>
        public Sequence Play()
        {
            // Tweens that read live values (DOTween Property "Ref") are rebuilt when starting from the beginning
            if (Sequence != null && HasDynamicValues() && !Sequence.IsPlaying() && (Sequence.IsComplete() || Sequence.Elapsed() <= 0))
            {
                Kill();
            }

            TryGenerateSequence();
            if (Sequence.IsComplete() && !Sequence.IsBackwards())
            {
                Sequence.Restart();
                return Sequence;
            }

            Sequence.PlayForward();
            return Sequence;
        }

        /// <summary>Plays the timeline backwards from its current position (does nothing if it is already at the start).</summary>
        public Sequence PlayBackwards()
        {
            TryGenerateSequence();
            Sequence.PlayBackwards();
            return Sequence;
        }

        /// <summary>Jumps to the end state, then plays the timeline backwards. Handy for "close" animations that were never played.</summary>
        public Sequence PlayBackwardsFromEnd()
        {
            TryGenerateSequence();
            Sequence.Complete();
            Sequence.PlayBackwards();
            return Sequence;
        }

        /// <summary>Plays forward, or backwards if the timeline is currently at its end / playing forward.</summary>
        public Sequence Toggle()
        {
            TryGenerateSequence();
            return Sequence.IsBackwards() ? Play() : PlayBackwards();
        }

        public Sequence Restart()
        {
            if (Sequence != null && HasDynamicValues())
            {
                Kill();
            }

            TryGenerateSequence();
            Sequence.Restart();
            return Sequence;
        }

        /// <summary>Rewinds to the start instantly.</summary>
        public void Rewind() => Sequence?.Rewind();

        /// <summary>Rewinds to the start by playing backwards at the timeline's own speed.</summary>
        public void SmoothRewind() => Sequence?.SmoothRewind();

        /// <summary>Jumps to the end, applying all the tweens.</summary>
        public void Complete() => Sequence?.Complete();

        public void Pause() => Sequence?.Pause();
        public void Resume() => Sequence?.Play();
        public void TogglePause() => Sequence?.TogglePause();

        /// <summary>Inverts the playback direction.</summary>
        public void Flip() => Sequence?.Flip();

        /// <summary>Goes to the given time (in seconds) and pauses.</summary>
        public void GoTo(float time)
        {
            TryGenerateSequence();
            Sequence.Goto(time);
            Sequence.Pause();
        }

        /// <summary>Kills the generated sequence, so the next Play() rebuilds it (e.g. after changing the tweens at runtime).</summary>
        public void Kill()
        {
            Sequence?.Kill();
            Sequence = null;
        }

        private bool HasDynamicValues()
        {
            foreach (var component in GetComponents<MonoBehaviour>())
            {
                if (component is IDOTweenAnimation { IsActive: true, HasDynamicValues: true })
                {
                    return true;
                }
            }

            return false;
        }

        // Wrappers for UnityEvent (requires void return type)
        public void DOPlay() => Play();
        public void DOPlayBackwards() => PlayBackwards();
        public void DOPlayBackwardsFromEnd() => PlayBackwardsFromEnd();
        public void DOToggle() => Toggle();
        public void DORestart() => Restart();
        public void DORewind() => Rewind();
        public void DOSmoothRewind() => SmoothRewind();
        public void DOComplete() => Complete();
        public void DOPause() => Pause();
        public void DOResume() => Resume();
        public void DOTogglePause() => TogglePause();
        public void DOFlip() => Flip();
        public void DOKill() => Kill();

        #endregion

        /// <summary>Used by <see cref="DOTweenLink"/>: always hands out a fresh sequence that can be nested into another one.</summary>
        internal Sequence PlayFresh()
        {
            Sequence?.Kill();
            return Play();
        }

        private void TryGenerateSequence()
        {
            if (Sequence != null) { return; }

            Sequence = DOTween.Sequence();
            // Keep the sequence after it completes, so it can be played backwards or replayed
            Sequence.SetAutoKill(false);
            Sequence.SetLink(gameObject, LinkBehaviour.KillOnDestroy);
            Sequence.OnKill(() => Sequence = null);
            var components = GetComponents<MonoBehaviour>();
            foreach (var component in components)
            {
                switch (component)
                {
                    case DOTweenAnimation animation:
                        if (!animation.isValid || !animation.isActive) continue;

                        animation.CreateTween(regenerateIfExists: true);
                        Sequence.Insert(0, animation.tween);
                        break;

                    case IDOTweenAnimation animation:
                        if (!animation.IsValid || !animation.IsActive) continue;

                        var tween = animation.CreateTween(regenerateIfExists: true);
                        if (tween == null) continue;

                        Sequence.Insert(0, tween);
                        break;
                }
            }

            // Wait for an explicit Play / PlayBackwards call
            Sequence.Pause();
        }

        private void OnDestroy()
        {
            // Already handled by SetLink, but needed to avoid warnings from children DOTweenAnimation.OnDestroy
            Sequence?.Kill();
        }

        public void OnValidate()
        {
            foreach (var doTweenAnimation in GetComponents<DOTweenAnimation>())
            {
                doTweenAnimation.autoGenerate = false;
            }
        }
    }
}
