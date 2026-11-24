# MT-015: UI smoke tests

**Status:** NEXT. Ready for the implementation session: all decisions made by the owner (2026-11-15 and 2026-11-23). Runs alone, after MT-016 and before MT-014 (MainWindow refactor).

## Purpose

MT-014 will move about 2,000 lines out of `MainWindow.xaml.cs`. Unit tests cover the services, but nothing automated checks the window itself: buttons, menus, keyboard shortcuts, and track rename. MT-015 adds a small set of **UI smoke tests** that start the real app, click and type like a user, and check the results. Running them before and after the refactor shows that nothing a user can see broke.

A *smoke test* is a quick, shallow check that the main paths work. It is not a full test of every detail. The name comes from switching on new hardware and checking that no smoke comes out.

## How it works (background for the owner)

- **UI Automation (UIA)** is a Windows feature that lets programs find and operate other programs' controls. Screen readers use it. Each control a user can see is exposed as an *element* with a type (Button, Text, Edit, Slider), a **Name** (the label a screen reader reads, for example `AutomationProperties.Name="Play"`), and an **AutomationId** (a stable ID meant for tests).
- **FlaUI** is a .NET library on top of UIA. A test starts `MusicTool.App.exe`, finds elements by AutomationId or Name, and clicks, types, or reads them.
- For named WPF controls, the `x:Name` (for example `PlayButton`) automatically becomes the AutomationId, so most XAML controls already work. Controls that `MainWindow.xaml.cs` builds in code (track headers, clips) have no name, so they need one added.
- **Layout containers are invisible to UIA.** A `Border`, `Grid`, or `StackPanel` has no UIA element, so setting an AutomationId on the track-header `Border` or the clip `Border` does nothing. IDs go on the visible controls inside them: the name `TextBlock`, the buttons, and the slider. To click a clip, a test clicks its name text, and the click reaches the clip underneath.

## Decisions

### Decided (owner, 2026-11-15)
1. Use FlaUI (`FlaUI.UIA3`).
2. Tests are opt-in with `MUSICTOOL_UI_TESTS=1`, because they take over the mouse and keyboard. A normal `dotnet test` and the GitHub Actions run skip them.
3. They live in a separate test project, `MusicTool.App.UITests`.
4. The set stays small, about 8–10 tests. Anything that needs a microphone stays with the hardware tests or the manual list.

### Decided (owner, 2026-11-23)
5. **Temp data folder:** an environment variable, `MUSICTOOL_DATA_ROOT`, set only for the app process a test launches. This is the same pattern as `MUSICTOOL_HARDWARE_TESTS`. There is no command-line argument.
6. **Test list:** the 10 tests below are approved (tests 1 and 10 adjusted under decision 8).
7. **Light app changes are allowed:** the data-folder override and the AutomationIds, with no visible change. Because app code changes, MT-015 follows the full workflow, not the lighter test-only path. Any app change beyond these two needs the owner's approval first.
8. **Settings file timing (2026-11-23, raised by the dev session):** the spec wrongly assumed `settings.json` is created at launch. The app keeps its current behavior: settings are written only when the user presses OK in Audio settings. A missing file means defaults, and launching never overwrites a saved preference. Tests 1 and 10 were adjusted to match (option A, plus an OK step in test 10). Writing settings at startup (option C) was rejected.

## App changes (small, no visible behavior change)

### 1. Data-folder override
Today the app reads and writes real folders in four places:

| Code | Folder |
| --- | --- |
| `ProjectStore.CreateDefault()` (`Services/ProjectStore.cs:38`) | `Documents\Music Tool Projects\My First Project.musictool` |
| `MainWindow.GetRecoveredProjectPath()` (`MainWindow.xaml.cs:484`) | `Documents\Music Tool Projects\Recovered` |
| `AppSettingsStore.GetSettingsRoot()` (`Services/AppSettingsStore.cs:57`) | `%LOCALAPPDATA%\MusicTool` (`settings.json`) |
| `AutosaveRecoveryService.GetRecoveryRoot()` (`Services/AutosaveRecoveryService.cs:224`) | `%LOCALAPPDATA%\MusicTool\Recovery` |

