// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

using SixLabors.ImageSharp.ColorSpaces.Companding;

namespace SixLabors.ImageSharp.PixelFormats.Utils
{
    internal static partial class Vector4Converters
    {
        /// <summary>
        /// Apply modifiers used requested by ToVector4() conversion.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void ApplyForwardConversionModifiers(Span<Vector4> vectors, PixelConversionModifiers modifiers)
        {
            if (modifiers.IsDefined(PixelConversionModifiers.SRgbCompand))
            {
                SRgbCompanding.Expand(vectors);
            }

            if (modifiers.IsDefined(PixelConversionModifiers.Premultiply))
            {
                Vector4Utils.Premultiply(vectors);
            }
        }

        /// <summary>
        /// Apply modifiers used requested by FromVector4() conversion.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void ApplyBackwardConversionModifiers(Span<Vector4> vectors, PixelConversionModifiers modifiers)
        {
            if (modifiers.IsDefined(PixelConversionModifiers.Premultiply))
            {
                Vector4Utils.UnPremultiply(vectors);
            }

            if (modifiers.IsDefined(PixelConversionModifiers.SRgbCompand))
            {
                SRgbCompanding.Compress(vectors);
            }
        }
    }
}