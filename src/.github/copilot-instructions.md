# CodeMetrics.Net Repository Instructions

## Core Principles

### I. Stable CLI and Output Contracts
Command names, aliases, defaults, validation behavior, help text, exit codes, stdout/stderr
routing, sidecar naming, report schemas, ordering, and metric definitions are public contracts.
Compatible releases MUST preserve these contracts. Any intentional breaking change MUST include
a major-version increment, migration guidance, release notes, and contract tests. Machine-readable
stdout MUST contain report data only; diagnostics and progress MUST use stderr. This stability is
required because CI pipelines and coding agents consume the tool without human interpretation.

### II. Deterministic, Invariant Analysis
For identical source, options, and tool version, analysis MUST produce the same metric values and
byte-stable machine-readable output across repeated runs. Ordering, tie-breaking, encodings, and
numeric formatting MUST be explicit and culture invariant. Parallel execution MAY improve
performance but MUST NOT alter observable results. Determinism makes reports reviewable, cacheable,
and safe for quality gates.

### III. Evidence-Based Correctness (NON-NEGOTIABLE)
Behavior changes MUST be test-first: add or update a test, observe it fail for the intended reason,
implement the smallest correct change, then refactor with all tests passing. Metric logic MUST have
focused tests for every supported C# construct. CLI changes MUST have process-level tests covering
arguments, files, streams, and exit codes. JSON, CSV, HTML data, and help contracts MUST have
snapshot, golden, or equivalent schema assertions. Every bug fix MUST include a regression test.
Build success alone is not evidence of correctness.

### IV. Syntax-Only Scope and Honest Semantics
The tool MUST remain a lightweight Roslyn syntax analyzer: it MUST NOT require loading target
projects through MSBuild, restoring target dependencies, or performing semantic compilation unless
an approved constitution amendment changes this scope. Metric definitions, approximations,
unsupported constructs, generated-file rules, and solution/project discovery limitations MUST be
documented. New C# syntax MUST be deliberately classified and covered by regression fixtures before
support is claimed. Results MUST never imply semantic precision the implementation does not provide.

### V. Maintainable, Focused .NET Design
Production code MUST compile with nullable analysis enabled and no newly introduced warnings. CLI
orchestration, source discovery, metric calculation, and reporting MUST retain clear boundaries and
single responsibilities. Prefer guard clauses, descriptive names, immutable data, explicit
dependencies, and the simplest design that meets current requirements. Normal members MUST keep
cyclomatic complexity at or below 7, cognitive complexity at or below 15, and parameters at or
below 6. Exceptions MUST be recorded in the implementation plan with measured evidence, rationale,
and a reduction plan. Comments MUST explain non-obvious intent or trade-offs, not restate code.

## Engineering and Release Constraints

- The supported .NET SDK and Roslyn parser versions MUST be pinned or explicitly documented.
- Dependencies MUST be minimal, justified, actively maintained, and reviewed for license and
	supply-chain risk before adoption.
- Restore, build, test, pack, and local-tool smoke validation MUST be reproducible from a clean
	checkout before release.
- NuGet packages MUST include accurate identity, version, description, authorship, repository,
	license, readme, and release metadata. Package versions MUST follow Semantic Versioning.
- Core reports MUST remain usable offline. External resources, absolute paths, or other potentially
	identifying data MUST be avoided by default or explicitly documented and configurable.
- Generated build outputs, packages, and reports MUST NOT be committed unless they are named test
	fixtures with a documented purpose.
- Performance work MUST use representative measurements. It MUST NOT compromise correctness,
	determinism, or contract compatibility.

## Development Workflow and Quality Gates

1. Specifications MUST state affected metric semantics and every observable CLI contract, including
	 arguments, streams, files, schemas, ordering, exit codes, privacy, and failure behavior.
2. Plans MUST pass the Constitution Check before research and again after design. Deviations require
	 a documented rationale and rejected simpler alternative.
3. Tasks for behavior changes MUST include tests before implementation, documentation updates, and
	 build/test/pack validation. Contract changes MUST include compatibility and migration tasks.
4. C# changes MUST be evaluated with `codemetrics -s <file> -f json` before and after modification.
	 Threshold violations MUST be refactored or explicitly approved under Principle V.
5. Reviews MUST verify functional correctness, regression coverage, deterministic output,
	 compatibility, error handling, privacy, and documentation. Self-review is not a substitute for
	 automated evidence.
6. A release candidate MUST pass clean restore, build, tests, package creation, package inspection,
	 and an installed-tool smoke test on the supported SDK.

## Governance

