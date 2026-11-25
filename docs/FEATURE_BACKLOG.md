# Feature Backlog

This is a list of possible improvements, not a combined implementation task. Work on **one feature at a time**, review the changes, and validate it before starting another.

## Priority and status key

- **NEXT** — the one feature selected to work on next; do not start other backlog items in the same task.
- **Proposed** — a candidate that has not been selected.
- **Implemented** — code is in place; any remaining hands-on verification is stated explicitly.
- **Feature IDs** (for example, `MT-001`) are permanent references: keep them when an item moves sections and never reuse them.
- Treat each feature entry as a developer handoff: document agreed user-visible behavior, important use cases and edge cases, persistence expectations, and focused acceptance/regression checks. Mark unresolved product choices **OPEN** instead of leaving developers to guess.
- Versioning: the app version is `<Version>` in `MusicTool.App/MusicTool.App.csproj`. The baseline is **0.1.0** (set by MT-016 on 2026-11-22), covering everything delivered up to then; earlier entries stay marked pre-versioning. While the app is pre-1.0, a feature with user-visible changes bumps the minor number (0.2.0, 0.3.0, …) and a fix bumps the patch number (0.1.1). Test-only and clean-up work that doesn't change the app (MT-014, MT-015) keeps the current version. The dev session bumps the version as part of the feature, and the delivered version is recorded on the completed entry.
- When a feature is implemented, move its full entry here rather than deleting it, and add a dated or versioned milestone to `DEVELOPMENT_LOG.md`. Keep both records; the log captures the change and rationale, while this section preserves backlog history.

## NEXT: clean-up before new features

- **Decision (owner, 2026-11-15):** refactor `MainWindow` now, before building more features, because it is the bottleneck for nearly every feature and gets harder to split later. The refactor is **MT-014** ([spec](features/MT-014-mainwindow-refactor.md)).
- **Sequence (owner-approved 2026-11-15):** MT-016 (repository hygiene, done 2026-11-22) → **MT-015** (UI smoke tests, the safety net) → MT-014 (refactor). Each runs alone. **MT-015 is NEXT** ([spec](features/MT-015-ui-smoke-tests.md); ready to build).
- **Deferred decisions** (owner, 2026-11-15; decide before MT-014 is merged): the feature order after the clean-up, splitting MT-013 into three IDs, and the MT-005 zoom gesture.

## Pending verification

Implemented features whose checks are still outstanding. Do not delete a row until its check is done; record the result and date in the feature's spec or the development log. **Automation** says what the [MT-012](features/MT-012-hardware-audio-tests.md) hardware tests (delivered 2026-11-15) already cover and what is left. Re-run them with `MUSICTOOL_HARDWARE_TESTS=1` after any change to audio code.

Verified by MT-012 on 2026-11-15 and removed from this table: a non-default microphone (Blue Snowball; the Windows default is the HyperX mic) is used for recording; a non-default output (NVIDIA 27G2G3, in the swapped output run) is used for playback and monitoring; and changing devices while monitoring restarts monitoring on the new output. Details are in the MT-012 spec.

| Feature | Check | Automation |
| --- | --- | --- |
| MT-002 | With no settings file, recording, playback, and monitoring behave as before | Partly: test 2 confirmed System default plays on the Windows default output; still listen for sound quality (no crackle/stutter) |
| MT-002 | A recorded take still lines up with the backing track after the WASAPI playback change | Partly: test 6 shows playback start delay is unchanged (WASAPI 15 ms vs old WaveOutEvent 16 ms median); final check by ear |
| MT-002 | Choices are remembered after restarting the app | Partly: persistence is unit-tested; quick check that the window shows them |
| MT-002 | Saved mic unplugged at launch → fallback notice, default mic used | Partly: fallback logic is unit-tested; notice is manual |
| MT-002 | Unplug the output during playback, monitoring, and recording → Decision 4 behavior | Manual (physical unplug); logic unit-testable after MT-014 |
| MT-002 | Unplug the mic mid-take → captured take kept, Ctrl+Z undoes it | Manual (physical unplug) |
| MT-002 | Audio settings is disabled while recording | Manual; unit-testable after MT-014 |
| MT-001 | Recover and discard an interrupted new recording and an interrupted punch-in; check missing/corrupt partial-take messages | Manual |
| MT-007 | Ctrl+Z, the project-menu command, and punch-in restoration in the running app | Manual |
| MT-008 | Double-click and context-menu rename, Enter/Escape, click-away save, track selection | Manual |
| MT-009 | Space with Play, Stop, and Record focused never starts or stops recording | Manual |

