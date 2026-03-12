// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.IO;

/// <summary>
/// A wrapper around the local File apis.
/// </summary>
internal sealed class LocalFileSystem : IFileSystem
{
    /// <inheritdoc/>
    public Stream OpenRead(string path) => File.OpenRead(path);

    /// <inheritdoc/>
    public Stream OpenReadAsynchronous(string path) => File.Open(path, new FileStreamOptions
    {
        Mode = FileMode.Open,
        Access = FileAccess.Read,
        Share = FileShare.Read,
        Options = FileOptions.Asynchronous,
    });

    /// <inheritdoc/>
    public Stream Create(string path) => File.Create(path);

    /// <inheritdoc/>
    public Stream CreateAsynchronous(string path) => File.Open(path, new FileStreamOptions
    {
        Mode = FileMode.Create,
        Access = FileAccess.ReadWrite,
        Share = FileShare.None,
        Options = FileOptions.Asynchronous,
    });
}
