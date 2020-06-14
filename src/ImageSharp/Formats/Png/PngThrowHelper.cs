// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Png
{
    /// <summary>
    /// Cold path optimizations for throwing png format based exceptions.
    /// </summary>
    internal static class PngThrowHelper
    {
        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowInvalidImageContentException(string errorMessage, Exception innerException)
            => throw new InvalidImageContentException(errorMessage, innerException);

        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowNoHeader() => throw new InvalidImageContentException("PNG Image does not contain a header chunk");

        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowNoData() => throw new InvalidImageContentException("PNG Image does not contain a data chunk");

        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowInvalidChunkType() => throw new InvalidImageContentException("Invalid PNG data.");

        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowInvalidChunkType(string message) => throw new InvalidImageContentException(message);

        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowInvalidChunkCrc(string chunkTypeName) => throw new InvalidImageContentException($"CRC Error. PNG {chunkTypeName} chunk is corrupt!");

        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowNotSupportedColor() => throw new NotSupportedException("Unsupported PNG color type");

        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowUnknownFilter() => throw new InvalidImageContentException("Unknown filter type.");
    }
}
