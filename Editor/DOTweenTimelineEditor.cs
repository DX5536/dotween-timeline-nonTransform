using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Dott.Editor
{
    [CustomEditor(typeof(DOTweenTimeline))]
    public class DOTweenTimelineEditor : UnityEditor.Editor
    {
        private DOTweenTimeline Timeline => (DOTweenTimeline)target;

        private DottController controller;
        private DottSelection selection;
        private DottView view;
        private float? dragTweenTimeShift;
        private IDOTweenAnimation dragAnchor;
        private Dictionary<Component, float> dragStartDelays;
        private IDOTweenAnimation pendingSingleSelect;
        private IDOTweenAnimation[] animations;

        public override bool RequiresConstantRepaint() => true;

        public override void OnInspectorGUI()
        {
            Timeline.OnValidate();

            animations = Timeline.GetComponents<MonoBehaviour>().Select(DottAnimation.FromComponent).Where(animation => animation != null).ToArray();
            selection.Validate(animations);

            view.DrawTimeline(animations, selection, controller.IsPlaying, controller.ElapsedTime,
                controller.Loop, controller.Paused);

            if (selection.Count > 1)
            {
                view.DrawMultiSelection(selection.All);
            }
            else if (selection.Animation != null)
            {
                view.DrawInspector(selection.GetAnimationEditor());
            }

            if (controller.Paused && Event.current.type == EventType.Repaint)
            {
                controller.GoTo(animations, controller.ElapsedTime);
            }

            // Smoother ui updates
            if (controller.IsPlaying || view.IsTimeDragging || view.IsTweenDragging)
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

            view.TweenSelected += OnTweenSelected;
            view.TweenDrag += DragSelectedAnimations;
            view.TweenDragEnd += OnTweenDragEnd;

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
            view.TweenSelected -= OnTweenSelected;
            view.TweenDrag -= DragSelectedAnimations;
            view.TweenDragEnd -= OnTweenDragEnd;

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

        private void DragSelectedAnimations(float time)
        {
            // A tween was deselected on mouse down, nothing to drag
            if (dragAnchor == null) { return; }

            // Delays are remembered on the first drag frame, so the whole drag is relative to them
            if (dragStartDelays == null)
            {
                dragStartDelays = selection.All.ToDictionary(animation => animation.Component, animation => animation.Delay);
                dragTweenTimeShift = time - dragAnchor.Delay;
            }

            var dragged = selection.All.Where(animation => dragStartDelays.ContainsKey(animation.Component)).ToArray();
            if (dragged.Length == 0 || !dragStartDelays.TryGetValue(dragAnchor.Component, out var anchorStartDelay)) { return; }

            // Sometimes (e.g., for Frame) undo is not recorded when dragging, so we force it
            var undoName = dragged.Length > 1 ? $"Drag {dragged.Length} tweens" : $"Drag {dragAnchor.Label}";
            Undo.RecordObjects(dragged.Select(animation => animation.Component).ToArray(), undoName);

            var delay = TrySnapDelay(time - dragTweenTimeShift.Value, dragged, anchorStartDelay, view.TimeScale);

            // Move every dragged tween by the same delta, so relative offsets are kept
            var delta = delay - anchorStartDelay;
            var minStartDelay = dragged.Min(animation => dragStartDelays[animation.Component]);
            delta = Mathf.Max(delta, -minStartDelay);

            foreach (var animation in dragged)
            {
                animation.Delay = (float)Math.Round(dragStartDelays[animation.Component] + delta, 2);
            }

            // Complete undo record
            Undo.FlushUndoRecordObjects();
        }

        private float TrySnapDelay(float newDelay, IDOTweenAnimation[] dragged, float anchorStartDelay, float timeScale)
        {
            if (!IsSnapActive() || animations.Length < 2)
            {
                return newDelay;
            }

            var snapThreshold = 1f / 40f / timeScale;
            var snapPoints = animations
                .Where(animation => !selection.Contains(animation))
                .SelectMany(animation => Enumerable.Empty<float>().Append(animation.Delay).Append(animation.Delay + animation.Duration * Mathf.Max(1, animation.Loops)))
                .Distinct().ToArray();

            // Everything is selected, nothing to snap to
            if (snapPoints.Length == 0)
            {
                return newDelay;
            }

            var delta = newDelay - anchorStartDelay;

            foreach (var edge in MovingEdges())
            {
                var snapPoint = snapPoints.OrderBy(point => Mathf.Abs(point - edge)).First();
                if (Math.Abs(snapPoint - edge) < snapThreshold)
                {
                    return newDelay + (snapPoint - edge);
                }
            }

            return newDelay;

            // Edges of the dragged tweens that snap, in order of priority
            IEnumerable<float> MovingEdges()
            {
                yield return newDelay;

                if (dragAnchor.Loops != -1)
                {
                    yield return newDelay + FullDuration(dragAnchor);
                }

                // The end of the last dragged tween
                var ends = dragged
                    .Where(animation => animation.Loops != -1)
                    .Select(animation => dragStartDelays[animation.Component] + FullDuration(animation))
                    .ToArray();

                if (ends.Length > 0)
                {
                    yield return ends.Max() + delta;
                }
            }
        }

        private static float FullDuration(IDOTweenAnimation animation)
        {
            return animation.Duration * Mathf.Max(1, animation.Loops);
        }

        private bool IsSnapActive()
        {
            var reverseSnap = Event.current.control;
            var snapEnabled = view.IsSnapping;
            return reverseSnap ? !snapEnabled : snapEnabled;
        }

        private void OnTweenSelected(IDOTweenAnimation animation, bool additive)
        {
            // clear focus to correctly update inspector
            GUIUtility.keyboardControl = 0;

            ResetDragState();

            if (animation == null)
            {
                selection.Clear();
                return;
            }

            if (additive)
            {
                selection.Toggle(animation);
            }
            else if (selection.Count > 1 && selection.Contains(animation))
            {
                // Keep the selection to be able to drag it as a group.
                // Collapse it to the pressed tween on mouse up if there was no drag.
                pendingSingleSelect = animation;
            }
            else
            {
                selection.Set(animation);
            }

            // A tween removed from the selection must not be dragged
            dragAnchor = selection.Contains(animation) ? animation : null;
        }

        private void OnTweenDragEnd()
        {
            if (pendingSingleSelect != null && dragStartDelays == null)
            {
                selection.Set(pendingSingleSelect);
            }

            ResetDragState();
        }

        private void ResetDragState()
        {
            dragAnchor = null;
            dragStartDelays = null;
            dragTweenTimeShift = null;
            pendingSingleSelect = null;
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

            selection.Set(animation);
        }

        private void Remove()
        {
            var selected = selection.All.ToArray();
            if (selected.Length == 0) { return; }

            Undo.SetCurrentGroupName(selected.Length > 1 ? $"Delete {selected.Length} tweens" : $"Delete {selected[0].Label}");
            var undoGroup = Undo.GetCurrentGroup();

            foreach (var animation in selected)
            {
                Undo.DestroyObjectImmediate(animation.Component);
            }

            Undo.CollapseUndoOperations(undoGroup);
            selection.Clear();
        }

        private void Duplicate()
        {
            var selected = selection.All.ToArray();
            if (selected.Length == 0) { return; }

            Undo.SetCurrentGroupName(selected.Length > 1 ? $"Duplicate {selected.Length} tweens" : $"Duplicate {selected[0].Label}");
            var undoGroup = Undo.GetCurrentGroup();

            var duplicates = selected
                .Select(animation => DottAnimation.FromComponent(DuplicateComponent(animation.Component)))
                .ToArray();

            Undo.CollapseUndoOperations(undoGroup);
            selection.Set(duplicates);
        }

        private static Component DuplicateComponent(Component source)
        {
            var dest = Undo.AddComponent(source.gameObject, source.GetType());
            EditorUtility.CopySerialized(source, dest);

            var components = source.GetComponents<Component>();
            var targetIndex = Array.IndexOf(components, source) + 1;
            var index = Array.IndexOf(components, dest);
            while (index > targetIndex)
            {
                ComponentUtility.MoveComponentUp(dest);
                index--;
            }

            return dest;
        }

        private void ToggleLoop(bool value)
        {
            controller.Loop = value;
        }

        private void ToggleSnap()
        {
            EditorPrefs.SetBool("Dott.Snap", view.IsSnapping);
        }

        // Reordering is supported for a single tween only
        private void MoveSelectedUp()
        {
            if (selection.Count != 1) { return; }

            var index = animations.FindIndex(animation => animation.Component == selection.Animation.Component);
            if (index > 0)
            {
                ComponentUtility.MoveComponentUp(selection.Animation.Component);
            }
        }

        private void MoveSelectedDown()
        {
            if (selection.Count != 1) { return; }

            var index = animations.FindIndex(animation => animation.Component == selection.Animation.Component);
            if (index < animations.Length - 1)
            {
                ComponentUtility.MoveComponentDown(selection.Animation.Component);
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