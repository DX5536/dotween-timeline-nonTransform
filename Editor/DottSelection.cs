using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Dott.Editor
{
    public class DottSelection
    {
        private static readonly List<IDOTweenAnimation> Animations = new();
        private UnityEditor.Editor editor;

        /// Last selected animation. Only it is shown in the inspector.
        public IDOTweenAnimation Animation => Animations.Count > 0 ? Animations[^1] : null;
        public IReadOnlyList<IDOTweenAnimation> All => Animations;
        public int Count => Animations.Count;

        public bool Contains(IDOTweenAnimation animation) => Animations.Contains(animation);

        public void Validate(IDOTweenAnimation[] animations)
        {
            Animations.RemoveAll(animation => !animations.Contains(animation));
        }

        public void Set(IDOTweenAnimation animation)
        {
            Animations.Clear();
            if (animation != null)
            {
                Animations.Add(animation);
            }
        }

        public void Set(IEnumerable<IDOTweenAnimation> animations)
        {
            Animations.Clear();
            Animations.AddRange(animations.Where(animation => animation != null));
        }

        /// Removes the animation if it is already selected, adds it otherwise.
        public void Toggle(IDOTweenAnimation animation)
        {
            if (animation == null) { return; }

            if (!Animations.Remove(animation))
            {
                Animations.Add(animation);
            }
        }

        public void Clear() => Animations.Clear();

        public UnityEditor.Editor GetAnimationEditor()
        {
            var animation = Animation;
            if (animation == null)
            {
                DisposeEditor();
                return null;
            }

            if (editor != null && editor.target != animation.Component)
            {
                DisposeEditor();
            }

            if (editor == null)
            {
                editor = UnityEditor.Editor.CreateEditor(animation.Component);
            }

            return editor;
        }

        public void Dispose()
        {
            DisposeEditor();
        }

        private void DisposeEditor()
        {
            if (editor == null) { return; }

            Object.DestroyImmediate(editor);
            editor = null;
        }
    }
}
