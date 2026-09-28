# DOTween Timeline
[![License: MIT](https://img.shields.io/badge/License-MIT-brightgreen.svg)](LICENSE)

A pocket timeline solution for DOTween Pro. Configure and organize complex tween animations directly in the Inspector.

![ezgif-478bb6b997c38b](https://github.com/user-attachments/assets/1cc3d251-d4a8-476a-9dc5-0b43ebe395d4)

## Installation
> [!IMPORTANT]
> **Required**: [**PRO**](https://dotween.demigiant.com/pro.php) version of DOTween.

### Releases page
Easiest way is to install DOTween Timeline as an asset package.
1. Download the latest ```.unitypackage``` file from the [Releases page](https://github.com/DX5536/dotween-timeline-nonTransform/releases).
2. Import it into your project via **Assets > Import Package > Custom Package**.

### Git UPM
You can also install this package via Git URL using Unity Package Manager.
1. Since DOTween is not distributed as a upm package by default, you need to manually generate `.asmdef` files for it:\
  Open **Tools > Demigiant > DOTween Utility Panel**, click **Create ASMDEF**
2. Then, add the following line to your `Packages/manifest.json`:
```
"com.dx5536.dotweentimeline": "https://github.com/DX5536/dotween-timeline-nonTransform.git#upm"
```

## How to use
Add the **DOTween > DOTween Timeline** component to a GameObject (use a separate GameObject for each animation sequence).

Control the timeline from code:

```c#
[SerializeField] private DOTweenTimeline timeline;

var tween = timeline.Play();

tween.OnComplete(() => Debug.Log("OnComplete"));
tween.Pause();
```

### Sample
A sample scene is included to help you get started. You can find it here: `Plugins/DOTweenTimeline/Sample`\
Open it to see an example of how to configure and use the timeline in practice.
<details>
  <summary>To make the sample scene work, enable TextMeshPro support in DOTween and set up TMP.</summary>
  
  1. Go to **Tools > Demigiant > DOTween Utility Panel**, press **"Setup DOTween..."**, enable **TextMeshPro**:
  ![TextMeshProSupport](https://github.com/user-attachments/assets/1674e9e9-ac6c-4b73-a278-37a548806a23)
  2. **Window > TextMeshPro > Import TMP Essential Resources**
</details>

## Recommendations

#### 1. Disable default DOTween Pro preview controls
In the Inspector, on any DOTween animation component:

![tips_preview_controls](https://github.com/user-attachments/assets/e8e3c39e-a1b0-4d4a-bd2d-de2af567eca7)

#### 2. Use a separate GameObject for each animation sequence
![tips_separate_go](https://github.com/user-attachments/assets/7fa9e9b5-d1af-4f2e-9b0e-28d9576d2198)


## Extras
### DOTween Timeline Player component
Automatically plays animations without code.\
Just add the **DOTween > DOTween Timeline Player** component to the same GameObject with the Timeline.

### Extra Actions
In addition to standard tweens, you can add special timeline actions via the Add dropdown:\
![Mask group (1)](https://github.com/user-attachments/assets/dc48d249-56f2-41cb-8259-b6aa8db3e46e)

#### DOTweenCallback component
A visual replacement for Sequence.InsertCallback().\
Use the `onCallback` UnityEvent in the Inspector or from code:
```c#
[SerializeField] private DOTweenCallback callback;

callback.onCallback.AddListener(() => Debug.Log("Callback"));
```
<img width="477" src="https://github.com/user-attachments/assets/746fca7e-1d70-4127-ba92-330c0f7470e6" />

#### DOTweenFrame component
Triggers immediate state changes in a single frame.\
Perfect for setting position, rotation, color, and more, without animation.

<img width="490" src="https://github.com/user-attachments/assets/df9226e8-dc83-419b-b1ca-daaf6b70811a" />

#### DOTweenLink component
A reference to another Timeline. Useful for complex animations with a large number of child tweens.\
See the Sample scene for usage details.

<img width="343" height="156" alt="image" src="https://github.com/user-attachments/assets/114cc9bc-8d4b-4373-a896-fa94f5c45023" />

## Contributing
Please report bugs in [Issues](https://github.com/medvejut/dotween-timeline/issues).\
Feel free to ask questions and share thoughts in [Discussions](https://github.com/medvejut/dotween-timeline/discussions).

**This project is inspired by:**\
[Animation Creator Timeline (UI Tween)](https://assetstore.unity.com/packages/tools/animation/animation-creator-timeline-ui-tween-186589)\, [DOTween Timeline Preview](https://www.youtube.com/watch?v=hrX0xZ3JCXU) & [Jitter](https://jitter.video/)

## Fork additions

### DOTween Property (tween any value)
Add it from the timeline **Add** dropdown (**Add Property**) or via **DOTween > DOTween Property**.
1. Drop a GameObject, component or ScriptableObject on **Target**.
2. Pick the **Component** to tween (Transform / RectTransform first; ScriptableObjects referenced by those components are listed too).
3. Pick the **Property**: any public or `[SerializeField]` number (int/float/double/...), `Vector2/3/4`, `Color` or `string` member, e.g. `Slider.value`, TMP `fontSize`, `maxVisibleCharacters`, `text` (typewriter/scramble) or `stats.health` inside a ScriptableObject. Public properties with a setter work too.

Options: Custom From, Relative, ease / custom curve, loops.

### Preview of inactive objects
Inactive targets (and their inactive parents) are enabled while the timeline preview runs and restored when it stops. The preview also stops on scene save.

### Playback and reverse
```c#
timeline.Play();                 // forward, restarts if finished
timeline.PlayBackwards();        // from the current position
timeline.PlayBackwardsFromEnd(); // jump to the end, then play in reverse
timeline.Toggle();               // forward <-> backwards
timeline.Restart(); timeline.Rewind(); timeline.SmoothRewind(); timeline.Complete();
timeline.Pause(); timeline.Resume(); timeline.TogglePause(); timeline.Flip(); timeline.GoTo(0.5f);
```
`DO*` void wrappers (`DOPlayBackwards`, `DOToggle`, `DORewind`, ...) can be used from UnityEvents. The timeline Player has a **Direction** option, and the inspector shows playback buttons in Play Mode.
The generated sequence is now kept after completing (`SetAutoKill(false)`) so it can be reversed and replayed; call `Kill()` to rebuild it.

#### From / To, single channels and live values
- **Custom From** gives both a From and a To value for any property (Move, Color, Scale, Slider value...). **Relative** adds the To value to the start value.
- Vector and color members also list single channels, e.g. `localPosition.x` or `color.a` (a fade that leaves RGB untouched).
- **Ref**: the From / To value can be read from a property of another object instead of typed in (e.g. `Slider.minValue` / `Slider.maxValue`). Timelines with Ref values are rebuilt before each forward `Play()` / `Restart()`, so changes made in between are picked up.

### Artist workflow
- **Reorder blocks:** drag a block up or down over another row to change the order (same as the Inspector up/down arrows, but visual).
- **Block colors:** select a block and use the color swatch next to Duplicate (the x resets it). Works for every block type, including DOTween Pro tweens; colors are stored on the DOTween Timeline component.
- **SELF:** DOTween Property has a SELF toggle (also for Ref values) to use the components on the same GameObject as the timeline, so no separate manager object is needed.
