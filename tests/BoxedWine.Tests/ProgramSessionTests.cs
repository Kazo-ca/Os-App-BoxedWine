using System.Text;
using BoxedWine.Lib;
using Kazo.Os.App.Abstractions;
using OsX86.Interface;

namespace BoxedWine.Tests;

/// <summary>
/// One program run by a window: the file the OS passed, read, checked by Os-x86, launched through
/// Ix86 with its console, and its end (work item 13).
/// </summary>
[Trait("Category", "unit")]
public sealed class ProgramSessionTests : IAsyncLifetime
{
    private const string Setup = "/home/user/Downloads/setup.exe";

    private readonly FakeX86 _x86 = new();
    private readonly FakeFiles _files = new FakeFiles().With(Setup, Encoding.ASCII.GetBytes("MZ fake program"));
    private Ix86? _available;
    private ProgramSession _session = null!;

    public Task InitializeAsync()
    {
        _available = _x86;
        _session = new ProgramSession(new BwLib(_ => ValueTask.FromResult<Ix86?>(_available)), _files);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _session.DisposeAsync();

    [Fact]
    public async Task The_program_runs_its_output_shows_and_its_exit_code_is_kept()
    {
        await StartAsync(Setup, "/S", "x");

        Assert.Equal(SessionState.Running, _session.State);
        Assert.Equal("setup.exe", _session.FileName);
        ProgramRunOptions run = Assert.Single(_x86.Runs);
        Assert.Equal("setup.exe", run.FileName);
        Assert.Equal(["/S", "x"], run.Arguments);
        Assert.Equal((80, 25), (run.Columns, run.Rows));

        _x86.Console.Write("Hello from Wine\r\n");
        _x86.Console.Exit(7);

        await Eventually.TrueAsync(() => _session.State == SessionState.Exited, "the program ends");
        Assert.Equal(7, _session.ExitCode);
        Assert.Equal(["Hello from Wine"], _session.Transcript.Lines);
        Assert.False(_session.AcceptsInput);
    }

    [Fact]
    public async Task A_line_typed_goes_with_a_carriage_return_and_ctrl_c_is_sent()
    {
        await StartAsync(Setup);

        await _session.SendLineAsync("yes");
        await _session.InterruptAsync();

        Assert.Equal("yes\r", Encoding.UTF8.GetString(Assert.Single(_x86.Console.Written)));
        Assert.Equal(1, _x86.Console.Interrupts);
    }

    [Fact]
    public async Task A_key_after_the_end_of_the_program_is_ignored()
    {
        await StartAsync(Setup);
        _x86.Console.WriteFailure = new X86Exception(X86ErrorCodes.ConsoleClosed, "closed");

        await _session.SendLineAsync("late");

        Assert.Equal(SessionState.Running, _session.State);
    }

    [Fact]
    public async Task Closing_the_window_closes_the_console_which_ends_the_program()
    {
        await StartAsync(Setup);

        await _session.DisposeAsync();
        await _session.DisposeAsync();

        Assert.True(_x86.Console.Disposed);
    }

    [Fact]
    public async Task A_windowed_program_is_refused_with_what_it_is_and_nothing_starts()
    {
        _x86.Detection = Detections.Windowed;

        await StartAsync(Setup);

        Assert.Equal(SessionState.Failed, _session.State);
        Assert.Equal(FailureKind.NotRunnable, _session.Failure?.Kind);
        Assert.Equal(BwSupport.Windowed, _session.Program?.Support);
        Assert.Empty(_x86.Runs);
    }

    [Fact]
    public async Task Without_Os_x86_the_window_says_so()
    {
        _available = null;

        await StartAsync(Setup);

        Assert.Equal(FailureKind.X86Missing, _session.Failure?.Kind);
    }

    [Fact]
    public async Task A_file_that_cannot_be_read_or_is_too_large_is_reported()
    {
        await StartAsync("/home/user/missing.exe");
        Assert.Equal(new SessionFailure(FailureKind.ReadFailed, "not-found", "/home/user/missing.exe"), _session.Failure);

        _files.Files["/home/user/big.exe"] = new ProgramFile("big.exe", null, ProgramFiles.TooLarge);
        await using var other = new ProgramSession(new BwLib(_ => ValueTask.FromResult<Ix86?>(_x86)), _files);
        await other.StartAsync(VirtualPath.Parse("/home/user/big.exe"), [], 80, 25, CancellationToken.None);
        Assert.Equal(FailureKind.TooLarge, other.Failure?.Kind);
    }

    public static TheoryData<Exception, string> LaunchFailures => new()
    {
        { new UnauthorizedAccessException("x86.programs.launch is required"), nameof(FailureKind.PermissionDenied) },
        { new VmMemoryBudgetExceededException(512, 1024, 1024, [], []), nameof(FailureKind.MemoryBudget) },
        { new X86Exception(X86ErrorCodes.ProgramNotSupported, "not yet"), nameof(FailureKind.NotRunnable) },
        { new X86Exception(X86ErrorCodes.ImageUnavailable, "no image"), nameof(FailureKind.X86Error) },
        { new X86Exception(X86ErrorCodes.AgentLost, "lost"), nameof(FailureKind.Lost) },
    };

    [Theory]
    [MemberData(nameof(LaunchFailures))]
    public async Task The_refusals_of_Os_x86_become_a_failure_of_the_window(Exception exception, string kind)
    {
        _x86.RunFailure = exception;

        await StartAsync(Setup);

        Assert.Equal(SessionState.Failed, _session.State);
        Assert.Equal(kind, _session.Failure?.Kind.ToString());
        Assert.Equal(exception.Message, _session.Failure?.Detail);
    }

    [Fact]
    public async Task A_bug_is_not_hidden_as_a_failure()
    {
        _x86.RunFailure = new NullReferenceException("bug");

        await Assert.ThrowsAsync<NullReferenceException>(() => StartAsync(Setup));
    }

    [Fact]
    public async Task The_vm_that_stops_under_the_program_is_reported()
    {
        await StartAsync(Setup);

        _x86.Console.Lose(new X86Exception(X86ErrorCodes.StartFailed, "the VM stopped"));

        await Eventually.TrueAsync(() => _session.State == SessionState.Failed, "the loss is seen");
        Assert.Equal(FailureKind.Lost, _session.Failure?.Kind);
    }

    [Fact]
    public async Task A_session_runs_one_program()
    {
        await StartAsync(Setup);

        await Assert.ThrowsAsync<InvalidOperationException>(() => StartAsync(Setup));
    }

    [Fact]
    public async Task Every_change_is_announced()
    {
        var states = new List<SessionState>();
        _session.Changed += () => states.Add(_session.State);

        await StartAsync(Setup);

        Assert.Equal([SessionState.Preparing, SessionState.Starting, SessionState.Running], states);
    }

    [Fact]
    public void Exceptions_are_described_or_left_alone()
    {
        Assert.Equal(FailureKind.X86Missing, ProgramSession.Describe(new BwException(BwErrorCodes.X86Missing, "m"))?.Kind);
        Assert.Equal(FailureKind.TooLarge, ProgramSession.Describe(new BwException(BwErrorCodes.TooLarge, "t"))?.Kind);
        Assert.Equal(FailureKind.NotRunnable, ProgramSession.Describe(new BwException(BwErrorCodes.NotRunnable, "n"))?.Kind);
        Assert.Null(ProgramSession.Describe(new InvalidOperationException()));
    }

    private Task StartAsync(string path, params string[] arguments) =>
        _session.StartAsync(VirtualPath.Parse(path), arguments, 80, 25, CancellationToken.None);
}
