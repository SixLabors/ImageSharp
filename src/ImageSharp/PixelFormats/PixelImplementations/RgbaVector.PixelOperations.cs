// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using SixLabors.ImageSharp.PixelFormats.Utils;

namespace SixLabors.ImageSharp.PixelFormats
{
    /// <content>
    /// Provides optimized overrides for bulk operations.
    /// </content>
    public partial struct RgbaVector
    {
        /// <summary>
        /// <see cref="PixelOperations{TPixel}"/> implementation optimized for <see cref="RgbaVector"/>.
        /// </summary>
        internal class PixelOperations : PixelOperations<RgbaVector>
        {
            /// <inheritdoc />
            internal override void FromVector4Destructive(
                Configuration configuration,
                Span<Vector4> sourceVectors,
                Span<RgbaVector> destinationColors,
                PixelConversionModifiers modifiers)
            {
                Guard.DestinationShouldNotBeTooShort(sourceVectors, destinationColors, nameof(destinationColors));

                Vector4Converters.ApplyBackwardConversionModifiers(sourceVectors, modifiers);
                MemoryMarshal.Cast<Vector4, RgbaVector>(sourceVectors).CopyTo(destinationColors);
            }

            /// <inheritdoc />
            internal override void ToVector4(
                Configuration configuration,
                ReadOnlySpan<RgbaVector> sourcePixels,
                Span<Vector4> destVectors,
                PixelConversionModifiers modifiers)
            {
                Guard.DestinationShouldNotBeTooShort(sourcePixels, destVectors, nameof(destVectors));

                MemoryMarshal.Cast<RgbaVector, Vector4>(sourcePixels).CopyTo(destVectors);
                Vector4Converters.ApplyForwardConversionModifiers(destVectors, modifiers);
            }

            internal override void ToGray8(Configuration configuration, ReadOnlySpan<RgbaVector> sourcePixels, Span<Gray8> destPixels)
            {
                Guard.DestinationShouldNotBeTooShort(sourcePixels, destPixels, nameof(destPixels));

                ref Vector4 sourceBaseRef = ref Unsafe.As<RgbaVector, Vector4>(ref MemoryMarshal.GetReference(sourcePixels));
                ref Gray8 destBaseRef = ref MemoryMarshal.GetReference(destPixels);

                for (int i = 0; i < sourcePixels.Length; i++)
                {
                    ref Vector4 sp = ref Unsafe.Add(ref sourceBaseRef, i);
                    ref Gray8 dp = ref Unsafe.Add(ref destBaseRef, i);

                    dp.ConvertFromRgbaScaledVector4(sp);
                }
            }

            internal override void ToGray16(Configuration configuration, ReadOnlySpan<RgbaVector> sourcePixels, Span<Gray16> destPixels)
            {
                Guard.DestinationShouldNotBeTooShort(sourcePixels, destPixels, nameof(destPixels));

                ref Vector4 sourceBaseRef = ref Unsafe.As<RgbaVector, Vector4>(ref MemoryMarshal.GetReference(sourcePixels));
                ref Gray16 destBaseRef = ref MemoryMarshal.GetReference(destPixels);

                for (int i = 0; i < sourcePixels.Length; i++)
                {
                    ref Vector4 sp = ref Unsafe.Add(ref sourceBaseRef, i);
                    ref Gray16 dp = ref Unsafe.Add(ref destBaseRef, i);

                    dp.ConvertFromRgbaScaledVector4(sp);
                }
            }
        }
    }
}
