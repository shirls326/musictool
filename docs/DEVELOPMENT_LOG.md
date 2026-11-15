# Music Tool Development Log

This file records the product's development milestones: what changed, why it changed, and what still needs attention. Add a new entry whenever a feature or meaningful product decision is made.

## Project so far

The project is a personal, Windows 11 music-making app for approachable vocal projects. The goal is to make it easy to bring in a karaoke/backing track, record and monitor vocals, and arrange simple multitrack projects without the complexity of a full digital audio workstation.

The current app uses **C# WPF** for its Windows desktop interface and **NAudio** for audio input, output, and files. The initial prototype and project files were committed together as `ac90533` (`Initial Music Tool prototype`); the milestones below describe the work represented by that prototype.

## Milestones

### Project brief and implementation direction

- **Changed:** Defined the product as a simple, local-first Windows app for small vocal and backing-track projects. Chose C# WPF + NAudio as the first implementation stack.
- **Why:** WPF and C# provide a direct route to a Windows desktop app, while NAudio supplies building blocks for audio capture and playback. This keeps the first version more approachable than an audio-focused C++ framework such as JUCE.
- **Status:** Initial direction. Audio-device behavior and monitoring latency still need to be evaluated on the intended hardware.

### First runnable application

- **Changed:** Created a .NET 8 WPF application and solution, organized into a window, project/track models, and separate project-file and audio services. Added instructions for running the app.
- **Why:** A small, runnable baseline makes it possible to build and learn incrementally, instead of trying to implement a complete music workstation before testing the core workflow.
- **Status:** Builds and launches on the development machine.

### Local projects and imported audio

- **Changed:** Added local project files with a `Media` folder, import and drag-and-drop for WAV/MP3 files, and project open, save, and save-copy actions. Imported audio is copied into the project.
- **Why:** Keeping media with a project makes it easier to reopen, move, and back up a small music project.
- **Status:** Project persistence and media-copy behavior have automated tests.

### Multitrack recording workspace

- **Changed:** Replaced the plain track table with a dark studio-style workspace: a time ruler, colored track lanes, waveform previews, horizontal scrolling, timeline zoom, track creation/removal, and drag-to-position clips.
- **Why:** A visual timeline makes the relationship between backing audio and vocal takes easier to understand and is closer to the simple recording-app experience the product aims for.
- **Status:** Basic clip arrangement and timeline scrubbing are implemented; more advanced editing remains future work.

### Basic clip and track controls

- **Changed:** Added per-track volume and mute controls, plus clip trimming through a right-click action. Added playback that mixes tracks at their selected start times.
- **Why:** These are useful, visible editing controls for common vocal projects without introducing a large mixer or advanced audio concepts.
- **Status:** Basic controls are implemented; the app does not yet include effects, plug-ins, or MIDI.

### Vocal recording and live input monitoring

- **Changed:** Added microphone recording into a WAV take on a selected empty track. The **Monitor input** checkbox can monitor the microphone while recording or while idle. Monitoring remains active after recording stops if the checkbox stays on; unchecking it stops monitoring. Turning monitoring off during a recording leaves recording active.
- **Why:** It should be possible to hear the microphone before recording as well as during a take, while keeping recording and monitoring as separate controls.
- **Status:** Implemented, but recording quality, device compatibility, latency, and feedback behavior still need hands-on testing with the intended microphone and headphones.

### Timeline playhead and click-to-seek

- **Changed:** Added a red playhead line across the ruler and track lanes. Clicking the ruler or an empty lane places it; clicking a clip places it at that point in the clip; pressing Play starts the mix from that timeline time. While audio plays, the line advances and the timeline follows it. Clicking during playback stops and moves the playhead.
- **Why:** A playhead is the familiar visual reference for "where am I?" in a recording timeline, and lets the user listen from a chosen point without restarting every track from zero.
- **Documentation:** Created the `docs/` folder and moved this feature log into it so product history has a dedicated home.
- **Status:** Implemented and build/test checked. Listen to playback from clicks at different points with real audio files and hardware to validate seeking behavior.

### Tempo and bar-based timeline

