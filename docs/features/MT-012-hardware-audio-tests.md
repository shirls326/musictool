# MT-012: Automated hardware audio tests

**Status:** NEXT — spec approved 2026-11-15. Test-only work, using the lighter process in `docs/WORKFLOW.md`.

## Purpose

Replace most of MT-002's deferred manual checks ("did audio go to the right device?") with repeatable tests that use this PC's real audio devices, and measure playback latency, the main risk left by MT-002 (playback moved from WinMM `WaveOutEvent` to shared-mode WASAPI with a 200 ms buffer). The same harness will be reused to check monitoring delay when vocal effects (MT-013) are added.

## Decisions (owner, 2026-11-15)

1. **Process:** lighter path — this spec, a feature branch, and PM review; no separate decision round.
2. **Scope:** test code only. **No changes to `MusicTool.App`.** Moving MT-002's disconnect and settings-apply rules out of `MainWindow` into testable code is a separate follow-up (MT-014). If a test seems to require an app change, stop and ask.
3. **Latency comparison:** measure both the current WASAPI playback path and the previous `WaveOutEvent` path once, and report both. The `WaveOutEvent` path exists only inside the test code.
4. **Test devices:** microphone *Blue Snowball*, main output *HyperX Cloud III*, and an NVIDIA monitor output as the "other" output that must **not** receive the test tone.

## How the tests run

- A normal `dotnet test` must still pass on any machine: hardware tests show as **skipped** unless `MUSICTOOL_HARDWARE_TESTS=1` is set.
- To run only the hardware tests:

  ```powershell
  $env:MUSICTOOL_HARDWARE_TESTS = "1"
  & "$env:USERPROFILE\.dotnet\dotnet.exe" test .\MusicTool.sln --filter "Category=Hardware" --logger "console;verbosity=detailed"
  ```

- Devices are chosen by a case-insensitive part of their Windows name. Defaults match the owner's devices and can be overridden:

  | Variable | Default | Used for |
  | --- | --- | --- |
  | `MUSICTOOL_TEST_MIC` | `Snowball` | Recording and monitoring input |
  | `MUSICTOOL_TEST_OUTPUT` | `HyperX` | Playback and monitoring output |
  | `MUSICTOOL_TEST_OTHER_OUTPUT` | `NVIDIA` | Output that must stay silent; optional |

- **Missing devices:** once hardware tests are enabled, a missing mic or main output **fails** the test with a clear message naming the device and listing the connected ones. Opting in means the devices are expected, and a silent skip would hide problems. (This is a deliberate change from the proposal; it also avoids adding a runtime-skip package to xUnit 2.) If the "other" output is missing, only the "not on the other output" assertion is omitted, and the test output says so.
- Tests use temp folders only and never touch `settings.json`, projects, or Windows default-device settings. Tones are short (≤ 1 s) and quiet (about −20 dBFS). The full hardware run should take under about 30 s.
- **Before running:** take headphones off or turn them down. Close other apps that might play sound.

## Technique

- **Loopback capture:** `WasapiLoopbackCapture` on a specific output device records what that device is playing.
- **Known tone:** tests play a generated 1 kHz sine and detect that frequency with the Goertzel algorithm (a cheap single-frequency check), not "any sound." Other system sounds therefore cannot cause a false pass.
- **Timing:** latency = time from calling `Play()` to the first loopback buffer in which the tone is detected. Repeat 5 times and report the median and range. Resolution is roughly one loopback buffer (about 10 ms), which is fine for this purpose.

## Tests

| # | Test | Pass condition | Replaces MT-002 check |
| --- | --- | --- | --- |
| 1 | Playback uses the chosen output | Play a tone WAV through `AudioPlaybackService` after `SetOutputDevice(testOutput)`. `ActiveOutputDeviceId` equals the test output's ID; the tone is detected on its loopback; the tone is **not** detected on the other output. | Non-default output (playback) |
| 2 | System default follows Windows | With `SetOutputDevice(null)`, `ActiveOutputDeviceId` equals `AudioDeviceCatalog.GetDefault(Render)` and the tone is detected there. | No settings file behaves as before (partly) |
| 3 | Recording uses the chosen mic | `SetDevices(testMic, testOutput)`, record about 1 s via `AudioRecordingService`. `ActiveInputDeviceId` equals the mic's ID during recording; the WAV is 44.1 kHz, 16-bit, mono, about 1 s long, and not all zero samples. | Non-default microphone |
| 4 | Monitoring uses the chosen output | Turn monitoring on. `ActiveMonitorOutputDeviceId` equals the test output's ID, and loopback on that output receives audio data. Weaker than test 1 because there is no known signal to detect. | Non-default output (monitoring) |
| 5 | Device change while monitoring | With monitoring on, call `SetDevices` with a different output (the other output, if present). Monitoring is still on and `ActiveMonitorOutputDeviceId` is the new output. If no other output exists, the test fails with a message saying it needs one. | Changing devices while monitoring |
| 6 | Playback latency report | Run the timing above for (a) `AudioPlaybackService` on WASAPI and (b) a test-only `WaveOutEvent` playing the same WAV. Write both results to the test output. Assert only a loose sanity bound (each median under 1 s) so the test is not flaky. `WaveOutEvent` can only use the Windows default output, so if the test output is not the Windows default, report (b) as "not measured" and say why. | Takes still line up (partly) |

Also add **non-hardware unit tests** for the tone tools that run in every normal `dotnet test`: the detector finds 1 kHz in a generated 1 kHz buffer and does not find it in silence or a 440 Hz buffer.

## Expected code changes

All in `MusicTool.App.Tests`; no app code changes.

| File | Change |
| --- | --- |
| `Hardware/HardwareFactAttribute.cs` *(new)* | `[HardwareFact]`: a `FactAttribute` whose `Skip` is set when `MUSICTOOL_HARDWARE_TESTS` isn't `1`. Tests also carry `[Trait("Category", "Hardware")]`. |
| `Hardware/TestDevices.cs` *(new)* | Reads the environment variables, matches names via `AudioDeviceCatalog.ListActive`, and produces the "missing device" message listing what is connected. |
| `Hardware/ToneSignal.cs` *(new)* | Writes a tone WAV to a temp folder and implements Goertzel detection on float or 16-bit buffers. |
| `Hardware/LoopbackProbe.cs` *(new)* | Wraps `WasapiLoopbackCapture` for one device: start, wait until the tone is detected or a timeout expires, and report when it was first detected. Always disposes the capture. |
| `Hardware/HardwareAudioTests.cs` *(new)* | Tests 1–6. Uses `ITestOutputHelper` to print the devices used and the latency numbers. |
| `ToneSignalTests.cs` *(new)* | Non-hardware detector tests. |

## Acceptance checks

Dev session:
- [ ] Normal `dotnet test` passes, with the hardware tests shown as skipped and the new tone tests passing.
- [ ] With `MUSICTOOL_HARDWARE_TESTS=1` on this PC, tests 1–6 pass, or each failure is explained.
- [ ] `git diff` shows no changes under `MusicTool.App/`.
- [ ] Summary includes the latency numbers from test 6 and whether Windows output volume or mute affected loopback detection.

Owner:
- [ ] Run the hardware command above once and confirm it passes on this PC.

PM close-out:
- [ ] Update the backlog's **Pending verification** table: mark rows covered by tests 1–5 as verified by automation (with the date), and record test 6's latency numbers in the development log.

## Out of scope

Physical unplug tests, judging sound quality (crackle or stutter), full round-trip latency through the air into the mic (MT-010), UI automation, and running the hardware tests in CI.
