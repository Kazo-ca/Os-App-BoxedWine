using System.Text;
using BoxedWine.Lib;
using Kazo.Os.App.Abstractions;
using OsX86.Interface;

namespace BoxedWine.Tests;

/// <summary>
/// The window of BoxedWine without its markup: the tutorial first at the first launch (decision
/// 71), the file the OS passed launched after it, the status and the texts of a failure.
/// </summary>
[Trait("Category", "unit")]
public sealed class WindowControllerTests : IAsyncLifetime
{
    private const string Setup = "/home/user/setup.exe";

    private readonly FakeX86 _x86 = new();
    private readonly FakeFiles _files = new FakeFiles().With(Setup, Encoding.ASCII.GetBytes("MZ")).With("/home/user/other.exe", Encoding.ASCII.GetBytes("MZ"));
    private readonly Tutorial _tutorial = new();
    private readonly List<ProgramSession> _sessions = [];
    private WindowController _window = null!;

    public Task InitializeAsync()
    {
        _window = new WindowController(
            () =>
            {
                var session = new ProgramSession(new BwLib(_ => ValueTask.FromResult<Ix86?>(_x86)), _files);
                _sessions.Add(session);
                return session;
            },
            _tutorial);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _window.DisposeAsync();

    [Fact]
    public async Task At_the_first_launch_the_tutorial_comes_before_the_program()
    {
        await _window.InitializeAsync(VirtualPath.Parse(Setup), ["/S"]);

        Assert.Equal(0, _window.TutorialStep);
        Assert.Null(_window.Session);
        Assert.Empty(_x86.Runs);

        _window.NextStep();
        _window.NextStep();
        _window.NextStep();
        Assert.Equal(WindowController.TutorialSteps - 1, _window.TutorialStep);
        _window.PreviousStep();
        Assert.Equal(1, _window.TutorialStep);

        await _window.FinishTutorialAsync();

        Assert.Null(_window.TutorialStep);
        Assert.True(_tutorial.Seen);
        Assert.Equal(SessionState.Running, _window.Session?.State);
        Assert.Equal(["/S"], Assert.Single(_x86.Runs).Arguments);
    }

    [Fact]
    public async Task Once_seen_the_program_starts_at_once_and_the_tutorial_can_be_shown_again()
    {
        _tutorial.Seen = true;

        await _window.InitializeAsync(VirtualPath.Parse(Setup), []);

        Assert.Null(_window.TutorialStep);
        Assert.Single(_x86.Runs);
        Assert.Equal("Status.Running", _window.Status?.Key);
        Assert.Equal(["setup.exe"], _window.Status?.Arguments!);

        _window.ShowTutorial();
        Assert.Equal(0, _window.TutorialStep);
    }

    [Fact]
    public async Task A_plain_launch_shows_the_home_page()
    {
        _tutorial.Seen = true;

        await _window.InitializeAsync(null, []);

        Assert.Null(_window.Session);
        Assert.Null(_window.Status);
        Assert.False(_window.IsBusy);
    }

    [Fact]
    public async Task A_second_program_opened_in_the_window_closes_the_first()
    {
        _tutorial.Seen = true;
        await _window.InitializeAsync(VirtualPath.Parse(Setup), []);

        await _window.OpenAsync(VirtualPath.Parse("/home/user/other.exe"));

        Assert.True(_x86.Console.Disposed);
        Assert.Equal(2, _x86.Runs.Count);
        Assert.Equal("other.exe", _window.Session?.FileName);
    }

    [Fact]
    public async Task A_program_chosen_during_the_tutorial_waits_for_its_end()
    {
        await _window.InitializeAsync(null, []);

        await _window.OpenAsync(VirtualPath.Parse(Setup));
        Assert.Empty(_x86.Runs);

        await _window.FinishTutorialAsync();
        Assert.Single(_x86.Runs);
    }

    [Fact]
    public async Task The_end_and_the_failures_have_their_texts()
    {
        _tutorial.Seen = true;
        await _window.InitializeAsync(VirtualPath.Parse(Setup), []);
        _x86.Console.Exit(3);
        await Eventually.TrueAsync(() => _window.Session?.State == SessionState.Exited, "the program ends");

        Assert.Equal("Status.Exited", _window.Status?.Key);
        Assert.Equal(["setup.exe", 3], _window.Status?.Arguments!);

        _x86.Detection = Detections.Windowed;
        await _window.OpenAsync(VirtualPath.Parse("/home/user/other.exe"));
        SessionFailure failure = _window.Session!.Failure!;
        Assert.Equal("Support.Windowed", _window.FailureKey(failure));
        Assert.Equal("Status.Failed", _window.Status?.Key);
        Assert.Equal("Failure.X86Missing", _window.FailureKey(new SessionFailure(FailureKind.X86Missing, null, "missing")));
    }

    [Fact]
    public async Task Every_change_is_announced_and_closing_twice_is_harmless()
    {
        int changes = 0;
        _window.Changed += () => changes++;

        await _window.InitializeAsync(VirtualPath.Parse(Setup), []);
        await _window.FinishTutorialAsync();

        Assert.True(changes >= 4);
        await _window.DisposeAsync();
        await _window.DisposeAsync();
        Assert.True(_x86.Console.Disposed);

        await _window.OpenAsync(VirtualPath.Parse(Setup));
        Assert.Single(_x86.Runs);
    }

    private sealed class Tutorial : ITutorialStore
    {
        public bool Seen { get; set; }

        public ValueTask<bool> SeenAsync() => ValueTask.FromResult(Seen);

        public ValueTask MarkSeenAsync()
        {
            Seen = true;
            return ValueTask.CompletedTask;
        }
    }
}
