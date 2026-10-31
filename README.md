# PlaySense

A modular playtesting toolkit for Unity. PlaySense records where players go, what they look at and what they do, then shows you:

- a **Dashboard** for each session, plus an **All Sessions** view that answers "how many players did X?"
- a **Scene-view Replay** with paths, a time-spent heatmap, attention and events, for one session or every session overlaid
- a **PDF report** for one session
- a **Welcome window** with a live setup checklist, which opens the first time the package is loaded

No servers or SDK keys. Sessions are plain JSON files (`.pss`) on disk.

> Requires Unity 6 (6000.0+).

---

## Install

**Package Manager → + → Add package from git URL…**

```
https://github.com/prantoverse/playsense.git?path=/Packages/com.prantoverse.playsense
```

The **Welcome to PlaySense** window opens on its own. Its checklist does each setup step for you and ticks it off. You can reopen it any time from **Window → PlaySense → Welcome & Setup Guide**.

Or clone this repo and open it as a project; `Assets/Demo/Demo.unity` is ready to play.

## Quick start

1. Select your **player** and run **Window → PlaySense → Set Up Scene**. This adds the manager, marks the player, and adds attention tracking and a camera view.
2. Select the objects you want attention data on and run **GameObject → PlaySense → Mark as Attention Target**.
3. Add `PlaySenseEvents.Log(...)` calls wherever something interesting happens (see below).
4. Press **Play**, play, exit Play mode. The session saves automatically.
5. Open **Window → PlaySense → Dashboard**.

## Track what players do

```csharp
using PlaySense;

PlaySenseEvents.Log("Puzzle", "Solved", "Lever Door");
PlaySenseEvents.Log("Combat", "AbilityUsed", "Shockwave", transform.position);
PlaySenseEvents.Log("Combat", "Kill", enemy, "method", "gun", "weapon", "AR");
```

`Log` does nothing when PlaySense isn't recording, so the calls can stay in shipping code. The key/value pairs after the target become **breakdowns** in the All Sessions view.

### Example: "how many players used abilities vs. raw gunfights?"

Log every kill with how it happened:

```csharp
PlaySenseEvents.Log("Combat", "Kill", enemy, "method", usedAbility ? "ability" : "gun");
PlaySenseEvents.Log("Combat", "AbilityUsed", abilityName, transform.position);
```

After a playtest, open **Dashboard → All Sessions → Events**:

```
Combat / Kill          20/20 (100%)   200x, 10/session
   method
      gun              70% of events, 20 session(s)
      ability          30% of events, 20 session(s)
Combat / AbilityUsed   14/20 (70%)    14x, 0.7/session
```

That reads as *14 of 20 players used an ability; 70% of all kills were gunfire.* **Export CSV** puts the same table in a spreadsheet. The **Replay → All Sessions** mode shows *where* abilities were used across everyone.

"% of sessions" means "% of players" when each tester plays once. Filter by scene to compare levels; the Summary tab shows how far players got (the share of sessions that reached each scene).

## Modules

Each module records one kind of data. Remove a module's folders and its components, Dashboard tab, Replay layer and checklist steps go with it.

| Module | Component | Goes on | Records |
|---|---|---|---|
| **Core** (required) | `PlaySenseManager`, `PlaySenseAgent` | manager / player | Session clock, scenes, gameplay events |
| **Movement** | `PlaySenseMovementModule` | manager (added automatically) | Player paths, speed, idle time, heatmap |
| **Attention** | `PlaySensePerception` + `PlaySenseView` + `PlaySenseTarget` | player / camera / objects | What players look at and for how long |
| **Performance** | `PlaySensePerformanceModule` | manager | FPS, 1% lows, hitches, per scene |

Editor features are modules too: **Replay**, **Reports** (PDF), and each module's Dashboard tab.

### Assemblies

