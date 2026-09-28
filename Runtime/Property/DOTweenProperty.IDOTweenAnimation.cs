using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Dott
{
    public partial class DOTweenProperty : IDOTweenAnimation
    {
        float IDOTweenAnimation.Delay
        {
            get => delay;
            set => delay = value;
        }

        float IDOTweenAnimation.Duration => duration;
        int IDOTweenAnimation.Loops => loops;

        bool IDOTweenAnimation.IsValid
        {
            get
            {
                var acc = GetAccessor();
                return acc != null && acc.Kind != DottValueKind.Unsupported;
            }
        }

        bool IDOTweenAnimation.IsActive => isActive;
        bool IDOTweenAnimation.HasDynamicValues => fromSource.useReference || toSource.useReference;
        bool IDOTweenAnimation.IsFrom => false;
        // Needed to restore the original value when the preview stops (see CreateEditorPreview)
        bool IDOTweenAnimation.AllowEditorCallbacks => true;
        Component IDOTweenAnimation.Component => this;

        string IDOTweenAnimation.Label
        {
            get
            {
                var infiniteSuffix = loops == -1 ? " ∞" : "";
                if (!string.IsNullOrEmpty(id))
                {
                    return id + infiniteSuffix;
                }

                if (target == null || string.IsNullOrEmpty(propertyPath))
                {
                    return "<i>Property (not set)</i>";
                }

                return $"{target.name}.{propertyPath}{infiniteSuffix}";
            }
        }

        IEnumerable<Object> IDOTweenAnimation.Targets => target != null ? new[] { target } : Enumerable.Empty<Object>();

        IEnumerable<Object> IDOTweenAnimation.PreviewActivationTargets =>
            new[] { dropTarget, target }.Where(o => o != null);

        Tween IDOTweenAnimation.CreateEditorPreview()
        {
            var acc = GetAccessor();
            if (acc == null)
            {
                return null;
            }

            var original = acc.Get();
            var preview = BuildTween();
            // Rewinding a tween with a custom "from" leaves the target at that value, so restore the real original on kill
            preview?.OnKill(() => acc.Set(original));
            return preview;
        }
    }
}
