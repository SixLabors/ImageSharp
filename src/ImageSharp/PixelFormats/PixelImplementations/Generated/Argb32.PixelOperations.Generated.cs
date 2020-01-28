﻿// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

// <auto-generated />

using SixLabors.ImageSharp.PixelFormats.Utils;
using System;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;


namespace SixLabors.ImageSharp.PixelFormats
{
    /// <content>
    /// Provides optimized overrides for bulk operations.
    /// </content>
    public partial struct Argb32
    {
        /// <summary>
        /// Provides optimized overrides for bulk operations.
        /// </summary>
        internal class PixelOperations : PixelOperations<Argb32>
        {
            /// <inheritdoc />
            public override void FromArgb32(Configuration configuration, ReadOnlySpan<Argb32> source, Span<Argb32> destinationPixels)
            {
                Guard.NotNull(configuration, nameof(configuration));
                Guard.DestinationShouldNotBeTooShort(source, destinationPixels, nameof(destinationPixels));

                source.CopyTo(destinationPixels);
            }

            /// <inheritdoc />
            public override void ToArgb32(Configuration configuration, ReadOnlySpan<Argb32> sourcePixels, Span<Argb32> destinationPixels)
            {
                Guard.NotNull(configuration, nameof(configuration));
                Guard.DestinationShouldNotBeTooShort(sourcePixels, destinationPixels, nameof(destinationPixels));

                sourcePixels.CopyTo(destinationPixels);
            }

            /// <inheritdoc />
            public override void FromVector4Destructive(Configuration configuration, Span<Vector4> sourceVectors, Span<Argb32> destinationPixels, PixelConversionModifiers modifiers)
            {
                Vector4Converters.RgbaCompatible.FromVector4(configuration, this, sourceVectors, destinationPixels, modifiers.Remove(PixelConversionModifiers.Scale));
            }

            /// <inheritdoc />
            public override void ToVector4(Configuration configuration, ReadOnlySpan<Argb32> sourcePixels, Span<Vector4> destVectors, PixelConversionModifiers modifiers)
            {
                Vector4Converters.RgbaCompatible.ToVector4(configuration, this, sourcePixels, destVectors, modifiers.Remove(PixelConversionModifiers.Scale));
            }
            /// <inheritdoc />
            public override void ToRgba32(Configuration configuration, ReadOnlySpan<Argb32> sourcePixels, Span<Rgba32> destinationPixels)
            {
                Guard.NotNull(configuration, nameof(configuration));
                Guard.DestinationShouldNotBeTooShort(sourcePixels, destinationPixels, nameof(destinationPixels));

                ref uint sourceRef = ref Unsafe.As<Argb32,uint>(ref MemoryMarshal.GetReference(sourcePixels));
                ref uint destRef = ref Unsafe.As<Rgba32, uint>(ref MemoryMarshal.GetReference(destinationPixels));

                for (int i = 0; i < sourcePixels.Length; i++)
                {
                    uint sp = Unsafe.Add(ref sourceRef, i);
                    Unsafe.Add(ref destRef, i) = PixelConverter.FromArgb32.ToRgba32(sp);
                }
            }

            /// <inheritdoc />
            public override void FromRgba32(Configuration configuration, ReadOnlySpan<Rgba32> sourcePixels, Span<Argb32> destinationPixels)
            {
                Guard.NotNull(configuration, nameof(configuration));
                Guard.DestinationShouldNotBeTooShort(sourcePixels, destinationPixels, nameof(destinationPixels));

                ref uint sourceRef = ref Unsafe.As<Rgba32,uint>(ref MemoryMarshal.GetReference(sourcePixels));
                ref uint destRef = ref Unsafe.As<Argb32, uint>(ref MemoryMarshal.GetReference(destinationPixels));

                for (int i = 0; i < sourcePixels.Length; i++)
                {
                    uint sp = Unsafe.Add(ref sourceRef, i);
                    Unsafe.Add(ref destRef, i) = PixelConverter.FromRgba32.ToArgb32(sp);
                }
            }
            /// <inheritdoc />
            public override void ToBgra32(Configuration configuration, ReadOnlySpan<Argb32> sourcePixels, Span<Bgra32> destinationPixels)
            {
                Guard.NotNull(configuration, nameof(configuration));
                Guard.DestinationShouldNotBeTooShort(sourcePixels, destinationPixels, nameof(destinationPixels));

                ref uint sourceRef = ref Unsafe.As<Argb32,uint>(ref MemoryMarshal.GetReference(sourcePixels));
                ref uint destRef = ref Unsafe.As<Bgra32, uint>(ref MemoryMarshal.GetReference(destinationPixels));

                for (int i = 0; i < sourcePixels.Length; i++)
                {
                    uint sp = Unsafe.Add(ref sourceRef, i);
                    Unsafe.Add(ref destRef, i) = PixelConverter.FromArgb32.ToBgra32(sp);
                }
            }

