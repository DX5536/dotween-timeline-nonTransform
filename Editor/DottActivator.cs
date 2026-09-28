using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Dott.Editor
{
    /// <summary>
    /// Inactive GameObjects are not rendered, so their tweens can't be previewed.
    /// This temporarily activates the tween targets (and their inactive parents) while previewing and restores them afterwards.
    /// </summary>
    public class DottActivator : IDisposable
    {
        private readonly List<GameObject> activated = new();

        public void Activate(IDOTweenAnimation[] animations)
        {
            foreach (var animation in animations)
            {
                if (!animation.IsValid || !animation.IsActive) { continue; }

                var objects = animation.PreviewActivationTargets;
                if (objects == null) { continue; }

                foreach (var obj in objects)
                {
                    var go = ToGameObject(obj);
                    if (go == null || EditorUtility.IsPersistent(go) || go.activeInHierarchy) { continue; }

                    for (var t = go.transform; t != null; t = t.parent)
                    {
                        if (t.gameObject.activeSelf) { continue; }

                        t.gameObject.SetActive(true);
                        activated.Add(t.gameObject);
                    }
                }
            }
        }

        public void Restore()
        {
            // Parents were activated after children, so restore in reverse order
            for (var i = activated.Count - 1; i >= 0; i--)
            {
                if (activated[i] != null)
                {
                    activated[i].SetActive(false);
                }
            }

            activated.Clear();
        }

        private static GameObject ToGameObject(Object obj) => obj switch
        {
            GameObject go => go,
            Component component => component.gameObject,
            _ => null
        };

        public void Dispose() => Restore();
    }
}
