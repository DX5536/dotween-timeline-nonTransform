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
        private bool isTweenDragPerformed;
        private IDOTweenAnimation pressedTween;
        private static readonly AddMoreItem[] AddMoreItems = CreateAddMoreItems();

        public float TimeScale { get; private set; }
        public bool IsTimeDragging => isTimeDragging;
        public bool IsTweenPressed => pressedTween != null;
        public bool IsSnapping { get; set; }

        public event Action<Event> TimeDragEnd;
        public event Action<float> TimeDrag;
        public event Action<IDOTweenAnimation> TweenSelectSet;
        public event Action<IDOTweenAnimation> TweenSelectToggle;
        public event Action<float> TweenDrag;
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

        public void DrawTimeline(IDOTweenAnimation[] animations, IReadOnlyCollection<IDOTweenAnimation> selectedAnimations, bool isPlaying, float currentPlayingTime, bool isLooping, bool isPaused)
        {
            var rect = DottGUI.GetTimelineControlRect(animations.Length);

            DottGUI.Background(rect);
            var headerRect = DottGUI.Header(rect);

            TimeScale = CalculateTimeScale(animations);
            var timeDragStarted = false;
            var timeRect = DottGUI.Time(rect, TimeScale, ref isTimeDragging, () => timeDragStarted = true, TimeDragEnd);
            var tweensRect = DottGUI.Tweens(rect, animations, TimeScale, selectedAnimations, IsTweenPressed,
                animation => OnTweenDown(animation, selectedAnimations), () => OnTweenUp(selectedAnimations));

            if (DottGUI.AddButton(rect))
            {
                AddClicked?.Invoke();
            }

            DottGUI.AddMoreButton(rect, AddMoreItems, item => AddMore?.Invoke(item.Type));

            var hasSelection = selectedAnimations.Count > 0;
            if (hasSelection && DottGUI.RemoveButton(rect))
            {
                RemoveClicked?.Invoke();
            }

            var singleSelection = selectedAnimations.Count == 1;
            if (singleSelection && DottGUI.DuplicateButton(rect))
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

            if (IsTweenPressed)
            {
                if (Event.current.type == EventType.MouseDrag)
                {
                    var time = DottGUI.GetScaledTimeUnderMouse(timeRect);
                    var rawTime = time / TimeScale;
                    isTweenDragPerformed = true;
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

            if (Event.current.type == EventType.MouseDown)
            {
                var mousePosition = Event.current.mousePosition;
                if (hasSelection && rect.Contains(mousePosition))
                {
                    TweenSelectSet?.Invoke(null);
                }
            }
        }

        private void OnTweenDown(IDOTweenAnimation animation, IReadOnlyCollection<IDOTweenAnimation> selectedAnimations)
        {
            isTweenDragPerformed = false;
            pressedTween = animation;

            if (animation == null)
            {
                TweenSelectSet?.Invoke(null);
                return;
            }

            if (Event.current.shift)
            {
                TweenSelectToggle?.Invoke(animation);
                return;
            }

            var alreadySelected = selectedAnimations.Contains(animation);
            var isGroupSelection = selectedAnimations.Count > 1;

            // Don't deselect others yet if clicking inside a group (to allow group drag)
            if (!alreadySelected || !isGroupSelection)
            {
                TweenSelectSet?.Invoke(animation);
            }
        }

        private void OnTweenUp(IReadOnlyCollection<IDOTweenAnimation> selectedAnimations)
        {
            var wasSimpleClick = !isTweenDragPerformed && pressedTween != null;
            var isGroupSelection = selectedAnimations.Count > 1;

            // If it was a click without dragging inside a group, select only this one (deselect others)
            if (!Event.current.shift && wasSimpleClick && isGroupSelection)
            {
                TweenSelectSet?.Invoke(pressedTween);
            }

            pressedTween = null;
        }

        public void DrawInspector(UnityEditor.Editor editor)
        {
            DottGUI.Inspector(editor, InspectorUpButtonClicked, InspectorDownButtonClicked);
        }

        public void DrawMultiInspector(IReadOnlyCollection<IDOTweenAnimation> animations)
        {
            DottGUI.MultiSelectionInspector(animations);
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