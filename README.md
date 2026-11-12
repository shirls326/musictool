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

- Use the top-left project menu's **Import audio...** command or drag WAV/MP3 files onto the timeline. Each imported file gets its own colored track with a waveform preview.
- Click **+ Add track** to create an empty track, or select an existing track to record onto it. Place the playhead where the take should begin, then use the red-dot button; the playhead and live waveform move as you record. Each take remains a separate clip, so gaps stay visible. A punch-in replaces only the overlapping section and preserves audio before and after it.
- The transport uses icon buttons: triangle for Play, square for Stop, red dot for Record, and headphones for Monitor input. Click the Play button while audio is playing to pause; click it again to resume from that point. Press **Space** during playback to stop. Hover for tooltips; the headphone icon turns yellow when monitoring is on.
- Drag clips left or right to line them up. Use **- / +** to zoom the timeline.
- Set the project **Tempo** (20-300 BPM) and **Time signature** in the transport bar. Press Enter or leave the tempo field to apply it; meter changes apply immediately. New projects start at 128 BPM and 4/4; tempo is counted in quarter notes per minute.
- The timeline ruler and track lanes are measured in bars, with beat divisions based on the selected time signature. Click the ruler or a track lane to place the red playhead; press **Play** to start from that point. Click a clip to select a playback position within it. Clicking during playback stops and repositions the playhead.
- Tempo and meter changes update the bar ruler, but leave clips at their existing real-time positions and do not change audio playback speed. Clip trim-in/out values remain in seconds because they refer to positions inside the source audio file.
- Use each track's **M** button and volume slider, then press **Play** to mix the tracks together.
- Right-click a clip to set its trim-in and trim-out. Monitor input works while idle or recording; turn it off when finished and use headphones to prevent speaker feedback. App monitoring uses Windows WASAPI event-driven audio with a short, bounded buffer to reduce delay. The actual result depends on the audio devices and drivers; an audio interface's direct-monitor feature can still be faster.
- Use the top-left project menu to create or open a project, import audio, save it, or save a copy.

## Documentation

The [development log](docs/DEVELOPMENT_LOG.md) tracks feature changes, their motivation, and what still needs validation.

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

This is an early prototype with a simple multitrack timeline inspired by familiar recording apps. It supports WAV/MP3 imports, waveform previews, bar-based clip positioning, tempo and time-signature settings, trimming, volume/mute, vocal recording, and local project files. It does not yet support MIDI, plug-ins, effects, or tempo-based time-stretching. Input-monitoring latency and device compatibility need to be tested on the Windows computer and audio hardware that will be used.
