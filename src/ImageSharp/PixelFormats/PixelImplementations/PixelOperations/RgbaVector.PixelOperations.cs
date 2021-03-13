// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats;
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
            private static readonly Lazy<PixelTypeInfo> LazyInfo =
                new Lazy<PixelTypeInfo>(() => PixelTypeInfo.Create<RgbaVector>(PixelAlphaRepresentation.Unassociated), true);

            /// <inheritdoc />
            public override PixelTypeInfo GetPixelTypeInfo() => LazyInfo.Value;

            /// <inheritdoc />
            public override void From<TSourcePixel>(
                Configuration configuration,
                ReadOnlySpan<TSourcePixel> sourcePixels,
                Span<RgbaVector> destinationPixels)
            {
                Span<Vector4> destinationVectors = MemoryMarshal.Cast<RgbaVector, Vector4>(destinationPixels);

                PixelOperations<TSourcePixel>.Instance.ToVector4(configuration, sourcePixels, destinationVectors);
            }

            /// <inheritdoc />
            public override void FromVector4Destructive(
                Configuration configuration,
                Span<Vector4> sourceVectors,
                Span<RgbaVector> destinationPixels,
                PixelConversionModifiers modifiers)
            {
                Guard.DestinationShouldNotBeTooShort(sourceVectors, destinationPixels, nameof(destinationPixels));

                Vector4Converters.ApplyBackwardConversionModifiers(sourceVectors, modifiers);
                MemoryMarshal.Cast<Vector4, RgbaVector>(sourceVectors).CopyTo(destinationPixels);
            }

            /// <inheritdoc />
            public override void ToVector4(
                Configuration configuration,
                ReadOnlySpan<RgbaVector> sourcePixels,
                Span<Vector4> destinationVectors,
                PixelConversionModifiers modifiers)
            {
                Guard.DestinationShouldNotBeTooShort(sourcePixels, destinationVectors, nameof(destinationVectors));

                MemoryMarshal.Cast<RgbaVector, Vector4>(sourcePixels).CopyTo(destinationVectors);
                Vector4Converters.ApplyForwardConversionModifiers(destinationVectors, modifiers);
            }

            public override void ToL8(
                Configuration configuration,
                ReadOnlySpan<RgbaVector> sourcePixels,
                Span<L8> destinationPixels)
            {
                Guard.DestinationShouldNotBeTooShort(sourcePixels, destinationPixels, nameof(destinationPixels));

                ref Vector4 sourceBaseRef = ref Unsafe.As<RgbaVector, Vector4>(ref MemoryMarshal.GetReference(sourcePixels));
                ref L8 destBaseRef = ref MemoryMarshal.GetReference(destinationPixels);

                for (int i = 0; i < sourcePixels.Length; i++)
                {
                    ref Vector4 sp = ref Unsafe.Add(ref sourceBaseRef, i);
                    ref L8 dp = ref Unsafe.Add(ref destBaseRef, i);

                    dp.ConvertFromRgbaScaledVector4(sp);
                }
            }

            public override void ToL16(
                Configuration configuration,
                ReadOnlySpan<RgbaVector> sourcePixels,
                Span<L16> destinationPixels)
            {
                Guard.DestinationShouldNotBeTooShort(sourcePixels, destinationPixels, nameof(destinationPixels));

                ref Vector4 sourceBaseRef = ref Unsafe.As<RgbaVector, Vector4>(ref MemoryMarshal.GetReference(sourcePixels));
                ref L16 destBaseRef = ref MemoryMarshal.GetReference(destinationPixels);

                for (int i = 0; i < sourcePixels.Length; i++)
                {
                    ref Vector4 sp = ref Unsafe.Add(ref sourceBaseRef, i);
                    ref L16 dp = ref Unsafe.Add(ref destBaseRef, i);

                    dp.ConvertFromRgbaScaledVector4(sp);
                }
            }
        }
    }
}
