# MT-001: Autosave and crash recovery

## Purpose

Protect recent project edits if Music Tool closes unexpectedly, without replacing the last valid project file or silently adding an unfinished take.

## User experience

- Music Tool creates a recovery checkpoint every 15 seconds while the app is open.
- On the next launch, each available checkpoint is offered explicitly. Choosing **Yes** opens it as a new project under `Documents\Music Tool Projects\Recovered`; choosing **No** discards that checkpoint.
- The temporary checkpoint files are stored under `%LOCALAPPDATA%\MusicTool\Recovery`. This is separate from the `Recovered` project folder, which is created only after recovery is accepted.
- The recovered copy includes completed project edits and copied media. The saved project that was open before the interruption is not overwritten.
- If the checkpoint includes captured audio from an active recording, the user is separately asked whether to include it. If accepted, the audio is added to a new `INCOMPLETE - <track name>` track at the original take start. It never replaces existing clips, including when the interrupted take was a punch-in.
- If an incomplete take is missing or invalid, Music Tool explains that the take could not be recovered; completed project edits can still be recovered.
- A normal close saves the project first and then removes the current recovery checkpoint.

## Implementation

- `AutosaveRecoveryService` stores recovery data in a per-project directory under `%LOCALAPPDATA%\MusicTool\Recovery`, keyed by the normalized full project path. Paste `%LOCALAPPDATA%\MusicTool\Recovery` into File Explorer to inspect the temporary checkpoints.
- Each checkpoint contains a serialized project snapshot, copies of referenced media, and a manifest. Temporary files are renamed into place before the manifest is updated so incomplete writes are not presented as complete checkpoints.
- While a recording is active and has captured audio, the service receives the track name, track index, start position, and a callback that writes a recoverable WAV snapshot. The active recording file is not modified.
- `AudioRecordingService.WriteRecoverySnapshot` flushes capture under the capture lock, copies the active WAV through a read/write-shared stream, locates its RIFF data chunk, and patches the copy's RIFF/data lengths. It relies on the current 44.1 kHz, 16-bit, mono PCM recording format.
- Recovery first opens the snapshot, then saves it as an independent project copy. `ProjectStore.AddRecoveredIncompleteTake` validates and copies any accepted partial take to the recovered project's media folder and adds it as a separate track.
- Starting a new or opened project resets the recovery context. Save As follows the new project path.

## Safety and failure handling

- Recovery data is never applied silently. An explicit user choice is required to recover or discard it.
- The original project file and its clips remain unchanged during recovery. Punch-in replacement is performed only during a normal recording completion, never as part of recovery.
- Missing/corrupt snapshot or media errors are surfaced. A failed recovery does not trigger automatic deletion of the checkpoint.
- A partial take is best-effort: a crash before any samples are captured leaves no recoverable take; invalid audio may be unavailable even if the completed project snapshot is usable.
- The recovery prompt presents each available checkpoint at startup. Declining a checkpoint discards only that checkpoint.

## Validation

- Automated tests cover checkpoint media copying, manifest discovery, explicit checkpoint discard, incomplete-take metadata, and adding a valid recovered take as a distinct track while preserving the original track's clips and settings.
- A focused test keeps the WAV writer active while copying its partial output and verifies the patched file is readable.
- **Verified manually:** copied the existing starter project and its four audio clips into a throwaway test project, created a checkpoint, launched Music Tool, accepted the actual startup recovery prompt, and confirmed the recovered project opened with three tracks and four clips. SHA-256 hashes confirmed the test copy's four audio files matched the originals, and the original project remained unchanged. This also confirmed that `%LOCALAPPDATA%\MusicTool\Recovery` contains temporary checkpoints, while `Documents\Music Tool Projects\Recovered` is created only after recovery is accepted.
- **Still requires hands-on validation:** recover/discard an interrupted new recording and an interrupted punch-in; verify missing/corrupt partial-take messaging and that accepted interrupted audio appears as a separate incomplete track. Automated tests cover the separate-track project-store behavior and partial WAV readability, but not a live microphone interruption.

## Limitations

- Autosaves are periodic, so edits made since the last completed checkpoint can be lost.
- Recovery snapshots copy project media and may briefly use additional disk space.
- An interrupted take is only recoverable if its WAV file can be flushed and its captured data is readable.
- This feature does not add project version history or general undo/redo.
