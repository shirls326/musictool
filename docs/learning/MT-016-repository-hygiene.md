# MT-016: Repository hygiene (learning notes)

**Spec:** [features/MT-016-repository-hygiene.md](../features/MT-016-repository-hygiene.md) · **Branch:** `feature/MT-016-repository-hygiene` · **Built:** 2026-11-20

## What this feature did

Four small clean-ups before the MainWindow refactor. None of them change what the app does:

1. Line-ending rules for Git (`.gitattributes`).
2. An automatic build and test run on GitHub (GitHub Actions).
3. A shared VS Code setting (`.vscode/settings.json`).
4. A version number for the app (`0.1.0`).

## Concepts and terms

- **Line endings:** the invisible character(s) at the end of each line of text. Windows uses two (**CRLF**, `\r\n`); Linux and macOS use one (**LF**, `\n`). The text looks the same, but Git sees different bytes.
- **Index vs working copy:** the *working copy* is the file in your folder. The *index* is Git's stored version, which goes into commits and up to GitHub. `git ls-files --eol` shows both: `i/lf w/crlf` means "LF in Git, CRLF on disk".
- **`core.autocrlf=true`:** a setting on one PC only. It converts CRLF to LF when you add a file and back to CRLF when Git writes it to your folder.
- **`.gitattributes`:** the same kind of rule, but stored in the repo, so every machine follows it.
- **CI (continuous integration):** a server that builds the code and runs the tests on every push, so a breakage shows up even if nobody ran the tests locally.
- **GitHub Actions vocabulary:** a *workflow* is the YAML file; a *trigger* (`on:`) is the event that starts it; a *job* runs on one *runner* (a fresh virtual machine); a job is a list of *steps*; an *action* is a reusable published step like `actions/checkout`.
- **Pinning (`@v4`):** use the latest 4.x of an action. You get its fixes, but not a breaking version 5 without choosing it.
- **YAML:** a config format where indentation shows structure. It uses spaces, never tabs.
- **Workspace vs user settings (VS Code):** workspace settings live in the project's `.vscode/` folder and are shared through Git. User settings are personal and stay in your Windows profile.
- **MSBuild property:** a named build setting inside `<PropertyGroup>` in a `.csproj`, like `<TargetFramework>` or `<Version>`.
- **Semantic versioning:** `major.minor.patch`. A major version of `0` means "early, still changing".

## The changes, file by file

### `.gitattributes` (new)
- Line 1, `* text=auto`: Git detects text files, stores them as LF, and writes them to disk with this PC's ending.
- `*.cs text diff=csharp`: diff hunk headers name the C# method a change is in.
- `*.sln text eol=crlf`: always CRLF on disk, because Visual Studio expects it.
- `*.wav`/`*.mp3`/`*.png binary`: never converted, so audio and image bytes can't be corrupted.

**Surprise:** `git add --renormalize .` changed nothing. `git ls-files --eol` had already shown `i/lf` for every file, so Git's stored copies were clean. Only 3 working copies had LF endings (the MT-002, MT-014 and MT-016 specs), and those caused the "LF will be replaced by CRLF" warnings. Deleting them and running `git checkout -- <file>` rewrote them with CRLF.

### `.github/workflows/build.yml` (new)
- Lines 3–7: run on pushes to `master` **and** on pull requests into `master`. A push to a feature branch alone does not start a run.
- Line 12, `runs-on: windows-latest`: WPF only builds on Windows.
- Lines 15–19: check out the code, then install the .NET 8 SDK.
- Lines 21–26: `dotnet build`, then `dotnet test --no-build`, which reuses the build from the step before. A failing test makes the run red.
- The 6 hardware tests skip themselves. `HardwareFactAttribute` (`MusicTool.App.Tests/Hardware/HardwareFactAttribute.cs:22`) checks `MUSICTOOL_HARDWARE_TESTS`, which GitHub's machine doesn't set.

### `.vscode/settings.json` (committed as is)
`"dotnet.defaultSolution": "MusicTool.sln"` tells the C# extension which solution to load, for IntelliSense and the test explorer.

### `MusicTool.App/MusicTool.App.csproj`
Line 8, `<Version>0.1.0</Version>`: stamped into `MusicTool.App.exe`. Check it with
`(Get-Item MusicTool.App\bin\Debug\net8.0-windows\MusicTool.App.exe).VersionInfo`.
`ProductVersion` reads like `0.1.0+0131c09…`: the .NET 8 SDK adds the commit the app was built from after the `+`.

## Commits, in reading order

| Commit | What |
| --- | --- |
| `9338a70` | Add `.gitattributes` |
| `3c71c50` | GitHub Actions workflow |
| `1c7fe2b` | `.vscode/settings.json` |
| `0131c09` | Version 0.1.0 |

Run `git log -p master..feature/MT-016-repository-hygiene` before merging to see them all.

## Worth remembering

- **PowerShell 5.1 and quotes:** `git commit -m` with a message containing `"double quotes"` breaks: the text after the quote is read as file names. Writing the message to a file and running `git commit -F file.txt` avoids that.
- **Run the CI commands locally first.** The workflow runs the same commands you do, so if they pass on your PC, any failure on GitHub points to something about the machine.
- **Results:** build 0 warnings, 0 errors. Tests: 42 passed, 0 failed, 6 skipped (hardware).

## Check your understanding

1. `git add --renormalize .` changed no files. What did `git ls-files --eol` show that predicted this?
2. You push commits to a feature branch and nothing appears on the Actions tab. Is the workflow broken? What starts a run?
3. Why do the 6 hardware tests skip, both locally and on GitHub?

<details>
<summary>Answers</summary>

1. Every file showed `i/lf`: Git's stored copies were already LF. Renormalizing only rewrites the stored copies, so there was nothing to change. The problem files were only LF on disk (`w/lf`, `w/mixed`).
2. It isn't broken. The triggers are pushes to `master` and pull requests into `master`, so opening a PR starts a run before you merge.
3. They use real speakers and a microphone, so they only run when `MUSICTOOL_HARDWARE_TESTS=1`. `HardwareFactAttribute` marks them skipped otherwise, and GitHub's runner never sets that variable.

</details>
