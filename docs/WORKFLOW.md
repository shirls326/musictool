# Development Workflow

Music Tool is built one feature at a time using two separate AI sessions with distinct roles. The repository documents are the handoff between them; neither session should rely on remembering the other's conversation.

## Roles

| Session | Responsibilities | Does not |
| --- | --- | --- |
| **Product manager (PM)** | Chooses the next feature from `FEATURE_BACKLOG.md`, writes its spec in `docs/features/MT-XXX-<name>.md`, resolves OPEN product questions with the owner, reviews the implementation against the spec, and updates the backlog, development log, and README after delivery. | Write app or test code. |
| **Implementation (dev)** | Reads the spec, builds the feature and its tests, runs build/test, and reports what changed and why. | Change product scope, start other backlog items, or resolve OPEN questions on its own. |

The owner (you) approves the spec, answers OPEN questions, does hands-on testing with real audio hardware, and decides when to commit.

## The loop for each feature

1. **Spec (PM).** Mark the feature **NEXT** in the backlog and write or refresh `docs/features/MT-XXX-<name>.md`: user-visible behavior, edge cases, persistence, expected code changes, acceptance checks, and any **OPEN** decisions with a recommendation.
2. **Decide (owner).** Answer every OPEN item. The PM records each answer in the spec as **Decided**. Implementation starts only when no OPEN items remain, unless the spec explicitly says a decision can wait.
3. **Build (dev).** Start a fresh dev session and point it at the spec. The dev session works on a feature branch, keeps to the spec, and ends with a summary: files changed, what each change does, test results, and anything it could not verify.
4. **Review (PM).** The PM reads the diff against the spec's acceptance checks and reports gaps. Fixes go back to the dev session.
5. **Verify (owner).** The owner runs the manual checks listed in the spec on real hardware and reports the results. If the owner accepts the feature with checks deferred, the PM adds each unfinished check to the **Pending verification** table in `FEATURE_BACKLOG.md` so it is not forgotten.
6. **Close out (PM).** Move the backlog entry to **Implemented** with its verification status, add a dated milestone to the bottom of `DEVELOPMENT_LOG.md`, update the README's usage notes, and select the next feature.

## Learning mode (owner preference, 2026-11-15)

The owner is a newer software engineer learning C# through this project and wants to understand all of the code, not just receive it. Every dev session and PM review follows these rules:

- **Plan first, in plain language.** Before writing code, explain the plan file by file: what will change, why, and how it connects to the rest of the app. Wait for approval.
- **Define terms once.** The first time a concept appears (for example *partial class*, *interface*, *dependency injection*, *event handler*, *dispatcher*), give a one- or two-sentence definition with an example from this codebase.
- **Small, explained commits.** Each commit does one thing, and its message says why, not just what.
- **End with a code tour.** For each changed file: what it does, how it fits with the rest of the app, and one pattern or idea worth learning from it, with `file:line` references. Suggest an order for reading the diff.
- **Check understanding.** End with two or three short questions the owner can try to answer about the change, with answers given separately afterwards.
- **No unexplained magic.** If code relies on something non-obvious (threading, Windows audio behavior, xUnit features), add a short comment in the code or an explanation in the summary.

The PM's reviews include a "what to learn from this change" section, and dev-session prompts written by the PM ask for all of the above.

### Lighter path for test-only work

When a backlog item changes only test code (no user-visible behavior), the owner can approve the PM's recommendations for its OPEN items in one step. Steps 1, 3, 4 and 6 still apply: a short spec, a feature branch, PM review, and close-out. The spec must state that app code is out of scope, and the dev session stops and asks if an app change seems necessary.

## Rules both sessions follow

- One feature per branch and per dev session. Name branches `feature/MT-XXX-short-name`.
- Questions the spec does not answer go back to the PM/owner and are not guessed. The dev session notes them in its summary.
- Feature IDs are permanent; see the key in `FEATURE_BACKLOG.md`.
- Anything that needs real microphones, headphones, or audio interfaces is listed as manual verification, never claimed as done.
