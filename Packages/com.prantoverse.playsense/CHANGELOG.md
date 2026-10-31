# Changelog

## [0.3.0] - 2026-10-31

### Added
- **Modules.** `PlaySenseModule` base class with session start/update/scene-change/stop callbacks and per-module data storage in the session (`SetModuleData` / `GetModuleData<T>`).
- **Separate assemblies per feature:** `PlaySense.Core`, `PlaySense.Attention`, `PlaySense.Performance`, plus editor assemblies for Core, Attention, Performance, Replay and Reports. Core references no module.
- **Editor extension points**, discovered automatically: `PlaySenseDashboardTab`, `PlaySenseReplayLayer`, `PlaySenseSessionAction`, `PlaySenseSetupStep`.
- **All Sessions view** in the Dashboard: % of sessions with each event, totals and per-session averages, metadata breakdowns (e.g. 70% of kills by gun), scene reach, attention across players, CSV export, scene filter.
- **Replay "All Sessions" mode:** combined heatmap, every path, every event and attention sized by how many players looked.
- **Sessions span scene loads** (`keepSessionAcrossScenes`, on by default). Scenes visited are recorded; frames, events and attention store their scene.
- **Welcome window** that opens on first load with a live setup checklist built from every module's setup steps, the event API, a module list with Add buttons, and tools.
- **Performance module:** FPS, 1% lows, hitches, per scene, with Dashboard tab.
- `PlaySenseManager.SessionStarted` event.
- Session file format v3 (older files still load).

### Changed
- `PlaySensePerception` is now the Attention module. Namespace `PlaySense.Perception` → `PlaySense.Attention`.
- Movement sampling moved into `PlaySenseMovementModule` (added to the manager automatically).
- The Dashboard is tabbed; tabs come from modules.
- "Set Up Scene" runs every module's automatic setup steps.
- Distance and paths break at scene changes as well as teleports.

## [0.2.0] - 2026-10-31

### Added
- Installable package (`com.prantoverse.playsense`) with Runtime and Editor assemblies.
- `PlaySenseEvents.Log(...)` static API for gameplay events, with optional position and key/value metadata.
- `PlaySenseTarget` component: only tagged objects receive attention by default, with display name, category and aim point.
- Replay window: Scene-view playback with path, time-spent heatmap, attention markers, events and view cone.
- Setup menu: one-click scene setup, "Mark as Attention Target", "Select Settings".
- Attention grace time and switch margin, so attention doesn't flicker between nearby objects.
- Reinspection flag, target category, view name and agent id on each attention observation.
- Session metadata: start time, Unity version, platform, format version.
- "Most attended objects" in the Dashboard and the PDF report.

### Changed
- One session clock for frames, events and attention (previously two clocks that drifted apart by seconds).
- The clock step is clamped, so load hitches no longer inflate session duration.
- Sampling keeps the timer remainder, so the real sample rate matches the setting (was ~9% low).
- `PlaySenseSettings` now drives every threshold; the Settings asset moved to `Assets/Resources`.
- Visibility is tested against the collider centre (not the pivot), ignores the player's own colliders, and uses the real camera frustum.
- Distance and speed are computed per agent, with teleports excluded.
- Builds save to `persistentDataPath`; files are written atomically.
- The Dashboard caches sessions instead of reloading every file on every repaint.
- Verbose logging is off by default.

### Fixed
- Attention in progress when the session ends is no longer lost.
- Objects with the same name are no longer merged in reports.
- Reinspection count was always 0.

### Removed
- Unused classes: `AttentionAnalyzer`, `SessionAttentionAnalyzer`, `PlaySenseBehaviourInterpreter`, `PlaySenseFindingAnalyzer`, `PlaySenseFinding`, `EventRecorder`, `AttentionData`, `SessionInfo`.
- URP template tutorial assets and orphan folder metas.
