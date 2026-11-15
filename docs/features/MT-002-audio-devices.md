# MT-002: Configure audio devices in Settings

**Status:** Implemented 2026-11-15 (branch `feature/MT-002-audio-devices`, merged to `master`). Automated checks pass; the manual hardware checks below are still outstanding. **Version:** Pre-versioning.

## Purpose

Let the user choose which microphone Music Tool records from and which output (headphones) it plays and monitors through, without changing Windows sound settings. Today every audio path uses the Windows default device.

## Behavior before MT-002 (as of 2026-11-14)

| Audio path | Code | Device used |
| --- | --- | --- |
| Recording input | `AudioRecordingService.CreateInput()` — `WasapiCapture` | `GetDefaultAudioEndpoint(DataFlow.Capture, Role.Console)` |
| Input monitoring output | `AudioRecordingService.EnableMonitoring()` — `WasapiOut` | Windows default output (no device passed) |
| Project playback | `AudioPlaybackService.Play()` — `WaveOutEvent` | Legacy WinMM default device (`DeviceNumber = -1`) |

Playback and monitoring use two different Windows audio APIs. WinMM device numbers and WASAPI device IDs cannot be reliably matched, so selecting one output for both requires playback to move to WASAPI.

## User experience

- A **Audio settings...** entry in the top-left project menu opens a small modal Settings window.
- The window has two dropdowns, **Microphone** and **Output (headphones)**. Each lists active Windows devices by friendly name, with **System default** as the first option.
- The window shows the device currently in use for each, plus monitoring status ("Input monitoring: on" / "off").
- **OK** applies and saves; **Cancel** closes without changes.
- Choices are app-wide preferences, remembered across restarts. They are not saved in the `.musictool` project.

## Decisions

The owner decided every item on 2026-11-14, adopting the PM's recommendation in each case.

1. **Decided — Include a "System default" option?** Yes, and make it the initial choice. It keeps today's behavior for anyone who never opens Settings and follows Windows when the user changes the default.
2. **Decided — One output for both playback and monitoring?** Yes, a single output choice. Vocal recording expects backing track and mic monitoring in the same headphones; separate outputs add a confusing control and drift between two clocks.
3. **Decided — Saved device missing at launch** (for example, USB mic unplugged). Fall back to System default for that session, keep the saved preference (so plugging it back in restores it next launch), and show a short non-blocking notice: "Blue Snowball isn't connected; using the system default microphone."
4. **Decided — Device disconnected while in use.**
   - During playback: stop playback and show a message.
   - During monitoring: turn monitoring off (headphone icon returns to off) and show a message.
   - During recording, if the **microphone** disconnects: stop the take through the normal stop path so captured audio is kept as a regular take (undoable with Ctrl+Z), then show a message. The owner confirmed keeping the cut-short take.
   - During recording, if only the **output** disconnects: stop playback and/or turn monitoring off with a message, but keep recording. (Owner clarification during implementation, consistent with the rule that stopping playback never stops a take.)
5. **Decided — Where Settings lives.** The project menu entry above for v1. A gear button can come later with other UI polish (MT-006).
6. **Decided — Changing devices while busy.** The **Audio settings...** entry is disabled while recording. If playback is running when OK is pressed, stop playback and then apply. If monitoring is on, restart it on the new devices automatically.

## Persistence

- Store preferences in `%LOCALAPPDATA%\MusicTool\settings.json` — next to the existing `Recovery` folder.
- Save the WASAPI device **ID** (`MMDevice.ID`), not the friendly name, since names can repeat. Also save the friendly name for display and the missing-device message.
- `null` / missing means System default.
- A missing or corrupt settings file loads as all defaults and must not block startup.

## Expected code changes

Guidance for the dev session; it may adjust details but should explain any departures in its summary.

| File | Change |
| --- | --- |
| `MusicTool.App/Models/AppSettings.cs` *(new)* | Plain data class: `InputDeviceId`, `InputDeviceName`, `OutputDeviceId`, `OutputDeviceName` (all nullable). |
| `MusicTool.App/Services/AppSettingsStore.cs` *(new)* | Load/save JSON. Constructor takes an optional root folder (same pattern as `AutosaveRecoveryService(string, string? recoveryRoot)`) so tests never touch the real `%LOCALAPPDATA%`. |
| `MusicTool.App/Services/AudioDeviceCatalog.cs` *(new)* | Lists active capture/render devices with `MMDeviceEnumerator.EnumerateAudioEndPoints(..., DeviceState.Active)`, and resolves a saved ID to a device or to the default (reporting when it fell back). Keep the ID-resolution logic a pure function so it can be unit tested without hardware. |
| `MusicTool.App/Services/AudioRecordingService.cs` | `CreateInput()` uses the selected capture device; `EnableMonitoring()` passes the selected render device to `WasapiOut`. Add a method to set devices that throws `InvalidOperationException` while recording. |
| `MusicTool.App/Services/AudioPlaybackService.cs` | Replace `WaveOutEvent` with `WasapiOut` on the selected render device. Keep `Play`/`Pause`/`Resume`/`Stop`, `IsPlaying`/`IsPaused`, and `PlaybackCompleted` behavior unchanged. |
| `MusicTool.App/SettingsWindow.xaml` + `.xaml.cs` *(new)* | The modal Settings window, code-behind style like `MainWindow`. |
| `MusicTool.App/MainWindow.xaml` / `.xaml.cs` | Menu entry; load settings at startup and hand devices to both services; show fallback and disconnect messages; disable the entry while recording. |
| `MusicTool.App.Tests/AppSettingsStoreTests.cs` *(new)* | Round-trip save/load, missing file → defaults, corrupt file → defaults. |
| `MusicTool.App.Tests/AudioDeviceCatalogTests.cs` *(new)* | Resolution: saved ID present → used; saved ID absent → default plus fallback flag; null → default. |

