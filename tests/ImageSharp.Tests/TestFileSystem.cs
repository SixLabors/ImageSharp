// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

#nullable enable

namespace SixLabors.ImageSharp.Tests;

/// <summary>
/// A test image file.
/// </summary>
public class TestFileSystem : ImageSharp.IO.IFileSystem
{
    private readonly Dictionary<string, Func<Stream>> fileSystem = new(StringComparer.OrdinalIgnoreCase);

    public void AddFile(string path, Func<Stream> data)
    {
        lock (this.fileSystem)
        {
            this.fileSystem.Add(path, data);
        }
    }

    public Stream Create(string path) => this.GetStream(path) ?? File.Create(path);

    public Stream CreateAsynchronous(string path) => this.GetStream(path) ?? File.Open(path, new FileStreamOptions
    {
        Mode = FileMode.Create,
        Access = FileAccess.ReadWrite,
        Share = FileShare.None,
        Options = FileOptions.Asynchronous,
    });

    public Stream OpenRead(string path) => this.GetStream(path) ?? File.OpenRead(path);

    public Stream OpenReadAsynchronous(string path) => this.GetStream(path) ?? File.Open(path, new FileStreamOptions
    {
        Mode = FileMode.Open,
        Access = FileAccess.Read,
        Share = FileShare.Read,
        Options = FileOptions.Asynchronous,
    });

    private Stream? GetStream(string path)
    {
        // if we have injected a fake file use it instead
        lock (this.fileSystem)
        {
            if (this.fileSystem.TryGetValue(path, out Func<Stream>? streamFactory))
            {
                Stream stream = streamFactory();
                stream.Position = 0;
                return stream;
            }
        }

        return null;
    }
}
