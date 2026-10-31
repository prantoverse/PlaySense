# PlaySense × Unity FPS Microgame

Drop-in bridge that records Unity's FPS Microgame with PlaySense, without editing any Microgame code.

1. Create the **FPS Microgame** from Unity Hub (Learn tab / Learning templates), Unity 6.
2. Install PlaySense: Package Manager → + → Add package from git URL →
   `https://github.com/prantoverse/PlaySense.git?path=/Packages/com.prantoverse.playsense`
3. Copy `PlaySenseFPSMicrogameBridge.cs` into the project's `Assets/` folder.
4. Open `MainScene`, select the `Player`, run **Window → PlaySense → Set Up Scene**.
5. Add `PlaySenseFPSMicrogameBridge` (and optionally the Performance module) to the `PlaySenseManager` object.
6. Play. Exit Play mode to save the session.

Logs: kills (weapon, enemy), shots (weapon), damage taken (source), deaths (killed by), pickups, ammo, objectives, win/lose. Enemies and pickups are tagged as attention targets automatically.
