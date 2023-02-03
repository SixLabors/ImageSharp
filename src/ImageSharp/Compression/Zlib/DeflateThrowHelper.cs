// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;

namespace SixLabors.ImageSharp.Compression.Zlib;

internal static class DeflateThrowHelper
{
    [DoesNotReturn]
    public static void ThrowAlreadyFinished() => throw new InvalidOperationException("Finish() already called.");

    [DoesNotReturn]
    public static void ThrowAlreadyClosed() => throw new InvalidOperationException("Deflator already closed.");

    [DoesNotReturn]
    public static void ThrowUnknownCompression() => throw new InvalidOperationException("Unknown compression function.");

    [DoesNotReturn]
    public static void ThrowNotProcessed() => throw new InvalidOperationException("Old input was not completely processed.");

    [DoesNotReturn]
    public static void ThrowNull(string name) => throw new ArgumentNullException(name);

    [DoesNotReturn]
    public static void ThrowOutOfRange(string name) => throw new ArgumentOutOfRangeException(name);

    [DoesNotReturn]
    public static void ThrowHeapViolated() => throw new InvalidOperationException("Huffman heap invariant violated.");

    [DoesNotReturn]
    public static void ThrowNoDeflate() => throw new ImageFormatException("Cannot deflate all input.");
}
