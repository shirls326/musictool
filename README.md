# Music Tool

A simple Windows 11 desktop app for recording vocals over backing tracks, without the complexity of a full digital audio workstation.

## Run it

The .NET 8 SDK used to build this prototype is installed for this Windows user at `%USERPROFILE%\.dotnet`. From the repository folder, run:

```powershell
& "$env:USERPROFILE\.dotnet\dotnet.exe" run --project .\MusicTool.App\MusicTool.App.csproj
```

If you install the .NET 8 SDK in a standard location that's on `PATH`, you can replace the full executable path with `dotnet`.

The app creates a starter project in `Documents\Music Tool Projects\My First Project.musictool`.

## Try the current prototype

- Use **+ Import audio** or drag WAV/MP3 files onto the timeline. Each imported file gets its own colored track with a waveform preview.
- Click **+ Add track** to create an empty track.  Select it, then record a vocal take; recording creates a new track instead of replacing an imported backing track.
- Drag clips left or right to line them up. Use **- / +** to zoom the timeline.
- Use each track's **M** button and volume slider, then press **Play** to mix the tracks together.
- Right-click a clip to set its trim-in and trim-out. **Monitor input** works while idle or recording; turn it off when finished and use headphones to prevent speaker feedback.
- Use **New project**, **Open project**, **Save**, and **Save as...** to manage local projects.

## How the code is organized

- `MusicTool.App\MainWindow.xaml` describes the screen layout.
- `MusicTool.App\MainWindow.xaml.cs` connects buttons and drag-and-drop to app behavior.
- `MusicTool.App\Models` contains the data stored for tracks and projects.
- `MusicTool.App\Services` handles audio playback/recording and project files.

This keeps the first version easy to follow: the window handles user actions, while services do the audio and file work.

To run the project-file tests:

```powershell
& "$env:USERPROFILE\.dotnet\dotnet.exe" test .\MusicTool.sln
```

## Current limits

This is an early prototype with a simple multitrack timeline inspired by familiar recording apps. It supports WAV/MP3 imports, waveform previews, clip positioning and trimming, volume/mute, vocal recording, and local project files. It does not yet support scrubbing, MIDI, plug-ins, or effects. Input-monitoring latency and device compatibility need to be tested on the Windows computer and audio hardware that will be used.
