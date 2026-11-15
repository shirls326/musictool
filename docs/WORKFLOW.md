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
5. **Verify (owner).** The owner runs the manual checks listed in the spec on real hardware and reports the results.
6. **Close out (PM).** Move the backlog entry to **Implemented** with its verification status, add a dated milestone to the bottom of `DEVELOPMENT_LOG.md`, update the README's usage notes, and select the next feature.

## Rules both sessions follow

- One feature per branch and per dev session. Name branches `feature/MT-XXX-short-name`.
- Questions the spec does not answer go back to the PM/owner and are not guessed. The dev session notes them in its summary.
- Feature IDs are permanent; see the key in `FEATURE_BACKLOG.md`.
- Anything that needs real microphones, headphones, or audio interfaces is listed as manual verification, never claimed as done.
