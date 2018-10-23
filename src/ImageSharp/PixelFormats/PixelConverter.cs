// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.PixelFormats
{
    /// <summary>
    /// Contains optimized implementations for conversion between pixel formats.
    /// </summary>
    /// <remarks>
    /// Implementations are based on ideas in:
    /// https://github.com/dotnet/coreclr/blob/master/src/System.Private.CoreLib/shared/System/Buffers/Binary/Reader.cs#L84
    /// The JIT should be able to detect and optimize ROL and ROR patterns.
    /// </remarks>
    internal static class PixelConverter
    {
        public static class Rgba32
        {
            /// <summary>
            /// Converts a packed <see cref="PixelFormats.Rgba32"/> to <see cref="Argb32"/>.
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            public static uint ToArgb32(uint packedRgba)
            {
                // packedRgba = [aa bb gg rr]
                // ROL(8, packedRgba):
                return (packedRgba << 8) | (packedRgba >> 24);
            }
        }

        public static class Argb32
        {
            /// <summary>
            /// Converts a packed <see cref="Argb32"/> to <see cref="PixelFormats.Rgba32"/>.
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            public static uint ToRgba32(uint packedArgb)
            {
                // packedArgb = [bb gg rr aa]
                // ROR(8, packedArgb):
                return (packedArgb >> 8) | (packedArgb << 24);
            }
        }
    }
}