---
name: release
description: Bump version, update changelog and What's New, build, test, commit, and push a new release of DatPlotX.
---

# Release Skill

Default version increment: **patch (0.0.1)**. If the user specifies a different increment (minor, major, or an explicit version), use that instead.

1. **Stage uncommitted changes first**: if there are unstaged/uncommitted changes, stage and commit them before proceeding.
2. **Gather all changes since last version bump**: run `git log --oneline <last-bump-commit>..HEAD` to find the commit that bumped the version (message contains "v{x.y.z}:", or "release: bump to {x.y.z}" on older releases). All commits after that one are included in this release.
3. **Bump version** in `Directory.Build.props` (`<Version>`). This is the single source of version truth — no other file hardcodes it.
4. **Update CHANGELOG.md** with a section for the new version (`## x.y.z — D Month YYYY`), grouped under the same kind of subheadings the existing entries use (Data safety, Plot correctness, Editing & dialogs, …). Include a bullet for every meaningful change from step 2. Fold any existing `## Unreleased` section into the new version section rather than leaving both.
4b. **Update What's New** in `DatPlotX/Content/AppChangelogContent.cs`: add a new `ChangelogEntry` at the top of the array with user-facing descriptions of the changes. Format: `new("x.y.z", "D Month YYYY", [ "...", ])`. Write these for users, not developers — no type or method names.
4c. **Update the in-app user guide** in `DatPlotX/Content/AppHelpContent.cs` when the release adds, removes, or changes a user-visible feature (menu items, dialogs, tabs, shortcuts, gestures). There is no separate user-guide document — this file is the guide. Search it for every place the affected feature is described; features are usually documented in more than one section (per-mode sections, right-click menu lists, and the shortcut tables).
5. **Build and test**:
   ```bash
   dotnet build DatPlotX/DatPlotX.csproj
   dotnet test DatPlotX.Tests/DatPlotX.Tests.csproj
   ```
   There is no `.slnx` / `.sln` in this repo — build the csproj directly. Do not modify `DatPlot.Modern/` (frozen legacy WPF app).
6. **Commit** with message `v{version}: {summary}`.
7. **Push** to origin. Releases land on `main`.
8. **Report** the version, the commit hash, and what was pushed. The GitHub Actions release workflow builds the platform artifacts on a version tag — mention whether a tag was pushed, and only create one if the user asks.
