# MT-016: Repository hygiene

**Status:** NEXT — approved by the owner 2026-11-15. Small, no app behavior changes. Runs alone, before MT-015 (UI smoke tests) and MT-014 (MainWindow refactor).

## Purpose

Fix four small repository issues before the refactor, while they are cheap: inconsistent line endings, no automated build on GitHub, an uncommitted editor setting, and no app version number.

## Decisions (owner, 2026-11-15)

1. Normalize line endings with a `.gitattributes` file.
2. Add a GitHub Actions workflow that builds and runs the unit tests.
3. **Commit** `.vscode/settings.json` (it only sets `"dotnet.defaultSolution": "MusicTool.sln"`).
4. Set a baseline app version of `0.1.0`.

## 1. Line endings

**Background:** Windows ends text lines with two characters (CRLF), Linux and macOS with one (LF). This machine has `core.autocrlf=true`, and files written by different tools use different endings, which causes the "LF will be replaced by CRLF" warning on most commits. Without a rule in the repository, line-ending changes can show up as fake "every line changed" diffs, the worst possible thing during a refactor.

- Add `.gitattributes` at the repository root. Suggested content:

  ```gitattributes
  * text=auto
  *.cs     text diff=csharp
  *.xaml   text
  *.csproj text
  *.json   text
  *.md     text
  *.sln    text eol=crlf
  *.wav    binary
  *.mp3    binary
  *.png    binary
  ```

  The repository stores text files with LF; Git converts them for the checkout. `.sln` stays CRLF because Visual Studio expects it. Audio and image files are never converted.
- Renormalize existing files with `git add --renormalize .` and commit that **by itself** (for example "Normalize line endings"), so the diff contains nothing but line-ending changes. `git diff --ignore-all-space` against the previous commit should then be empty.
- Afterwards, a normal commit no longer prints LF/CRLF warnings.

## 2. GitHub Actions build

**Background:** GitHub Actions runs scripts on GitHub's servers when you push. Here it builds the solution and runs the unit tests on every push, so a broken build or failing test is visible on GitHub even if nobody ran the tests locally.

- Add `.github/workflows/build.yml` that runs on `push` and `pull_request` to `master`, on `windows-latest` (WPF needs Windows). Steps: check out the code, install the .NET 8 SDK (`actions/setup-dotnet`, `dotnet-version: 8.0.x`), `dotnet build`, `dotnet test`. Pin each action to a major version (for example `actions/checkout@v4`).
- Hardware tests (MT-012) and the future UI tests (MT-015) skip automatically because their environment variables aren't set on GitHub.
- If any existing unit test fails on GitHub because it needs something only this PC has (a sound device, a Documents folder), **don't change or skip the test**. Stop and report it; the PM will decide.
- The owner pushes to GitHub and confirms the first run is green on the repository's **Actions** tab. Note: if the repository is private, GitHub's free plan includes a monthly allowance of Actions minutes, and Windows minutes count double. A run of this size should take a few minutes.

## 3. Commit `.vscode/settings.json`

Commit it as is. It tells the VS Code C# extension which solution to open and contains nothing personal.

## 4. App version

- Add `<Version>0.1.0</Version>` to the `PropertyGroup` in `MusicTool.App/MusicTool.App.csproj`. This sets the version stamped into the built app (visible under the `.exe` file's Properties → Details).
- No UI change: the version is not shown in the app yet.
- `0.1.0` is the baseline for everything delivered so far (MT-001, MT-002, MT-007–MT-009, MT-012). The PM updates the versioning rule in the backlog at close-out.

## Expected changes

| File | Change |
| --- | --- |
| `.gitattributes` *(new)* | Line-ending rules |
| Many tracked text files | Line endings only, in the separate renormalize commit |
| `.github/workflows/build.yml` *(new)* | Build and unit tests on GitHub |
| `.vscode/settings.json` | Committed as is |
| `MusicTool.App/MusicTool.App.csproj` | `<Version>0.1.0</Version>` |

No C# code changes.

## Acceptance checks

Dev session:
- [ ] The renormalize commit contains only line-ending changes.
- [ ] A test commit afterwards shows no LF/CRLF warnings.
- [ ] Local `dotnet build` and `dotnet test` pass (42 passed, 6 hardware tests skipped).
- [ ] The built `MusicTool.App.exe` reports version 0.1.0.

Owner:
- [ ] After pushing, the first GitHub Actions run is green.

## Out of scope

Showing the version in the app, a CI status badge in the README, release builds or installers, and running hardware or UI tests on GitHub.
