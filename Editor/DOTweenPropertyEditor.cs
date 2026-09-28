using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Dott.Editor
{
    [CustomEditor(typeof(DOTweenProperty))]
    public class DOTweenPropertyEditor : UnityEditor.Editor
    {
        // Created lazily: NicifyVariableName can't be called while the Editor (a ScriptableObject) is being constructed
        private static string[] easeNames;
        private static int[] easeIndices;
        private static string[] EaseNames => easeNames ??= CreateEaseNames(out easeIndices);

        private static int[] EaseIndices
        {
            get
            {
                if (easeIndices == null) { easeNames = CreateEaseNames(out easeIndices); }
                return easeIndices;
            }
        }

        private readonly TargetPicker mainPicker = new();
        private readonly TargetPicker fromPicker = new();
        private readonly TargetPicker toPicker = new();

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var idProp = serializedObject.FindProperty("id");
            var activeProp = serializedObject.FindProperty("isActive");

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(idProp, new GUIContent("ID"));
                activeProp.boolValue = EditorGUILayout.ToggleLeft("Active", activeProp.boolValue, GUILayout.Width(60));
            }

            // 1. Drop a GameObject / Component / ScriptableObject, 2. pick the component, 3. pick the value to tween
            var changed = mainPicker.Draw(
                serializedObject.FindProperty("dropTarget"),
                serializedObject.FindProperty("target"),
                serializedObject.FindProperty("propertyPath"),
                filter: null, targetLabel: "Target",
                serializedObject.FindProperty("useSelf"), ((Component)target).gameObject);

            var pathProp = serializedObject.FindProperty("propertyPath");
            var entry = mainPicker.FindEntry(pathProp.stringValue);
            if (changed && entry.HasValue)
            {
                serializedObject.ApplyModifiedProperties();
                InitValuesFromCurrent(entry.Value);
                serializedObject.Update();
            }

            if (entry.HasValue)
            {
                EditorGUILayout.Space();
                DrawValues(entry.Value);
            }
            else if (!string.IsNullOrEmpty(pathProp.stringValue) && serializedObject.FindProperty("target").objectReferenceValue != null)
            {
                EditorGUILayout.HelpBox($"Member '{pathProp.stringValue}' not found on {serializedObject.FindProperty("target").objectReferenceValue.GetType().Name}. Pick another property.", MessageType.Warning);
            }

            EditorGUILayout.Space();
            DrawTiming();

            serializedObject.ApplyModifiedProperties();
        }

        #region Target picker

        /// <summary>Target > Component > Property selection, used for the tweened value and for "Ref" From / To values.</summary>
        private class TargetPicker
        {
            private const double RefreshInterval = 1.0;

            private struct ComponentOption
            {
                public Object Object;
                public string Label;
            }

            private List<ComponentOption> componentOptions = new();
            private int componentCacheKey;
            private double componentCacheTime;

            private List<DottMembers.Entry> members = new();
            private int memberCacheKey;
            private double memberCacheTime;

            public DottMembers.Entry? FindEntry(string path)
            {
                if (string.IsNullOrEmpty(path)) return null;
                var index = members.FindIndex(entry => entry.Path == path);
                return index >= 0 ? members[index] : null;
            }

            /// <returns>True if the selected property was changed by the user.</returns>
            public bool Draw(SerializedProperty dropProp, SerializedProperty targetProp, SerializedProperty pathProp,
                Func<DottMembers.Entry, bool> filter, string targetLabel, SerializedProperty selfProp, GameObject selfGo)
            {
                var pathChanged = false;

                EditorGUI.BeginChangeCheck();
                using (new EditorGUILayout.HorizontalScope())
                {
                    var selfTooltip = "SELF: use the components of this GameObject";
                    var self = GUILayout.Toggle(selfProp.boolValue, new GUIContent("SELF", selfTooltip), EditorStyles.miniButton, GUILayout.Width(44));
                    if (self != selfProp.boolValue)
                    {
                        selfProp.boolValue = self;
                        if (self) { dropProp.objectReferenceValue = selfGo; }
                    }

                    if (selfProp.boolValue)
                    {
                        EditorGUILayout.LabelField("Will animate components on this GameObject", EditorStyles.miniLabel);
                    }
                    else
                    {
                        EditorGUILayout.PropertyField(dropProp, new GUIContent(targetLabel, "Drop a GameObject, Component or ScriptableObject"));
                    }
                }

                if (EditorGUI.EndChangeCheck())
                {
                    var dropped = dropProp.objectReferenceValue;
                    RefreshComponentOptions(dropped, force: true);
                    targetProp.objectReferenceValue = componentOptions.Count > 0 ? PickDefault(dropped) : null;
                    pathProp.stringValue = string.Empty;
                    members.Clear();
                    pathChanged = true;
                }

                var dropTarget = dropProp.objectReferenceValue;
                if (dropTarget == null) return pathChanged;

                RefreshComponentOptions(dropTarget, force: false);
                DrawComponentPopup(targetProp, pathProp);

                var target = targetProp.objectReferenceValue;
                if (target == null) return pathChanged;

                RefreshMembers(target, filter);
                return DrawPropertyPopup(pathProp) || pathChanged;
            }

            private static Object PickDefault(Object dropped) => dropped switch
            {
                GameObject go => go.transform,
                _ => dropped
            };

            private void DrawComponentPopup(SerializedProperty targetProp, SerializedProperty pathProp)
            {
                if (componentOptions.Count == 0)
                {
                    EditorGUILayout.HelpBox("Nothing to tween on this object.", MessageType.Warning);
                    return;
                }

                var index = componentOptions.FindIndex(option => option.Object == targetProp.objectReferenceValue);
                EditorGUI.BeginChangeCheck();
                var newIndex = EditorGUILayout.Popup(new GUIContent("Component", "Which component (or ScriptableObject) to use"),
                    index, componentOptions.Select(option => option.Label).ToArray());
                if (EditorGUI.EndChangeCheck() && newIndex >= 0 && newIndex != index)
                {
                    targetProp.objectReferenceValue = componentOptions[newIndex].Object;
                    pathProp.stringValue = string.Empty;
                    members.Clear();
                    memberCacheKey = 0;
                }
            }

            private bool DrawPropertyPopup(SerializedProperty pathProp)
            {
                var labels = new List<string> { "None" };
                labels.AddRange(members.Select(entry => entry.Label));

                var index = members.FindIndex(entry => entry.Path == pathProp.stringValue) + 1;
                EditorGUI.BeginChangeCheck();
                var newIndex = EditorGUILayout.Popup(new GUIContent("Property", "Public / [SerializeField] number, vector, color or string"), index, labels.ToArray());
                var changed = EditorGUI.EndChangeCheck() && newIndex != index;
                if (changed)
                {
                    pathProp.stringValue = newIndex <= 0 ? string.Empty : members[newIndex - 1].Path;
                }

                if (members.Count == 0)
                {
                    EditorGUILayout.HelpBox("No matching (number / Vector / Color / string) members found.", MessageType.Info);
                }

                return changed;
            }

            private void RefreshComponentOptions(Object dropTarget, bool force)
            {
                var key = dropTarget != null ? dropTarget.GetInstanceID() : 0;
                if (dropTarget is GameObject go) key = key * 31 + go.GetComponents<Component>().Length;
                if (dropTarget is Component c) key = key * 31 + c.GetComponents<Component>().Length;

                if (!force && key == componentCacheKey && EditorApplication.timeSinceStartup - componentCacheTime < RefreshInterval) return;

                componentCacheKey = key;
                componentCacheTime = EditorApplication.timeSinceStartup;
                componentOptions = BuildComponentOptions(dropTarget);
            }

            private static List<ComponentOption> BuildComponentOptions(Object dropTarget)
            {
                var options = new List<ComponentOption>();
                if (dropTarget == null) return options;

                if (dropTarget is ScriptableObject)
                {
                    options.Add(new ComponentOption { Object = dropTarget, Label = $"{dropTarget.GetType().Name} (ScriptableObject)" });
                    return options;
                }

                var gameObject = dropTarget is Component component ? component.gameObject : (GameObject)dropTarget;
                var components = gameObject.GetComponents<Component>().Where(c => c != null).ToArray();
                var typeCounts = new Dictionary<Type, int>();

                foreach (var c in components)
                {
                    var type = c.GetType();
                    typeCounts.TryGetValue(type, out var count);
                    typeCounts[type] = count + 1;
                    var suffix = count > 0 ? $" ({count + 1})" : string.Empty;
                    options.Add(new ComponentOption { Object = c, Label = type.Name + suffix });
                }

                // ScriptableObjects referenced by those components can be used directly too
                foreach (var c in components)
                {
                    using var so = new SerializedObject(c);
                    var iterator = so.GetIterator();
                    var enterChildren = true;
                    while (iterator.Next(enterChildren))
                    {
                        enterChildren = iterator.propertyType == SerializedPropertyType.Generic;
                        if (iterator.propertyType != SerializedPropertyType.ObjectReference) continue;
                        if (iterator.objectReferenceValue is not ScriptableObject scriptable) continue;
                        if (options.Any(option => option.Object == scriptable)) continue;

                        options.Add(new ComponentOption
                        {
                            Object = scriptable,
                            Label = $"{c.GetType().Name} ▸ {iterator.displayName} [{scriptable.GetType().Name}]"
                        });
                    }
                }

                return options;
            }

            private void RefreshMembers(Object target, Func<DottMembers.Entry, bool> filter)
            {
                var key = target.GetInstanceID();
                if (key == memberCacheKey && EditorApplication.timeSinceStartup - memberCacheTime < RefreshInterval) return;

                memberCacheKey = key;
                memberCacheTime = EditorApplication.timeSinceStartup;
                var entries = DottMembers.Enumerate(target);
                members = filter != null ? entries.Where(filter).ToList() : entries;
            }
        }

        #endregion

        #region Values

        private void DrawValues(DottMembers.Entry entry)
        {
            var useFromProp = serializedObject.FindProperty("useCustomFrom");
            var relativeProp = serializedObject.FindProperty("isRelative");
            var accessor = DottMembers.CreateAccessor(((DOTweenProperty)target).target, entry.Path);

            using (new EditorGUILayout.HorizontalScope())
            {
                useFromProp.boolValue = EditorGUILayout.ToggleLeft(new GUIContent("Custom From", "Off: tween from the current value"), useFromProp.boolValue);
                if (entry.Kind != DottValueKind.String)
                {
                    relativeProp.boolValue = EditorGUILayout.ToggleLeft(new GUIContent("Relative", "Add the To value to the start value"), relativeProp.boolValue);
                }
            }

            if (useFromProp.boolValue)
            {
                ValueField("From", entry, accessor, isFrom: true);
            }

            ValueField("To", entry, accessor, isFrom: false);

            if (entry.Kind == DottValueKind.String)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("richText"));
                var scramble = serializedObject.FindProperty("scrambleMode");
                EditorGUILayout.PropertyField(scramble);
                if (scramble.enumValueIndex == (int)DG.Tweening.ScrambleMode.Custom)
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("scrambleChars"));
                }
            }
        }

        private void ValueField(string label, DottMembers.Entry entry, DottMembers.Accessor accessor, bool isFrom)
        {
            var prefix = isFrom ? "from" : "to";
            var sourceProp = serializedObject.FindProperty(prefix + "Source");
            var useReferenceProp = sourceProp.FindPropertyRelative("useReference");

            using (new EditorGUILayout.HorizontalScope())
            {
                if (useReferenceProp.boolValue)
                {
                    EditorGUILayout.LabelField(label, "Read from another object:");
                }
                else
                {
                    ConstantField(label, entry, prefix);
                }

                var refTooltip = "Read this value from a property of another object (e.g. Slider.maxValue) instead of typing it in";
                useReferenceProp.boolValue = GUILayout.Toggle(useReferenceProp.boolValue, new GUIContent("Ref", refTooltip), EditorStyles.miniButton, GUILayout.Width(34));

                if (!useReferenceProp.boolValue)
                {
                    var tooltip = "Set to the current value of the property";
                    if (GUILayout.Button(new GUIContent("◎", tooltip), GUILayout.Width(24)) && accessor != null)
                    {
                        ApplyCurrent(entry, accessor, prefix);
                    }
                }
            }

            if (!useReferenceProp.boolValue) return;

            var picker = isFrom ? fromPicker : toPicker;
            var indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel++;
            picker.Draw(sourceProp.FindPropertyRelative("dropTarget"), sourceProp.FindPropertyRelative("target"),
                sourceProp.FindPropertyRelative("propertyPath"), candidate => candidate.Kind == entry.Kind, "Object",
                sourceProp.FindPropertyRelative("useSelf"), ((Component)target).gameObject);
            EditorGUI.indentLevel = indent;

            if (string.IsNullOrEmpty(sourceProp.FindPropertyRelative("propertyPath").stringValue))
            {
                EditorGUILayout.HelpBox($"No {entry.Kind} property selected: the constant value is used until you pick one.", MessageType.None);
            }
        }

        private void ConstantField(string label, DottMembers.Entry entry, string prefix)
        {
            switch (entry.Kind)
            {
                case DottValueKind.Number:
                {
                    var prop = serializedObject.FindProperty(prefix + "Number");
                    prop.doubleValue = DottMembers.IsIntegral(entry.ValueType)
                        ? EditorGUILayout.LongField(label, (long)prop.doubleValue)
                        : EditorGUILayout.DoubleField(label, prop.doubleValue);
                    break;
                }
                case DottValueKind.Vector2:
                {
                    var prop = serializedObject.FindProperty(prefix + "Vector");
                    var v = prop.vector4Value;
                    var edited = EditorGUILayout.Vector2Field(label, v);
                    prop.vector4Value = new Vector4(edited.x, edited.y, v.z, v.w);
                    break;
                }
                case DottValueKind.Vector3:
                {
                    var prop = serializedObject.FindProperty(prefix + "Vector");
                    var v = prop.vector4Value;
                    var edited = EditorGUILayout.Vector3Field(label, v);
                    prop.vector4Value = new Vector4(edited.x, edited.y, edited.z, v.w);
                    break;
                }
                case DottValueKind.Vector4:
                {
                    var prop = serializedObject.FindProperty(prefix + "Vector");
                    prop.vector4Value = EditorGUILayout.Vector4Field(label, prop.vector4Value);
                    break;
                }
                case DottValueKind.Color:
                {
                    var prop = serializedObject.FindProperty(prefix + "Color");
                    prop.colorValue = EditorGUILayout.ColorField(label, prop.colorValue);
                    break;
                }
                case DottValueKind.String:
                {
                    var prop = serializedObject.FindProperty(prefix + "String");
                    prop.stringValue = EditorGUILayout.TextField(label, prop.stringValue);
                    break;
                }
            }
        }

        private void InitValuesFromCurrent(DottMembers.Entry entry)
        {
            var accessor = DottMembers.CreateAccessor(((DOTweenProperty)target).target, entry.Path);
            if (accessor == null) return;

            serializedObject.Update();
            ApplyCurrent(entry, accessor, "from");
            ApplyCurrent(entry, accessor, "to");
            serializedObject.ApplyModifiedProperties();
        }

        private void ApplyCurrent(DottMembers.Entry entry, DottMembers.Accessor accessor, string prefix)
        {
            var current = accessor.Get();
            if (current == null) return;

            switch (entry.Kind)
            {
                case DottValueKind.Number:
                    serializedObject.FindProperty(prefix + "Number").doubleValue = accessor.GetNumber();
                    break;
                case DottValueKind.Vector2:
                    serializedObject.FindProperty(prefix + "Vector").vector4Value = (Vector2)current;
                    break;
                case DottValueKind.Vector3:
                    serializedObject.FindProperty(prefix + "Vector").vector4Value = (Vector3)current;
                    break;
                case DottValueKind.Vector4:
                    serializedObject.FindProperty(prefix + "Vector").vector4Value = (Vector4)current;
                    break;
                case DottValueKind.Color:
                    serializedObject.FindProperty(prefix + "Color").colorValue = (Color)current;
                    break;
                case DottValueKind.String:
                    serializedObject.FindProperty(prefix + "String").stringValue = (string)current;
                    break;
            }
        }

        #endregion

        #region Timing

        private void DrawTiming()
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("delay"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("duration"));

            var easeProp = serializedObject.FindProperty("ease");
            var useCurveProp = serializedObject.FindProperty("useEaseCurve");
            using (new EditorGUILayout.HorizontalScope())
            {
                if (useCurveProp.boolValue)
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("easeCurve"), new GUIContent("Ease"));
                }
                else
                {
                    var current = Array.IndexOf(EaseIndices, easeProp.enumValueIndex);
                    var selected = EditorGUILayout.Popup("Ease", current, EaseNames);
                    if (selected >= 0 && selected != current)
                    {
                        easeProp.enumValueIndex = EaseIndices[selected];
                    }
                }

                useCurveProp.boolValue = GUILayout.Toggle(useCurveProp.boolValue, new GUIContent("Curve", "Use a custom AnimationCurve"), GUILayout.Width(52));
            }

            var loopsProp = serializedObject.FindProperty("loops");
            EditorGUILayout.PropertyField(loopsProp, new GUIContent("Loops", "-1 for infinite"));
            loopsProp.intValue = Mathf.Max(-1, loopsProp.intValue);
            if (loopsProp.intValue != 1)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("loopType"));
            }
        }

        // Hides internal Ease values (INTERNAL_Zero, INTERNAL_Custom)
        private static string[] CreateEaseNames(out int[] indices)
        {
            var names = Enum.GetNames(typeof(DG.Tweening.Ease));
            var visible = new List<int>();
            for (var i = 0; i < names.Length; i++)
            {
                if (!names[i].StartsWith("INTERNAL")) visible.Add(i);
            }

            indices = visible.ToArray();
            return visible.Select(i => ObjectNames.NicifyVariableName(names[i])).ToArray();
        }

        #endregion
    }
}
