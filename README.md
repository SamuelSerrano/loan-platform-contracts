# Loan Platform Contracts

Language-neutral executable contract assets and validation tooling for the Loan Onboarding Platform.

## Status

This repository is being bootstrapped under `SamuelSerrano/loan-platform-architecture#18`. M1 remains `Defined`, service implementation has not started, and the planned `v0.1.0` compatibility baseline is unpublished.

## Authority and boundaries

Domain and architecture meaning remains canonical in [`loan-platform-architecture`](https://github.com/SamuelSerrano/loan-platform-architecture) at source commit `b545d085441ce02a61a400f4eb778673410a366d`. This repository will materialize only the initial catalog authorized by ADR-005 and the canonical contract catalog. It never distributes shared C# service models.

The repository owns OpenAPI, AsyncAPI, JSON Schema, fictitious examples, contract-governance validation, compatibility evidence, and changelog history. It excludes service code, AWS infrastructure, production authentication, generated SDK adoption, and business-domain implementations.

## Architecture

The validator uses Hexagonal Architecture:

- Domain: pure contract-governance invariants.
- Application: validation use cases and ports.
- Infrastructure: filesystem, parsers, standards tools, Git, and evidence adapters.
- CLI: driving adapter and composition root.

External validators remain behind Application ports. C# and .NET 10 are the primary implementation.

## Local commands

```bash
dotnet restore --locked-mode
dotnet build --no-restore
dotnet format --verify-no-changes --no-restore
dotnet test --no-build
dotnet run --project src/LoanPlatform.Contracts.Cli -- validate
npm ci
```

Run `npm ci` before the CLI. The CLI is the single consolidated validation entry point and invokes pinned OpenAPI, AsyncAPI and Markdown validators through bounded Infrastructure process adapters. It also records schema/example, exact 16/175, policy-integrity, Q-008, compatibility and tracked-source secret-scan gates in one sanitized report.

## Security

OAuth 2.0/JWT metadata is limited to the public HTTP description. Tokens, claims, credentials, provider payloads, raw evidence, PII, and unrestricted rule traces are prohibited from contract payloads and examples. Q-008 remains open: internal reason codes are not applicant-facing and are not translated into public messages.

## Versioning and consumption

Visible contract versions are independent from repository releases. `v0.1.0` will become the first compatibility baseline only after explicit review, merge, and release. Consumers use language-neutral artifacts and map them into private local models; they do not reference a shared DTO assembly.

See [the implementation design](docs/INITIAL_M1_DESIGN.md) for the approved delivery approach.
