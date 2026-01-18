using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;

namespace Dott.Editor
{
    public class DottSelection
    {
        private static readonly HashSet<IDOTweenAnimation> selectedAnimations = new HashSet<IDOTweenAnimation>();
        private UnityEditor.Editor editor;

        public IDOTweenAnimation Animation => selectedAnimations.FirstOrDefault();

        public IReadOnlyCollection<IDOTweenAnimation> SelectedAnimations => selectedAnimations;

        public int Count => selectedAnimations.Count;

        public bool IsMultiSelection => selectedAnimations.Count > 1;

        public IDOTweenAnimation FindHead()
        {
            return selectedAnimations.OrderBy(animation => animation.Delay).First();
        }

        public IDOTweenAnimation FindTail()
        {
            return selectedAnimations.OrderByDescending(animation => animation.Loops == -1
                ? float.MaxValue
                : animation.Delay + animation.Duration * Mathf.Max(1, animation.Loops)).First();
        }

        public void Validate(IDOTweenAnimation[] animations)
        {
            var toRemove = selectedAnimations.Where(anim => !animations.Contains(anim)).ToList();
            foreach (var anim in toRemove)
            {
                selectedAnimations.Remove(anim);
            }
        }

        public void Set([CanBeNull] IDOTweenAnimation animation)
        {
            selectedAnimations.Clear();
            if (animation != null)
            {
                selectedAnimations.Add(animation);
            }
            DisposeEditor();
        }

        public void Add(IDOTweenAnimation animation)
        {
            if (animation != null)
            {
                selectedAnimations.Add(animation);
                DisposeEditor();
            }
        }

        public void Remove(IDOTweenAnimation animation)
        {
            if (animation != null)
            {
                selectedAnimations.Remove(animation);
                DisposeEditor();
            }
        }

        public void Toggle(IDOTweenAnimation animation)
        {
            if (animation == null)
            {
                Clear();
                return;
            }

            if (selectedAnimations.Contains(animation))
            {
                Remove(animation);
            }
            else
            {
                Add(animation);
            }
        }

        public void Clear()
        {
            selectedAnimations.Clear();
            DisposeEditor();
        }

        public bool Contains(IDOTweenAnimation animation)
        {
            return animation != null && selectedAnimations.Contains(animation);
        }

        public UnityEditor.Editor GetAnimationEditor()
        {
            var animation = Animation;
            if (editor != null && (animation == null || editor.target != animation.Component))
            {
                DisposeEditor();
            }

            if (animation == null)
            {
                return null;
            }

            if (editor == null)
            {
                editor = UnityEditor.Editor.CreateEditor(animation.Component);
            }

            return editor;
        }

        public void Dispose()
        {
            if (editor != null)
            {
                DisposeEditor();
            }
        }

        private void DisposeEditor()
        {
            if (editor != null)
            {
                Object.DestroyImmediate(editor);
                editor = null;
            }
        }
    }
}