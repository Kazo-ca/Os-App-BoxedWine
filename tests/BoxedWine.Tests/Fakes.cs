using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Kazo.Os.App.Abstractions;
using OsX86.Interface;

namespace BoxedWine.Tests;

/// <summary>An <see cref="Ix86"/> that answers what a test sets and records what BoxedWine asks.</summary>
internal sealed class FakeX86 : Ix86
{
    public ProgramDetection Detection { get; set; } = Detections.Console;

    public Exception? RunFailure { get; set; }

    public FakeConsole Console { get; } = new();

    public List<(byte[] Header, string? FileName)> Detected { get; } = [];

    public List<ProgramRunOptions> Runs { get; } = [];

    public string Version => "0.1.0";

    public event EventHandler<VmStateChangedEventArgs>? VmStateChanged
    {
        add { }
        remove { }
    }

    public ValueTask<ProgramDetection> DetectProgramAsync(ReadOnlyMemory<byte> header, string? fileName, CancellationToken cancellationToken)
    {
        Detected.Add((header.ToArray(), fileName));
        return ValueTask.FromResult(Detection);
    }

    public ValueTask<IVmConsole> RunProgramAsync(ProgramRunOptions options, CancellationToken cancellationToken)
    {
        Runs.Add(options);
        return RunFailure is { } failure ? ValueTask.FromException<IVmConsole>(failure) : ValueTask.FromResult<IVmConsole>(Console);
    }

    public ValueTask<IReadOnlyList<VmSummary>> GetVmsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask StartVmAsync(Guid vmId, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask StopVmAsync(Guid vmId, bool force, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<IVmConsole> OpenConsoleAsync(Guid vmId, VmConsoleOptions options, CancellationToken cancellationToken) => throw new NotSupportedException();
}

/// <summary>A console whose output, exit and failures a test drives.</summary>
internal sealed class FakeConsole : IVmConsole
{
    private readonly Channel<ReadOnlyMemory<byte>> _output = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
    private readonly TaskCompletionSource<int> _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Guid VmId { get; } = Guid.NewGuid();

    public bool IsSerial => false;

    public List<byte[]> Written { get; } = [];

    public int Interrupts { get; private set; }

    public bool Disposed { get; private set; }

    public Exception? WriteFailure { get; set; }

    public Task<int> Exited => _exited.Task;

    public void Write(string text) => _output.Writer.TryWrite(System.Text.Encoding.UTF8.GetBytes(text));

    /// <summary>Ends the output, then the program with its code.</summary>
    public void Exit(int code)
    {
        _output.Writer.TryComplete();
        _exited.TrySetResult(code);
    }

    /// <summary>The VM stops under the program.</summary>
    public void Lose(Exception exception) => _output.Writer.TryComplete(exception);

    public async IAsyncEnumerable<ReadOnlyMemory<byte>> ReadOutputAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (ReadOnlyMemory<byte> chunk in _output.Reader.ReadAllAsync(cancellationToken))
        {
            yield return chunk;
        }
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        if (WriteFailure is { } failure)
        {
            return ValueTask.FromException(failure);
        }

        Written.Add(data.ToArray());
        return ValueTask.CompletedTask;
    }

    public ValueTask SendInterruptAsync(CancellationToken cancellationToken)
    {
        Interrupts++;
        return ValueTask.CompletedTask;
    }

    public ValueTask ResizeAsync(int columns, int rows, CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        _output.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }
}

/// <summary>Program files a test sets.</summary>
internal sealed class FakeFiles : IProgramFiles
{
    public Dictionary<string, ProgramFile> Files { get; } = new(StringComparer.Ordinal);

    public ValueTask<ProgramFile> ReadAsync(VirtualPath path, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Files.TryGetValue(path.Value, out ProgramFile? file) ? file : new ProgramFile(path.Value[(path.Value.LastIndexOf('/') + 1)..], null, "not-found"));

    public FakeFiles With(string path, byte[] content)
    {
        Files[path] = new ProgramFile(path[(path.LastIndexOf('/') + 1)..], content, null);
        return this;
    }
}

/// <summary>Detections as Os-x86 gives them (the details of its <c>ProgramDetector</c>).</summary>
internal static class Detections
{
    public static ProgramDetection Console { get; } = new(ProgramKind.Windows, ProgramMimeTypes.PortableExecutable, ProgramDetectionSource.Header, "PE32 console i386")
    {
        Bits = 32,
        Subsystem = ProgramSubsystem.Console,
        IsComplete = true,
    };

    public static ProgramDetection Windowed { get; } = Console with { Detail = "PE32 windowed i386", Subsystem = ProgramSubsystem.Windowed };
}

/// <summary>Waits for what a background pump does.</summary>
internal static class Eventually
{
    public static async Task TrueAsync(Func<bool> condition, string what)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!condition())
        {
            Assert.False(timeout.IsCancellationRequested, $"Timed out waiting: {what}");
            await Task.Delay(10);
        }
    }
}
