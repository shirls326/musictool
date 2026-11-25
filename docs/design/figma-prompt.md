# Figma AI prompts: Music Tool visual redesign

Written by the PM session on 2026-11-23 from the current UI (`MainWindow.xaml`, `SettingsWindow.xaml`, and the controls `MainWindow.xaml.cs` builds in code) and the owner's taste notes. The redesign is backlog idea **MT-017**. It gets built after MT-014, when the UI is split into smaller parts.

## The owner's taste (2026-11-23)

- **Likes:** Notion and Spotify (modern, sleek), GarageBand (simplicity), Discord (slick, colorful, unique). Open to surprises.
- **Feel:** bold, premium, slick.
- **Theme:** dark mode first, with **sunset colors** (orange, pink, purple, yellow, white) on an almost-black background. Maybe more themes later.
- **Type:** try a few fonts, including a neo-brutalist, "tech / IDE" feel (monospace accents).
- **Avoid:** very bright pastels, and clutter.

## How to use

Paste the prompts **one at a time, in order**, and review each result before the next. This follows the usual design process: **style cards first** (cheap to compare), then full screens in the style you choose, then a design system with reusable components. If Figma loses context in a later step, paste Prompt 1 again.

| Step | Prompt | You get | Your job afterwards |
| --- | --- | --- | --- |
| 1 | The brief | Shared understanding | Answer any questions it asks |
| 2 | Style cards | 4 style cards: palette, type, shapes, sample controls | Pick 1–2 favourites |
| 3 | Main window in your favourites | 1–2 full main-window mockups | Pick one direction |
| 4 | All screens | The complete screen set | Ask for changes until you like it |
| 5 | Design system | Variables, themes, reusable components | Go through the checklist at the end |

When step 5 is done, bring the variable names and the component list back to the PM session, which will turn them into the MT-017 spec.

---

## Prompt 1: The brief (paste first)

> I'm designing **Music Tool**, a Windows desktop app for recording vocals. A singer records takes over a backing track, arranges them on a timeline, and plays them back. It is deliberately simpler than a professional DAW: few controls, plain words, no plug-in racks. It's for one person singing at home.
>
> **I don't want you to recreate the current look.** Treat everything below as a list of what the app must *do*, not what it must look like.
>
> **My taste:**
> - I love how **Notion and Spotify** feel modern and sleek, **GarageBand's** simplicity, and **Discord's** slick, colorful, unique style.
> - I want it to feel **bold, premium, and slick**.
> - **Dark mode first**, with **sunset colors**: orange, pink, purple, yellow, and white on an almost-black background. A starting point you can change: near-black `#0B0A0F`, orange `#FF7A1A`, pink `#FF3D7F`, purple `#8B5CF6`, yellow `#FFC93C`, white `#FAF7F5`.
> - I'm curious about a **neo-brutalist, "tech / IDE" feel**: monospace type for numbers and labels, strong outlines, hard shadows.
> - I'd like **more color themes later**, so design with that in mind.
> - **Avoid** very bright pastels and clutter.
> - I'm open to ideas I haven't thought of.
>
> **Platform:** a Windows desktop window, 1280×780 by default, at least 960×620. It is built in WPF, so these are all fine: flat or gradient fills, thick or thin borders, rounded or sharp corners, hard offset shadows, soft shadows and glows, and icons. Custom fonts work too, if they're free or open license so they can be bundled with the app: for example Inter, Space Grotesk, Geist and Geist Mono, JetBrains Mono, IBM Plex Sans and Mono, Archivo. Avoid see-through "glass" blur of what's behind a panel, because WPF can't do it.
>
> **Everything the app has today, which must stay:**
> - **Project menu:** project name; New project, Open project…, Import audio…, Undo latest recording (Ctrl+Z), Audio settings…, Save, Save copy…
> - **Transport:** Play (becomes Pause while playing, Resume when paused), Stop, Record (shows Stop while recording), and a Monitor input on/off toggle (hear your mic in your headphones).
> - **Timing:** Tempo (BPM, number field), Time signature (3/4, 4/4, 5/4, 6/8, 7/8, 12/8), Zoom in/out with the current zoom level.
> - **Timeline:** a ruler with bar numbers; a playhead you click on the ruler to move; one lane per track; vertical scroll for many tracks; horizontal scroll along time.
> - **Track header, one per track:** an editable name (double-click or right-click → Rename track…), Remove track, Mute, and a Volume slider with a percentage. The selected track is highlighted; recording goes to the selected track.
> - **Clips:** blocks on a lane showing the track name and a small waveform, in the track's own color. You can select, drag to move, trim in/out, and delete (Backspace or right-click → Delete clip). While recording, a live clip grows in real time.
> - **Add track:** please make it a compact "+" beside a "Tracks" heading.
> - **Status bar:** one line of plain-language messages, for example "The saved microphone isn't connected; using the system default microphone."
> - **Audio settings dialog:** a Microphone dropdown and an Output (headphones) dropdown, each starting with "System default"; a small "in use" line under each; whether monitoring is on; OK and Cancel.
> - **Messages:** small dialogs for errors and questions, for example "Recover your unsaved session?"
>
> **Coming soon (leave room for these):**
> - An **input level meter** with a clipping warning, near Record/Monitor.
> - **Per-track vocal effects**: presets (small room reverb, hall reverb, an experimental reverb, warmth) with one "Amount" control, opened from each track header.
> - **Mouse-wheel zoom**, so the zoom buttons can be smaller.
> - **Color themes** the user can choose.
>
> **Principles that every style must keep:**
> - Recording state is impossible to miss.
> - Plain words, not audio jargon.
> - Big, easy targets for the transport controls.
> - Readable text contrast (WCAG AA).
> - Every icon-only button has a tooltip.
> - It works with the keyboard: Space plays or pauses, Ctrl+Z undoes the latest take, Backspace deletes the selected clip.
> - The design is original, not a copy of any existing app's look or branding.
>
> Don't design anything yet. Confirm you understand, and ask me any questions.

