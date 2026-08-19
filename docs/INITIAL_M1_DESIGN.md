# Initial M1 Executable Contracts — Implementation Design

**Status:** Approved implementation design

**Architecture source commit:** `b545d085441ce02a61a400f4eb778673410a366d`

## 1. Purpose and authority

This document describes how the initial M1 executable contracts will be implemented and validated. It is not a source of domain meaning, contract semantics, field authorization, security policy, or delivery status. If this design conflicts with a canonical source, the canonical source wins and implementation stops for review.

The authoritative inputs are:

- `SamuelSerrano/loan-platform-architecture#18` and its approved scope;
- `AGENTS.md` and `README.md` at the source commit;
- `docs/adr/ADR-001-MULTI-REPOSITORY.md`;
- `docs/adr/ADR-005-CONTRACT-GOVERNANCE.md`;
- `docs/contracts/CONTRACTS_BASELINE.md`;
- `docs/contracts/INITIAL_CONTRACT_CATALOG.md`;
- `docs/architecture/SECURITY_MODEL.md`;
- `docs/architecture/SYSTEM_CONTEXT.md`;
- `docs/architecture/CONTAINER_DIAGRAM.md`;
- `docs/architecture/DATA_OWNERSHIP.md`;
- `docs/domain/DOMAIN_EVENTS.md`;
- `docs/workflows/LOAN_ONBOARDING.md`;
- `docs/workflows/CREDIT_DECISION.md`;
- `docs/workflows/DOCUMENT_SIGNING.md`;
- `docs/workflows/DISBURSEMENT.md`;
- `roadmap/REPOSITORY_DELIVERY_ROADMAP.md`.

The catalog remains the sole authority for the initial inventory and its deny-by-default field approvals. This design neither copies its rows nor authorizes additions.

## 2. Scope and status boundary

The executable boundary is exactly the cataloged initial inventory: five public HTTP contract entries (including the shared problem shape), nine integration events, one asynchronous policy request, and one versioned policy contract. The validation manifest must resolve these 16 unique entries and exactly 175 unique approved field paths back to the canonical catalog. Any additional contract, version, field, consumer, classification, or semantic authority is a validation failure requiring new governance.

M1 remains `Defined` throughout this phase. Repository release `v0.1.0` is only the planned initial compatibility baseline: no tag or GitHub Release is published, and no compatibility comparison against an imaginary prior release is performed. Service implementation, generated SDK adoption, AWS infrastructure, Cognito integration, and downstream milestone work have not started.

## 3. Technology and architecture

The primary implementation is C# on the pinned .NET 10 SDK. Nullable reference types, warnings as errors, deterministic builds, Central Package Management, `System.Text.Json`, xUnit, and exact dependency versions are repository-wide requirements.

The solution uses Hexagonal Architecture:

- **Domain** contains only contract-governance concepts and invariants: descriptors, categories, approved field paths, versions, findings, severity, compatibility changes, and rules for allowlists, classifications, closed schemas, and compatibility. It has no filesystem, process, YAML, JSON parser, Git, network, or external-validator dependency and contains no loan-service domain model.
- **Application** contains validation use cases and ports. It orchestrates inventory, 175-path reconciliation, closed-schema checks, example validation, prohibited-field detection, version checks, compatibility classification, reproducible evidence, and the consolidated report. It depends only on Domain.
- **Infrastructure** implements driven adapters for filesystem access, YAML/JSON parsing, JSON Schema 2020-12 validation, OpenAPI validation and bundling, AsyncAPI validation, controlled external processes, Git/release-baseline inspection, and report persistence. External validators are invoked only here through Application ports.
- **CLI** is the driving adapter and composition root. `validate` assembles adapters, executes every mandatory gate, writes deterministic evidence, prints a sanitized summary, and exits nonzero if any required gate fails.
- **Tests** are separated into Domain, Application, Infrastructure, Architecture, and Contract test projects. Productive projects never reference tests.

