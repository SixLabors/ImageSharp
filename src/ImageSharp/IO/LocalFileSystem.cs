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
    public Stream OpenReadAsynchronous(string path) => new FileStream(
        path,
        FileMode.Open,
        FileAccess.Read,
        FileShare.Read,
        4096,
        FileOptions.Asynchronous
    );

    /// <inheritdoc/>
    public Stream Create(string path) => File.Create(path);

    /// <inheritdoc/>
    public Stream CreateAsynchronous(string path) => new FileStream(
        path,
        FileMode.Create,
        FileAccess.ReadWrite,
        FileShare.None,
        4096,
        FileOptions.Asynchronous
    );
}
