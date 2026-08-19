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
