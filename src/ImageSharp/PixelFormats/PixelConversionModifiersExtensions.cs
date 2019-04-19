// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.PixelFormats
{
    internal static class PixelConversionModifiersExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsDefined(this PixelConversionModifiers modifiers, PixelConversionModifiers expected) =>
            (modifiers & expected) == expected;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PixelConversionModifiers Remove(
            this PixelConversionModifiers modifiers,
            PixelConversionModifiers removeThis) =>
            modifiers & ~removeThis;
    }
}