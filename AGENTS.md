# Copilot Instructions

## Build and test

Run these PowerShell commands from the repository root. The README uses the .NET 8 SDK installed under `%USERPROFILE%\.dotnet`; replace the executable with `dotnet` if the SDK is on `PATH`.

```powershell
& "$env:USERPROFILE\.dotnet\dotnet.exe" build .\MusicTool.sln
& "$env:USERPROFILE\.dotnet\dotnet.exe" test .\MusicTool.sln
& "$env:USERPROFILE\.dotnet\dotnet.exe" test .\MusicTool.sln --filter "FullyQualifiedName~AutosaveRecoveryTests.CheckpointIncludesIncompleteTakeMetadataAndSnapshot"
& "$env:USERPROFILE\.dotnet\dotnet.exe" run --project .\MusicTool.App\MusicTool.App.csproj
```

The test project uses xUnit. Change the filter to the test class or method to run another focused test. There is no repository-configured lint command.

## Architecture

- `MusicTool.App` is a .NET 8 Windows WPF application. `App.xaml` launches `MainWindow`; the window's XAML defines the UI and `MainWindow.xaml.cs` handles input, builds the timeline visuals, and coordinates project and audio services. The app uses code-behind rather than an MVVM layer.
- `Models` defines `MusicProject`, `AudioTrack`, and `AudioClip`. `ProjectStore` serializes project data as JSON in `.musictool` files and manages each project's `Media` directory. Imported audio is copied into that directory and clips store project-relative paths. Project opening migrates legacy single-audio track fields into clips.
- `AudioPlaybackService` mixes project clips with NAudio and plays them through shared-mode WASAPI (`WasapiOut`). `AudioRecordingService` captures WAV audio with WASAPI and optionally monitors input. `MainWindow` coordinates playback, recording, and project edits; recording is committed to the project when the take ends.
- Audio devices (MT-002): `AppSettingsStore` keeps app-wide preferences in `%LOCALAPPDATA%\MusicTool\settings.json` (missing/corrupt loads defaults). `AudioDeviceCatalog` lists and opens WASAPI endpoints and resolves a saved device ID to itself or the system default (`null` ID = System default). Both audio services take device IDs and refuse device changes while recording/playing. `AudioDeviceWatcher` reports unplugged devices via Windows endpoint notifications; `MainWindow` applies the disconnect rules in `docs/features/MT-002-audio-devices.md`. `SettingsWindow` is the modal Audio settings dialog. Do not change the monitoring/capture buffer constants in `AudioRecordingService` without a hardware test.
- `MusicalTimeline` converts between elapsed seconds and bar/beat display using the project's tempo and time signature. Clip positions and trims remain in seconds; changing tempo or meter does not time-stretch audio.
- `AutosaveRecoveryService` writes periodic checkpoints and copied media under `%LOCALAPPDATA%\MusicTool\Recovery`. Recovery opens as a separate project copy; see `docs/features/MT-001-autosave-recovery.md` for recovery behavior and safety details.
- `MusicTool.App.Tests` references the app project and covers project persistence/migration, clip and punch-in behavior, recovery, playback eligibility, musical-time conversion, app settings persistence, and audio-device resolution. Audio-device and live-recording behavior may still need verification on real Windows audio hardware.

## Workflow

Features are built one at a time using a product-manager session and a separate implementation session; see `docs/WORKFLOW.md`. An implementation session builds only the feature marked **NEXT** in `docs/FEATURE_BACKLOG.md`, follows its spec in `docs/features/`, works on a `feature/MT-XXX-short-name` branch, does not resolve **OPEN** spec items itself, and ends with a summary of files changed, what each change does, test results, and what still needs hands-on verification. The owner is learning C# through this project: follow the **Learning mode** rules in `docs/WORKFLOW.md` (plan first in plain language, define new terms, small explained commits, a code tour at the end, and a few check-your-understanding questions).

## Repository-specific behavior

- Keep UI event wiring in `MainWindow.xaml` and its code-behind consistent. User-facing failures are surfaced by the window; services report operation failures with exceptions.
- Project mutations are persisted through `ProjectStore`. Punch-in replaces only the overlapped part of a clip, preserving audio before and after it; undo snapshots the prior clip arrangement for the latest take.
- Removing a clip must not delete its media file: another clip may share that source. Track and clip paths are resolved relative to the project directory.
- Recovery is non-destructive: never overwrite the original project during restore. An accepted interrupted recording is added as a distinct `INCOMPLETE` track, not as an automatic punch-in replacement.
