using OsX86.Interface;

namespace BoxedWine.Lib;

/// <summary>
/// The implementation of <see cref="IBwLib"/> on <see cref="Ix86"/>. It asks for the service at
/// each call (ADR 0262 of Kazo: the instance is not kept beyond its immediate use), so Os-x86
/// installed while BoxedWine runs is found, and its absence is a clear error, never a crash.
/// </summary>
public sealed class BwLib : IBwLib
{
    /// <summary>How many first bytes of a file give every detail of its type (Os-x86 reads 4 KiB).</summary>
    public const int HeaderLength = 4096;

    private static readonly BwFileType[] Types =
    [
        new(".exe", ProgramMimeTypes.PortableExecutable, Execute: true),
        new(".com", ProgramMimeTypes.DosExecutable, Execute: false),
        new(".bat", ProgramMimeTypes.Batch, Execute: true),
        new(".cmd", ProgramMimeTypes.Batch, Execute: true),
        new(".msi", ProgramMimeTypes.WindowsInstaller, Execute: true),
    ];

    private readonly Func<CancellationToken, ValueTask<Ix86?>> _x86;

    /// <summary>Creates the library on a way to get Os-x86.</summary>
    /// <param name="x86">
    /// Gets the service, or <see langword="null"/> when Os-x86 is not installed; in a Kazo app,
    /// <c>ct =&gt; KazoFeatures.GetServiceAsync&lt;Ix86&gt;(ct)</c>.
    /// </param>
    public BwLib(Func<CancellationToken, ValueTask<Ix86?>> x86)
    {
        ArgumentNullException.ThrowIfNull(x86);
        _x86 = x86;
    }

    /// <summary>The files BoxedWine opens, without an instance (its manifest declares them).</summary>
    public static IReadOnlyList<BwFileType> HandledFileTypes => Types;

    /// <inheritdoc />
    public IReadOnlyList<BwFileType> FileTypes => Types;

    /// <summary>Whether a file name has one of the extensions BoxedWine opens.</summary>
    /// <param name="fileName">A file name or a path.</param>
    public static bool IsHandled(string? fileName) =>
        fileName is not null && Types.Any(type => fileName.EndsWith(type.Extension, StringComparison.OrdinalIgnoreCase));

    /// <inheritdoc />
    public async ValueTask<BwProgram> InspectAsync(string fileName, ReadOnlyMemory<byte> header, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        Ix86 x86 = await RequireAsync(cancellationToken).ConfigureAwait(false);
        string name = SafeFileName(fileName);
        ReadOnlyMemory<byte> head = header.Length > HeaderLength ? header[..HeaderLength] : header;
        ProgramDetection detection = await x86.DetectProgramAsync(head, name, cancellationToken).ConfigureAwait(false);
        return new BwProgram(name, detection, Classify(detection));
    }

    /// <inheritdoc />
    public async ValueTask<IVmConsole> LaunchAsync(BwLaunch launch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(launch);
        ArgumentException.ThrowIfNullOrWhiteSpace(launch.FileName);
        ArgumentNullException.ThrowIfNull(launch.Arguments);
        if (launch.Content.Length > ProgramRunOptions.MaxContentBytes)
        {
            throw new BwException(BwErrorCodes.TooLarge, $"The program is larger than the {ProgramRunOptions.MaxContentBytes / (1024 * 1024)} MiB Os-x86 copies into a prefix.");
        }

        BwProgram program = await InspectAsync(launch.FileName, launch.Content, cancellationToken).ConfigureAwait(false);
        if (!program.CanRun)
        {
            throw new BwException(BwErrorCodes.NotRunnable, $"{program.FileName} cannot run yet: {program.Support} ({program.Detection.Detail}).", program);
        }

        // Os-x86 checks again and stays the authority (decision 193 A): BoxedWine names neither the
        // VM nor the prefix, Os-x86 chooses them for the user that calls.
        Ix86 x86 = await RequireAsync(cancellationToken).ConfigureAwait(false);
        return await x86.RunProgramAsync(
            new ProgramRunOptions
            {
                FileName = program.FileName,
                Content = launch.Content,
                Arguments = [.. launch.Arguments],
                Columns = Math.Clamp(launch.Columns, 1, 1000),
                Rows = Math.Clamp(launch.Rows, 1, 1000),
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// What BoxedWine can do with a program now. The same rules as <c>x86 run</c> of Os-x86
    /// (<c>VmManager.WhyNotSupported</c>), which still checks on its side.
    /// </summary>
    /// <param name="detection">What Os-x86 read in the file.</param>
    public static BwSupport Classify(ProgramDetection detection)
    {
        ArgumentNullException.ThrowIfNull(detection);
        return detection switch
        {
            { Source: ProgramDetectionSource.Conflict } => BwSupport.Mismatch,
            { Kind: ProgramKind.Unknown } => BwSupport.Unknown,
            { Kind: ProgramKind.Linux } => BwSupport.Linux,
            { Kind: ProgramKind.Dos } => BwSupport.Dos,
            { Kind: ProgramKind.Batch } => BwSupport.Batch,
            { Kind: ProgramKind.WindowsInstaller } => BwSupport.Installer,
            { Kind: ProgramKind.Windows, IsLibrary: true } => BwSupport.Library,
            { Kind: ProgramKind.Windows, Bits: 64 } => BwSupport.Windows64,
            { Kind: ProgramKind.Windows, Bits: 16 } => BwSupport.Windows16,
            { Kind: ProgramKind.Windows, Subsystem: ProgramSubsystem.Windowed } => BwSupport.Windowed,
            { Kind: ProgramKind.Windows, Subsystem: ProgramSubsystem.Other } => BwSupport.OtherWindows,
            { Kind: ProgramKind.Windows, IsComplete: true } when !detection.Detail.Contains("i386", StringComparison.Ordinal) => BwSupport.OtherWindows,
            { Kind: ProgramKind.Windows, Subsystem: ProgramSubsystem.Console } => BwSupport.Console,
            _ => BwSupport.Unknown,
        };
    }

    /// <summary>The last part of a path, as Os-x86 names the copy of the program (<c>/a/b/setup.exe</c> → <c>setup.exe</c>).</summary>
    /// <param name="fileName">A file name or a path, with <c>/</c> or <c>\</c>.</param>
    internal static string SafeFileName(string fileName)
    {
        string name = fileName.Trim();
        int slash = name.LastIndexOfAny(['/', '\\']);
        name = slash >= 0 ? name[(slash + 1)..] : name;
        return name.Length == 0 ? throw new ArgumentException("The file name has no last part.", nameof(fileName)) : name;
    }

    private async ValueTask<Ix86> RequireAsync(CancellationToken cancellationToken) =>
        await _x86(cancellationToken).ConfigureAwait(false)
        ?? throw new BwException(BwErrorCodes.X86Missing, "Os-x86 is not installed: BoxedWine runs programs through it.");
}
