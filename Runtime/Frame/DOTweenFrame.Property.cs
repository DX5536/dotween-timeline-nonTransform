using System;
using UnityEngine;

namespace Dott
{
    public partial class DOTweenFrame
    {
        [Serializable]
        public class FrameProperty
        {
            // WARNING: Do not change the order of this enum, as it is used for serialization
            public enum PropertyType
            {
                None,
                Position, LocalPosition,
                Scale,
                Fade, Color,
                Active, Enabled,
                Rotation
            }

            public GameObject TargetGameObject;
            public Component Target;
            public PropertyType Property;
            public bool IsRelative;

            public Vector3 EndValueVector3;
            public float EndValueFloat;
            public Color EndValueColor;

            public bool OptionalBool;
        }
    }
}