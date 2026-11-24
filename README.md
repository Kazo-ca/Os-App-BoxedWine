> [Version française](readme-fr.md)

# BoxedWine

BoxedWine is the [Kazo OS](https://github.com/Kazo-ca/Os) app that opens Windows and DOS programs
(`.exe`, `.com`, `.bat`, `.cmd`, `.msi`). It is the interface of
[Os-x86](https://github.com/Kazo-ca/Os-x86), the Kazo OS feature that runs them in x86 virtual
machines inside the browser: double-click a program, BoxedWine receives it and asks Os-x86 to launch
it through `Ix86`, and shows its console.

It keeps the name of the Boxedwine project but none of its code: Windows programs run with Wine
inside a Linux guest of Os-x86, never with the Boxedwine emulator. Licensed under the MIT license.

- **Milestone 0** (this version): console programs and batch files, with their console in the window;
  a tutorial at the first launch. Windowed programs, installers, the library and shortcuts come next.
- Documentation: [docs/en](docs/en/README.md) ([what is delivered, verified and not](docs/en/boxedwine.md)).

## Build

```text
git clone --recurse-submodules https://github.com/Kazo-ca/Os-App-BoxedWine.git
dotnet build BoxedWine.slnx -c Release
dotnet test BoxedWine.slnx -c Release --no-build
```

`external/KazoOs` is Kazo OS on a release tag (it gives the SDK version); `external/Os-x86` is
Os-x86, whose `OsX86.Interface` project gives `Ix86`. Debug builds Kazo OS from the submodule's
sources; Release uses the packages from nuget.org.

## Layout

| Path | Content |
|---|---|
| `src/BoxedWine/` | the Kazo app `ca.kazo.boxedwine` |
| `src/BoxedWine.Lib/` | `BoxedWine.Lib`: `IBwLib` and its implementation on `Ix86` |
| `tests/BoxedWine.Tests/` | unit tests (`Category=unit`) |
| `build/` | build integration (temporary workaround of question 321) |
| `docs/fr`, `docs/en` | documentation, features, changelog |