- Add one small static class, `Services/AppPaths.cs`, as the single place that decides these folders: `ProjectsDirectory`, `AppDataDirectory`, and `RecoveryDirectory`.
- With no override, each property returns exactly today's path. With the override set to a folder `X`, the projects folder is `X\Projects` and the app-data folder is `X\AppData\MusicTool` (recovery goes in `X\AppData\MusicTool\Recovery`).
- Change the four call sites to use `AppPaths`. Each should be a one-line change.
- Add unit tests for `AppPaths` to `MusicTool.App.Tests` (with and without the override). These run in the normal suite.
- File dialogs (Open, Save copy, Import) are not changed; the tests don't use them.
- Add a code comment that the override exists for automated tests.

### 2. AutomationIds on controls built in code
Use `AutomationProperties.SetAutomationId(element, id)` in `MainWindow.xaml.cs`, and `AutomationProperties.AutomationId="…"` in XAML. `{n}` is the track's position, starting at 0. `{c}` is the clip's position within its track.

| Element | AutomationId |
| --- | --- |
| Track name `TextBlock` | `TrackName_{n}` |
| Track rename `TextBox` (while editing) | `TrackNameEditor` |
| Remove-track button | `TrackRemove_{n}` |
| Mute button | `TrackMute_{n}` |
| Volume slider | `TrackVolume_{n}` |
| Clip name `TextBlock` | `Clip_{n}_{c}` |
| "+ Add track" button (XAML, no `x:Name` today) | `AddTrackButton` |

Do not rename existing `x:Name`s: the tests depend on them, and MT-014 must keep them too. If FlaUI can't find an element by its ID, check the UIA tree with a tool like **FlaUInspect** or **Accessibility Insights** and report what you saw. Don't restructure the UI to get around it.

## Test project

- New project `MusicTool.App.UITests` (`net8.0-windows`, xUnit 2.9.3 like the existing tests), added to `MusicTool.sln`. Add the latest stable `FlaUI.UIA3` 4.x package and record which version you used.
- An opt-in attribute `[UIFact]` that works like `[HardwareFact]` (`MusicTool.App.Tests/Hardware/HardwareFactAttribute.cs`): it skips the test unless `MUSICTOOL_UI_TESTS=1`, with a skip reason that says how to turn them on.
- The tests must run one at a time, never in parallel, because they share one mouse and keyboard. Turn off xUnit parallelization for this project.
- A small helper (for example `AppSession`) that, for each test:
  1. creates a fresh temp folder;
  2. optionally seeds it with a project file or WAV before launch;
  3. starts the built `MusicTool.App.exe` with `MUSICTOOL_DATA_ROOT` set to that folder;
  4. waits for the main window;
  5. on dispose, closes the window normally and deletes the temp folder. Closing the window saves the project (`Window_Closing`), so a test can close the app and then read the saved `.musictool` JSON to check what was persisted.
- **Waiting:** wait for conditions with FlaUI's retry helpers (`Retry.WhileNull`/`Retry.WhileFalse` or similar) and a timeout of a few seconds. Don't use fixed `Thread.Sleep` delays. Explain why in the learning note: fixed delays make tests slow and flaky.
- If a test fails, it saves a screenshot of the window to the test output folder, to help with debugging.
- Seeding a clip: write a short silent WAV (for example 1 second, 44.1 kHz) into the temp project's `Media` folder, plus a project file whose clip references it. Use `ProjectStore` from the app project if that's the simplest route. Silent audio means playback makes no sound.

## Tests

| # | Test | Checks | Replaces manual row |
| --- | --- | --- | --- |
| 1 | Launch uses the temp folder | The window opens showing "My First Project"; `My First Project.musictool` is created under the temp folder's `Projects`; no `settings.json` is created at launch (the app writes settings only when the user presses OK in Audio settings) | — (protects the owner's real files) |
| 2 | Add and remove a track | Add track → `TrackName_1` reads "Audio 2"; remove it → gone; the saved project has one track | — |
| 3 | Rename by double-click, Enter saves | Double-click `TrackName_0`, type "Lead vocal", Enter → name shown and saved | MT-008 |
| 4 | Rename: Escape cancels, blank rejected | Escape keeps the old name; clearing the name and pressing Enter keeps the old name | MT-008 |
| 5 | Rename from the context menu, click-away saves | Right-click the track name → **Rename track...** → type → click elsewhere → saved | MT-008 |
| 6 | Space never records | Focus **Record**, press Space → Record's Name is still "Record vocal" (it becomes "Stop recording" while recording). Repeat with **Play** and **Stop** focused | MT-009 |
| 7 | Delete a clip keeps its media | Seeded clip → click `Clip_0_0` → Backspace → clip gone and saved project has no clip; the WAV is still in `Media` | — (repo rule: deleting a clip never deletes media) |
| 8 | Mute and volume are saved | Click `TrackMute_0`, set `TrackVolume_0` to about 50% → saved `IsMuted` is true and the volume is about 0.5 | — |
| 9 | Tempo edit is saved | Type 90 in Tempo, press Enter → saved `TempoBpm` is 90 | — |
| 10 | Audio settings: Cancel saves nothing, OK saves to the temp folder | Project menu → **Audio settings...** → the dialog shows Microphone and Output lists, each starting with "System default". Cancel closes it, and `settings.json` still doesn't exist. Open it again and press OK with System default selected → `settings.json` now exists under the temp folder's `AppData\MusicTool`, which proves the override routes settings too | MT-002 (partly: "the window shows them") |

