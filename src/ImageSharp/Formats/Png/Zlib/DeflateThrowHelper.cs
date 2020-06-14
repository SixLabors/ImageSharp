// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Png.Zlib
{
    internal static class DeflateThrowHelper
    {
        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowAlreadyFinished() => throw new InvalidOperationException("Finish() already called.");

        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowAlreadyClosed() => throw new InvalidOperationException("Deflator already closed.");

        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowUnknownCompression() => throw new InvalidOperationException("Unknown compression function.");

        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowNotProcessed() => throw new InvalidOperationException("Old input was not completely processed.");

        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowNull(string name) => throw new ArgumentNullException(name);

        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowOutOfRange(string name) => throw new ArgumentOutOfRangeException(name);

        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowHeapViolated() => throw new InvalidOperationException("Huffman heap invariant violated.");

        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowNoDeflate() => throw new ImageFormatException("Cannot deflate all input.");
    }
}
