using System;
using System.Collections.Generic;
using System.Linq;
using DG.DemiEditor;
using DG.Tweening;
using UnityEngine;

namespace Dott.Editor
{
    public class DottView
    {
        private bool isTimeDragging;
        private bool isTweenDragging;
        private static readonly AddMoreItem[] AddMoreItems = CreateAddMoreItems();

        public float TimeScale { get; private set; }
        public bool IsTimeDragging => isTimeDragging;
        public bool IsTweenDragging => isTweenDragging;
        public bool IsSnapping { get; set; }

        public event Action<Event> TimeDragEnd;
        public event Action<float> TimeDrag;

        /// Animation pressed with the mouse. Null means deselect. The flag is set when the selection modifier is held.
        public event Action<IDOTweenAnimation, bool> TweenSelected;
        public event Action<float> TweenDrag;
        public event Action TweenDragEnd;
        public event Action AddClicked;
        public event Action<Type> AddMore;
        public event Action RemoveClicked;
        public event Action DuplicateClicked;
        public event Action StopClicked;
        public event Action PlayClicked;
        public event Action<bool> LoopToggled;
        public event Action SnapToggled;
        public event Action PreviewDisabled;
        public event Action InspectorUpButtonClicked;
        public event Action InspectorDownButtonClicked;

        public void DrawTimeline(IDOTweenAnimation[] animations, DottSelection selection, bool isPlaying, float currentPlayingTime, bool isLooping, bool isPaused)
        {
            var rect = DottGUI.GetTimelineControlRect(animations.Length);

            DottGUI.Background(rect);
            var headerRect = DottGUI.Header(rect);

            TimeScale = CalculateTimeScale(animations);
            var timeDragStarted = false;
            var timeRect = DottGUI.Time(rect, TimeScale, ref isTimeDragging, () => timeDragStarted = true, TimeDragEnd);
            var tweensRect = DottGUI.Tweens(rect, animations, TimeScale, selection, ref isTweenDragging, TweenSelected, TweenDragEnd);

            if (DottGUI.AddButton(rect))
            {
                AddClicked?.Invoke();
            }

            DottGUI.AddMoreButton(rect, AddMoreItems, item => AddMore?.Invoke(item.Type));

            if (selection.Count > 0 && DottGUI.RemoveButton(rect))
            {
                RemoveClicked?.Invoke();
            }

            if (selection.Count > 0 && DottGUI.DuplicateButton(rect))
            {
                DuplicateClicked?.Invoke();
            }

            if (isPlaying || isPaused)
            {
                var scaledTime = currentPlayingTime * TimeScale;
                var verticalRect = timeRect.Add(tweensRect);
                DottGUI.TimeVerticalLine(verticalRect, scaledTime, isPaused);

                if (isPaused)
                {
                    DottGUI.PlayheadLabel(timeRect, scaledTime, currentPlayingTime);
                }
            }

            if (isTimeDragging)
            {
                var scaledTime = DottGUI.GetScaledTimeUnderMouse(timeRect);
                var rawTime = scaledTime / TimeScale;
                DottGUI.TimeVerticalLine(timeRect.Add(tweensRect), scaledTime, underLabel: true);
                DottGUI.PlayheadLabel(timeRect, scaledTime, rawTime);

                if (Event.current.type is EventType.MouseDrag || timeDragStarted)
                {
                    TimeDrag?.Invoke(rawTime);
                }
            }

            if (isTweenDragging && selection.Count > 0)
            {
                var time = DottGUI.GetScaledTimeUnderMouse(timeRect);

                if (Event.current.type == EventType.MouseDrag)
                {
                    var rawTime = time / TimeScale;
                    TweenDrag?.Invoke(rawTime);
                }
            }

            switch (isPlaying)
            {
                case true when DottGUI.StopButton(rect):
                    StopClicked?.Invoke();
                    break;
                case false when DottGUI.PlayButton(rect):
                    PlayClicked?.Invoke();
                    break;
            }

            var snapToggle = DottGUI.SnapToggle(rect, IsSnapping);
            if (snapToggle != IsSnapping)
            {
                IsSnapping = snapToggle;
                SnapToggled?.Invoke();
            }

            var loopResult = DottGUI.LoopToggle(rect, isLooping);
            if (loopResult != isLooping)
            {
                LoopToggled?.Invoke(loopResult);
            }

            if (DottGUI.PreviewEye(headerRect, isPlaying, isPaused, isTimeDragging))
            {
                PreviewDisabled?.Invoke();
            }

            // Keep the selection when the modifier is held, so a missed click does not reset it
            if (Event.current.type == EventType.MouseDown && !Event.current.shift)
            {
                var mousePosition = Event.current.mousePosition;
                if (selection.Count > 0 && rect.Contains(mousePosition))
                {
                    TweenSelected?.Invoke(null, false);
                }
            }
        }

        public void DrawInspector(UnityEditor.Editor editor)
        {
            DottGUI.Inspector(editor, InspectorUpButtonClicked, InspectorDownButtonClicked);
        }

        public void DrawMultiSelection(IReadOnlyList<IDOTweenAnimation> selected)
        {
            DottGUI.MultiSelection(selected, InspectorUpButtonClicked, InspectorDownButtonClicked);
        }

        private static float CalculateTimeScale(IDOTweenAnimation[] animations)
        {
            var maxTime = animations.Length > 0
                ? animations.Max(animation => animation.Delay + animation.Duration * Mathf.Max(1, animation.Loops))
                : 1f;
            return 1f / maxTime;
        }

        private static AddMoreItem[] CreateAddMoreItems()
        {
            var types = AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => !assembly.IsDynamic)
                .SelectMany(assembly => assembly.GetExportedTypes())
                .Where(type => type.IsClass && !type.IsAbstract && typeof(IDOTweenAnimation).IsAssignableFrom(type))
                .ToArray();

            return types
                .Select((type, _) => new AddMoreItem(new GUIContent($"Add {type.Name.Replace("DOTween", "")}"), type))
                .Prepend(new AddMoreItem(new GUIContent("Add Tween"), typeof(DOTweenAnimation)))
                .ToArray();
        }

        public struct AddMoreItem
        {
            public readonly GUIContent Content;
            public readonly Type Type;

            public AddMoreItem(GUIContent content, Type type)
            {
                Content = content;
                Type = type;
            }
        }
    }
}