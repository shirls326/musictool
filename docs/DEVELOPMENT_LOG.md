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

## Verification so far

- The WPF solution builds successfully with the .NET 8 SDK.
- Eight automated tests cover project persistence, copied media, empty tracks/recording-track persistence, unsupported import types, tempo and meter persistence, backward-compatible project defaults, and musical time conversion.
- The app has been launched successfully on the development machine.
- Automated checks do not substitute for testing audio with the actual microphone, output device, and headphones.

## Current gaps and possible next work

- Test live input monitoring and recording with the intended audio hardware; adjust the capture/output approach if latency or device routing is unsuitable.
- Test playhead seeking during actual backing-track playback and improve clip editing based on actual use.
- Consider audio-device selection, additional file formats, or richer mixing controls only when the core workflow calls for them.

## How to maintain this log

For each meaningful change, add a dated or versioned milestone near the top of **Milestones**. State what changed, why it was chosen, and whether it is implemented or still needs verification. Keep this log focused on product and feature history; use the README for current run instructions and current usage guidance.