Dependency direction is `CLI -> Application <- Infrastructure` with both Application and Infrastructure depending on Domain only as required by their roles. Domain depends on nothing else in the solution. Infrastructure implements Application ports; it does not become a source of policy.

## 4. Contract artifact structure

OpenAPI 3.1.2 describes the public HTTP boundary and references closed JSON Schemas. Two AsyncAPI 3.1.0 documents keep integration events separate from the asynchronous policy request. Independent JSON Schema Draft 2020-12 documents define common transport structures, event payloads, the request, and the versioned policy. Examples remain fictitious, minimized, and classified as positive or intentionally negative evidence.

OpenAPI 3.1.2 is retained after reevaluation because the selected lint, bundle, structural-validation, and documentation chain supports the complete 3.1 feature set consistently, while equivalent end-to-end 3.2 support is not yet demonstrated. Adopting 3.2 requires separate approval.

Every object schema, including object items inside arrays, is closed with executable behavior equivalent to `additionalProperties: false`. Primitive arrays declare explicit item types. Monetary values use decimal-safe JSON numbers paired with their approved currency field. Timestamps are RFC 3339 UTC values ending in `Z`. IDs are opaque strings; property and schema casing follows the canonical baseline.

The machine-readable governance manifest under `docs/` is derived evidence. It records the architecture repository and source SHA, inventory metadata, schema locations, classifications, and canonical references needed to prove 16/175 without becoming a second semantic authority.

## 5. Security boundary

JWT appears only in the OpenAPI OAuth 2.0 security metadata for the four approved applicant scopes. Each operation receives only its required scope. The public contract distinguishes `401 Unauthorized` authentication failures from `403 Forbidden` authorization or ownership failures and uses the approved safe problem shape.

JWT tokens, claims, refresh tokens, Cognito metadata, provider credentials, and authentication material are prohibited from schemas, events, asynchronous requests, examples, fixtures, reports, logs, and versioned configuration. Resource authorization remains independent of authentication. Future services translate validated claims to a provider-neutral `ActorContext`; this repository does not implement that runtime behavior or synchronous service-to-service authentication.

Q-008 remains open. Internal reason codes may occur only in their explicitly approved service-to-service locations. Automated checks reject their presence in applicant responses, public examples, the problem shape, or applicant-facing text. Technical, malformed, provider, transport, and infrastructure failures never become `Favorable` or `Unfavorable` outcomes.

## 6. Validation flow and evidence

The CLI loads the derived governance manifest, discovers contract artifacts, and validates uniqueness and exact counts. It then executes structural standards adapters, reconciles every materialized leaf against the approved path set, verifies closed objects and typed arrays, validates positive and negative examples, scans prohibited fields and public reason-code leakage, checks versions, and evaluates compatibility status.

Producer and consumer fixtures use separate private C# types. The producer serializes its own model and the consumer deserializes to a different private model; JSON Schema is the shared boundary. No DTO assembly or NuGet package is produced.

The deterministic report at `artifacts/validation/validation-report.json` records the source commit, current commit, UTC execution time, .NET and validator versions, selected standards, counts, gate outcomes, compatibility status, and overall result. Paths are repository-relative, sensitive values are excluded, and CI supplies its secret-scan result. Generated artifacts remain ignored unless explicitly attached by CI.

Initial compatibility reports exactly `Initial baseline — no previous release`. After a reviewed `v0.1.0` release exists, a future change may compare against the latest published baseline through the Git adapter. This phase does not simulate or publish that baseline.

## 7. Test and CI strategy

TDD covers governance invariants and orchestration before adapters. Architecture tests enforce project references, layer direction, absence of future-service domain models, and absence of a shared DTO package. Contract tests cover all approved positive and negative scenarios, unsupported versions, additional and prohibited fields, invalid timestamps, open nested structures, identity precondition failure, operational dispositions, both credit outcomes, immutable offers, duplicates, and malformed messages.

