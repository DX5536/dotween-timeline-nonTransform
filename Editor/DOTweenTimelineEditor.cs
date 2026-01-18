using System;
using System.Linq;
using DG.Tweening;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Dott.Editor
{
    [CustomEditor(typeof(DOTweenTimeline))]
    public class DOTweenTimelineEditor : UnityEditor.Editor
    {
        private class DragAnchor
        {
            public IDOTweenAnimation Head;
            public float TimeShift;
            public float TailTimeShift;
        }

        private DOTweenTimeline Timeline => (DOTweenTimeline)target;

        private DottController controller;
        private DottSelection selection;
        private DottView view;

        [CanBeNull] private DragAnchor dragAnchor;

        private IDOTweenAnimation[] animations;

        public override bool RequiresConstantRepaint() => true;

        public override void OnInspectorGUI()
        {
            Timeline.OnValidate();

            animations = Timeline.GetComponents<MonoBehaviour>().Select(DottAnimation.FromComponent).Where(animation => animation != null).ToArray();
            selection.Validate(animations);

            view.DrawTimeline(animations, selection.SelectedAnimations, controller.IsPlaying, controller.ElapsedTime,
                controller.Loop, controller.Paused);

            if (selection.Count == 1)
            {
                view.DrawInspector(selection.GetAnimationEditor());
            }
            else if (selection.Count > 1)
            {
                view.DrawMultiInspector(selection.SelectedAnimations);
            }

            if (controller.Paused && Event.current.type == EventType.Repaint)
            {
                controller.GoTo(animations, controller.ElapsedTime);
            }

            // Smoother ui updates
            if (controller.IsPlaying || view.IsTimeDragging || view.IsTweenPressed)
            {
                Repaint();
            }
        }

        private void OnEnable()
        {
            controller = new DottController();
            selection = new DottSelection();
            view = new DottView();

            view.IsSnapping = EditorPrefs.GetBool("Dott.Snap", true);

            view.TweenSelectSet += OnTweenSelectSet;
            view.TweenSelectToggle += OnTweenSelectToggle;

            view.TweenDrag += DragSelectedAnimation;

            view.TimeDragEnd += OnTimeDragEnd;
            view.TimeDrag += GoTo;
            view.PreviewDisabled += controller.Stop;

            view.AddClicked += AddAnimation;
            view.AddMore += AddMore;
            view.RemoveClicked += Remove;
            view.DuplicateClicked += Duplicate;

            view.PlayClicked += Play;
            view.StopClicked += controller.Stop;
            view.LoopToggled += ToggleLoop;
            view.SnapToggled += ToggleSnap;

            view.InspectorUpButtonClicked += MoveSelectedUp;
            view.InspectorDownButtonClicked += MoveSelectedDown;

            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            view.TweenSelectSet -= OnTweenSelectSet;
            view.TweenSelectToggle -= OnTweenSelectToggle;

            view.TweenDrag -= DragSelectedAnimation;

            view.TimeDragEnd -= OnTimeDragEnd;
            view.TimeDrag -= GoTo;
            view.PreviewDisabled -= controller.Stop;

            view.AddClicked -= AddAnimation;
            view.AddMore -= AddMore;
            view.RemoveClicked -= Remove;
            view.DuplicateClicked -= Duplicate;

            view.PlayClicked -= Play;
            view.StopClicked -= controller.Stop;
            view.LoopToggled -= ToggleLoop;
            view.SnapToggled -= ToggleSnap;

            view.InspectorUpButtonClicked -= MoveSelectedUp;
            view.InspectorDownButtonClicked -= MoveSelectedDown;

            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;

            controller.Dispose();
            controller = null;

            selection.Dispose();
            selection = null;

            view = null;

            animations = null;
        }

        private void Play()
        {
            controller.Play(animations);
        }

        private void GoTo(float time)
        {
            controller.GoTo(animations, time);
        }

        private void OnTimeDragEnd(Event mouseEvent)
        {
            const int mouseButtonMiddle = 2;
            if (mouseEvent.IsRightMouseButton() || mouseEvent.button == mouseButtonMiddle)
            {
                controller.Stop();
                return;
            }

            controller.Pause();
        }

        private void DragSelectedAnimation(float time)
        {
            // Sometimes (e.g., for Frame) undo is not recorded when dragging, so we force it
            foreach (var animation in selection.SelectedAnimations)
            {
                Undo.RecordObject(animation.Component, $"Drag {animation.Label}");
            }

            if (dragAnchor == null)
            {
                var headAnimation = selection.FindHead();
                var tailAnimation = selection.FindTail();
                dragAnchor = new DragAnchor
                {
                    Head = headAnimation,
                    TimeShift = time - headAnimation.Delay,
                    TailTimeShift = time - tailAnimation.Delay
                };
            }

            var delay = time - dragAnchor.TimeShift;
            var tailDelay = time - dragAnchor.TailTimeShift;
            delay = TrySnapTime(delay, tailDelay, view.TimeScale);
            delay = Mathf.Max(0, delay);
            delay = (float)Math.Round(delay, 2);

            var delayOffset = delay - dragAnchor.Head.Delay;
            foreach (var selectedAnimation in selection.SelectedAnimations)
            {
                selectedAnimation.Delay += delayOffset;
            }

            // Complete undo record
            Undo.FlushUndoRecordObjects();
        }

        private float TrySnapTime(float newDelay, float newTailDelay, float timeScale)
        {
            if (!IsSnapActive() || animations.Length < 2)
            {
                return newDelay;
            }

            var snapThreshold = 1f / 40f / timeScale;
            var snapPoints = animations
                .Where(animation => !selection.SelectedAnimations.Contains(animation))
                .SelectMany(animation => Enumerable.Empty<float>().Append(animation.Delay).Append(animation.Delay + animation.Duration * Mathf.Max(1, animation.Loops)))
                .Distinct().ToArray();

            if (snapPoints.Length == 0)
            {
                return newDelay;
            }

            var snapTime = snapPoints.OrderBy(snapPoint => Mathf.Abs(snapPoint - newDelay)).First();
            if (Math.Abs(snapTime - newDelay) < snapThreshold)
            {
                return snapTime;
            }

            var tail = selection.FindTail();
            if (tail.Loops == -1)
            {
                return newDelay;
            }

            var tailFullDuration = tail.Duration * Mathf.Max(1, tail.Loops);
            var newEndTime = newTailDelay + tailFullDuration;
            var snapEndTime = snapPoints.OrderBy(snapPoint => Mathf.Abs(snapPoint - newEndTime)).First();
            if (Math.Abs(snapEndTime - newEndTime) < snapThreshold)
            {
                var snapTailDelay = snapEndTime - tailFullDuration;
                var difference = snapTailDelay - newTailDelay;
                return newDelay + difference;
            }

            return newDelay;
        }

        private bool IsSnapActive()
        {
            var reverseSnap = Event.current.control;
            var snapEnabled = view.IsSnapping;
            return reverseSnap ? !snapEnabled : snapEnabled;
        }

        private void OnTweenSelectSet(IDOTweenAnimation animation)
        {
            OnTweenSelectChanged();
            selection.Set(animation);
        }

        private void OnTweenSelectToggle(IDOTweenAnimation animation)
        {
            OnTweenSelectChanged();
            selection.Toggle(animation);
        }

        private void OnTweenSelectChanged()
        {
            // clear focus to correctly update inspector
            GUIUtility.keyboardControl = 0;
            dragAnchor = null;
        }

        private void AddAnimation()
        {
            Add(Timeline, typeof(DOTweenAnimation));
        }

        private void AddMore(Type type)
        {
            Add(Timeline, type);
        }

        private void Add(DOTweenTimeline timeline, Type type)
        {
            var component = ObjectFactory.AddComponent(timeline.gameObject, type);
            var animation = DottAnimation.FromComponent(component);
            if (controller.Paused)
            {
                animation!.Delay = (float)Math.Round(controller.ElapsedTime, 2);
            }

            if (type == typeof(DOTweenAnimation))
            {
                ((DOTweenAnimation)component).targetIsSelf = false;
            }

            selection.Set(animation);
        }

        private void Remove()
        {
            if (selection.Count > 1)
            {
                Undo.SetCurrentGroupName($"Remove {selection.Count} tweens");
                var group = Undo.GetCurrentGroup();
                foreach (var animation in selection.SelectedAnimations)
                {
                    Undo.DestroyObjectImmediate(animation.Component);
                }
                Undo.CollapseUndoOperations(group);
            }
            else
            {
                Undo.DestroyObjectImmediate(selection.Animation.Component);
            }

            selection.Clear();
        }

        private void Duplicate()
        {
            if (selection.IsMultiSelection)
            {
                return;
            }

            var selected = selection.Animation;

            Undo.SetCurrentGroupName($"Duplicate {selected.Label}");

            var source = selected.Component;

            var dest = Undo.AddComponent(source.gameObject, source.GetType());
            EditorUtility.CopySerialized(source, dest);

            var animation = DottAnimation.FromComponent(dest);
            selection.Set(animation);

            var components = source.GetComponents<Component>();
            var targetIndex = Array.IndexOf(components, source) + 1;
            var index = Array.IndexOf(components, dest);
            while (index > targetIndex)
            {
                ComponentUtility.MoveComponentUp(dest);
                index--;
            }
        }

        private void ToggleLoop(bool value)
        {
            controller.Loop = value;
        }

        private void ToggleSnap()
        {
            EditorPrefs.SetBool("Dott.Snap", view.IsSnapping);
        }

        private void MoveSelectedUp()
        {
            if (selection.IsMultiSelection)
            {
                return;
            }

            var selected = selection.Animation;
            var index = animations.FindIndex(animation => animation.Component == selected.Component);
            if (index > 0)
            {
                ComponentUtility.MoveComponentUp(selected.Component);
            }
        }

        private void MoveSelectedDown()
        {
            if (selection.IsMultiSelection)
            {
                return;
            }

            var selected = selection.Animation;
            var index = animations.FindIndex(animation => animation.Component == selected.Component);
            if (index < animations.Length - 1)
            {
                ComponentUtility.MoveComponentDown(selected.Component);
            }
        }

        private void OnPlayModeStateChanged(PlayModeStateChange stateChange)
        {
            // Rewind tweens before play mode. OnDisable is too late (runs after dirty state is saved)
            if (stateChange == PlayModeStateChange.ExitingEditMode)
            {
                controller.Stop();
            }
        }
    }
}