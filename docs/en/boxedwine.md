> [Version française](../fr/boxedwine.md)

# BoxedWine (work item 13, milestone 0)

BoxedWine (`ca.kazo.boxedwine`, decision 222 A) is the Kazo OS app that opens Windows and DOS
programs. It is the interface of [Os-x86](https://github.com/Kazo-ca/Os-x86), the Feature
`ca.kazo.x86` that runs them (decisions 40 and 49 A: "Os-x86 renders, BoxedWine shows"). Os-x86
registers no file association (64): BoxedWine receives the files, and Os-x86 decides the virtual
machine, the prefix and the permissions (193 A).

This page describes what work item 13 delivers (line 13 of the milestone 0 execution plan), what is
verified and what is not.

## What is delivered

| Piece | Where | Role |
|---|---|---|
| Kazo app `ca.kazo.boxedwine` | `src/BoxedWine/` | window, associations, launch, console, tutorial |
| `BoxedWine.Lib` (`IBwLib`, `BwLib`) | `src/BoxedWine.Lib/` | client-side Wine logic: what a file is, what can run, launch through `Ix86`; depends on `Ix86` only |
| Contracts of Os-x86 | submodule `external/Os-x86` | `OsX86.Interface` (`Ix86`), a project reference while Os-x86 publishes no package (question 320) |
| Kazo OS 3.5.2 | submodule `external/KazoOs` | SDK version, Debug (sources) and Release (packages) profiles of the `kazoos-solution` layout |
| Permission workaround | `build/FeaturePermissions.targets` | adds `x86.programs.launch` to the generated `manifest.json` (question 321) |

### The manifest

- Kind `Window`, not single instance (184 A: one window per launch).
- **Required dependency** `ca.kazo.x86` from 0.1.0 (ADR 0248 of Kazo): installing BoxedWine installs
  Os-x86 with one consent; without Os-x86 the catalog leaves BoxedWine out
  (`catalog.dependency.missing`); removing Os-x86 disables BoxedWine.
- **Associations** (`FileHandlers`, media types of ADR 0251 §3):

  | Extensions | Media type | Actions |
  |---|---|---|
  | `.exe` | `application/vnd.microsoft.portable-executable` | `open`, `execute` |
  | `.bat`, `.cmd` | `application/x-bat` | `open`, `execute` |
  | `.msi` | `application/x-msi` | `open`, `execute` |
  | `.com` | `application/x-dosexec` | `open` |

  `open` serves the double-click, "Open with" and `xdg-open`. `execute` is the action of ADR 0251
  (253 A): a path typed in the shell. Kazo 3.5.2 does not read it yet; it takes effect with the route
  of the shell. A pure DOS program typed in the shell goes to the `x86` command, hence `.com`
  without `execute`; on a double-click it comes to BoxedWine.
- **Permissions**: `filesystem.user-home.read` (read the program), `dialogs.file-open` ("Open a
  program…"), `filesystem.private.read` and `.write` (the tutorial setting), `x86.programs.launch`
  (a normal permission declared by Os-x86, required by `Ix86.RunProgramAsync`, ADR 0246).
- **Setting** `tutorial-seen` (boolean, user scope) in `~/.config/ca.kazo.boxedwine/settings.config`.

### The launch

1. Kazo opens BoxedWine with the path of the file as its first argument (double-click, "Open with",
   `xdg-open ./setup.exe`); the following arguments go to the program.
2. BoxedWine reads the file through the app's file system gateway (Kazo checks the permission),
   refusing more than 256 MiB, what Os-x86 copies into a prefix.
3. `IBwLib.InspectAsync` asks Os-x86 the type of the program (`Ix86.DetectProgramAsync`, 4 KiB of
   header; header first, extension next, a contradiction refused: 162 A) and classifies it with the
   rules of `x86 run`.
4. A program that can run (32-bit i386 console PE, .NET included, or a batch file) is launched by
   `IBwLib.LaunchAsync`, so `Ix86.RunProgramAsync`, obtained with
   `KazoFeatures.GetServiceAsync<Ix86>()` at each call (ADR 0262). BoxedWine names neither the VM
   nor the prefix: Os-x86 chooses them for the calling user, makes the `windows` VM when needed,
   starts it, downloads the image the first time.
5. The console of the program shows in the window: output relayed as text (the escape sequences of
   the console of Wine are read, never interpreted as markup), a typed line sent with a carriage
   return, a Ctrl+C button (216 A), the exit code shown at the end (110). **Closing the window closes
   the console, which ends the program** (147) and releases the VM (166 A).

What cannot run yet is refused **before** Os-x86 starts anything, with a message that says what the
file is and what will come: windowed program, `.msi` installer and 16-bit program (display in
milestone 3), DOS program (`x86 run` in the Terminal until BoxedWine shows DOS, 164 A), Linux
program (`x86 shell`), 64-bit (never: v86 is 32-bit), DLL, another processor, unknown file or
content that contradicts its extension. Os-x86 has the last word: a refusal of its own
(`program-not-supported`, memory budget, permission removed, image missing) is shown as is, with
its code.

### The tutorial (decision 71: required)

At the first launch of each user the tutorial opens **before** the program asked for, which waits
until it is closed. Three pages: what BoxedWine is (programs run in a VM of Os-x86, not in Kazo), how
to launch a program (double-click, "Open with", `xdg-open`, the console, Ctrl+C, closing ends it,
first download), and **how to add one's programs oneself**: no automatic detection; the "Add an
application to Kazo OS" menu (executable or `.lnk`, Start menu entry required, desktop shortcut
optional, D57) comes with milestone 1. The "Tutorial" button opens it again. When the setting cannot
be read or written, the tutorial shows again at the next launch; nothing else is lost.

## `IBwLib`

| Member | Role |
|---|---|
| `FileTypes` (and `BwLib.HandledFileTypes`) | the five extensions, their media type, the `execute` action |
| `InspectAsync(name, header)` → `BwProgram` | detection by Os-x86 and `BwSupport` (what can run now) |
| `LaunchAsync(BwLaunch)` → `IVmConsole` | launch through `Ix86.RunProgramAsync`, refusals as `BwException` (`x86-missing`, `not-runnable`, `too-large`) |
| `BwLib.Classify(ProgramDetection)` | the rules of `x86 run`, without a call |

`BwLib` takes a function that returns `Ix86` or null; in a Kazo app,
`ct => KazoFeatures.GetServiceAsync<Ix86>(ct)`. The public API is tracked in
`PublicAPI.Unshipped.txt`. Commands in a prefix, reading `.lnk` files and icons, and the library come
with milestone 1, when `Ix86` has "run in the prefix".

## Verified

- `dotnet build BoxedWine.slnx -c Release -warnaserror` and `-c Debug` (Kazo from the sources of the
  submodule) without warnings.
- `unit` tests (77): classification of programs, launch through a simulated `Ix86` (name, bytes,
  arguments, no VM nor prefix imposed, console size bounded), refusal before anything starts, Os-x86
  missing, console (UTF-8 cut, carriage return, sequences of Wine, markup kept as text, bounds),
  session (output, exit code, line and Ctrl+C, closing ends the program, VM lost, each failure of
  Os-x86), bounded read of the file, window (tutorial before the program, program chosen during the
  tutorial, a second program closes the first, texts), **the shipped manifest read by the real
  catalog and file association resolver of Kazo 3.5.2**: the five extensions go to BoxedWine, the
  only candidate, Os-x86 declares none, BoxedWine left out without Os-x86. Line coverage: 80.8 %
  (BoxedWine.Lib 100 %).

## Not verified, and limits

- **The route of the shell**: `./setup.exe` typed in the Terminal does not reach BoxedWine yet. Kazo
  3.5.2 does not ship the code of ADR 0251: the shell still reads the file as a script and answers
  "cannot execute binary file" (126). BoxedWine already declares the `execute` action; the
  demonstration "`./setup.exe` in the terminal reaches BoxedWine" waits for that version of Kazo.
  Meanwhile: `xdg-open ./setup.exe` in the Terminal, or a double-click.
- **`boxedwine setup.exe`** (163 A): the Terminal waits for the window to close, but the console is in
  the window, not in the Terminal (question 325); the code returned to the shell is the window's (0, 1
  or 130), not the program's (limit of ADR 0251 §2, 110).
- **Line-mode console**: no VT emulator (question 322); full-screen programs do not show correctly.
- **Only console programs run** in milestone 0 (`Ix86.RunProgramAsync`); the rest is refused with its
  message.
- **Defect of the manifest generator of Kazo** (question 321): the permission `x86.programs.launch` is
  added to `manifest.json` by `build/FeaturePermissions.targets`, not by `BoxedWineApp`.
- **An end-to-end run in a real Kazo** (host with Os-x86, Wine image, double-click): see the pull
  request for its status.
- Firefox, Safari and mobile are not tried.