**Main risk:** switching playback to WASAPI changes output buffering, which could shift how the backing track lines up with a recorded take. Do not change the monitoring buffer constants (see the WASAPI monitoring entry in the development log).

## Acceptance checks

Automated (dev session must run and report):
- [x] `dotnet build` and `dotnet test` pass, including the new tests (35 of 35 on 2026-11-15).
- [x] Settings round-trip, missing-file and corrupt-file tests pass.
- [x] Device resolution tests pass for present, missing, and default IDs.

Manual (owner, on real hardware):
- [ ] With no settings file, the app records/plays/monitors exactly as before.
- [ ] Select a non-default mic → a recorded take comes from that mic.
- [ ] Select a non-default output → playback **and** monitoring come out of it.
- [ ] Restart the app → choices are remembered.
- [ ] Unplug the selected mic, restart → fallback notice appears, recording uses the default.
- [ ] Unplug the output during playback, and during monitoring → behavior matches Decision 4.
- [ ] Unplug the output while recording → playback/monitoring stop with a message, and the take keeps recording.
- [ ] Unplug the mic mid-take → the captured take is kept and Ctrl+Z undoes it.
- [ ] Change devices while monitoring is on → monitoring restarts on the new devices.
- [ ] Record over a backing track after the playback change → the take still lines up by ear.
- [ ] Audio settings is disabled while recording.

## Implementation notes

- **Files:** `Models/AppSettings.cs`, `Services/AppSettingsStore.cs`, `Services/AudioDeviceCatalog.cs`, `Services/AudioDeviceWatcher.cs`, `Services/AudioDeviceFailedEventArgs.cs`, `SettingsWindow.xaml(.cs)`, plus changes to `AudioPlaybackService`, `AudioRecordingService`, and `MainWindow`. Tests: `AppSettingsStoreTests` and `AudioDeviceCatalogTests`.
- **Disconnect detection:** event-driven WASAPI streams can stall silently when a USB device is unplugged instead of reporting an error, so `AudioDeviceWatcher` listens for Windows endpoint notifications (`IMMNotificationClient`). A stream that stops with an error is also treated as a lost device. The services cache the IDs of the devices they opened, and `MainWindow` compares a lost device against them to apply Decision 4.
- **Session fallback:** a saved device that is missing at launch is replaced by System default for that session only; `settings.json` keeps the saved choice. In the Settings window that device appears as "*Name* (not connected)" and stays selected, so pressing OK without changes does not overwrite the saved preference.
- **Startup notice:** the "isn't connected" notice uses the status bar so it does not block startup. Disconnects during use show a warning dialog.
- **Playback output:** `AudioPlaybackService` now uses shared-mode, event-driven `WasapiOut` with a 200 ms playback-only buffer (`PlaybackLatencyMilliseconds`, NAudio's default; the previous WinMM `WaveOutEvent` buffered about 300 ms). The monitoring and capture buffer constants are unchanged.
- **Smoke check (2026-11-15):** the app launched; **Audio settings...** opened the modal window; both dropdowns listed the connected devices with System default selected; Cancel created no settings file; the app closed normally.

## Out of scope

Input level meter (MT-003), latency measurement (MT-010), exclusive-mode or ASIO devices, sample-rate/buffer settings, and per-project device choices.

## Known limitations

- **System default mid-session:** if the Windows default device changes while Music Tool is open and System default is selected, playback uses the new default on the next Play, but the microphone and input monitoring stay on the previous device until capture or monitoring restarts. (Noted in PM review, 2026-11-15.)

## Review and acceptance (2026-11-15)

- PM review against this spec found no blocking issues; build and 35/35 tests pass.
- The owner accepted MT-002 with the manual checks above deferred. They are tracked in the backlog's **Pending verification** table, and most can be automated by MT-012.
- Watch during the unplug checks: `AudioDeviceCatalog.OpenDevice` releases its `MMDeviceEnumerator` right after opening a device, whereas the previous code kept the enumerator alive alongside the open device. This is expected to be safe; investigate it first if disconnect handling misbehaves.
