# Contributing

Every change must be governed by an issue and identify its canonical source, owner, consumers, classification, field allowlist, compatibility effect, examples, tests, and changelog impact.

Use one branch per issue: `feat/issue-<number>-<name>`, `fix/issue-<number>-<name>`, or `docs/issue-<number>-<name>`. Do not work directly on `main` after bootstrap.

Before requesting review, run restore, build, formatting, all tests, CLI validation, standards validators, compatibility checks, link checks, and secret scanning. Review the complete diff and obtain CODEOWNERS approval for governed assets.

Never add shared service-domain models, unapproved fields, real customer/provider data, credentials, tokens, or applicant-facing reason-code translations.
