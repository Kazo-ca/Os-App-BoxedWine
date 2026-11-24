using Kazo.Os.App.Abstractions;
using Kazo.Os.Simulation.Abstractions.FileSystem;
using Kazo.Os.Simulation.Abstractions.Gateways;
using OsX86.Interface;

namespace BoxedWine.Tests;

/// <summary>The program file read before its launch, bounded by what Os-x86 copies into a prefix.</summary>
[Trait("Category", "unit")]
public sealed class ProgramFilesTests
{
    [Fact]
    public async Task A_program_is_read_whole()
    {
        byte[] content = [.. Enumerable.Range(0, 200_000).Select(static i => (byte)i)];

        ProgramFile file = await ProgramFiles.ReadContentAsync("tool.exe", content.Length, new MemoryStream(content), CancellationToken.None);

        Assert.Equal("tool.exe", file.Name);
        Assert.Null(file.Error);
        Assert.Equal(content, file.Content);
    }

    [Fact]
    public async Task A_declared_length_over_the_limit_is_refused_before_reading()
    {
        var content = new ZeroStream(1);

        ProgramFile file = await ProgramFiles.ReadContentAsync("big.exe", ProgramRunOptions.MaxContentBytes + 1L, content, CancellationToken.None);

        Assert.Equal(ProgramFiles.TooLarge, file.Error);
        Assert.Null(file.Content);
        Assert.Equal(0, content.Position);
    }

    [Fact]
    public async Task A_content_longer_than_the_limit_is_refused_while_it_is_read()
    {
        ProgramFile file = await ProgramFiles.ReadContentAsync("big.exe", null, new ZeroStream(ProgramRunOptions.MaxContentBytes + 1L), CancellationToken.None);

        Assert.Equal(ProgramFiles.TooLarge, file.Error);
        Assert.Null(file.Content);
    }

    [Fact]
    public async Task A_file_the_OS_refuses_to_read_is_reported_with_its_code()
    {
        var files = new ProgramFiles(new RefusingGateway());

        ProgramFile file = await files.ReadAsync(VirtualPath.Parse("/root/secret.exe"), CancellationToken.None);

        Assert.Equal("secret.exe", file.Name);
        Assert.Equal(nameof(FileSystemErrorCode.PermissionDenied), file.Error);
        Assert.Null(file.Content);
    }

    /// <summary>A file system gateway that refuses every read, as for a path outside the app's permission.</summary>
    private sealed class RefusingGateway : IAppFileSystemGateway
    {
        public ValueTask<FileSystemResult<FileSystemContentReadHandle>> ReadAsync(FileSystemReadRequest request, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(FileSystemResult<FileSystemContentReadHandle>.Failure(
                new FileSystemError(FileSystemOperation.Read, FileSystemErrorCode.PermissionDenied, request.Path)));

        public ValueTask<FileSystemResult<FileSystemDirectorySnapshot>> EnumerateAsync(FileSystemEnumerateRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<FileSystemMutationResult> CreateAsync(FileSystemCreateRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<FileSystemMutationResult> WriteAsync(FileSystemWriteRequest request, IFileSystemContentSource content, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<FileSystemMutationResult> MoveAsync(FileSystemMoveRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<FileSystemMutationResult> CopyAsync(FileSystemCopyRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<FileSystemMutationResult> DeleteAsync(FileSystemDeleteRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<FileSystemResult<FileSystemEntrySnapshot>> StatAsync(FileSystemStatRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<FileSystemMutationResult> SetPermissionsAsync(FileSystemSetPermissionsRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public IAppFileSystemGateway WithSelectedHandle(FileSystemSelectedResourceHandle handle) => this;
    }

    /// <summary>A stream of zeros of a given length, without holding it.</summary>
    private sealed class ZeroStream(long length) : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => length;

        public override long Position { get; set; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = (int)Math.Min(count, length - Position);
            Array.Clear(buffer, offset, read);
            Position += read;
            return read;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