## Prompt 2: Style cards

> Make **4 style cards** (also called style tiles): one frame each, side by side. They show a visual direction without designing full screens yet. All are dark, and all use the sunset palette in their own way:
>
> 1. **Sunset Brutal / Tech:** neo-brutalism meets a code editor. Near-black base, thick outlines, hard offset shadows in sunset colors, monospace for numbers and labels, sharp or slightly rounded corners. Bold and confident.
> 2. **Sunset Premium:** Spotify/Notion-level sleek. Deep blacks, generous space, a sunset gradient used sparingly as the hero accent (for example on Record and the playhead), soft depth, a refined sans-serif.
> 3. **Sunset Vibrant:** Discord-like color and personality. Layered dark surfaces, a different sunset color per track, rounded friendly shapes, playful but never cluttered.
> 4. **Your wildcard:** a direction I haven't thought of that still fits "bold, premium, slick" and the sunset palette.
>
> **Each card contains:**
> - The name, 3 words for the feel, and who it would appeal to.
> - **Palette:** base background, surface, raised surface, border, primary text, muted text, accent, the "recording" color, success/warning, and 6 track colors. Show each with its hex code and its contrast ratio against the background.
> - **Typography:** a pairing of 2 fonts (the tech direction can use a mono font). Show a heading, body text, a small label, and numbers such as "128 BPM" and "00:42.3".
> - **Shape language:** corner radius, border width, shadow or glow style, and an icon style sample (Play, Stop, Record, headphones, plus).
> - **Sample controls in that style:** a Play button, the Record button idle and recording, a toggle, a slider, a dropdown, one track header, and two clips (one selected).
> - **One trade-off** for that direction.
>
> No full screens yet.

## Prompt 3: The main window in my favourites

Fill in your choice first, for example "Cards 1 and 2" or "Card 1, but with Card 2's fonts".

> I like **[your 1–2 choices, plus any mix]**. For each, design the **main window at 1280×780**: 3–4 tracks with several clips, one selected clip and one muted track, the playhead mid-song, the transport bar, the timing controls, the ruler, track headers, the "+ Tracks" heading, and the status bar. Next to it, show the **recording state**: the transport, a live growing clip, and monitoring on.
>
> Keep every function from the brief. If you remove or move something, tell me where it went. Keep it uncluttered.

