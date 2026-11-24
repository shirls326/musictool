# Figma AI prompt: Music Tool visual design

Written by the PM session on 2026-11-23 from the current UI (`MainWindow.xaml`, `SettingsWindow.xaml`, and the controls `MainWindow.xaml.cs` builds in code). Paste the prompt below into Figma's AI, then refine it there. If the UI changes before you use it, ask the PM session to refresh it.

The design is exploration only. Building it is a separate future feature, after MT-014, because the refactor makes UI changes much easier.

---

## Prompt

Design a desktop app called **Music Tool**: a simple, friendly vocal-recording app for Windows. A singer records vocal takes over a backing track, arranges them on a timeline, and plays them back. It is deliberately simpler than a professional DAW (digital audio workstation): few controls, plain words, no plug-in racks. Make it feel modern, calm, and focused, like a creative tool for one person singing at home, not a studio console.

**Platform and size:** a Windows desktop app, 1280×780 by default, at least 960×620. Use a dark theme. Today's palette works and can be refined: background `#14151A`, panels `#1C1D24`, raised surfaces `#252630`, main text `#F6F4FA`, muted text `#9A9AA8`, accent purple `#B69CFF`, record red `#FF5964`. Keep text contrast at WCAG AA or better.

**Main window, top to bottom:**

1. **Top bar**
   - Left: a project menu button ("More project actions") next to the project name, for example "My First Project".
   - Center: transport buttons: Play (switches to Pause while playing), Stop, Record (a red dot that becomes a square while recording), and a Monitor input toggle with a headphones icon.
   - Right: a Tempo field (BPM, for example 128), a Time signature dropdown (3/4, 4/4, 5/4, 6/8, 7/8, 12/8), and Zoom out/in buttons with the current zoom level.
2. **Project menu** (a dropdown from the menu button): New project, Open project…, Import audio…, Undo latest recording (Ctrl+Z), Audio settings…, Save, Save copy…
3. **Timeline panel** (a rounded panel taking most of the window)
   - **Ruler** across the top showing bar numbers, with a playhead the user can click to move.
   - **Track headers** on the left, about 220 px wide, one per track: an editable track name (double-click to rename), a remove (×) button, a Mute (M) button, a volume percentage, and a volume slider. The selected track is highlighted.
   - **Track lanes** on the right, one per track: audio clips drawn as rounded colored blocks with the track name and a small waveform. Each track has its own color. Clips can be selected, dragged, trimmed, and deleted. While recording, a live clip grows in real time.
   - Vertical scrolling for many tracks, and horizontal scrolling along the timeline.
4. **Add track:** today this is a "+ Add track" button below the timeline. Please redesign it as a compact "+" button beside a "Tracks" heading above the track headers, with a tooltip.
5. **Status bar** at the bottom: one line of plain-language status, for example "Recording on Audio 2…" or "The saved microphone isn't connected; using the system default microphone."

**Audio settings dialog** (modal, about 440 px wide): a Microphone dropdown and an Output (headphones) dropdown, each starting with "System default". Under each dropdown, a small line shows the device in use. Add a line saying whether monitoring is on, and OK and Cancel buttons.

**Please design these screens and states:**
1. A new project with one empty track. Show a helpful empty-lane hint inviting the user to record or import.
2. A project with 3–4 tracks and several clips, while playing (Pause shown, playhead moving).
3. While recording: a clear recording state (red accent, a live growing clip, Record shown as Stop) that is impossible to miss.
4. A track name being renamed inline.
5. The project menu open.
6. The Audio settings dialog.
7. A small component sheet: buttons (default, hover, pressed, disabled), the transport buttons in each state, the toggle, the slider, text field, dropdown, track header, and clip (normal and selected).

**Leave room for upcoming features** (show them lightly or as notes):
- An **input level meter** with a clipping warning, near the Record/Monitor controls.
- **Per-track vocal effects** with simple presets (small room reverb, hall reverb, an experimental reverb, warmth) and one "Amount" control. A small effects button or chip on each track header would open them.
- **Zoom with the mouse wheel** (Ctrl + wheel), so the zoom buttons could become smaller.

**Design principles:**
- Recording state must always be obvious.
- Use plain words, not audio-engineering jargon.
- Big, easy click targets for the transport controls.
- Keyboard friendly: Space plays or pauses, Ctrl+Z undoes the latest take, Backspace deletes the selected clip.
- Every icon-only button needs a tooltip and an accessible name.

**Constraints:** the design will be built in WPF (Windows Presentation Foundation), so prefer flat shapes, rounded rectangles, simple gradients, and standard fonts (Segoe UI or Segoe UI Variable). Avoid effects that are hard to build, like heavy blur or glass. The design must be original, not a copy of any existing music app's look or branding.