```
PlaySense.Core                 runtime: manager, session, movement, events, storage, analysis
PlaySense.Attention            runtime: perception, views, targets            → Core
PlaySense.Performance          runtime: frame rate                            → Core
PlaySense.Editor               dashboard, overview, welcome, setup, extension points → Core
PlaySense.Attention.Editor     attention tab, replay layer, setup steps
PlaySense.Performance.Editor   performance tab
PlaySense.Replay.Editor        Replay window + path/heatmap/events layers
PlaySense.Reports.Editor       PDF export
```

Core never references a module, so you can delete any module folder and the rest still compiles.

## Write your own module

Subclass `PlaySenseModule`, put it on the manager (or player), and store any `[Serializable]` class in the session:

```csharp
using System;
using System.Collections.Generic;
using PlaySense.Core;

[Serializable]
public class HealthData
{
    public List<float> Times = new List<float>();
    public List<float> Health = new List<float>();
}

[PlaySenseModuleInfo("Health", "Player health over time.")]
public class HealthModule : PlaySenseModule
{
    public PlayerHealth player;
    private HealthData _data;
    private float _timer;

    public override void OnSessionStart(PlaySenseSession session) => _data = new HealthData();

    public override void OnSessionUpdate(PlaySenseSession session, float deltaTime)
    {
        if ((_timer += deltaTime) < 1f) return;
        _timer = 0f;
        _data.Times.Add(session.Time);
        _data.Health.Add(player.Current);
    }

    public override void OnSessionStop(PlaySenseSession session) => session.SetModuleData(ModuleId, _data);
}
```

Read it back with `session.GetModuleData<HealthData>("HealthModule")`. To give it a Dashboard tab, subclass `PlaySenseDashboardTab` in an Editor assembly. PlaySense finds tabs, Replay layers (`PlaySenseReplayLayer`), Dashboard buttons (`PlaySenseSessionAction`) and checklist items (`PlaySenseSetupStep`) automatically. `PlaySensePerformanceModule` and its editor are a complete working example.

Module callbacks:

| Callback | When |
|---|---|
| `OnSessionStart` | Recording starts, or the module is enabled mid-session |
| `OnSessionUpdate` | Every frame, after the session clock advances |
| `OnSceneChanged` | The active scene changes (sessions span scene loads) |
| `OnSessionStop` | Recording stops, or the module is disabled mid-session |

## Sessions across scenes

With **Keep Session Across Scenes** on (the default), the manager survives scene loads, so one playthrough is one session. Managers placed in later levels step aside automatically. Every frame, event and attention record stores which scene it happened in; the Dashboard can filter by scene, and Replay only draws data from the scene you have open. Turn the option off to record each scene as its own session.

## How attention works

Several times a second, perception finds `PlaySenseTarget`s near the player and keeps the ones the camera can see: inside the frustum, with nothing in between (the player's own colliders are ignored, so third-person works). A target is **attended** when it stays within the attention angle (15° by default) of the view centre for the minimum attention time (0.5 s). Brief interruptions under the grace time don't break it, and focus only switches when another target is clearly more central. Repeat looks are flagged as **reinspections**.

## Settings

All thresholds live in one asset (**Window → PlaySense → Select Settings**): sampling and perception rates, max session length, clock clamp, scene persistence, attention angle/timing, analysis cut-offs, replay defaults and verbose logging.

## Where sessions are saved

- **Editor:** `<project>/PlaySenseSessions/`
- **Builds:** `Application.persistentDataPath/PlaySenseSessions/`

To analyse a build playtest, collect testers' `.pss` files into the project's `PlaySenseSessions` folder and hit **Refresh**. `PlaySenseManager.SessionSaved` fires after every save if you want to upload them yourself.

## Project layout

```
Packages/com.prantoverse.playsense/
  Runtime/Core | Attention | Performance
  Editor/Core | Attention | Performance | Replay | Reports
Assets/Demo/                demo scene and scripts (not part of the package)
```

## License

MIT. See [LICENSE](LICENSE).