- **Changed:** Added a project tempo control (20-300 BPM, default 128) and common time-signature choices (2/4, 3/4, 4/4, 5/4, 6/8, 7/8, and 12/8; default 4/4). The ruler and track grid now show numbered bars and beat divisions. Clip positions, widths, drag operations, and playhead rendering convert between timeline bars and the existing seconds-based audio positions. Tempo and meter persist with each project.
- **Why:** Measure/bar markings are a more familiar musical way to arrange tracks than a ruler labeled only in elapsed seconds.
- **Behavior decision:** Changing tempo or meter preserves each clip's real-time position and source playback speed, so its displayed bar may change. Tempo is measured in quarter notes per minute; this version does not time-stretch clips or add a metronome. Clip trim-in/out remain in seconds because they edit the source recording.
- **Status:** Implemented with project-persistence and musical-time conversion tests. Verify the controls and ruler visually in the running app.

### Icon-based transport controls

- **Changed:** Replaced Play and Stop text labels with icon buttons, changed Record to a red dot that becomes a stop-square while recording, and replaced the labeled input-monitor checkbox with a headphone toggle that highlights yellow when enabled. Added tooltips and accessibility names for the icon controls.
- **Why:** The transport should be quick to scan and visually simple while remaining understandable through hover text and assistive technology.
- **Status:** Implemented and build/test checked. Validate monitoring and recording states with the intended audio hardware.

### Play/pause toggle and Space-to-stop shortcut

- **Changed:** The Play icon changes to Pause while playback runs. Clicking it pauses the current audio stream and clicking it again resumes at the same playhead position. Pressing Space during playback stops the mix using the same behavior as the Stop button; Space is ignored while focus is in a text box or combo box.
- **Why:** Pause/resume and a keyboard stop shortcut make quick listening and navigation more convenient.
- **Status:** Implemented and build/test checked. Verify pause/resume against actual audio playback devices.

### Compact project menu and single-row transport layout

- **Changed:** Moved New/Open/Import audio/Save/Save copy into a styled top-left project flyout opened by a three-dot button. The project name is left-aligned beside it; the transport remains middle-left on the same row, BPM/time signature remain centered, and zoom stays on the right.
- **Why:** Keep project actions accessible while reducing header clutter and making the transport easier to find.
- **Status:** Implemented and build/test checked. Verify menu interaction and layout at different window sizes.

### Punch-in recording on the selected track

- **Changed:** Record now targets the selected track instead of silently creating another track when the selection already has audio. A take starts at the playhead and replaces only the overlapping timeline region; audio before and after the take is preserved.
- **Why:** Let users redo a specific vocal section in place without losing the rest of the track or creating an unexpected new track.
- **Status:** Implemented with separate clip records and project-persistence tests. Validate recording and monitoring with the intended microphone and headphones.

### Separate clips and live recording preview

- **Changed:** Tracks now hold multiple audio clips. Existing project tracks are migrated to a single clip when opened. Recording shows a growing waveform and advancing playhead; each stopped take stays separate with its gap visible. Punching into existing audio splits the old clip around the replaced interval instead of flattening the result.
- **Why:** Make recording position and progress visible, support takes at later measures on the same track, and keep edits non-destructive outside the punch-in range.
- **Status:** Implemented and covered by project migration, separated-take, and overlap-splitting tests. The solution builds and all 11 automated tests pass; live capture still needs verification with the intended microphone and headphones.

### Reduced input-monitoring buffer latency

- **Experiment:** Reduced microphone capture buffers from 50 ms to 20 ms and the monitoring output latency target from 80 ms to 40 ms.
- **Result:** On the Blue Snowball USB microphone and HyperX USB headset, the change made monitoring more stuttery and laggy, so the original 50 ms / 80 ms values were restored.
- **Takeaway:** Buffer tuning alone is not a reliable fix across USB input/output devices. Software monitoring still has noticeable delay; an audio interface's direct-monitor path is the recommended low-latency option when available.

### WASAPI event-driven input monitoring

