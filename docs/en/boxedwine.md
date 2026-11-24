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
- `unit` tests (84): classification of programs, launch through a simulated `Ix86` (name, bytes,
  arguments, no VM nor prefix imposed, console size bounded), refusal before anything starts, Os-x86
  missing, console (UTF-8 cut, carriage return, sequences of Wine, endless title, markup kept as text,
  bounds), session (output, exit code, console released when the program ends, line and Ctrl+C,
  window closed during the start, closing ends the program, VM lost, each failure of Os-x86), bounded
  read of the file, window (tutorial before the program, program chosen during the tutorial, a second
  program closes the first, texts), **the shipped manifest read by the real catalog and file
  association resolver of Kazo 3.5.2**: the five extensions go to BoxedWine, the only candidate,
  Os-x86 declares none, BoxedWine left out without Os-x86. Line coverage: 81.6 % (BoxedWine.Lib 100 %).
- **End-to-end run in a real Kazo 3.5.2** (Release dev host with the Os-x86 Feature of `j0/base` and
  BoxedWine, Wine pack 0.1.0 of work item 10 served locally, Chrome, guest session, Kazo in French),
  with the dependency declared optional **for the run only** (see the second defect of Kazo below):
  - `setup.exe` (the `osx86-hello.exe` of Os-x86) dropped in `/home/guest`; **double-click** in the
    File Explorer → BoxedWine opens, tutorial first (three pages), then `GetServiceAsync<Ix86>()`
    returns Os-x86, detection gives a console program, Os-x86 makes the `windows` VM, downloads the
    pack, starts Wine: the window shows `osx86-hello windows=C:\windows user=osx86-default args=0` and
    "setup.exe ended with code 7" (about 70 s, download included);
  - `xdg-open /home/guest/setup.exe` in the Terminal → BoxedWine through the association;
    `xdg-mime query filetype setup.exe` → `application/vnd.microsoft.portable-executable`;
  - `boxedwine /home/guest/setup.exe --read` → a second window (one per launch), without the tutorial
    (the per-user setting is written), arguments passed, a typed line reaches the program
    (`osx86-hello read bonjour Kazo`, code 0); the Terminal waits for the window to close and returns 0;
  - `./setup.exe` → "Permission denied", 126 (no route of the shell, see below);
  - with every BoxedWine window closed, `x86 vm list` shows the `windows` VM stopped.

  This run found two defects, fixed in the pull request: the console of a finished program stayed open
  as long as its window did, and kept the VM running; the French texts used "tu" where Kazo uses
  "vous".

## Not verified, and limits

- **Two defects of Kazo keep BoxedWine from working as is** (question 321), in 3.5.2,
  3.6.0-preview.1, `main` and `feature/base`:
  1. the manifest generator drops the dependencies before it checks the permissions
     (`AppDefinitionInspector.cs`, `CheckEntryPointKinds`) and refuses the permission of a Feature in
     an app (KOSAPP0009); workaround in this repository: `build/FeaturePermissions.targets` adds
     `x86.programs.launch` to `manifest.json`;
  2. at launch, the lazy registry builds its discovery catalog with the manifest alone
     (`BuildKnownLazyAppDescriptorRegistry.cs`, `DiscoveryManifestsOf`): an app with a **required**
     dependency on a Feature is refused ("Required dependency 'ca.kazo.x86' is missing"), on a
     double-click as from the menu, built into the host or installed from a package (same registry).
     No workaround in this repository: the dependency stays required (ADR 0248); the run above made it
     optional locally.
- **The route of the shell**: `./setup.exe` typed in the Terminal does not reach BoxedWine yet. Kazo
  3.5.2 does not ship the code of ADR 0251: the shell refuses the file ("Permission denied", 126, or
  "cannot execute binary file" with the execute bit). BoxedWine already declares the `execute`
  action; the demonstration "`./setup.exe` in the terminal reaches BoxedWine" waits for that version
  of Kazo. Meanwhile: `xdg-open ./setup.exe` in the Terminal, or a double-click.
- **`boxedwine setup.exe`** (163 A): the Terminal waits for the window to close, but the console is in
  the window, not in the Terminal (question 325); the code returned to the shell is the window's (0, 1
  or 130), not the program's (limit of ADR 0251 §2, 110).
- **Ctrl+C**: in the run, the button (the byte 0x03, as `x86 run` sends it) did not stop
  `osx86-hello --wait` in two tries; closing the window did. The pack tried carries an older agent
  (protocol 2) than `j0/base`; to redo with an up-to-date pack and to compare with `x86 run`.
- **First typed line lost**: the first line sent right after "reading" was lost, the second arrived;
  a known limit of the console of Wine in line mode (Os-x86, work item 10).
- **Line-mode console**: no VT emulator (question 322); full-screen programs do not show correctly.
- **Only console programs run** in milestone 0 (`Ix86.RunProgramAsync`); the rest is refused with its
  message.
- Firefox, Safari and mobile are not tried; the dev host of the run is not in the repository (it only
  works with the optional dependency until Kazo is fixed).
