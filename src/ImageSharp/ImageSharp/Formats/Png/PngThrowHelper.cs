// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Png
{
    /// <summary>
    /// Cold path optimizations for throwing png format based exceptions.
    /// </summary>
    internal static class PngThrowHelper
    {
        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowNoHeader() => throw new ImageFormatException("PNG Image does not contain a header chunk");

        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowNoData() => throw new ImageFormatException("PNG Image does not contain a data chunk");

        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowInvalidChunkType() => throw new ImageFormatException("Invalid PNG data.");

        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowInvalidChunkCrc(string chunkTypeName) => throw new ImageFormatException($"CRC Error. PNG {chunkTypeName} chunk is corrupt!");

        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowNotSupportedColor() => new NotSupportedException("Unsupported PNG color type");

        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowUnknownFilter() => throw new ImageFormatException("Unknown filter type.");
    }
}