## Provisional priorities after NEXT

Revisit this order after the current feature is complete; it is a guide, not a commitment to implement these as a batch. Ratings are relative to this app's simple vocal-recording workflow.

Proposed order after the clean-up (PM, 2026-11-15; the owner will confirm it before MT-014 is merged):

1. **MT-003 — Show input level and warn when the microphone clips** — High value, medium effort and risk. Likely cheaper than it looks: `AudioRecordingService` already computes each buffer's peak. After MT-014, it can be built at the same time as MT-005/MT-006 (one audio branch plus one UI branch). Gives immediate confidence that the mic is receiving signal and helps avoid unusable takes, and helps judge levels once effects exist; the meter must remain responsive without disrupting audio.
2. **MT-013 — Vocal effects, part 1 (warmth on playback)** — High value to the owner. Establishes per-track effects, their persistence, and their controls without touching the monitoring path. Splitting MT-013 into three IDs (playback effects, reverbs, monitoring through effects) is **OPEN** for the owner.
3. **Vocal effects, part 2 — reverb presets** (small room, hall, one experimental).
4. **Vocal effects, part 3 — hear effects while monitoring.** Riskiest part; use MT-012's harness to check it adds no noticeable delay.
5. **MT-004 — Keep playback running through empty measures** — Medium value, low-medium effort and risk. Helps users continue recording or arranging after the last clip, but first decide how the eight-measure minimum is measured.
6. **MT-005 + MT-006 — Timeline UI polish** (one branch) — Mouse-wheel zoom (PM suggestion: Ctrl + wheel zooms, wheel scrolls tracks, Shift + wheel scrolls sideways; **OPEN** for the owner) and the compact Add-track button. Low risk; a good partner branch for an audio feature.

## Proposed features

1. **MT-003 — Show input level and warn when the microphone clips**
   - Show a simple input-level meter and a clear clipping warning during recording or monitoring.
   - Keep feedback responsive without disrupting capture or monitoring.

2. **MT-004 — Keep playback running through empty measures**
   - Let playback continue through sections where no audio clips are active, instead of stopping as soon as the last clip ends.
   - Support at least eight measures of empty timeline during playback.
   - Decide whether the eight-measure minimum applies after the last clip, after the playhead, or to the project timeline as a whole; confirm the expected behavior before implementation.

3. **MT-005 — Zoom the timeline with the mouse wheel**
   - Use scrolling over the timeline to zoom in and out, replacing the current zoom buttons.
   - Choose a gesture that does not interfere with scrolling through tracks or navigating the timeline, and keep the zoom centered around a useful position such as the pointer or playhead.

4. **MT-006 — Move Add track to the Tracks heading**
   - Replace the current Add track button with a compact plus-sign button beside the Tracks label.
   - Keep the action easy to find and make its purpose clear with a tooltip/accessibility name.

