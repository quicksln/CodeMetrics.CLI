# Contributing to CodeMetrics.Cli

Thank you for your interest in improving **CodeMetrics.Cli** — a .NET global tool
that measures code quality and complexity in C# source files without restoring or
compiling the target project.

Every C# project is different, and its contributors have different workflows.
This project has its own flow, so please read this guide before writing code.
The short version:

1. **`dev` is the development branch.** All pull requests target `dev`.
2. **Open an issue first, then submit a PR based on that issue.**
3. **Need a new feature?** Open an issue, let's talk about it, and let the
   community vote on it before any code is written.

## Code of Conduct

Be respectful, constructive, and patient. Critique the code, not the person.
We aim to keep discussions welcoming to developers of every experience level.

## Branching Model

| Branch | Purpose |
| --- | --- |
| `main` | Stable, released code. Tagged versions ship from here. |
| `dev` | Integration branch for daily development. **Base your work and PRs on `dev`.** |

- Do not commit directly to `main` or `dev`; everything arrives through a PR.


## Open an Issue First

Behavior-changing work, new options, output-format changes, and metric definition
changes must start as an issue. A PR opened without a prior issue may be closed
unreviewed — not because your idea is bad, but because each contributor works
differently and we need to align on the approach before code is written.

Docs-only fixes, typos, and obvious trivial bugs may go straight to a PR
(opening a companion issue is still appreciated).

A good bug report includes:

- The exact `codemetrics` command line and the installed tool version
  (`dotnet tool list -g` shows it, package id `CodeMetrics.Cli`).
- What you expected vs. what actually happened.
- A minimal C# snippet or `.cs` file that reproduces it.
- Machine-readable output if relevant (`--format json` snippet).
- Your OS and .NET SDK version.

## Development Workflow

1. Search [existing issues](https://github.com/quicksln/CodeMetrics.CLI/issues)
   to see whether your idea or bug is already known.
2. Open an issue (see below) and wait for feedback or approval for anything
   bigger than an obvious fix.
3. Branch from `dev`, one branch per issue.
4. Write a failing test first, then implement the smallest correct change.
5. Validate locally (build, tests, and dogfood the tool on your changed files).
6. Update documentation that describes the behavior you changed.
7. Open a PR against `dev`, linking the issue with `Fixes #<number>`.

## New Features: Propose, Discuss, Vote

This is how feature work happens here — because "we all have different
workflows", design agreement comes before implementation:

1. **Propose**: open a *feature request* issue describing the problem and use
   case, not the implementation. One issue per feature.
2. **Discuss**: maintainers and contributors discuss scope, CLI surface, and
   compatibility in the issue thread.
3. **Vote**: react with 👍 / 👎 on the issue — votes are how the community
   prioritizes what gets picked up next (a signal, not an automatic gate).
4. **Approval**: wait for a maintainer to approve the direction (label
   `approved`) before starting the PR. A maintainer may ask for a different
   design or decline the feature; that is a valid outcome.

## Review Process

- A maintainer reviews every PR; expect at least one approval before merge.
- Review focus: correctness, deterministic output, contract stability,
  regression coverage, the complexity budget above, and minimal dependencies.
- Please respond to review comments even when you disagree; silent staleness
  is the main risk in a volunteer project.

## AI-Assisted Contributions

Using AI tools (Copilot, agents, etc.) to write code or tests is welcome.
You are responsible for understanding, reviewing, testing, and debugging every
line you submit, and for stating in the PR description that AI assistance was
used. Never commit credentials, tokens, or private source code from third-party
projects.

## License

This project is licensed under the [MIT License](LICENSE). By contributing,
you agree that your contributions are licensed under the same MIT license.

## Getting Help

- Issues: <https://github.com/quicksln/CodeMetrics.CLI/issues>
- Unsure whether your idea fits? Open a `question` issue and ask before coding.