            /// <inheritdoc />
            public override void FromBgra32(Configuration configuration, ReadOnlySpan<Bgra32> sourcePixels, Span<Argb32> destinationPixels)
            {
                Guard.NotNull(configuration, nameof(configuration));
                Guard.DestinationShouldNotBeTooShort(sourcePixels, destinationPixels, nameof(destinationPixels));

                ref uint sourceRef = ref Unsafe.As<Bgra32,uint>(ref MemoryMarshal.GetReference(sourcePixels));
                ref uint destRef = ref Unsafe.As<Argb32, uint>(ref MemoryMarshal.GetReference(destinationPixels));

                for (int i = 0; i < sourcePixels.Length; i++)
                {
                    uint sp = Unsafe.Add(ref sourceRef, i);
                    Unsafe.Add(ref destRef, i) = PixelConverter.FromBgra32.ToArgb32(sp);
                }
            }

            /// <inheritdoc />
            public override void ToBgr24(Configuration configuration, ReadOnlySpan<Argb32> sourcePixels, Span<Bgr24> destinationPixels)
            {
                Guard.NotNull(configuration, nameof(configuration));
                Guard.DestinationShouldNotBeTooShort(sourcePixels, destinationPixels, nameof(destinationPixels));

                ref Argb32 sourceRef = ref MemoryMarshal.GetReference(sourcePixels);
                ref Bgr24 destRef = ref MemoryMarshal.GetReference(destinationPixels);

                for (int i = 0; i < sourcePixels.Length; i++)
                {
                    ref Argb32 sp = ref Unsafe.Add(ref sourceRef, i);
                    ref Bgr24 dp = ref Unsafe.Add(ref destRef, i);

                    dp.FromArgb32(sp);
                }
            }

            /// <inheritdoc />
            public override void ToL8(Configuration configuration, ReadOnlySpan<Argb32> sourcePixels, Span<L8> destinationPixels)
            {
                Guard.NotNull(configuration, nameof(configuration));
                Guard.DestinationShouldNotBeTooShort(sourcePixels, destinationPixels, nameof(destinationPixels));

                ref Argb32 sourceRef = ref MemoryMarshal.GetReference(sourcePixels);
                ref L8 destRef = ref MemoryMarshal.GetReference(destinationPixels);

                for (int i = 0; i < sourcePixels.Length; i++)
                {
                    ref Argb32 sp = ref Unsafe.Add(ref sourceRef, i);
                    ref L8 dp = ref Unsafe.Add(ref destRef, i);

                    dp.FromArgb32(sp);
                }
            }

            /// <inheritdoc />
            public override void ToL16(Configuration configuration, ReadOnlySpan<Argb32> sourcePixels, Span<L16> destinationPixels)
            {
                Guard.NotNull(configuration, nameof(configuration));
                Guard.DestinationShouldNotBeTooShort(sourcePixels, destinationPixels, nameof(destinationPixels));

                ref Argb32 sourceRef = ref MemoryMarshal.GetReference(sourcePixels);
                ref L16 destRef = ref MemoryMarshal.GetReference(destinationPixels);

                for (int i = 0; i < sourcePixels.Length; i++)
                {
                    ref Argb32 sp = ref Unsafe.Add(ref sourceRef, i);
                    ref L16 dp = ref Unsafe.Add(ref destRef, i);

                    dp.FromArgb32(sp);
                }
            }

            /// <inheritdoc />
            public override void ToLa16(Configuration configuration, ReadOnlySpan<Argb32> sourcePixels, Span<La16> destinationPixels)
            {
                Guard.NotNull(configuration, nameof(configuration));
                Guard.DestinationShouldNotBeTooShort(sourcePixels, destinationPixels, nameof(destinationPixels));

                ref Argb32 sourceRef = ref MemoryMarshal.GetReference(sourcePixels);
                ref La16 destRef = ref MemoryMarshal.GetReference(destinationPixels);

                for (int i = 0; i < sourcePixels.Length; i++)
                {
                    ref Argb32 sp = ref Unsafe.Add(ref sourceRef, i);
                    ref La16 dp = ref Unsafe.Add(ref destRef, i);

                    dp.FromArgb32(sp);
                }
            }

