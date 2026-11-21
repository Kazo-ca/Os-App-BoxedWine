using BoxedWine.Lib;
using OsX86.Interface;

namespace BoxedWine.Tests;

/// <summary>BwLib on <see cref="Ix86"/>: what a file is, whether it runs yet, and its launch.</summary>
[Trait("Category", "unit")]
public sealed class BwLibTests
{
    private readonly FakeX86 _x86 = new();

    private BwLib Lib => new(_ => ValueTask.FromResult<Ix86?>(_x86));

    public static TheoryData<ProgramDetection, BwSupport> Classifications => new()
    {
        { Detections.Console, BwSupport.Console },
        { Detections.Console with { IsDotNet = true }, BwSupport.Console },
        { Detections.Windowed, BwSupport.Windowed },
        { Detections.Console with { Bits = 64, Detail = "PE32+ console x86-64" }, BwSupport.Windows64 },
        { Detections.Console with { Bits = 16, Detail = "NE" }, BwSupport.Windows16 },
        { Detections.Console with { IsLibrary = true }, BwSupport.Library },
        { Detections.Console with { Subsystem = ProgramSubsystem.Other }, BwSupport.OtherWindows },
        { Detections.Console with { Detail = "PE32 console arm" }, BwSupport.OtherWindows },
        { new(ProgramKind.Batch, ProgramMimeTypes.Batch, ProgramDetectionSource.Extension, "batch"), BwSupport.Batch },
        { new(ProgramKind.WindowsInstaller, ProgramMimeTypes.WindowsInstaller, ProgramDetectionSource.Extension, "msi"), BwSupport.Installer },
        { new(ProgramKind.Dos, ProgramMimeTypes.DosExecutable, ProgramDetectionSource.Header, "MZ"), BwSupport.Dos },
        { new(ProgramKind.Linux, ProgramMimeTypes.Executable, ProgramDetectionSource.Header, "ELF 32"), BwSupport.Linux },
        { new(ProgramKind.Unknown, ProgramMimeTypes.Unknown, ProgramDetectionSource.None, "unknown"), BwSupport.Unknown },
        { new(ProgramKind.Windows, ProgramMimeTypes.PortableExecutable, ProgramDetectionSource.Conflict, "PE with .png"), BwSupport.Mismatch },
        // With 64 bytes only, the subsystem is not known: nothing to run on that.
        { new(ProgramKind.Windows, ProgramMimeTypes.PortableExecutable, ProgramDetectionSource.Header, "PE"), BwSupport.Unknown },
    };

    [Theory]
    [MemberData(nameof(Classifications))]
    public void A_program_is_classified_as_x86_run_would(ProgramDetection detection, BwSupport expected)
    {
        Assert.Equal(expected, BwLib.Classify(detection));
    }

    [Fact]
    public void The_five_file_types_are_handled_and_dos_is_not_executed_by_boxedwine()
    {
        Assert.Equal([".exe", ".com", ".bat", ".cmd", ".msi"], Lib.FileTypes.Select(static type => type.Extension));
        Assert.Equal(Lib.FileTypes, BwLib.HandledFileTypes);
        Assert.False(BwLib.HandledFileTypes.Single(static type => type.Extension == ".com").Execute);
        Assert.All(BwLib.HandledFileTypes.Where(static type => type.Extension != ".com"), static type => Assert.True(type.Execute));
        Assert.True(BwLib.IsHandled("/home/a/SETUP.EXE"));
        Assert.False(BwLib.IsHandled("notes.txt"));
        Assert.False(BwLib.IsHandled(null));
    }

    [Fact]
    public async Task Inspect_sends_at_most_4_KiB_and_the_file_name_without_its_path()
    {
        BwProgram program = await Lib.InspectAsync("/home/user/dl/hello.exe", new byte[10_000], CancellationToken.None);

        (byte[] header, string? name) = Assert.Single(_x86.Detected);
        Assert.Equal(BwLib.HeaderLength, header.Length);
        Assert.Equal("hello.exe", name);
        Assert.Equal("hello.exe", program.FileName);
        Assert.True(program.CanRun);
    }

    [Fact]
    public async Task Without_Os_x86_inspect_and_launch_say_it_is_missing()
    {
        var lib = new BwLib(_ => ValueTask.FromResult<Ix86?>(null));

        BwException inspect = await Assert.ThrowsAsync<BwException>(() => lib.InspectAsync("a.exe", new byte[64], CancellationToken.None).AsTask());
        BwException launch = await Assert.ThrowsAsync<BwException>(() => lib.LaunchAsync(new BwLaunch { FileName = "a.exe", Content = new byte[64] }, CancellationToken.None).AsTask());

        Assert.Equal(BwErrorCodes.X86Missing, inspect.Code);
        Assert.Equal(BwErrorCodes.X86Missing, launch.Code);
    }

    [Fact]
    public async Task Launch_passes_the_program_and_its_arguments_and_lets_Os_x86_choose_the_vm()
    {
        byte[] content = [1, 2, 3];

        IVmConsole console = await Lib.LaunchAsync(
            new BwLaunch { FileName = "C:\\dl\\tool.exe", Content = content, Arguments = ["--help", "a b"], Columns = 5000, Rows = 0 },
            CancellationToken.None);

        Assert.Same(_x86.Console, console);
        ProgramRunOptions run = Assert.Single(_x86.Runs);
        Assert.Equal("tool.exe", run.FileName);
        Assert.Equal(content, run.Content.ToArray());
        Assert.Equal(["--help", "a b"], run.Arguments);
        Assert.Null(run.VmId);
        Assert.Null(run.Prefix);
        Assert.Equal(1000, run.Columns);
        Assert.Equal(1, run.Rows);
    }

    [Fact]
    public async Task A_program_that_cannot_run_yet_is_refused_before_Os_x86_starts_anything()
    {
        _x86.Detection = Detections.Windowed;

        BwException refused = await Assert.ThrowsAsync<BwException>(() => Lib.LaunchAsync(new BwLaunch { FileName = "app.exe", Content = new byte[64] }, CancellationToken.None).AsTask());

        Assert.Equal(BwErrorCodes.NotRunnable, refused.Code);
        Assert.Equal(BwSupport.Windowed, refused.Program?.Support);
        Assert.Empty(_x86.Runs);
    }

    [Fact]
    public async Task A_program_too_large_for_a_prefix_is_refused()
    {
        var launch = new BwLaunch { FileName = "big.exe", Content = new byte[ProgramRunOptions.MaxContentBytes + 1] };

        BwException refused = await Assert.ThrowsAsync<BwException>(() => Lib.LaunchAsync(launch, CancellationToken.None).AsTask());

        Assert.Equal(BwErrorCodes.TooLarge, refused.Code);
        Assert.Empty(_x86.Detected);
    }

    [Fact]
    public async Task The_refusals_of_Os_x86_reach_the_caller()
    {
        _x86.RunFailure = new X86Exception(X86ErrorCodes.ImageUnavailable, "no image");

        X86Exception failure = await Assert.ThrowsAsync<X86Exception>(() => Lib.LaunchAsync(new BwLaunch { FileName = "a.bat", Content = "@echo off"u8.ToArray() }, CancellationToken.None).AsTask());

        Assert.Equal(X86ErrorCodes.ImageUnavailable, failure.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/home/user/")]
    public async Task A_file_name_without_last_part_is_refused(string fileName)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => Lib.InspectAsync(fileName, new byte[1], CancellationToken.None).AsTask());
    }
}
