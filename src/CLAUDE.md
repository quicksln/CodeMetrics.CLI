# CodeMetrics.Net — CLAUDE.md

Follow the project constitution in [.specify/memory/constitution.md](.specify/memory/constitution.md). If guidance conflicts, the constitution takes precedence.

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
- Keep normal members at cyclomatic complexity <= 7, cognitive complexity <= 15, and parameter count <= 6. Exceptions must be recorded in the implementation plan with measured evidence, rationale, and a reduction plan.
- Avoid speculative abstractions, hidden global state, broad exception handling, and clever code that obscures metric semantics.
- Keep output deterministic despite parallel analysis. Define stable ordering and tie-breakers explicitly; use culture-invariant formatting. Parallel execution may improve performance but must not alter observable results.
- Preserve cancellation and async behavior where relevant. Use asynchronous file APIs in asynchronous flows.
- Validate external input at the boundary and produce actionable errors without leaking unnecessary absolute paths or environment details.
- Comments should explain non-obvious intent, metric rationale, compatibility constraints, or trade-offs — not restate code.
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

## Constitutional Principles (summary)

The full rationale lives in [.specify/memory/constitution.md](.specify/memory/constitution.md); this is a quick-reference summary.

1. **Stable CLI and Output Contracts** — command surface, exit codes, stream routing, sidecar naming, and report schemas are public API; breaking changes require a major version, migration guidance, release notes, and contract tests.
2. **Deterministic, Invariant Analysis** — identical source/options/tool version must yield byte-stable, culture-invariant output regardless of parallelism.
3. **Evidence-Based Correctness (NON-NEGOTIABLE)** — test-first for every behavior change; build success alone is not evidence of correctness.
4. **Syntax-Only Scope and Honest Semantics** — no MSBuild project loading, target restore, or semantic compilation without an approved constitution amendment; approximations and unsupported constructs must be documented.
5. **Maintainable, Focused .NET Design** — nullable-enabled, no new warnings, clear component boundaries, and the complexity/parameter limits above.

Every feature plan and code review must record constitution compliance. Non-compliance blocks merge unless the same review contains a time-bounded, maintainer-approved exception with owner, rationale, risk, and remediation date.
