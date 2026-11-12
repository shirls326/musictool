# Feature Backlog

This is a list of possible improvements, not a combined implementation task. Work on **one feature at a time**, review the changes, and validate it before starting another.

## Priority and status key

- **NEXT** — the one feature selected to work on next; do not start other backlog items in the same task.
- **Proposed** — a candidate that has not been selected.
- **Implemented** — code is in place; any remaining hands-on verification is stated explicitly.
- When a feature is implemented, move its full entry here rather than deleting it, and add a dated or versioned milestone to `DEVELOPMENT_LOG.md`. Keep both records; the log captures the change and rationale, while this section preserves backlog history.

## Next priority: Undo the latest recording

Keep undo scoped to recording takes, including restoring any audio replaced by a punch-in. Do not add general edit history or redo in this first version.

## Proposed features

1. **Undo the latest recording**
   - Offer Ctrl+Z and an Undo command in the project menu.
   - Undo recording takes only, not other edits; no redo in this first version.
   - Keep undo available for the latest take until another take is recorded or the app closes.
   - Restore the previous clips when undoing a punch-in, including audio replaced by the take.

2. **Choose audio devices and show their status**
   - Let the user select the WASAPI microphone input and headphone/output device in Music Tool.
   - Show the selected devices and whether input monitoring is active.
   - Do not switch devices during recording; apply changes while idle.
   - Keep device choices for the current app session rather than storing them in each project.

3. **Keep playback running through empty measures**
   - Let playback continue through sections where no audio clips are active, instead of stopping as soon as the last clip ends.
   - Support at least eight measures of empty timeline during playback.
   - Decide whether the eight-measure minimum applies after the last clip, after the playhead, or to the project timeline as a whole; confirm the expected behavior before implementation.

4. **Move Add track to the Tracks heading**
   - Replace the current Add track button with a compact plus-sign button beside the Tracks label.
   - Keep the action easy to find and make its purpose clear with a tooltip/accessibility name.

5. **Zoom the timeline with the mouse wheel**
   - Use scrolling over the timeline to zoom in and out, replacing the current zoom buttons.
   - Choose a gesture that does not interfere with scrolling through tracks or navigating the timeline, and keep the zoom centered around a useful position such as the pointer or playhead.

## Implemented features

### Rename tracks

- **Status:** Implemented; verify double-click, context-menu rename, Enter/Escape, click-away save, and track selection in the running app.
- Double-click the track name to edit inline, or choose **Rename track...** from the track header's right-click menu. Enter or clicking away saves; Escape cancels. Leading/trailing whitespace is trimmed and blank names are rejected. Duplicate names are allowed.
- Track names are persisted in the project; renaming does not alter clips or track settings.
- **History:** See [Track renaming](DEVELOPMENT_LOG.md#track-renaming) in the development log.

### Make Space safe for playback

- **Status:** Implemented; hands-on verification in the running app is still needed.
- Space toggles playback only: start, pause, or resume. It must not activate the focused Record button or start/stop recording, and is ignored while editing text or using a combo box.
- **History:** See [Space playback shortcut safety](DEVELOPMENT_LOG.md#space-playback-shortcut-safety) in the development log.

## Later ideas

- **Recording safety:** autosave/recovery after a crash.
- **Input feedback:** show input level and warn when the microphone clips.
- **Latency confidence:** measure round-trip latency and explain app monitoring versus direct monitoring.
- **Editing:** expand undo/redo to clip moves, trims, and track operations after recording undo is reliable.

## Suggested starting point

Undoing the latest recording is the selected next feature. The Space shortcut can be hands-on checked separately. The WASAPI monitoring improvement (`2df738a`) is separate and should remain unchanged while working on backlog items.
