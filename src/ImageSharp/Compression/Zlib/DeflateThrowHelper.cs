// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Compression.Zlib;

internal static class DeflateThrowHelper
{
    public static void ThrowAlreadyFinished() => throw new InvalidOperationException("Finish() already called.");

    public static void ThrowAlreadyClosed() => throw new InvalidOperationException("Deflator already closed.");

    public static void ThrowUnknownCompression() => throw new InvalidOperationException("Unknown compression function.");

    public static void ThrowNotProcessed() => throw new InvalidOperationException("Old input was not completely processed.");

    public static void ThrowNull(string name) => throw new ArgumentNullException(name);

    public static void ThrowOutOfRange(string name) => throw new ArgumentOutOfRangeException(name);

    public static void ThrowHeapViolated() => throw new InvalidOperationException("Huffman heap invariant violated.");

    public static void ThrowNoDeflate() => throw new ImageFormatException("Cannot deflate all input.");
}
