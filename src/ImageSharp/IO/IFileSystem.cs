// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.IO;

/// <summary>
/// A simple interface representing the filesystem.
/// </summary>
internal interface IFileSystem
{
    /// <summary>
    /// Opens a file as defined by the path and returns it as a readable stream.
    /// </summary>
    /// <param name="path">Path to the file to open.</param>
    /// <returns>A stream representing the opened file.</returns>
    Stream OpenRead(string path);

    /// <summary>
    /// Opens a file as defined by the path and returns it as a readable stream
    /// that can be used for asynchronous reading.
    /// </summary>
    /// <param name="path">Path to the file to open.</param>
    /// <returns>A stream representing the opened file.</returns>
    Stream OpenReadAsynchronous(string path);

    /// <summary>
    /// Creates or opens a file as defined by the path and returns it as a writable stream.
    /// </summary>
    /// <param name="path">Path to the file to open.</param>
    /// <returns>A stream representing the opened file.</returns>
    Stream Create(string path);

    /// <summary>
    /// Creates or opens a file as defined by the path and returns it as a writable stream
    /// that can be used for asynchronous reading and writing.
    /// </summary>
    /// <param name="path">Path to the file to open.</param>
    /// <returns>A stream representing the opened file.</returns>
    Stream CreateAsynchronous(string path);
}