This constitution supersedes conflicting project practices and generated templates. Amendments MUST
be proposed in writing, explain their motivation and compatibility impact, update dependent Spec Kit
artifacts in the same change, and receive maintainer approval. Breaking principle removals or
redefinitions require a MAJOR version; new principles or materially expanded obligations require a
MINOR version; clarifications and non-semantic corrections require a PATCH version.

Every feature plan and code review MUST record constitution compliance. Non-compliance MUST block
merge unless the same review contains a time-bounded, maintainer-approved exception with owner,
rationale, risk, and remediation date. Governance is reviewed whenever a contract changes, before
each release, and at least annually. Runtime guidance MAY add detail but MUST NOT weaken these rules.

## Project Context

- This repository produces `CodeMetrics.Cli`, a .NET 10 global tool invoked as `codemetrics`.
- The tool performs syntax-only C# analysis with `Microsoft.CodeAnalysis.CSharp`; do not introduce MSBuild project loading, target-project restore, or semantic compilation without an approved architectural and constitutional change.
- Preserve the current boundaries: `Program.cs` orchestrates execution, `Cli/` owns arguments, `Analysis/` owns source discovery and metrics, `Reporting/` owns output formats, and `samples/` contains noncompiled syntax fixtures.
- Keep dependencies minimal. Prefer the .NET base class library and existing Roslyn APIs over new packages.

## Public Contracts

Treat all observable behavior as versioned API:

- Command names, options, aliases, defaults, validation, and help text.
- Exit codes: `0` for success, `1` for a configured threshold breach, and `2` for usage/input failures.
- stdout/stderr routing. Machine-readable stdout must contain report data only; diagnostics and progress belong on stderr.
- Single-file behavior, including stdout plus sidecar output and `<source>.codemetrics.<extension>` naming.
- JSON properties and types, CSV headers/order/escaping, table behavior, HTML data, report ordering, and invariant numeric formatting.
- Metric definitions, included member kinds, line numbers, source-discovery rules, and generated-file exclusions.
- Thresholds are exceeded only when a metric is strictly greater than the configured positive limit.

Do not change a public contract accidentally. For an intentional breaking change, require explicit approval, a major-version plan, migration guidance, release notes, and golden contract tests.

## Implementation Standards

- Write idiomatic modern C# with nullable reference types enabled.
- Prefer descriptive names, guard clauses, immutable data, explicit dependencies, and small single-purpose members.
- Keep normal members at cyclomatic complexity <= 7, cognitive complexity <= 15, and parameter count <= 6.
- Avoid speculative abstractions, hidden global state, broad exception handling, and clever code that obscures metric semantics.
- Keep output deterministic despite parallel analysis. Define stable ordering and tie-breakers explicitly; use culture-invariant formatting.
- Preserve cancellation and async behavior where relevant. Use asynchronous file APIs in asynchronous flows.
- Validate external input at the boundary and produce actionable errors without leaking unnecessary absolute paths or environment details.
- Comments should explain non-obvious intent, metric rationale, compatibility constraints, or trade-offs—not restate code.
- Do not edit generated files under `bin/`, `obj/`, `nupkg/`, or generated metric reports.

## Testing Workflow

For every behavior change:

1. Add or update the smallest relevant automated test first and confirm it fails for the intended reason.
2. Implement the smallest correct change.
3. Add regression coverage for bugs and focused syntax coverage for metric changes.
4. Use process-level integration tests for arguments, help, streams, files, and exit codes.
5. Use golden or schema assertions for JSON, CSV, HTML data, and other stable text contracts.
6. Verify repeated executions produce identical machine-readable output.

Do not weaken or delete a test merely to make a change pass. If no test project exists for behavior-changing work, surface that gap and establish an appropriate .NET test project before treating the work as complete.

## Validation

Run validation from the repository workspace and fix relevant failures before completion:

- `dotnet restore CodeMetrics.Net.csproj`
- `dotnet build CodeMetrics.Net.csproj --no-restore`
- `dotnet test` when a test project exists
- Run the locally built tool against every changed C# file with single-file JSON output; inspect cyclomatic complexity, cognitive complexity, parameter count, nesting depth, maintainability index, and lines of code.
- Delete unneeded `.codemetrics.*` sidecar reports after inspection.
- For packaging changes, run `dotnet pack CodeMetrics.Net.csproj`, inspect package metadata/content, install the produced tool into an isolated tool path, and smoke-test its help and representative analysis commands.

Never report success without stating which validations ran and which could not run.

## Documentation and Reviews

- Update CLI help, README guidance, examples, package metadata, and release notes whenever their corresponding behavior changes.
- Document syntax-only approximations and unsupported C# constructs honestly.
- Review changes for compatibility, deterministic output, regression coverage, failure behavior, offline use, path disclosure, dependency risk, and maintainability.
- Keep changes narrowly scoped. Do not combine unrelated refactoring with contract changes.