## Prompt 4: Refine the chosen direction into every screen

> I've chosen **[direction, plus any changes]**. Design the complete set of screens, all at 1280×780 unless noted:
>
> 1. **New project:** one empty track, with a friendly empty-lane hint inviting the user to record or import.
> 2. **Arranging:** 4 tracks, one clip selected, one track muted.
> 3. **Playing:** Pause showing, the playhead moving.
> 4. **Recording:** unmistakable recording state, with a live growing clip, Record showing as Stop, and monitoring on.
> 5. **Renaming a track** inline.
> 6. **Project menu open.**
> 7. **Right-click menus** for a clip (Delete clip, Set trim in / out…) and a track (Rename track…).
> 8. **Audio settings dialog** (about 440 px wide).
> 9. **A message dialog** with OK (for example "Couldn't finish saving the project"), and one with Yes/No.
> 10. **Future features, as concept frames:** the input level meter (normal, loud, clipping); the effects panel opened from a track header (preset list plus one Amount slider); and a theme picker showing 3 themes.
> 11. **Minimum size:** the arranging screen at 960×620.

## Prompt 5: Design system and reusable components

> Turn the chosen design into a reusable **design system** on its own page called "Design system".
>
> **1. Variables (tokens).** Create Figma variables and use them everywhere. Don't hard-code colors or sizes.
> - **Colors, with semantic names:** `color/bg/base`, `color/bg/surface`, `color/bg/raised`, `color/border`, `color/text/primary`, `color/text/muted`, `color/accent`, `color/record`, `color/focus`, `color/success`, `color/warning`, `color/track/1`…`color/track/6`.
> - Put the colors in one **collection with a mode per theme**: **Sunset** (the default), plus 2 alternative themes in the same style, for example a cool "Midnight" (blues and teals) and a "Mono" (grayscale with one accent). Switching the mode must re-theme every screen.
> - **Spacing:** `space/1`…`space/8` on a 4 px grid. **Corner radius:** `radius/sm`, `radius/md`, `radius/lg`. **Border widths** and **shadow/glow** styles.
> - **Text styles:** heading, body, label, small, and numeric (tempo and time readouts).
>
> **2. Components.** Each uses auto layout, is built only from variables, and has variants or properties for its states. States: default, hover, pressed, focused (keyboard), disabled.
> - Button: primary, secondary, ghost, danger; with or without an icon.
> - Icon button (with a tooltip example).
> - Transport buttons: Play/Pause/Resume, Stop, Record/Stop-recording, and the Monitor toggle (on/off).
> - Text field (normal, focused, invalid), number field (Tempo), dropdown (closed/open), slider, toggle.
> - Menu and menu item, and the context menu.
> - Track header: name, rename-editing state, mute on/off, volume; selected and unselected; muted.
> - Clip: normal, selected, recording (live), very short (narrow); a track-color property.
> - The ruler with bar numbers, the playhead, and the empty-lane hint.
> - The "Tracks" heading with its "+" button.
> - Status bar (normal, warning).
> - A dialog shell (title, body, buttons), and the Audio settings dialog built from it.
> - Future: the input level meter (levels and clipping), an effect preset chip, and a theme swatch.
>
> **3. Rebuild every screen from these components** so they update when a component changes.
>
> **4. Add a documentation frame** listing every variable with its value in each theme, and every component with its variants. Name things clearly and consistently (`Button/Primary/Hover`): a developer will turn the variables into WPF resources and the components into WPF styles and controls, using the same names.

---

## Checklist before bringing it back

- [ ] Every function in the brief appears somewhere in the screens.
- [ ] Recording state is obvious at a glance.
- [ ] Text contrast passes WCAG AA in every theme (Figma has contrast-checker plugins).
- [ ] Colors and sizes come from variables, not hard-coded values, and switching themes re-themes everything.
- [ ] Every component has a focused (keyboard) state.
- [ ] The design works at 960×620 and isn't cluttered.
- [ ] No see-through blur; the fonts are free or open license.
