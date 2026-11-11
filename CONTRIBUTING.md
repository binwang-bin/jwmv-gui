# Contributing to jwmv

Thanks for your interest in contributing to **jwmv** — a Windows-first CLI for managing Java versions, inspired by `nvm` and SDKMAN. Whether you're fixing a bug, improving documentation, or proposing a new feature, your help is appreciated.

This document describes the tooling, conventions, and workflow we use. Please read it before opening a pull request.

## Code of Conduct

Participation in this project is governed by our [Code of Conduct](./CODE_OF_CONDUCT.md). By contributing, you agree to uphold it. Please report unacceptable behavior to **stescobedo.31@gmail.com**.

## Requirements

To build and test jwmv locally you will need:

- **Windows 10, Windows 11, or Windows Server 2022** (x64 or arm64). jwmv is a Windows-only tool; most changes cannot be validated on other platforms.
- **.NET 8 SDK** — install from [https://dotnet.microsoft.com/download/dotnet/8.0](https://dotnet.microsoft.com/download/dotnet/8.0) or via `winget install Microsoft.DotNet.SDK.8`.
- **Git** and a GitHub account.
- **PowerShell 5.1** or **PowerShell 7+** (to exercise the shell integration paths).

Optional but recommended:

- An editor with OmniSharp or the C# Dev Kit (VS Code, Rider, or Visual Studio 2022).

## Getting the code

```powershell
git clone https://github.com/stescobedo92/jwmv.git
cd jwmv
dotnet restore
dotnet build
```

## Running tests

All tests live under `tests/Jwmv.Tests`. Run the full suite with:

```powershell
dotnet test
```

If you're iterating on a single project, you can scope the run:

```powershell
dotnet test tests/Jwmv.Tests/Jwmv.Tests.csproj
```

Please add tests for any new behavior or bug fix. A change without a regression test is much harder to review and accept.

## Formatting and style

We rely on `dotnet format` to keep the codebase consistent. Before opening a PR, run:

```powershell
dotnet format
```

CI will fail if the tree is not formatted. The project follows standard .NET naming and style conventions (PascalCase for types/members, camelCase for locals, `_camelCase` for private fields).

## Commit messages

This project uses [**Conventional Commits**](https://www.conventionalcommits.org/en/v1.0.0/). The commit subject must follow this shape:

```
<type>(<optional scope>): <short summary>
```

Common types:

- `feat` — a new user-facing capability
- `fix` — a bug fix
- `docs` — documentation only
- `refactor` — internal change with no behavior shift
- `test` — adding or adjusting tests
- `chore` — build, CI, tooling, dependencies
- `perf` — performance improvement
- `ci` — CI configuration changes

Examples:

```
feat(cli): add `jwmv installed --json` output
fix(integrate): handle missing PowerShell profile directory
docs(readme): document .jwmvrc lookup order
```

Breaking changes should append a `!` after the type/scope and include a `BREAKING CHANGE:` footer.

## Pull request process

1. **Open an issue first** for non-trivial changes so we can align on scope before you invest time.
2. **Fork** the repository and create a topic branch from `main`:
   ```powershell
   git checkout -b feat/my-change
   ```
3. Make your changes, with commits that follow Conventional Commits.
4. Run `dotnet format` and `dotnet test` locally — both must pass.
5. Update the **[CHANGELOG.md](./CHANGELOG.md)** under the `## [Unreleased]` section.
6. Update documentation (README, command help text) if user-visible behavior changed.
7. Push your branch and open a pull request against `main`. Fill in the PR template.
8. Be responsive to review feedback — small, focused PRs merge fastest.

A maintainer will review your PR, request changes if needed, and merge once CI is green.

## Reporting bugs

Open a [bug report](https://github.com/stescobedo92/jwmv/issues/new?template=bug_report.yml) and include:

- The output of `jwmv --version`.
- Your Windows version and architecture (x64 / arm64).
- Your shell (PowerShell 5.1, PowerShell 7+, or other).
- Minimal steps to reproduce.
- The output of `jwmv doctor` — this captures most of the environment we need.

## Requesting features

Open a [feature request](https://github.com/stescobedo92/jwmv/issues/new?template=feature_request.yml) describing the **problem** you're trying to solve, not just a proposed implementation. The more concrete the use case, the better we can evaluate and design the change.

## Security issues

Please do **not** open a public issue for security vulnerabilities. Follow the process described in [SECURITY.md](./SECURITY.md) instead.

## License

By contributing, you agree that your contributions will be licensed under the [MIT License](./LICENSE) that covers this project.
