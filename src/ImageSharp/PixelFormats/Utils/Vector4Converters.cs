// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

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
        [MethodImpl(InliningOptions.ShortMethod)]
        internal static void ApplyForwardConversionModifiers(Span<Vector4> vectors, PixelConversionModifiers modifiers)
        {
            if (modifiers.IsDefined(PixelConversionModifiers.SRgbCompand))
            {
                SRgbCompanding.Expand(vectors);
            }

            if (modifiers.IsDefined(PixelConversionModifiers.Premultiply))
            {
                Numerics.Premultiply(vectors);
            }
        }

        /// <summary>
        /// Apply modifiers used requested by FromVector4() conversion.
        /// </summary>
        [MethodImpl(InliningOptions.ShortMethod)]
        internal static void ApplyBackwardConversionModifiers(Span<Vector4> vectors, PixelConversionModifiers modifiers)
        {
            if (modifiers.IsDefined(PixelConversionModifiers.Premultiply))
            {
                Numerics.UnPremultiply(vectors);
            }

            if (modifiers.IsDefined(PixelConversionModifiers.SRgbCompand))
            {
                SRgbCompanding.Compress(vectors);
            }
        }
    }
}
