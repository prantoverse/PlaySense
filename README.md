# PlaySense

A lightweight playtesting toolkit for Unity. PlaySense records where players go, what they look at and what they do, then gives you:

- a **Dashboard** with session metrics, a speed chart, plain-language findings and the most-looked-at objects
- a **Scene-view Replay** with the player's path, a time-spent heatmap, attention markers and events on a timeline
- a **PDF report** you can hand to a designer

No servers or SDK keys. Sessions are plain JSON files (`.pss`) on disk.

> Requires Unity 6 (6000.0+). Uses built-in physics for visibility checks.

---

## Install

**Package Manager → + → Add package from git URL…**

```
https://github.com/prantoverse/playsense.git?path=/Packages/com.prantoverse.playsense
```

Or clone this repo and open it as a project. The `Assets/Demo/Demo.unity` scene is ready to play.

## Set up a scene (1 minute)

1. Select your **player** object.
2. Run **Window → PlaySense → Set Up Scene**. This adds:
   - a `PlaySenseManager` to the scene
   - `PlaySenseAgent` + `PlaySensePerception` to your player
   - `PlaySenseView` to your gameplay camera
   - a settings asset at `Assets/Resources/PlaySenseSettings.asset`
3. Select the objects you want attention data on (doors, clues, enemies, signs) and run **GameObject → PlaySense → Mark as Attention Target**. Each needs a collider.
4. Press **Play**, play for a bit, exit Play mode. The session is saved automatically.
5. Open **Window → PlaySense → Dashboard**.

## Log gameplay events

Call this from anywhere. It's a no-op when nothing is recording, so it's safe to leave in shipping code.

```csharp
using PlaySense;

PlaySenseEvents.Log("Puzzle", "Solved", "Lever Door");
PlaySenseEvents.Log("Combat", "Died", "Spike Trap", transform.position);
PlaySenseEvents.Log("Shop", "Bought", gameObject, "item", "Lantern", "price", "40");
```

Events show up in the Dashboard, the PDF and as markers in the Replay.

## Components

| Component | Put it on | What it does |
|---|---|---|
| `PlaySenseManager` | Any object (one per scene) | Owns the session clock, samples agents, saves the session when the scene unloads or the app quits. `StartRecording()` / `StopRecording()` for manual control. |
| `PlaySenseAgent` | The player | Records position and view direction. Set **Agent Id** if you record more than one agent. |
| `PlaySensePerception` | The player (next to the agent) | Detects which targets are visible (in frustum, not occluded) and which one the player is focusing on. |
| `PlaySenseView` | Gameplay camera | The point of view used for visibility. Works without a Camera using fallback FOV. |
| `PlaySenseTarget` | Objects you care about | Marks an object for attention tracking. Optional display name, category (shown in reports) and aim point. |

## How attention works

Ten times a second (configurable), perception finds `PlaySenseTarget`s within the detection radius and keeps the ones a view can actually see: inside the camera frustum, with nothing blocking the line from the camera to the target's centre. The player's own colliders are ignored, so third-person cameras work.

A target is **attended** when it stays within the attention angle (15° by default) of the view centre for at least the minimum attention time (0.5 s). Short interruptions under the grace time (0.25 s) don't break attention. Focus only switches to another target when it is clearly more central. Each completed attention period becomes one observation, flagged as a **reinspection** if the player had already looked at that target.

Want attention on everything without tagging? Set **Target Mode** on `PlaySensePerception` to *Any Collider*. It's handy for a first look but noisy (floors and walls count).

## Settings

All thresholds live in one asset (**Window → PlaySense → Select Settings**), so the runtime, Dashboard and PDF always agree:

- **Recording:** sampling rate, max length, clock clamp (stops loading hitches from inflating session time), auto-save, record in builds
- **Perception:** scan rate, detection radius, attention angle, minimum attention, grace time, switch margin, score weights
- **Analysis:** brief/sustained attention thresholds, idle speed, teleport speed cutoff
- **Replay:** default playback speed, heatmap cell size
- **Diagnostics:** verbose logging (off by default)

## Where sessions are saved

- **Editor:** `<project>/PlaySenseSessions/`
- **Builds:** `Application.persistentDataPath/PlaySenseSessions/`

To analyse sessions from a build, copy the `.pss` files into the project's `PlaySenseSessions` folder and hit **Refresh** in the Dashboard. `PlaySenseManager.SessionSaved` fires after every save if you want to upload files yourself.

## Replay

**Window → PlaySense → Replay**, or **Replay** next to a session in the Dashboard.

- Scrub or play the timeline at 0.1–10× speed
- Toggle path, heatmap, attention, events and view cone
- Click any moment in the list to jump to it
- **Frame** zooms the Scene view to the play area; **Follow player** tracks the player while playing

Open the scene the session was recorded in so the replay lines up. The window offers a button for it.

## Project layout

```
Packages/com.prantoverse.playsense/   the package (what users install)
  Runtime/                            components, recording, storage, analysis
  Editor/                             Dashboard, Replay, PDF export, setup menu
Assets/Demo/                          demo scene and scripts (not part of the package)
```

## License

MIT. See [LICENSE](LICENSE).
