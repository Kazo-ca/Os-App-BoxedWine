using Kazo.Os.App.Abstractions;

namespace BoxedWine;

/// <summary>Whether the tutorial was shown to the user (decision 71: required at the first launch).</summary>
internal interface ITutorialStore
{
    /// <summary>Whether the user has seen it; <see langword="false"/> when it cannot be read.</summary>
    ValueTask<bool> SeenAsync();

    /// <summary>Records that the user has seen it; a failure only makes it show again next time.</summary>
    ValueTask MarkSeenAsync();
}

/// <summary>
/// What the window of BoxedWine shows and does, without the markup: the tutorial first at the first
/// launch of each user, the program asked for waiting until it is closed, then one
/// <see cref="ProgramSession"/> at a time (decision 184 A: a new window per launch; a second program
/// opened in the same window replaces the first, whose console is closed).
/// </summary>
internal sealed class WindowController(Func<ProgramSession> newSession, ITutorialStore tutorial) : IAsyncDisposable
{
    /// <summary>The number of pages of the tutorial.</summary>
    public const int TutorialSteps = 3;

    /// <summary>The width of the console of a program.</summary>
    public const int ConsoleColumns = 80;

    /// <summary>The height of the console of a program.</summary>
    public const int ConsoleRows = 25;

    // Opening, replacing and closing a program never interleave: a session is made and kept, or
    // closed, under this gate; its start (VM boot, download) runs outside it.
    private readonly SemaphoreSlim _gate = new(1, 1);
    private (VirtualPath Path, IReadOnlyList<string> Arguments)? _pending;
    private bool _disposed;

    /// <summary>Raised after every change of what the window shows (on any thread).</summary>
    public event Action? Changed;

    /// <summary>The page of the tutorial shown (0 to <see cref="TutorialSteps"/> − 1), or <see langword="null"/>.</summary>
    public int? TutorialStep { get; private set; }

    /// <summary>The program of the window, once one was asked for.</summary>
    public ProgramSession? Session { get; private set; }

    /// <summary>Whether a program is being prepared (« Ouvrir un programme… » waits).</summary>
    public bool IsBusy => Session?.State is SessionState.Preparing or SessionState.Starting;

    /// <summary>The key of the status text and its arguments, or <see langword="null"/> when there is no program.</summary>
    public (string Key, object[] Arguments)? Status => Session switch
    {
        null => null,
        { State: SessionState.Exited } s => ("Status.Exited", [s.FileName ?? string.Empty, s.ExitCode ?? 0]),
        { State: SessionState.Idle } => null,
        { } s => ($"Status.{s.State}", [s.FileName ?? string.Empty]),
    };

    /// <summary>The key of the text that explains a failure: what the program is, or why it did not run.</summary>
    /// <param name="failure">The failure of the session.</param>
    public string FailureKey(SessionFailure failure) => failure.Kind == FailureKind.NotRunnable && Session?.Program is { } program
        ? $"Support.{program.Support}"
        : $"Failure.{failure.Kind}";

    /// <summary>Starts the window: the tutorial at the first launch, then the file the OS passed, if any.</summary>
    /// <param name="file">The file to open, or <see langword="null"/> for a plain launch.</param>
    /// <param name="arguments">The arguments after the file.</param>
    public async Task InitializeAsync(VirtualPath? file, IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (file is { } path)
        {
            _pending = (path, arguments);
        }

        if (!await tutorial.SeenAsync().ConfigureAwait(false))
        {
            TutorialStep = 0;
            Changed?.Invoke();
            return;
        }

        await StartPendingAsync().ConfigureAwait(false);
    }

    /// <summary>Shows the tutorial again (its button).</summary>
    public void ShowTutorial() => Step(0);

    /// <summary>The next page of the tutorial.</summary>
    public void NextStep() => Step(Math.Min((TutorialStep ?? 0) + 1, TutorialSteps - 1));

    /// <summary>The previous page of the tutorial.</summary>
    public void PreviousStep() => Step(Math.Max((TutorialStep ?? 0) - 1, 0));

    /// <summary>Closes the tutorial, records it as seen, and starts the program that waited.</summary>
    public async Task FinishTutorialAsync()
    {
        TutorialStep = null;
        Changed?.Invoke();
        await tutorial.MarkSeenAsync().ConfigureAwait(false);
        await StartPendingAsync().ConfigureAwait(false);
    }

    /// <summary>Opens a program chosen in the window (« Ouvrir un programme… »).</summary>
    /// <param name="path">The file chosen.</param>
    public Task OpenAsync(VirtualPath path)
    {
        _pending = (path, []);
        return TutorialStep is null ? StartPendingAsync() : Task.CompletedTask;
    }

    /// <summary>Closes the console of the program, which ends it.</summary>
    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            await CloseSessionAsync().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private void Step(int step)
    {
        TutorialStep = step;
        Changed?.Invoke();
    }

    private async Task StartPendingAsync()
    {
        ProgramSession session;
        (VirtualPath Path, IReadOnlyList<string> Arguments) pending;
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_pending is not { } next || _disposed)
            {
                return;
            }

            pending = next;
            _pending = null;
            await CloseSessionAsync().ConfigureAwait(false);
            session = newSession();
            session.Changed += OnSessionChanged;
            Session = session;
        }
        finally
        {
            _gate.Release();
        }

        Changed?.Invoke();
        await session.StartAsync(pending.Path, pending.Arguments, ConsoleColumns, ConsoleRows, CancellationToken.None).ConfigureAwait(false);
    }

    private async Task CloseSessionAsync()
    {
        if (Session is { } session)
        {
            Session = null;
            session.Changed -= OnSessionChanged;
            await session.DisposeAsync().ConfigureAwait(false);
        }
    }

    private void OnSessionChanged() => Changed?.Invoke();
}
