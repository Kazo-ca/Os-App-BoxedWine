using System.Text;
using BoxedWine.Console;
using BoxedWine.Lib;
using OsX86.Interface;

namespace BoxedWine;

/// <summary>Where a <see cref="ProgramSession"/> stands.</summary>
internal enum SessionState
{
    /// <summary>Nothing asked yet.</summary>
    Idle,

    /// <summary>The file is read and Os-x86 reads its type.</summary>
    Preparing,

    /// <summary>Os-x86 prepares the VM, the prefix and the program (it can download the image the first time).</summary>
    Starting,

    /// <summary>The program runs; its console is open.</summary>
    Running,

    /// <summary>The program ended; <see cref="ProgramSession.ExitCode"/> is known.</summary>
    Exited,

    /// <summary>The program cannot run (yet), or launching it failed; <see cref="ProgramSession.Failure"/> says why.</summary>
    Failed,
}

/// <summary>Why a program did not run.</summary>
internal enum FailureKind
{
    /// <summary>The file could not be read (its code in the detail).</summary>
    ReadFailed,

    /// <summary>The file is larger than what Os-x86 copies into a prefix.</summary>
    TooLarge,

    /// <summary>Os-x86 is not installed.</summary>
    X86Missing,

    /// <summary>BoxedWine may not run programs (the permission <c>x86.programs.launch</c> was refused).</summary>
    PermissionDenied,

    /// <summary>The program cannot run yet (<see cref="ProgramSession.Program"/> says what it is).</summary>
    NotRunnable,

    /// <summary>Starting the VM would exceed the memory budget of the machine.</summary>
    MemoryBudget,

    /// <summary>Os-x86 refused or failed, with its code.</summary>
    X86Error,

    /// <summary>The VM stopped under the program.</summary>
    Lost,
}

/// <summary>Why a program did not run, with the code and the English detail of the refusal.</summary>
/// <param name="Kind">The kind of failure.</param>
/// <param name="Code">The stable code (Os-x86's, BwLib's or the file system's), or <see langword="null"/>.</param>
/// <param name="Detail">The English detail, shown under the localized message.</param>
internal sealed record SessionFailure(FailureKind Kind, string? Code, string Detail);

/// <summary>
/// One program run by a window of BoxedWine (decision 184 A: a new instance per launch): the file
/// read, its type read by Os-x86, the launch by <see cref="IBwLib"/> (so by <see cref="Ix86"/>), the
/// output relayed to a <see cref="ConsoleTranscript"/>, the lines typed, Ctrl+C, and the exit code.
/// Disposing the session closes the console, which ends the program (decisions 147, 208 A).
/// </summary>
internal sealed class ProgramSession(IBwLib bwLib, IProgramFiles files) : IAsyncDisposable
{
    private readonly CancellationTokenSource _stop = new();
    private IVmConsole? _console;
    private Task? _pump;
    private bool _disposed;

    /// <summary>Raised after every change of state or of the transcript (on any thread).</summary>
    public event Action? Changed;

    /// <summary>Where the session stands.</summary>
    public SessionState State { get; private set; }

    /// <summary>The program, once Os-x86 has read its type.</summary>
    public BwProgram? Program { get; private set; }

    /// <summary>The file name, once known.</summary>
    public string? FileName { get; private set; }

    /// <summary>Why the program did not run, in <see cref="SessionState.Failed"/>.</summary>
    public SessionFailure? Failure { get; private set; }

    /// <summary>The program's exit code, in <see cref="SessionState.Exited"/>.</summary>
    public int? ExitCode { get; private set; }

    /// <summary>What the program wrote.</summary>
    public ConsoleTranscript Transcript { get; } = new();

    /// <summary>Whether lines can be sent to the program.</summary>
    public bool AcceptsInput => State == SessionState.Running;