- **Changed:** Replaced the legacy WaveIn/WaveOut monitor path with Windows WASAPI shared-mode event-driven capture and playback. The monitor buffer is now bounded to 100 ms and clears stale queued audio on overflow so a delayed queue cannot build up indefinitely.
- **Why:** The legacy path produced a noticeable delay on the Blue Snowball USB microphone and HyperX USB headset, and smaller legacy buffers caused stuttering. WASAPI lets Windows service capture and output with event-driven buffers while retaining shared access to the user's normal devices.
- **Status:** Build and automated tests pass. Perceived delay and stability still need to be checked on the user's actual devices; if it stutters or remains delayed, device/driver behavior will guide the next adjustment.

### Space playback shortcut safety

- **Changed:** Space now starts, pauses, or resumes playback and is consumed at the window preview-key stage so a focused transport button cannot interpret it as a click. Repeated key-down events do not toggle repeatedly; Space remains ignored in text and combo-box inputs.
- **Why:** Playback shortcuts should be predictable, and recording should always require an explicit click on Record. Pressing Space does not stop an active recording.
- **Status:** Build and automated tests pass. Verify Space with Play, Stop, and Record controls focused in the running app.

### Selecting and deleting audio clips

- **Changed:** Clicking or right-clicking a clip selects it and gives it a distinct highlight. The clip context menu now offers Delete clip, and Backspace deletes the selected clip when focus is outside text/combo inputs.
- **Why:** Clip-level selection makes destructive edits explicit and lets users remove one take without removing its entire track.
- **Behavior:** Existing click-to-position and track-selection behavior remains. Deletion saves the project but leaves source media in the project folder because multiple clips may share one audio file. Deleting a clip on the actively recording track is blocked.
- **Status:** Project persistence is covered by a shared-media deletion test; build/test checks pass. Verify selection, context-menu deletion, keyboard deletion, and recording protection in the running app.

### Hear project tracks while recording

- **Changed:** Recording now starts project playback from the playhead when audio is scheduled there, or joins playback already running. A take can still be recorded without backing audio. Stopping a take leaves playback running; Stop remains the playback control.
- **Why:** Vocal recording over a backing track should be a single, natural action instead of requiring a separate Play click.
- **Status:** Playback eligibility is covered by a trim-boundary test. Verify audio synchronization and the separate record/stop behavior with the intended microphone and headphones.

### Track renaming

- **Changed:** Track names can be edited inline by double-clicking the name or choosing **Rename track...** from the track header's context menu. Enter or clicking away saves, Escape cancels, leading/trailing whitespace is trimmed, and blank names are rejected. Duplicate names are allowed.
- **Why:** Users can give backing tracks and vocal tracks meaningful names without changing their audio or interfering with single-click track selection.
- **Status:** Project-store tests cover trimming, blank-name rejection, and persistence after reopening. Verify the UI gestures and keyboard behavior in the running app.

### Undo the latest recording

- **Changed:** Added **Ctrl+Z** and a project-menu command to undo the most recently completed take during the current app session. Undo restores the target track's clip arrangement from immediately before that take, including audio removed by punch-in. A later take replaces the undo state; switching projects clears it. No redo or general edit undo is included.
- **Why:** Let users safely reject a take without manually rebuilding the previous clips.
- **Status:** Project-store tests cover punch-in restoration and recovery of original media after Save As. Verify the keyboard/menu actions and recording workflow in the running app.

### Autosave and crash recovery

- **Changed:** Added a 15-second recovery checkpoint containing the project state and copies of referenced media. On startup, the user can recover it as a separate project copy or discard it. Captured audio from an interrupted recording can be separately accepted as a clearly labeled `INCOMPLETE` track; it never replaces existing clips.
- **Why:** Preserve recent project edits after an unexpected shutdown while keeping the last saved project and its audio safe.
- **Behavior:** Graceful close saves first and then clears the active checkpoint. Snapshot and media errors are surfaced. Missing or invalid partial takes are explained while completed edits remain recoverable.
- **Documentation:** See [MT-001 implementation notes](features/MT-001-autosave-recovery.md) for storage details, safety rules, and validation cases.
- **Status:** All 22 automated tests pass. Manually created a checkpoint from a copy of the starter project, accepted the actual startup recovery prompt, and confirmed a separate recovered project contained all three tracks and four clips with identical audio hashes; the original project remained unchanged. Live interrupted-recording and punch-in recovery still need hands-on testing. Checkpoints live under `%LOCALAPPDATA%\MusicTool\Recovery`; accepted project copies go to `Documents\Music Tool Projects\Recovered`.

