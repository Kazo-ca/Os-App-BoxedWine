using OsX86.Interface;

namespace BoxedWine.Lib;

/// <summary>
/// BwLib (plan §1.1 of Os-x86): the Wine logic of BoxedWine on the client side. It depends on
/// <see cref="Ix86"/> only. It never chooses the VM nor the prefix: Os-x86 does (decision 193 A).
/// This first surface (work item 13, milestone 0) tells what a file is, whether Os-x86 can run it
/// yet, and launches it with its console; the commands in a prefix, the shortcuts, the icons and the
/// library come with milestone 1.
/// </summary>
public interface IBwLib
{
    /// <summary>The files BoxedWine opens (plan §1.1): <c>.exe</c>, <c>.com</c>, <c>.bat</c>, <c>.cmd</c> and <c>.msi</c>.</summary>
    IReadOnlyList<BwFileType> FileTypes { get; }

    /// <summary>
    /// Tells what a program is and whether it can run now (decision 162 A, read by Os-x86: the header
    /// first, the extension when the header says nothing, a contradiction refused). Starts nothing.
    /// </summary>
    /// <param name="fileName">The file name, with its extension.</param>
    /// <param name="header">The first bytes of the file; <see cref="BwLib.HeaderLength"/> give every detail.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <exception cref="BwException"><see cref="BwErrorCodes.X86Missing"/>: Os-x86 is not installed.</exception>
    ValueTask<BwProgram> InspectAsync(string fileName, ReadOnlyMemory<byte> header, CancellationToken cancellationToken);

    /// <summary>
    /// Launches a program through Os-x86 (<see cref="Ix86.RunProgramAsync"/>): Os-x86 chooses the VM
    /// and the prefix, starts the VM when needed and runs the program on a pseudo-terminal. The
    /// console carries its output, the keystrokes, Ctrl+C and its exit code; disposing it ends the
    /// program (decision 147).
    /// </summary>
    /// <param name="launch">The program and its arguments.</param>
    /// <param name="cancellationToken">Cancels the wait, not the boot.</param>
    /// <exception cref="BwException"><see cref="BwErrorCodes.X86Missing"/>, or <see cref="BwErrorCodes.NotRunnable"/> with the reason when the program cannot run yet.</exception>
    /// <exception cref="X86Exception">Os-x86 refused or failed (its <see cref="X86Exception.Code"/>).</exception>
    /// <exception cref="UnauthorizedAccessException">The app lacks <c>x86.programs.launch</c>.</exception>
    ValueTask<IVmConsole> LaunchAsync(BwLaunch launch, CancellationToken cancellationToken);
}

/// <summary>A file type BoxedWine opens.</summary>
/// <param name="Extension">The extension, with its dot, in lowercase.</param>
/// <param name="MediaType">Its media type (ADR 0251 of Kazo, §3).</param>
/// <param name="Execute">
/// Whether BoxedWine also takes it when it is typed as a path in the shell (the <c>execute</c> action of
/// ADR 0251): PE, NE, batch files and installers; a pure DOS program goes to the <c>x86</c> command.
/// </param>
public sealed record BwFileType(string Extension, string MediaType, bool Execute);

/// <summary>What BoxedWine can do with a program now.</summary>
public enum BwSupport
{
    /// <summary>A 32-bit console program for i386 (.NET or not): it runs, with its console.</summary>
    Console = 0,

    /// <summary>A batch file of <c>cmd.exe</c>: it runs, with its console.</summary>
    Batch = 1,

    /// <summary>A windowed Windows program: it needs a display (milestone 3).</summary>
    Windowed = 2,

    /// <summary>A Windows Installer package: it needs a display (milestone 3).</summary>
    Installer = 3,

    /// <summary>A 16-bit Windows program: it needs a display (milestone 3).</summary>
    Windows16 = 4,

    /// <summary>A 64-bit Windows program: the emulated processor is 32-bit, it never runs.</summary>
    Windows64 = 5,

    /// <summary>A library (DLL), not a program.</summary>
    Library = 6,

    /// <summary>A Windows program for another processor, or for neither the console nor a window.</summary>
    OtherWindows = 7,

    /// <summary>A DOS program: Os-x86 shows it in the Terminal (<c>x86 run</c>); BoxedWine shows DOS later (decision 164 A).</summary>
    Dos = 8,

    /// <summary>A Linux program: it runs in a Linux VM of Os-x86 (<c>x86 shell</c>).</summary>
    Linux = 9,

    /// <summary>Not a program Os-x86 knows.</summary>
    Unknown = 10,

    /// <summary>The header and the extension disagree: refused, never run (decision 162 A).</summary>
    Mismatch = 11,
}

/// <summary>A program as BoxedWine sees it.</summary>
/// <param name="FileName">The file name.</param>
/// <param name="Detection">What Os-x86 read in it.</param>
/// <param name="Support">What BoxedWine can do with it now.</param>
public sealed record BwProgram(string FileName, ProgramDetection Detection, BwSupport Support)
{
    /// <summary>Whether BoxedWine can launch it now.</summary>
    public bool CanRun => Support is BwSupport.Console or BwSupport.Batch;
}

/// <summary>A program to launch (<see cref="IBwLib.LaunchAsync"/>).</summary>
public sealed record BwLaunch
{
    /// <summary>The file name of the program, with its extension; a path is reduced to its last part.</summary>
    public required string FileName { get; init; }

    /// <summary>The bytes of the program file (<see cref="ProgramRunOptions.MaxContentBytes"/> at most).</summary>
    public required ReadOnlyMemory<byte> Content { get; init; }

    /// <summary>The arguments after the program name, passed as they are (never through a shell).</summary>
    public IReadOnlyList<string> Arguments { get; init; } = [];

    /// <summary>The width of the console in character cells.</summary>
    public int Columns { get; init; } = 80;

    /// <summary>The height of the console in character cells.</summary>
    public int Rows { get; init; } = 25;
}