CI uses pinned GitHub Actions and exact tool versions. It restores, builds, verifies formatting, runs every test project, invokes the CLI, validates OpenAPI/AsyncAPI/JSON Schema, verifies examples and 16/175 counts, checks closed schemas and prohibited fields, evaluates initial compatibility, scans secrets, validates Markdown links/anchors, and uploads sanitized evidence. No AWS, paid service, external runner, persistent infrastructure, or disabled warning/test is allowed.

## 8. Failure behavior

A missing or extra contract/field, unresolved reference, open object, invalid example, validator failure, unsupported standard, prohibited datum, reason-code leak, compatibility violation, secret finding, or evidence-write failure produces a structured finding and nonzero exit. Infrastructure failures remain technical findings and cannot be translated into credit outcomes. Validators run with bounded execution, captured sanitized output, and explicit pinned versions.

If tooling requires changing an approved standard, a schema requires an unapproved field, or the 175-path mapping cannot be proven exactly, implementation stops without weakening a gate.

## 9. Delivery sequence

1. Build and validate a truthful local bootstrap on `main`; it makes no M1 completion claim.
2. Create the public `SamuelSerrano/loan-platform-contracts` repository from that bootstrap and publish `main`.
3. Implement executable contracts and validation on `feat/issue-18-initial-m1-contracts`.
4. Run all local gates, review the complete diff, commit, publish the branch, and open a Draft PR targeting `main`.
5. Reference `SamuelSerrano/loan-platform-architecture#18` without closing it; publish one consolidated issue comment.
6. Keep the PR Draft, M1 `Defined`, and `v0.1.0` unpublished until explicit review approval.
7. In a later phase only: mark Ready, merge, publish and verify `v0.1.0`, then update architecture evidence and milestone status through a separately governed architecture PR.

Q-001, Q-002, Q-006, Q-007, and Q-008 remain open. No issue #19 or later work begins in this phase.

## 10. Implementation plan

> **For agentic workers:** Execute this plan inline task-by-task with test-first checkpoints. No subagent delegation is authorized for this repository task.

**Goal:** Publish a reviewable Draft PR containing the exact initial M1 executable contracts, their hexagonal C# validator, tests, CI, and reproducible evidence without releasing `v0.1.0` or advancing M1.

**Architecture:** C# owns governance rules and orchestration. Standards tools and filesystem/Git/process concerns remain Infrastructure adapters behind Application ports; the CLI composes and runs all gates.

**Tech stack:** .NET SDK `10.0.400`; C#; xUnit `2.9.3`; Microsoft.NET.Test.Sdk `18.8.1`; YamlDotNet `18.1.0`; JsonSchema.Net `9.4.0`; Node `20.20.2`; Redocly CLI `2.46.2`; AsyncAPI CLI `6.0.2`; AJV `8.20.0`; YAML `2.9.0`; Markdown Link Check `3.15.0`.

### Global constraints

- Preserve architecture source SHA `b545d085441ce02a61a400f4eb778673410a366d` and its canonical authority.
- Materialize exactly 16 contracts and 175 unique approved field paths; deny everything else.
- Use OpenAPI 3.1.2, AsyncAPI 3.1.0, JSON Schema 2020-12, RFC 3339 UTC `Z`, and RFC 9457.
- Keep schemas closed, money decimal-safe, IDs opaque, properties `camelCase`, and schemas `PascalCase`.
- Keep JWT/OAuth metadata at HTTP boundaries only and preserve Q-008.
- Keep M1 `Defined`; do not tag, release, merge, or start service implementation.

### Task 1: Governed repository bootstrap