### Two-session development workflow (2026-11-14)

- **Changed:** Adopted a product-manager session and a separate implementation session, with feature specs in `docs/features/` as the handoff. Documented the loop in [WORKFLOW.md](WORKFLOW.md). Selected MT-002 (audio device settings) as the next feature and drafted its [spec](features/MT-002-audio-devices.md).
- **Why:** Keep one feature in progress at a time, settle product decisions before coding, and have implementation reviewed against written acceptance checks.
- **Status:** Process in place. The owner decided all MT-002 OPEN items on 2026-11-14; see the next entry.

### Audio device settings (2026-11-15)

- **Changed:** Added **Audio settings...** to the project menu (MT-002). A modal window chooses the microphone and a single output used for both playback and input monitoring, each with **System default** as the first and initial option. Choices are saved as app preferences in `%LOCALAPPDATA%\MusicTool\settings.json`. Project playback moved from WinMM `WaveOutEvent` to shared-mode WASAPI `WasapiOut` so it can use the same selected output as monitoring.
- **Why:** Let the user pick the intended microphone and headphones inside Music Tool instead of changing Windows sound settings. One output keeps the backing track and monitored voice in the same headphones.
- **Behavior decisions:** A saved device missing at launch falls back to System default for that session, keeps the saved preference, and shows a status-bar notice. If a device in use disconnects, playback stops, monitoring turns off, and a lost microphone ends the take while keeping its captured audio; losing only the output does not stop a recording. Settings are unavailable while recording; changing devices stops playback and restarts monitoring.
- **Implementation notes:** Windows endpoint notifications detect unplugged devices because event-driven WASAPI streams can stall without reporting an error. Playback uses a new 200 ms playback-only buffer; the monitoring and capture buffer constants are unchanged. See the [MT-002 spec](features/MT-002-audio-devices.md#implementation-notes).
- **Status:** Build and all 35 automated tests pass (13 new: settings persistence and device resolution). Smoke-checked in the running app: the Settings window listed the connected microphones and outputs, and Cancel saved nothing. Hands-on checks with real devices are still needed: device selection, restart persistence, unplug behavior, and whether a take still lines up with the backing track after the playback change.

## Verification so far

- The WPF solution builds successfully with the .NET 8 SDK.
- Automated tests cover app settings persistence (round-trip, missing and corrupt files), saved-device resolution and fallback, project persistence, copied media, recovery checkpoints, interrupted-take metadata and separate-track recovery, empty tracks/recording-track persistence, unsupported import types, tempo and meter persistence, backward-compatible project defaults, musical time conversion, separate takes, punch-in clip splitting, legacy-track migration, and deleting a clip while preserving shared media.
- The app has been launched successfully on the development machine.
- Automated checks do not substitute for testing audio with the actual microphone, output device, and headphones.

## Current gaps and possible next work

- Test live input monitoring and recording with the intended audio hardware; adjust the capture/output approach if latency or device routing is unsuitable.
- Test playhead seeking during actual backing-track playback and improve clip editing based on actual use.
- Verify MT-002 audio-device selection and disconnect handling on real hardware, and confirm takes still line up with backing tracks now that playback uses WASAPI.
- Consider additional file formats or richer mixing controls only when the core workflow calls for them; see `FEATURE_BACKLOG.md` for the ranked list.

## How to maintain this log

For each meaningful change, add a dated or versioned milestone at the bottom of **Milestones** (entries are in chronological order; earlier entries are undated, and their dates are in git history). State what changed, why it was chosen, and whether it is implemented or still needs verification. Keep this log focused on product and feature history; use the README for current run instructions and current usage guidance.