5. **MT-014 — Refactor MainWindow into focused parts** (selected; full spec in [features/MT-014-mainwindow-refactor.md](features/MT-014-mainwindow-refactor.md))
   - Split `MainWindow.xaml.cs` (~2,300 lines) into partial-class files by area, then move the transport rules and MT-002's device-loss rules into small unit-tested classes, optionally with a track-header user control. No behavior changes; code-behind stays.
   - Widened on 2026-11-15 from its original scope (making MT-002's device rules unit-testable, split out of MT-012), which is now step 2b.

6. **MT-015 — UI smoke tests** (NEXT; approved by the owner 2026-11-15; full spec in [features/MT-015-ui-smoke-tests.md](features/MT-015-ui-smoke-tests.md); all decisions made 2026-11-23)
   - **Goal:** a small automated safety net for the refactor that drives the real app window and checks key behaviors still work, and replaces several manual rows in **Pending verification** (MT-008 rename, MT-009 Space safety, clip delete, Audio settings opening).
   - **Approach (proposed):** FlaUI, a UI Automation library, launches the built app and clicks/types like a user. Tests are opt-in (`MUSICTOOL_UI_TESTS=1`) because they take over the mouse and keyboard, and they live in a separate test project so the normal `dotnet test` stays fast.
   - **Small app changes required:** (1) a test-only data-folder override so the app uses a temp folder for its starter project, recovery checkpoints, and `settings.json` and never touches the owner's real projects; (2) stable automation IDs on controls the app builds in code (track headers, names, mute/volume, clips). No visible behavior change.
   - **Limits:** UI tests are slower and more brittle than unit tests, so keep the set to about 8–10. Behaviors needing a microphone stay with the hardware tests or the manual list.

## Implemented features

### MT-016 — Repository hygiene

- **Status:** Implemented 2026-11-22 and merged via PR #1. PM review found no issues. Build passes; tests: 42 passed, 6 hardware tests skipped. The first GitHub Actions runs (pull request and `master`) were green. **Version:** 0.1.0 (baseline).
- Added `.gitattributes` (text stored as LF, `.sln` kept CRLF, audio/images binary), a GitHub Actions workflow that builds and runs the unit tests on Windows for pushes and pull requests to `master`, the shared `.vscode/settings.json`, and `<Version>0.1.0</Version>` in the app project. No C# code changed.
- **Finding:** Git's stored copies were already all LF, so renormalizing changed nothing and no separate line-ending commit was needed. The LF/CRLF warnings came from three working copies only, which were rewritten.
- **Also introduced:** the first learning note, [docs/learning/MT-016-repository-hygiene.md](learning/MT-016-repository-hygiene.md). Learning notes are now standard for every feature (see Learning mode in [WORKFLOW.md](WORKFLOW.md)).
- **History:** See the [MT-016 spec](features/MT-016-repository-hygiene.md) and the [development log](DEVELOPMENT_LOG.md#mt-016-repository-hygiene-delivered-2026-11-22).

### MT-012 — Automated hardware audio tests

- **Status:** Implemented 2026-11-15 (test code only; no app changes). Normal `dotnet test`: 42 passed, 6 hardware tests skipped. Hardware run on the owner's PC: 6 of 6 passed, twice with the HyperX as the main output and once with output roles swapped (NVIDIA 27G2G3 as the main output), which confirms selection of a non-default output. **Version:** Pre-versioning.
- Hardware tests are skipped unless `MUSICTOOL_HARDWARE_TESTS=1`. They use loopback capture and a 1 kHz tone to confirm which device playback, recording, and monitoring use, and they measure playback start delay.
- **Result:** moving playback to WASAPI (MT-002) did not slow playback start: 15 ms median vs 16 ms for the old `WaveOutEvent` path, and more consistent (15–16 ms vs 15–45 ms). These numbers stop at the Windows mixer; USB/headset delay comes on top.
- **Origin and decisions:** proposed by the implementation session after MT-002. Owner decisions (2026-11-15): lighter process; test code only (the `MainWindow` refactor moved to MT-014); compare WASAPI and `WaveOutEvent` latency once; test devices Blue Snowball, HyperX Cloud III, and an NVIDIA monitor output.
- **History:** See the [MT-012 spec](features/MT-012-hardware-audio-tests.md) and the [development log](DEVELOPMENT_LOG.md#mt-012-hardware-audio-tests-delivered-2026-11-15).

### MT-001 — Autosave/recovery after a crash

- **Status:** Implemented; automated tests and completed-edit startup recovery pass. Hands-on interrupted-capture and punch-in recovery verification remain. **Version:** Pre-versioning.
- Recover completed project edits without overwriting a valid saved project or restoring an inconsistent recording.
- On the next launch, offer recovery when a usable interrupted session is found; do not silently restore or discard it.
- Include a best-effort recovery of a usable unfinished recording. Present it as a clearly marked incomplete take and add it only after the user chooses to recover it.
- Never apply punch-in replacement automatically during recovery; leave the original project audio unchanged unless the user explicitly chooses an action that replaces it.
- If an interrupted recording cannot be recovered, explain that clearly rather than silently discarding it.
- Test crash recovery with completed edits, an interrupted new take, and an interrupted punch-in; cover both recover and discard choices and verify the original project remains intact.
- **History:** See [Autosave and crash recovery](DEVELOPMENT_LOG.md#autosave-and-crash-recovery) and the [MT-001 implementation notes](features/MT-001-autosave-recovery.md).

### MT-002 — Configure audio devices in Settings

- **Status:** Implemented 2026-11-15 and accepted by the owner with hardware checks deferred; build and all 35 automated tests pass, PM review found no blocking issues, and the Settings window was smoke-checked in the running app. Hands-on checks with real microphones and headphones are still needed (tracked in **Pending verification**), especially that takes still line up with the backing track after playback moved to WASAPI. **Version:** Pre-versioning.
- **Audio settings...** in the project menu chooses the microphone and one output (headphones) used for both playback and input monitoring. Each list starts with **System default**, the initial choice. The window shows the devices in use and whether monitoring is on.
- Choices are app preferences in `%LOCALAPPDATA%\MusicTool\settings.json`, not project settings; a missing or corrupt file loads as defaults. Devices are saved by WASAPI ID with the friendly name for display.
- A saved device missing at launch falls back to System default for that session, keeps the saved preference, and shows a status-bar notice.
- Disconnect while in use: playback stops, monitoring turns off, and a lost microphone ends the take with its captured audio kept. Losing only the output does not stop a recording.
- The entry is disabled while recording; pressing OK during playback stops it first; monitoring restarts on the new devices.
- **History:** See [Audio device settings](DEVELOPMENT_LOG.md#audio-device-settings-2026-11-15) in the development log and the [MT-002 spec](features/MT-002-audio-devices.md).

### MT-007 — Undo the latest recording

- **Status:** Implemented; verify Ctrl+Z, the project-menu command, and punch-in restoration in the running app. **Version:** Pre-versioning.
- Undo is limited to the most recently completed take in the current app session. It restores the target track's clip arrangement from immediately before that take, including audio displaced by punch-in. A later take replaces the undo state; switching projects clears it. No redo or general edit undo is included.
- **History:** See [Undo the latest recording](DEVELOPMENT_LOG.md#undo-the-latest-recording) in the development log.

### MT-008 — Rename tracks

- **Status:** Implemented; verify double-click, context-menu rename, Enter/Escape, click-away save, and track selection in the running app. **Version:** Pre-versioning.
- Double-click the track name to edit inline, or choose **Rename track...** from the track header's right-click menu. Enter or clicking away saves; Escape cancels. Leading/trailing whitespace is trimmed and blank names are rejected. Duplicate names are allowed.
- Track names are persisted in the project; renaming does not alter clips or track settings.
- **History:** See [Track renaming](DEVELOPMENT_LOG.md#track-renaming) in the development log.

### MT-009 — Make Space safe for playback

- **Status:** Implemented; hands-on verification in the running app is still needed. **Version:** Pre-versioning.
- Space toggles playback only: start, pause, or resume. It must not activate the focused Record button or start/stop recording, and is ignored while editing text or using a combo box.
- **History:** See [Space playback shortcut safety](DEVELOPMENT_LOG.md#space-playback-shortcut-safety) in the development log.

## Later ideas

- These are unranked ideas; consider them after the provisional priorities above.
- **MT-010 — Latency confidence:** measure round-trip latency and explain app monitoring versus direct monitoring.
- **MT-011 — Editing:** expand undo/redo to clip moves, trims, and track operations after recording undo is reliable.
- **MT-017 — Visual redesign** *(owner idea, 2026-11-23)*: refresh the app's look using designs explored in Figma ([prompt](design/figma-prompt.md)). Build it only after MT-014, when the UI is split into smaller parts. It could absorb MT-006 (compact Add track button). Direction (owner, 2026-11-23): dark, bold, premium and slick, with sunset colors on near-black; neo-brutalist "tech" type is being explored. User-selectable color themes are a possible later feature, so tokens are designed with theme modes from the start. Visuals only, with no behavior changes; the MT-015 UI tests must still pass.
- **MT-013 — Vocal effects library** *(owner idea, 2026-11-15; provisionally ranked 2–4 in the order above)*
  - **Idea:** a built-in library of simple effects, such as several kinds of reverb (for example room, hall, plate) and a "brighten" EQ for vocals, applied to a track. When input monitoring is on for a track that has effects, the user hears the effects live while singing.
  - **Fits the product if:** effects stay preset-based with one or two plain controls (for example "Amount"), not a full plug-in rack. Third-party plug-ins (VST) remain out of scope.
  - **Likely shape when selected (PM suggestion):** split into separate features instead of one large one:
    1. Per-track effects on playback, starting with warmth (a tone/EQ filter; NAudio's `BiQuadFilter` provides this).
    2. Reverb presets (NAudio has no reverb, so this needs a built-in algorithm such as Freeverb, or a library).
    3. Hear the selected track's effects while monitoring.
  - **Constraints to respect:** effects are saved per track in the project file, and older projects open with no effects. Monitoring through effects only works with in-app monitoring; an audio interface's direct-monitor path bypasses the app. Effects must not add noticeable monitoring delay or stutter (see the WASAPI monitoring entries in the development log), so MT-012's latency harness is a useful prerequisite.
  - **Decided (owner, 2026-11-15) — Dry takes:** recordings save the plain voice; effects are applied during playback and monitoring only, so changing or removing an effect later changes how existing takes sound without re-recording.
  - **Decided (owner, 2026-11-15) — Monitoring follows the vocal track's effects:** if the track being recorded on has effects turned on, input monitoring plays the voice through those same effects; if the track has no effects, monitoring stays dry (today's behavior). There is no separate monitor-effects setting. When monitoring while idle, "the track" is the selected track, the one Record would use (confirmed by the owner).
  - **Decided (owner, 2026-11-15) — Starter set:** small-room reverb, large hall reverb, at least one experimental reverb, and warmth. Brighten (part of the original idea), echo/delay, and compression come later.
  - **OPEN — Which experimental reverb:** decide at spec time. Candidates: *shimmer* (a sparkly, higher-pitched tail; most complex), *gated* (a big reverb that cuts off abruptly; simple), or *reverse* (swells into each word). Reverse reverb needs audio that hasn't been sung yet, so it can work on playback but **cannot** be heard live while monitoring.

## Suggested starting point

Clean-up comes first: MT-016 (repository hygiene, done) → MT-015 (UI smoke tests) → MT-014 (MainWindow refactor), then MT-003 leads the feature order. Outstanding checks for implemented features are tracked in **Pending verification**; MT-002's are the most important because playback now uses WASAPI. The WASAPI monitoring improvement (`2df738a`) is separate and should remain unchanged while working on backlog items.
