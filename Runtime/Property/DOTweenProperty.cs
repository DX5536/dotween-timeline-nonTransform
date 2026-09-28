using DG.Tweening;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Dott
{
    /// <summary>
    /// Tweens any public / [SerializeField] number, Vector, Color or string member of a component or ScriptableObject
    /// (e.g. Slider.value, TMP_Text.fontSize, TMP_Text.text or a value inside a referenced ScriptableObject).
    /// </summary>
    [AddComponentMenu("DOTween/DOTween Property")]
    public partial class DOTweenProperty : MonoBehaviour
    {
        [SerializeField] public string id;
        [SerializeField] public bool isActive = true;

        // The object as it was dropped in the inspector (GameObject, Component or ScriptableObject)
        [SerializeField] public Object dropTarget;
        // The component / ScriptableObject that owns the tweened member
        [SerializeField] public Object target;
        // Member path, nested members are separated with dots, e.g. "value" or "stats.health"
        [SerializeField] public string propertyPath;

        [Min(0), SerializeField] public float delay;
        [Min(0), SerializeField] public float duration = 1f;
        [SerializeField] public Ease ease = Ease.OutQuad;
        [SerializeField] public bool useEaseCurve;
        [SerializeField] public AnimationCurve easeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] public int loops = 1;
        [SerializeField] public LoopType loopType = LoopType.Restart;
        [SerializeField] public bool isRelative;

        [SerializeField] public bool useCustomFrom;
        [SerializeField] public double fromNumber;
        [SerializeField] public double toNumber;
        [SerializeField] public Vector4 fromVector;
        [SerializeField] public Vector4 toVector;
        [SerializeField] public Color fromColor = Color.white;
        [SerializeField] public Color toColor = Color.white;
        [SerializeField] public string fromString;
        [SerializeField] public string toString;

        // Optional live values (e.g. Slider.minValue / maxValue) that replace the constants above
        [SerializeField] public DottValueSource fromSource = new();
        [SerializeField] public DottValueSource toSource = new();

        [SerializeField] public bool richText = true;
        [SerializeField] public ScrambleMode scrambleMode = ScrambleMode.None;
        [SerializeField] public string scrambleChars;

        private Tween tween;
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

        public Tween CreateTween(bool regenerateIfExists, bool andPlay = true)
        {
            if (tween != null)
            {
                if (tween.IsActive())
                {
                    if (!regenerateIfExists)
                    {
                        return tween;
                    }

                    tween.Kill();
                }

                tween = null;
            }

            tween = BuildTween();
            if (tween == null)
            {
                return null;
            }

            if (andPlay)
            {
                tween.Play();
            }
            else
            {
                tween.Pause();
            }

            return tween;
        }

        private Tweener BuildTween()
        {
            var acc = GetAccessor();
            if (acc == null || acc.Kind == DottValueKind.Unsupported || acc.Get() == null)
            {
                return null;
            }

            // The getter is only used by DOTween to read the start value, so a custom "from" is just a different getter
            var custom = useCustomFrom;
            Tweener result;

            switch (acc.Kind)
            {
                case DottValueKind.Number:
                {
                    var from = fromSource.ResolveNumber(fromNumber);
                    result = DOTween.To(() => custom ? from : acc.GetNumber(), acc.SetNumber, toSource.ResolveNumber(toNumber), duration);
                    break;
                }
                case DottValueKind.Vector2:
                {
                    var from = fromSource.Resolve((Vector2)fromVector);
                    result = DOTween.To(() => custom ? from : (Vector2)acc.Get(), v => acc.Set(v), toSource.Resolve((Vector2)toVector), duration);
                    break;
                }
                case DottValueKind.Vector3:
                {
                    var from = fromSource.Resolve((Vector3)fromVector);
                    result = DOTween.To(() => custom ? from : (Vector3)acc.Get(), v => acc.Set(v), toSource.Resolve((Vector3)toVector), duration);
                    break;
                }
                case DottValueKind.Vector4:
                {
                    var from = fromSource.Resolve(fromVector);
                    result = DOTween.To(() => custom ? from : (Vector4)acc.Get(), v => acc.Set(v), toSource.Resolve(toVector), duration);
                    break;
                }
                case DottValueKind.Color:
                {
                    var from = fromSource.Resolve(fromColor);
                    result = DOTween.To(() => custom ? from : (Color)acc.Get(), v => acc.Set(v), toSource.Resolve(toColor), duration);
                    break;
                }
                case DottValueKind.String:
                {
                    var from = fromSource.Resolve(fromString) ?? string.Empty;
                    var stringTween = DOTween.To(() => custom ? from : (string)acc.Get() ?? string.Empty, v => acc.Set(v), toSource.Resolve(toString) ?? string.Empty, duration);
                    stringTween.SetOptions(richText, scrambleMode, scrambleChars);
                    result = stringTween;
                    break;
                }
                default:
                    return null;
            }

            result.SetDelay(delay).SetLoops(loops, loopType).SetTarget(target);

            if (isRelative && acc.Kind != DottValueKind.String)
            {
                result.SetRelative();
            }

            if (useEaseCurve)
            {
                result.SetEase(easeCurve);
            }
            else
            {
                result.SetEase(ease);
            }

            if (!string.IsNullOrEmpty(id))
            {
                result.SetId(id);
            }

            return result;
        }

        private void OnDestroy()
        {
            tween?.Kill();
        }
    }
}
