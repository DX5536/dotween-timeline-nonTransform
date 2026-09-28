using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Dott
{
    /// <summary>
    /// Optional "live" value for a tween's From / To: instead of a hard-coded number, read a member of another object
    /// (e.g. Slider.maxValue) when the tween is created.
    /// </summary>
    [Serializable]
    public class DottValueSource
    {
        public bool useReference;
        // The object as it was dropped in the inspector (GameObject, Component or ScriptableObject)
        public Object dropTarget;
        // The component / ScriptableObject that owns the member
        public Object target;
        public string propertyPath;

        private DottMembers.Accessor accessor;
        private Object accessorTarget;
        private string accessorPath;

        public DottMembers.Accessor GetAccessor()
        {
            if (accessor == null || accessorTarget != target || accessorPath != propertyPath || !accessor.IsAlive)
            {
                accessor = DottMembers.CreateAccessor(target, propertyPath);
                accessorTarget = target;
                accessorPath = propertyPath;
            }

            return accessor;
        }

        /// <summary>Returns the referenced value if it is set and has type <typeparamref name="T"/>, otherwise <paramref name="constant"/>.</summary>
        public T Resolve<T>(T constant)
        {
            if (!useReference) return constant;

            var value = GetAccessor()?.Get();
            return value is T typed ? typed : constant;
        }

        public double ResolveNumber(double constant)
        {
            if (!useReference) return constant;

            var value = GetAccessor()?.Get();
            return value != null && DottMembers.KindOf(value.GetType()) == DottValueKind.Number ? Convert.ToDouble(value) : constant;
        }
    }
}