**Files:** Create `README.md`, `AGENTS.md`, `LICENSE`, `CONTRIBUTING.md`, `CHANGELOG.md`, `.gitignore`, `.editorconfig`, `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `LoanPlatform.Contracts.slnx`, `.github/CODEOWNERS`, `.github/workflows/ci.yml`, `package.json`, `package-lock.json`, `redocly.yaml`, and the required `src/`, `tests/`, `openapi/`, `asyncapi/`, `schemas/`, and `examples/` structure.

**Produces:** A truthful bootstrap on `main`, pinned toolchain, repository governance, and buildable empty layer/test projects.

- [ ] Create repository metadata that states purpose, exclusions, source authority, unpublished `v0.1.0`, M1 `Defined`, and no service implementation.
- [ ] Pin `10.0.400` in `global.json`, all NuGet versions in `Directory.Packages.props`, and npm versions without ranges in `package.json`.
- [ ] Create the four productive projects and five xUnit projects with references enforcing `Application -> Domain`, `Infrastructure -> Application + Domain`, and `CLI -> Application + Infrastructure`.
- [ ] Add all projects to `LoanPlatform.Contracts.slnx`; run `dotnet restore`, `dotnet build -warnaserror`, and `dotnet test` expecting success.
- [ ] Commit with `chore: bootstrap contracts repository` only after README/governance/build checks pass.

### Task 2: Domain governance model (red-green-refactor)

**Files:** Create focused types under `src/LoanPlatform.Contracts.Domain/Governance/` and matching tests under `tests/LoanPlatform.Contracts.Domain.Tests/Governance/`.

**Produces:** `ContractDescriptor`, `ContractCategory`, `ApprovedFieldPath`, `ContractVersion`, `ValidationFinding`, `ValidationSeverity`, `CompatibilityChange`, `ContractInventory`, and pure invariant services.

- [ ] Write failing tests proving duplicate contract names, duplicate field paths, unapproved classifications, invalid visible versions, extra fields, and open object declarations are rejected.
- [ ] Run `dotnet test tests/LoanPlatform.Contracts.Domain.Tests` and confirm the intended failures.
- [ ] Implement immutable records/value objects with factory methods returning findings rather than throwing for user-controlled artifact errors.
- [ ] Run Domain tests and commit `feat: add contract governance domain` when green.

### Task 3: Application ports and validation pipeline

**Files:** Create ports under `src/LoanPlatform.Contracts.Application/Ports/`, use cases under `Validation/`, report contracts under `Evidence/`, and Application tests with in-memory fakes.

**Interfaces:** `IArtifactRepository`, `ISchemaValidator`, `IExternalSpecificationValidator`, `ICompatibilityBaseline`, `IValidationReportWriter`, `IClock`, and `IRepositoryMetadata`; `ValidateContracts.ExecuteAsync(ValidationRequest, CancellationToken)` returns `ValidationReport`.

- [ ] Write failing orchestration tests for exact 16/175 reconciliation, ordered gate execution, aggregate findings, nonzero overall result, sanitized relative paths, and `Initial baseline — no previous release`.
- [ ] Implement ports and a deterministic pipeline whose gate results are sorted by stable gate ID.
- [ ] Test cancellation and adapter failure mapping as technical findings, never credit outcomes.
- [ ] Run Application and Domain tests; commit `feat: orchestrate contract validation`.

### Task 4: Derived manifest and exact executable schemas

**Files:** Create `docs/contract-governance-manifest.yaml`; OpenAPI under `openapi/`; separate AsyncAPI documents under `asyncapi/`; Draft 2020-12 schemas under `schemas/common`, `schemas/events`, `schemas/requests`, and `schemas/policies`; examples under `examples/`.

**Produces:** The canonical-catalog-derived 16/175 mapping with no unapproved leaf.

- [ ] Extract catalog rows from the architecture source at the pinned SHA into a temporary review matrix; compare unique contract/path counts before authoring schemas.
- [ ] Write contract tests that fail until manifest entries resolve to real schemas and every approved leaf is materialized exactly once in its governed location.
- [ ] Author closed common primitives/envelopes, then the nine event payloads, the separate request, the policy, and the four operations plus shared problem shape in OpenAPI.
- [ ] Encode only approved enums/constraints; reject request identity status other than `Verified`; preserve `Insufficient` as PendingEvidence evidence only.
- [ ] Add fictitious positive/negative examples for all required scenarios and run the contract tests until 16/175 and closure checks pass.
- [ ] Commit `feat: materialize initial M1 contracts`.

### Task 5: Infrastructure adapters

**Files:** Create adapters under `src/LoanPlatform.Contracts.Infrastructure/` for filesystem, YAML/JSON, JsonSchema.Net, external processes, Git baseline, and JSON report persistence; create Infrastructure tests with temp directories and fake executables.

**Produces:** Application-port implementations with bounded process execution and sanitized diagnostics.

- [ ] Write failing adapter tests for YAML/JSON loading, `$ref` resolution, Draft 2020-12 validation, timeout/nonzero process results, Git no-release baseline, and deterministic report JSON.
- [ ] Implement filesystem and serializer adapters using `System.Text.Json`, YamlDotNet, and JsonSchema.Net.
- [ ] Implement process adapters invoking local pinned npm binaries only, with argument lists, timeout, captured size limits, and no shell interpolation.
- [ ] Run Infrastructure tests and commit `feat: add validation infrastructure adapters`.

### Task 6: CLI and reproducible evidence

**Files:** Create CLI command/composition files under `src/LoanPlatform.Contracts.Cli/` and CLI-oriented Application/Contract tests.

**Produces:** `dotnet run --project src/LoanPlatform.Contracts.Cli -- validate` and `artifacts/validation/validation-report.json`.

- [ ] Write failing tests for missing command, successful validation exit `0`, failed mandatory gate exit `1`, sanitized console output, and deterministic report ordering.
- [ ] Compose all production adapters and implement `validate` without business rules in `Program.cs`.
- [ ] Run the CLI against positive artifacts and then a controlled invalid fixture; verify expected exit codes and report shape.
- [ ] Commit `feat: add contracts validation cli`.

### Task 7: Independent producer/consumer and architecture tests

**Files:** Create private fixture models in separate namespaces/files under `tests/LoanPlatform.Contracts.ContractTests/Fixtures/Producer/` and `Consumer/`; create architecture rules under `tests/LoanPlatform.Contracts.Architecture.Tests/`.

**Produces:** Schema-mediated interoperability without shared DTOs and executable layer boundaries.

- [ ] Write a producer test serializing its private model, validate JSON against the schema, then deserialize into a distinct consumer private model.
- [ ] Cover duplicate, changed replay, unsupported version, malformed message, both outcomes, all operational dispositions, immutable offer, and Q-008 leak rejection.
- [ ] Add reflection/project-reference tests proving layer direction, Infrastructure port implementations, CLI composition role, no productive-to-test reference, no future-service domain models, and no packable shared DTO project.
- [ ] Run Contract and Architecture tests; commit `test: prove contract and architecture boundaries`.

### Task 8: CI, documentation, and publication

**Files:** Finalize `.github/workflows/ci.yml`, README, CONTRIBUTING, CHANGELOG, CODEOWNERS, and validator configuration.

**Produces:** Reproducible local/CI gates, remote public repository, feature branch, Draft PR, and one architecture-issue comment.

- [ ] Pin actions by full commit SHA with version comments; run restore locked, build, format, all tests, CLI, standards validation, 16/175/closure/prohibited checks, Markdown links, secret scan, and evidence upload.
- [ ] Run locally: restore, build, format verification, every test project, CLI, Redocly lint/bundle, AsyncAPI validate, JSON Schema/example checks, architecture checks, links/anchors, secret scan, and both diff checks.
- [ ] Confirm no `bin/`, `obj/`, `node_modules/`, generated artifacts, caches, local paths, secrets, or untracked temporary files are committed.
- [ ] Create the public remote only from the coherent bootstrap, push `main`, create `feat/issue-18-initial-m1-contracts`, commit its complete implementation, and push it.
- [ ] Open a Draft PR against `main` with mandatory handoff sections and `Tracks SamuelSerrano/loan-platform-architecture#18`; do not use `Closes #18`.
- [ ] Publish or update exactly one consolidated issue #18 comment, then verify Draft/open/base/head/SHA, issue OPEN, no release/tag, M1 `Defined`, and clean working tree.