Space and Backspace go to the focused window as real key presses, so these tests exercise the same `Window_PreviewKeyDown` path a user's keyboard does.

**Not covered (stays manual or with the hardware tests):** anything that needs recording, so MT-007 undo and "Audio settings disabled while recording"; device unplug; and drag-to-move and trim. Drag is possible with FlaUI but fragile; it is a candidate for later.

**If a test finds a real bug:** don't fix the app. Mark the test `Skip = "Bug: …"`, describe the bug in the summary, and the PM will add it to the backlog. Each feature stays one thing.

## How to run

```powershell
$env:MUSICTOOL_UI_TESTS = "1"
& "$env:USERPROFILE\.dotnet\dotnet.exe" test .\MusicTool.App.UITests
Remove-Item Env:MUSICTOOL_UI_TESTS
```

Don't touch the mouse or keyboard while the tests run, and keep the screen unlocked. Expect the run to take about a minute. The tests never play audible sound, so no headphones are needed. Add these instructions to the README and to AGENTS.md's build-and-test section.

## Expected changes

| File | Change |
| --- | --- |
| `MusicTool.App/Services/AppPaths.cs` *(new)* | The folder decisions, with the test override |
| `Services/ProjectStore.cs`, `Services/AppSettingsStore.cs`, `Services/AutosaveRecoveryService.cs`, `MainWindow.xaml.cs` | Use `AppPaths` (one line each) |
| `MainWindow.xaml.cs`, `MainWindow.xaml` | AutomationIds (one line per control) |
| `MusicTool.App.Tests/AppPathsTests.cs` *(new)* | Unit tests for the override |
| `MusicTool.App.UITests/` *(new project)* | `[UIFact]`, the `AppSession` helper, WAV/project seeding, and the 10 tests |
| `MusicTool.sln` | Adds the new project |
| `README.md`, `AGENTS.md` | How to run the UI tests |
| `docs/learning/MT-015-ui-smoke-tests.md` *(new)* | Learning note |

**Version:** unchanged (0.1.0), since nothing changes for the user.

**Suggested commit order** (each one small, each after the owner says "commit now"):
1. `AppPaths` and its unit tests
2. Call sites switched to `AppPaths`
3. AutomationIds
4. Test project skeleton: `[UIFact]`, `AppSession`, test 1
5. Tests 2–5
6. Tests 6–10
7. README/AGENTS.md
8. Learning note

## Acceptance checks

Dev session:
- [ ] With `MUSICTOOL_DATA_ROOT` unset, the app uses exactly the same folders as before, confirmed by the `AppPaths` unit tests.
- [ ] Normal `dotnet test .\MusicTool.sln`: all unit tests pass (42 + the new `AppPaths` tests), and the 6 hardware tests and 10 UI tests are skipped.
- [ ] With `MUSICTOOL_UI_TESTS=1`, all 10 UI tests pass twice in a row.
- [ ] Nothing is created in the owner's real `Documents\Music Tool Projects` or `%LOCALAPPDATA%\MusicTool` during a UI test run (check their modified times before and after).
- [ ] GitHub Actions stays green; the UI tests are skipped there.

Owner:
- [ ] Run the UI tests once yourself and watch the app being driven.
- [ ] Launch the app normally: it opens your usual project and settings.

## Out of scope

Running UI tests on GitHub Actions, testing file dialogs, drag/trim tests, visual comparisons from screenshots, fixing bugs the tests find, and any MVVM or UI restructuring.
