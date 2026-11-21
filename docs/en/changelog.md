> [Version française](../fr/changelog.md)

# Changelog

## Unreleased

- Minimal BoxedWine (work item 13 of milestone 0): Kazo app `ca.kazo.boxedwine` 0.1.0 on Kazo 3.5.2,
  required dependency on the Feature `ca.kazo.x86` (ADR 0248), associations `.exe`, `.com`, `.bat`,
  `.cmd` and `.msi` (actions `open` and, except `.com`, `execute` of ADR 0251), launch through
  `Ix86.RunProgramAsync` with the console in the window (output, line, Ctrl+C, exit code), explained
  refusal of what cannot run yet, tutorial at the first launch (per-user setting `tutorial-seen`).
- `BoxedWine.Lib` 0.1.0-alpha: `IBwLib`, `BwLib`, `BwFileType`, `BwProgram`, `BwSupport`, `BwLaunch`,
  `BwException`, `BwErrorCodes`.
- Contracts of Os-x86 through the submodule `external/Os-x86` (question 320); workaround
  `build/FeaturePermissions.targets` for the permission `x86.programs.launch` (question 321).
- License: the repository moves from GPL-2.0 to MIT (decision 47 B); it holds no code of the
  Boxedwine project.