            /// <inheritdoc />
            public override void ToLa32(Configuration configuration, ReadOnlySpan<Argb32> sourcePixels, Span<La32> destinationPixels)
            {
                Guard.NotNull(configuration, nameof(configuration));
                Guard.DestinationShouldNotBeTooShort(sourcePixels, destinationPixels, nameof(destinationPixels));

                ref Argb32 sourceRef = ref MemoryMarshal.GetReference(sourcePixels);
                ref La32 destRef = ref MemoryMarshal.GetReference(destinationPixels);

                for (int i = 0; i < sourcePixels.Length; i++)
                {
                    ref Argb32 sp = ref Unsafe.Add(ref sourceRef, i);
                    ref La32 dp = ref Unsafe.Add(ref destRef, i);

                    dp.FromArgb32(sp);
                }
            }

            /// <inheritdoc />
            public override void ToRgb24(Configuration configuration, ReadOnlySpan<Argb32> sourcePixels, Span<Rgb24> destinationPixels)
            {
                Guard.NotNull(configuration, nameof(configuration));
                Guard.DestinationShouldNotBeTooShort(sourcePixels, destinationPixels, nameof(destinationPixels));

                ref Argb32 sourceRef = ref MemoryMarshal.GetReference(sourcePixels);
                ref Rgb24 destRef = ref MemoryMarshal.GetReference(destinationPixels);

                for (int i = 0; i < sourcePixels.Length; i++)
                {
                    ref Argb32 sp = ref Unsafe.Add(ref sourceRef, i);
                    ref Rgb24 dp = ref Unsafe.Add(ref destRef, i);

                    dp.FromArgb32(sp);
                }
            }

            /// <inheritdoc />
            public override void ToRgb48(Configuration configuration, ReadOnlySpan<Argb32> sourcePixels, Span<Rgb48> destinationPixels)
            {
                Guard.NotNull(configuration, nameof(configuration));
                Guard.DestinationShouldNotBeTooShort(sourcePixels, destinationPixels, nameof(destinationPixels));

                ref Argb32 sourceRef = ref MemoryMarshal.GetReference(sourcePixels);
                ref Rgb48 destRef = ref MemoryMarshal.GetReference(destinationPixels);

                for (int i = 0; i < sourcePixels.Length; i++)
                {
                    ref Argb32 sp = ref Unsafe.Add(ref sourceRef, i);
                    ref Rgb48 dp = ref Unsafe.Add(ref destRef, i);

                    dp.FromArgb32(sp);
                }
            }

            /// <inheritdoc />
            public override void ToRgba64(Configuration configuration, ReadOnlySpan<Argb32> sourcePixels, Span<Rgba64> destinationPixels)
            {
                Guard.NotNull(configuration, nameof(configuration));
                Guard.DestinationShouldNotBeTooShort(sourcePixels, destinationPixels, nameof(destinationPixels));

                ref Argb32 sourceRef = ref MemoryMarshal.GetReference(sourcePixels);
                ref Rgba64 destRef = ref MemoryMarshal.GetReference(destinationPixels);

                for (int i = 0; i < sourcePixels.Length; i++)
                {
                    ref Argb32 sp = ref Unsafe.Add(ref sourceRef, i);
                    ref Rgba64 dp = ref Unsafe.Add(ref destRef, i);

                    dp.FromArgb32(sp);
                }
            }

            /// <inheritdoc />
            public override void ToBgra5551(Configuration configuration, ReadOnlySpan<Argb32> sourcePixels, Span<Bgra5551> destinationPixels)
            {
                Guard.NotNull(configuration, nameof(configuration));
                Guard.DestinationShouldNotBeTooShort(sourcePixels, destinationPixels, nameof(destinationPixels));

                ref Argb32 sourceRef = ref MemoryMarshal.GetReference(sourcePixels);
                ref Bgra5551 destRef = ref MemoryMarshal.GetReference(destinationPixels);

                for (int i = 0; i < sourcePixels.Length; i++)
                {
                    ref Argb32 sp = ref Unsafe.Add(ref sourceRef, i);
                    ref Bgra5551 dp = ref Unsafe.Add(ref destRef, i);

                    dp.FromArgb32(sp);
                }
            }
            /// <inheritdoc />
            public override void From<TSourcePixel>(
                Configuration configuration,
                ReadOnlySpan<TSourcePixel> sourcePixels,
                Span<Argb32> destinationPixels)
            {
                PixelOperations<TSourcePixel>.Instance.ToArgb32(configuration, sourcePixels, destinationPixels);
            }
        }
    }
}
