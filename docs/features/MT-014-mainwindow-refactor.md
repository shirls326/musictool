# MT-014: Refactor MainWindow into focused parts

**Status:** Selected (owner, 2026-11-15) — runs after MT-016 (repository hygiene) and MT-015 (UI smoke tests), and before any new feature. Spec draft; no OPEN items.

## Purpose

`MainWindow.xaml.cs` holds about 2,300 of the app's ~4,300 lines. Nearly every feature edits it, which causes merge conflicts, makes parallel feature branches impractical, and leaves important rules (such as "Space never stops recording") testable only by hand. Upcoming features — the input meter (MT-003), per-track effects (MT-013), and monitoring through effects — all land in this file, so it should be split before they are built.

## Decisions (owner, 2026-11-15)

1. **Refactor now**, before MT-003 and the effects work. It runs alone: no other feature branch is open while it is in progress.
2. **Both steps:** split the file by area (step 1), then move selected logic into small testable classes (step 2).
3. **No behavior changes.** The app must look and act exactly as before. No new features, no UI changes, no wording changes.
4. **Keep code-behind.** No MVVM framework or architectural rewrite; AGENTS.md's code-behind approach stays.
5. **Absorbs the earlier MT-014 scope** (making MT-002's device rules unit-testable); it is now step 2b.

## Step 1 — Split `MainWindow` into partial-class files

C# `partial class` lets one class span several files. Move each group of methods into its own file with **no code changes** beyond the move. Target: each file under about 400 lines.

| File | Contents (current methods) |
| --- | --- |
| `MainWindow.xaml.cs` | Fields, constructor, shared helpers (`ShowError`, `Brush`, `UpdateProjectName`) |
| `MainWindow.Devices.cs` | Audio settings and device handling (`InitializeAudioDevices` … `SameDevice`) |
| `MainWindow.Recovery.cs` | Crash recovery (`OfferProjectRecovery`, `RecoveryTimer_Tick`, `ResetRecoveryService`, …) |
| `MainWindow.Project.cs` | New/Open/Save/Save copy, import and drag-drop, add/remove track, `Window_Closing` |
| `MainWindow.Transport.cs` | Play/Stop/Record/Monitor, undo latest take, keyboard shortcuts, `FinishRecording`, `Playback_Completed` |
| `MainWindow.Timeline.cs` | `RefreshTimeline`, lanes, ruler, grid, playhead, zoom, scrolling, tempo/meter and time conversion |
| `MainWindow.Tracks.cs` | Track headers, rename, mute/volume, track selection |
| `MainWindow.Clips.cs` | Clip visuals, waveforms, trim, delete, clip selection |
| `MainWindow.LiveRecording.cs` | Live recording preview (`Recording_Progress` … `ClearLiveRecordingClip`) |

The dev session may adjust the grouping if a method clearly belongs elsewhere, and should explain changes in its summary. Commit step 1 on its own, so the diff is pure moves (`git diff --color-moved` shows this).

## Step 2 — Move logic into small, unit-tested classes

Do these in order, one commit each. **2a and 2b are required; 2c is optional and is the first thing to cut** if the work grows too large.

### 2a. `TransportController`

A plain C# class (no WPF types) that owns the play/record/monitor rules and is called by `MainWindow.Transport.cs`. Examples of rules to move and unit-test:

- Space starts, pauses, or resumes playback and never starts or stops recording (MT-009).
- Recording starts playback when project audio exists at the playhead, joins playback already running, and works without backing audio.
- Stopping a take leaves playback running; Stop is the playback control.
- Audio settings is unavailable while recording.

It talks to the audio services through small interfaces, so tests can use fakes instead of real devices.

### 2b. Device-loss rules (former MT-014)

Turn MT-002's Decision 4 into a pure function: given the lost device ID, the active input/monitor/playback device IDs, and whether recording or monitoring is on, it returns the actions to take (stop playback, turn monitoring off, finish the take) and the message. `MainWindow.Devices.cs` performs the actions. Unit-test every case in Decision 4, including "only the output was lost while recording → recording continues."

### 2c. `TrackHeaderView` user control *(optional)*

Move the track-header UI that `CreateTrackHeader` builds in code (name, rename box, mute, volume, context menu) into a XAML `UserControl`. This gives the per-track effects controls (MT-013) a clear home. Keep the timeline lane and clip drawing in `MainWindow.Timeline.cs`/`MainWindow.Clips.cs`.

## Rules for the dev session

- Build and **all** tests pass after every commit.
- Stable automation IDs added by MT-015 must be kept, so the UI smoke tests keep working.
- If a behavior looks like a bug, don't fix it here: note it in the summary and the PM adds it to the backlog.
- Don't refactor the services (`AudioRecordingService`, `ProjectStore`) in this feature.

## Safety net and acceptance checks

Before starting and after finishing:
- [ ] Normal `dotnet test` passes (unit tests).
- [ ] MT-015 UI smoke tests pass (if MT-015 is delivered; otherwise rely on the click-through below).
- [ ] MT-012 hardware tests pass (`MUSICTOOL_HARDWARE_TESTS=1`).

After finishing:
- [ ] `MainWindow.xaml.cs` and each new partial file are under about 400 lines.
- [ ] New unit tests cover the 2a transport rules and every 2b device-loss case.
- [ ] Owner click-through (about 10 minutes) shows no change in behavior:
  1. Launch: the starter project opens; recovery prompt behavior is unchanged.
  2. Import a WAV from the menu and by drag-and-drop; its waveform appears.
  3. Add a track; rename by double-click and from the context menu; Escape cancels; a blank name is rejected.
  4. Mute and volume; Play mixes; Play again pauses; resume; Stop.
  5. Space starts/pauses/resumes; with Record focused, Space never records.
  6. Click the ruler, a lane, and a clip to move the playhead; clicking during playback stops and repositions it.
  7. Drag a clip; zoom in/out; change tempo and time signature and watch the ruler update.
  8. Record on a track with backing audio; stopping the take leaves playback running; punch in over a clip; Ctrl+Z undoes it.
  9. Monitor on/off; the headphone icon turns yellow.
  10. Select a clip and delete it with Backspace and with right-click; open Set trim in/out.
  11. Audio settings opens and is disabled while recording.
  12. Save, Save copy, New, Open; close the app normally.
- [ ] AGENTS.md (Architecture) and README (How the code is organized) describe the new structure.

## Out of scope

MVVM or other architecture changes, redrawing the timeline with XAML templates, changes to services, and any behavior or UI change.
