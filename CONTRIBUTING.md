# Contributing to DatPlotX

Thanks for your interest in improving DatPlotX! This guide covers how to get set
up and the conventions we follow.

## Licensing of contributions

DatPlotX is released under the **MIT License**, and there is **no CLA to sign**.
By submitting a pull request, you agree that your contribution is licensed under
the same MIT terms as the rest of the project (inbound = outbound). That's it —
no copyright assignment, no sign-off ceremony.

If your employer owns the rights to your work, make sure you are authorized to
contribute it under the MIT License.

## Prerequisites

- **.NET 10 SDK** — <https://dotnet.microsoft.com/download>
- Git
- An editor with EditorConfig support (VS / VS Code / Rider all work). The repo
  ships an [`.editorconfig`](.editorconfig) — please keep it enabled.

## Build, run, and test

```bash
# Build the active app
dotnet build DatPlotX/DatPlotX.csproj

# Run it
dotnet run --project DatPlotX

# Run the test suite
dotnet test DatPlotX.Tests/DatPlotX.Tests.csproj

# Verify formatting before you push (CI enforces this)
dotnet format --verify-no-changes
```

## Coding conventions

DatPlotX is an Avalonia + .NET 10 MVVM application. Please match the existing style:

- **Strict MVVM.** Code-behind is for view wiring only — no business logic in views.
  ViewModels never reference Views directly; bridge with events and services.
- **Compiled bindings.** Prefer Avalonia compiled bindings (`x:DataType`) over
  reflection bindings.
- **Async all the way.** Use `async`/`await` for I/O. No `.Result`, no `.Wait()`,
  no `async void` outside event handlers.
- **Nullable reference types are enabled** repo-wide. Keep new code warning-clean.
- **Culture-aware parsing.** Always pass an explicit `IFormatProvider`
  (`options.Culture`) — never rely on the current culture.
- **Security.** Route all file paths through `FilePathValidator` and all column
  names through `InputValidator`. Never introduce `BinaryFormatter` or other unsafe
  serializers.
- Follow [`.editorconfig`](.editorconfig) for formatting; run `dotnet format`.

The authoritative deep-dive on architecture, data flow, and the trickier Avalonia
quirks (DataGrid rebuild path, the three plot modes, analysis wiring) lives in
[`DatPlotX/CLAUDE.md`](DatPlotX/CLAUDE.md). Read it before making non-trivial
changes inside `DatPlotX/`.

## Pull request checklist

1. Branch from `main`.
2. Keep PRs focused — one logical change per PR.
3. Add or update tests in `DatPlotX.Tests/` for behavior changes.
4. Ensure `dotnet build`, `dotnet test`, and `dotnet format --verify-no-changes`
   all pass locally (CI runs them on Windows, macOS, and Linux).
5. Update [`CHANGELOG.md`](CHANGELOG.md) if your change is user-visible.

## Reporting bugs and asking questions

- **Bugs / feature requests:** open a GitHub [Issue](https://github.com/aeroperf/DatPlotX/issues).
- **Usage questions:** use [Discussions](https://github.com/aeroperf/DatPlotX/discussions),
  not Issues.
- **Security vulnerabilities:** do **not** open a public issue — see
  [SECURITY.md](SECURITY.md).

By contributing, you agree that your contributions will be licensed under the
project's MIT License.