    /// <summary>Reads, checks and launches a program, then relays its console until it ends.</summary>
    /// <param name="path">The file, as the OS passed it.</param>
    /// <param name="arguments">The arguments after the file.</param>
    /// <param name="columns">The width of the console.</param>
    /// <param name="rows">The height of the console.</param>
    /// <param name="cancellationToken">Cancels the preparation.</param>
    /// <exception cref="InvalidOperationException">The session already started.</exception>
    public async Task StartAsync(Kazo.Os.App.Abstractions.VirtualPath path, IReadOnlyList<string> arguments, int columns, int rows, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (State != SessionState.Idle)
        {
            throw new InvalidOperationException("A session runs one program.");
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stop.Token);
        SetState(SessionState.Preparing);
        try
        {
            ProgramFile file = await files.ReadAsync(path, linked.Token).ConfigureAwait(false);
            FileName = file.Name;
            if (file.Content is null)
            {
                Fail(file.Error == ProgramFiles.TooLarge ? FailureKind.TooLarge : FailureKind.ReadFailed, file.Error, path.Value);
                return;
            }

            Program = await bwLib.InspectAsync(file.Name, file.Content, linked.Token).ConfigureAwait(false);
            if (!Program.CanRun)
            {
                Fail(FailureKind.NotRunnable, BwErrorCodes.NotRunnable, Program.Detection.Detail);
                return;
            }

            SetState(SessionState.Starting);
            _console = await bwLib.LaunchAsync(
                new BwLaunch { FileName = file.Name, Content = file.Content, Arguments = arguments, Columns = columns, Rows = rows },
                linked.Token).ConfigureAwait(false);
            SetState(SessionState.Running);
            _pump = PumpAsync(_console, _stop.Token);
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested)
        {
            // The window closed while the program was being prepared.
        }
        catch (Exception exception) when (Describe(exception) is { } failure)
        {
            Failure = failure;
            SetState(SessionState.Failed);
        }
    }

    /// <summary>Sends a line typed by the user, ended by a carriage return (how the console of Wine reads Enter).</summary>
    /// <param name="line">The line, without its end.</param>
    public async ValueTask SendLineAsync(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (_console is { } console && AcceptsInput)
        {
            await Guard(console.WriteAsync(Encoding.UTF8.GetBytes(line + "\r"), _stop.Token)).ConfigureAwait(false);
        }
    }

    /// <summary>Sends Ctrl+C to the program (decision 216 A).</summary>
    public async ValueTask InterruptAsync()
    {
        if (_console is { } console && AcceptsInput)
        {
            await Guard(console.SendInterruptAsync(_stop.Token)).ConfigureAwait(false);
        }
    }

    /// <summary>Closes the console, which ends the program and releases the VM.</summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _stop.CancelAsync().ConfigureAwait(false);
        if (_console is { } console)
        {
            await console.DisposeAsync().ConfigureAwait(false);
        }

        if (_pump is { } pump)
        {
            await pump.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }

        _stop.Dispose();
    }

    /// <summary>The failure an exception of a launch means, or <see langword="null"/> for a bug (rethrown).</summary>
    internal static SessionFailure? Describe(Exception exception) => exception switch
    {
        BwException { Code: BwErrorCodes.X86Missing } e => new(FailureKind.X86Missing, e.Code, e.Message),
        BwException { Code: BwErrorCodes.TooLarge } e => new(FailureKind.TooLarge, e.Code, e.Message),
        BwException e => new(FailureKind.NotRunnable, e.Code, e.Message),
        VmMemoryBudgetExceededException e => new(FailureKind.MemoryBudget, e.Code, e.Message),
        X86Exception { Code: X86ErrorCodes.ProgramNotSupported } e => new(FailureKind.NotRunnable, e.Code, e.Message),
        X86Exception { Code: X86ErrorCodes.AgentLost } e => new(FailureKind.Lost, e.Code, e.Message),
        X86Exception e => new(FailureKind.X86Error, e.Code, e.Message),
        UnauthorizedAccessException e => new(FailureKind.PermissionDenied, null, e.Message),
        _ => null,
    };

    private async Task PumpAsync(IVmConsole console, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (ReadOnlyMemory<byte> chunk in console.ReadOutputAsync(cancellationToken).ConfigureAwait(false))
            {
                Transcript.Append(chunk.Span);
                Changed?.Invoke();
            }

            ExitCode = await console.Exited.WaitAsync(cancellationToken).ConfigureAwait(false);
            SetState(SessionState.Exited);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The window closed: the console is disposed with it.
        }
        catch (Exception exception) when (Describe(exception) is { } failure)
        {
            Failure = failure with { Kind = failure.Kind == FailureKind.X86Error ? FailureKind.Lost : failure.Kind };
            SetState(SessionState.Failed);
        }
    }

    private async ValueTask Guard(ValueTask operation)
    {
        try
        {
            await operation.ConfigureAwait(false);
        }
        catch (X86Exception exception) when (exception.Code == X86ErrorCodes.ConsoleClosed)
        {
            // The program ended between the key and its delivery: the pump reports the exit code.
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested)
        {
        }
    }

    private void Fail(FailureKind kind, string? code, string detail)
    {
        Failure = new SessionFailure(kind, code, detail);
        SetState(SessionState.Failed);
    }

    private void SetState(SessionState state)
    {
        State = state;
        Changed?.Invoke();
    }
}
