# Repository Collaboration Protocol

GitHub is the source of truth. Read the governing issue, comments, this file, canonical architecture sources, and current `main` before editing. Preserve user changes and stop on contradictions.

Never work directly on `main` except for the explicitly authorized initial bootstrap. Use one issue branch and keep every commit within scope. Contract meaning and field authorization remain in `SamuelSerrano/loan-platform-architecture`; derived manifests never override them.

Use Spec-Driven Development, TDD, .NET 10, Hexagonal Architecture, closed schemas, and language-neutral artifacts. Domain has no infrastructure dependencies; Application depends only on Domain; Infrastructure implements Application ports; CLI is the composition root. Never publish shared service DTO/domain packages.

Before publishing, run every required validation, review the full diff, scan secrets, confirm a clean tree, commit intentionally, push, and create or update a Draft PR. Synchronize the governing architecture issue with one consolidated comment and do not duplicate comments.

PR bodies must contain: Summary, Source documents reviewed, Files changed, Decisions preserved, Validation, Open questions, Out of scope, and ChatGPT Work handoff. Final responses must use:

```text
TASK HANDOFF

Repository:
Issue:
Branch:
Pull request:
Latest commit:
Status:
Files changed:
Validations:
Open questions:
Next recommended action:
```

Do not mark work `Ready for review` until the remote branch, Draft PR, issue comment, validations, SHA equality, and clean tree are verified. Do not merge or release without explicit approval.
