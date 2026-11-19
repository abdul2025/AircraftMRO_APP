# Minimal .NET 10 Engineering Standards

This folder is the shared baseline for the four lifecycle skills in `.agents/skills`.
It is intentionally small: it protects important engineering qualities without defining
how every application must be structured. This repository's required feature structure
is defined separately in [Application feature pattern](FEATURE-PATTERN.md).

## Authority

Use this order when guidance conflicts:

1. The user's explicit requirements and authorization.
2. Repository instructions, established contracts, and deliberate local conventions.
3. [Engineering core](CORE.md).
4. Framework defaults and general practice.

Raise a conflict instead of silently following a convention that creates a credible
correctness, security, data-integrity, or compatibility defect.

## Platform baseline

- Production projects target `net10.0` unless an approved compatibility requirement
  says otherwise, and use a supported security-serviced .NET 10 patch.
- Preview SDKs, language features, runtimes, and packages require explicit approval and
  must not become accidental production dependencies.
- Enable nullable reference types and the repository's .NET analyzers. Keep dependency
  versions visible and builds reproducible.
- The local database runs in a container built from `mcr.microsoft.com/mssql/server`.
- The solution has two ASP.NET Core hosts, `AircraftMRO.Web` (MVC) and `AircraftMRO.Api`,
  both run locally and both may share Infrastructure/Identity — see
  [Application feature pattern](FEATURE-PATTERN.md#identity-and-authentication-features)
  for how their authentication behavior must differ.

## Lifecycle skills

| Skill | Primary responsibility | May edit production code? | Required result |
| --- | --- | --- | --- |
| `plan-dotnet-feature` | Turn requirements and repository evidence into an implementation-ready plan | No | Decisions, affected locations, ordered work, and verification |
| `implement-dotnet-feature` | Deliver the smallest complete authorized behavior change | Yes | Working vertical change with verification evidence |
| `review-dotnet-feature` | Find concrete defects in a defined change set | No, unless fixes are separately requested | Prioritized evidence-backed findings |
| `test-dotnet-feature` | Design, add, or run tests according to the request | Only tests unless broader edits are authorized | Risk-based coverage and actual results |

Every skill reads `CORE.md` and `FEATURE-PATTERN.md`, inspects the repository, and then
performs only its own mode. The skills must not pretend that planning, review, or testing authorizes
implementation.

## Deliberately not standardized

The generic core does not prescribe project count, Clean Architecture, CQRS, MediatR,
repositories, mapping libraries, test naming, or a fixed delivery template. The
repository-specific feature folder, service, DTO, result, entity, enum, and query rules
are intentionally isolated in `FEATURE-PATTERN.md`.

Keep future pattern changes in `FEATURE-PATTERN.md`. This preserves a stable generic
baseline while allowing the application's design style to evolve.
