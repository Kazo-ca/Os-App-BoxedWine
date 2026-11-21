> [Version française](../fr/features.md)

# Features of BoxedWine

| Feature | Work item | Decisions | State |
|---|---|---|---|
| Kazo app `ca.kazo.boxedwine`, required dependency `ca.kazo.x86` (ADR 0248) | 13 | 222 A, 40, 49 A | delivered |
| Associations `.exe`, `.com`, `.bat`, `.cmd`, `.msi` (`open`; `execute` except `.com`) | 13 | 62 B, 64, 253 A | delivered; `execute` waits for the route of the shell of Kazo (ADR 0251) |
| Launch through `Ix86` (`GetServiceAsync<Ix86>()`), VM and prefix chosen by Os-x86 | 13 | 193 A, 162 A | delivered for console programs and batch files |
| Console in the window: output, typed line, Ctrl+C, exit code; closing ends it | 13 | 110, 147, 213 A, 216 A | delivered, in line mode |
| Tutorial at the first launch, can be opened again | 13 | 71 (instructions §13) | delivered |
| `BoxedWine.Lib` (`IBwLib`) | 13 | plan §1.1 | delivered (type, what can run, launch) |
| MIT license, no code of the Boxedwine project | 13 | 47 B | delivered |
| `./setup.exe` typed in the Terminal reaches BoxedWine | 13 | 62 B, 240 D | waits for Kazo (code of ADR 0251) |
| Windowed programs, installers, 16-bit, graphical DOS | M2, M3 | 164 A, 170 | to come |
| "Add an application to Kazo OS", library, shortcuts, icons | M1 | 57 A, 71 | to come |
