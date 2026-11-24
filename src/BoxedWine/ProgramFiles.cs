using Kazo.Os.App.Abstractions;
using Kazo.Os.Simulation.Abstractions.FileSystem;
using Kazo.Os.Simulation.Abstractions.Gateways;
using OsX86.Interface;

namespace BoxedWine;

/// <summary>A program file read whole, or why it could not be read.</summary>
/// <param name="Name">Its file name (the last part of its path).</param>
/// <param name="Content">Its bytes, or <see langword="null"/> on failure.</param>
/// <param name="Error">The code of the failure (<see cref="ProgramFiles.TooLarge"/> or the file system's), or <see langword="null"/>.</param>
internal sealed record ProgramFile(string Name, byte[]? Content, string? Error);

/// <summary>Reads the program a window was asked to open.</summary>
internal interface IProgramFiles
{
    /// <summary>Reads a file whole, refusing one larger than what Os-x86 copies into a prefix.</summary>
    /// <param name="path">The absolute path, as the OS passed it.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    ValueTask<ProgramFile> ReadAsync(VirtualPath path, CancellationToken cancellationToken);
}

/// <summary>
/// Reads through the app's file system gateway (as Kazo Paint does): the OS checks the app's
/// permission on the path, and the size is checked before the content is copied.
/// </summary>
internal sealed class ProgramFiles(IAppFileSystemGateway fileSystem) : IProgramFiles
{
    /// <summary>The file is larger than <see cref="ProgramRunOptions.MaxContentBytes"/>.</summary>
    public const string TooLarge = "too-large";

    private const int CopyChunkBytes = 81920;

    /// <inheritdoc />
    public async ValueTask<ProgramFile> ReadAsync(VirtualPath path, CancellationToken cancellationToken)
    {
        string name = path.Value[(path.Value.LastIndexOf('/') + 1)..];
        FileSystemResult<FileSystemContentReadHandle> result = await fileSystem
            .ReadAsync(new FileSystemReadRequest(path), cancellationToken)
            .ConfigureAwait(false);
        if (!result.Succeeded || result.Value is null)
        {
            return new ProgramFile(name, null, result.Error?.Code.ToString() ?? "read-failed");
        }

        await using FileSystemContentReadHandle handle = result.Value;
        long? length = handle.Entry.Metadata is FileMetadata metadata ? metadata.Length : null;
        return await ReadContentAsync(name, length, handle.Content, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Copies the content of a program, refusing it as soon as it is known to be larger than what
    /// Os-x86 copies into a prefix: from its declared length, or while it is read (never more than
    /// the limit is held).
    /// </summary>
    /// <param name="name">The file name.</param>
    /// <param name="length">The length the file system declares, or <see langword="null"/>.</param>
    /// <param name="content">The content, read from its start.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    internal static async ValueTask<ProgramFile> ReadContentAsync(string name, long? length, Stream content, CancellationToken cancellationToken)
    {
        if (length > ProgramRunOptions.MaxContentBytes)
        {
            return new ProgramFile(name, null, TooLarge);
        }

        using var buffer = new MemoryStream();
        byte[] chunk = new byte[CopyChunkBytes];
        int read;
        while ((read = await content.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > ProgramRunOptions.MaxContentBytes)
            {
                return new ProgramFile(name, null, TooLarge);
            }

            buffer.Write(chunk, 0, read);
        }

        return new ProgramFile(name, buffer.ToArray(), null);
    }
}
