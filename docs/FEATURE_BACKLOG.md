# Feature Backlog

This is a list of possible improvements, not a combined implementation task. Work on **one feature at a time**, review the changes, and validate it before starting another.

## Priority and status key

- **NEXT** — the one feature selected to work on next; do not start other backlog items in the same task.
- **Proposed** — a candidate that has not been selected.
- **Implemented** — code is in place; any remaining hands-on verification is stated explicitly.
- **Feature IDs** (for example, `MT-001`) are permanent references: keep them when an item moves sections and never reuse them.
- Treat each feature entry as a developer handoff: document agreed user-visible behavior, important use cases and edge cases, persistence expectations, and focused acceptance/regression checks. Mark unresolved product choices **OPEN** instead of leaving developers to guess.
- Recommended versioning: use `v0.x.y` while the app is pre-1.0, increment the minor number for user-facing features and the patch number for fixes, and record the delivered version on completed entries. The project does not currently declare an app version, so existing features are marked pre-versioning rather than assigned invented versions; assign a baseline version explicitly when versioned builds begin.
- When a feature is implemented, move its full entry here rather than deleting it, and add a dated or versioned milestone to `DEVELOPMENT_LOG.md`. Keep both records; the log captures the change and rationale, while this section preserves backlog history.

## NEXT: not yet selected

- MT-002 is implemented (see **Implemented features**). The PM and owner choose the next feature per the [development workflow](WORKFLOW.md); MT-003 is first in the provisional priorities below.

## Provisional priorities after NEXT

Revisit this order after the current feature is complete; it is a guide, not a commitment to implement these as a batch. Ratings are relative to this app's simple vocal-recording workflow.

1. **MT-003 — Show input level and warn when the microphone clips** — High value, medium effort and risk. Gives immediate confidence that the mic is receiving signal and helps avoid unusable takes; the meter must remain responsive without disrupting audio.
2. **MT-004 — Keep playback running through empty measures** — Medium value, low-medium effort and risk. Helps users continue recording or arranging after the last clip, but first decide how the eight-measure minimum is measured.
3. **MT-005 — Zoom the timeline with the mouse wheel** — Medium value, medium effort and risk. Could make navigation faster, but must not conflict with scrolling through tracks or the timeline.
4. **MT-006 — Move Add track to the Tracks heading** — Low value, low effort and risk. A small discoverability and layout improvement, best bundled only with related UI polish.

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

## Implemented features

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

- **Status:** Implemented 2026-11-15; build and all 35 automated tests pass, and the Settings window was smoke-checked in the running app. Hands-on checks with real microphones and headphones are still needed (see the spec's manual acceptance checks), especially that takes still line up with the backing track after playback moved to WASAPI. **Version:** Pre-versioning.
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

## Suggested starting point

No feature is currently selected as NEXT; MT-003 leads the provisional priorities. Outstanding hands-on checks for implemented features (MT-001, MT-002, MT-007, MT-008, MT-009) can be done separately; MT-002's hardware checks are the most important because playback now uses WASAPI. The WASAPI monitoring improvement (`2df738a`) is separate and should remain unchanged while working on backlog items.
