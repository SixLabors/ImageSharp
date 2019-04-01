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
    public partial struct Rgb24
    {
        /// <summary>
        /// Provides optimized overrides for bulk operations.
        /// </summary>
        internal class PixelOperations : PixelOperations<Rgb24>
        {
            /// <inheritdoc />
            internal override void FromRgb24(Configuration configuration, ReadOnlySpan<Rgb24> source, Span<Rgb24> destPixels)
            {
                Guard.NotNull(configuration, nameof(configuration));
                Guard.DestinationShouldNotBeTooShort(source, destPixels, nameof(destPixels));

                source.CopyTo(destPixels);
            }

            /// <inheritdoc />
            internal override void ToRgb24(Configuration configuration, ReadOnlySpan<Rgb24> sourcePixels, Span<Rgb24> destPixels)
            {
                Guard.NotNull(configuration, nameof(configuration));
                Guard.DestinationShouldNotBeTooShort(sourcePixels, destPixels, nameof(destPixels));

                sourcePixels.CopyTo(destPixels);
            }

            /// <inheritdoc />
            internal override void FromVector4Destructive(Configuration configuration, Span<Vector4> sourceVectors, Span<Rgb24> destPixels, PixelConversionModifiers modifiers)
            {
                Vector4Converters.RgbaCompatible.FromVector4(configuration, this, sourceVectors, destPixels, modifiers.Remove(PixelConversionModifiers.Scale | PixelConversionModifiers.Premultiply));
            }

            /// <inheritdoc />
            internal override void ToVector4(Configuration configuration, ReadOnlySpan<Rgb24> sourcePixels, Span<Vector4> destVectors, PixelConversionModifiers modifiers)
            {
                Vector4Converters.RgbaCompatible.ToVector4(configuration, this, sourcePixels, destVectors, modifiers.Remove(PixelConversionModifiers.Scale | PixelConversionModifiers.Premultiply));
            }

            /// <inheritdoc />
            internal override void ToArgb32(Configuration configuration, ReadOnlySpan<Rgb24> sourcePixels, Span<Argb32> destPixels)
            {
                Guard.NotNull(configuration, nameof(configuration));
                Guard.DestinationShouldNotBeTooShort(sourcePixels, destPixels, nameof(destPixels));

                ref Rgb24 sourceRef = ref MemoryMarshal.GetReference(sourcePixels);
                ref Argb32 destRef = ref MemoryMarshal.GetReference(destPixels);

                for (int i = 0; i < sourcePixels.Length; i++)
                {
                    ref Rgb24 sp = ref Unsafe.Add(ref sourceRef, i);
                    ref Argb32 dp = ref Unsafe.Add(ref destRef, i);

                    dp.FromRgb24(sp);
                }
            }

            /// <inheritdoc />
            internal override void ToBgr24(Configuration configuration, ReadOnlySpan<Rgb24> sourcePixels, Span<Bgr24> destPixels)
            {
                Guard.NotNull(configuration, nameof(configuration));
                Guard.DestinationShouldNotBeTooShort(sourcePixels, destPixels, nameof(destPixels));

                ref Rgb24 sourceRef = ref MemoryMarshal.GetReference(sourcePixels);
                ref Bgr24 destRef = ref MemoryMarshal.GetReference(destPixels);

                for (int i = 0; i < sourcePixels.Length; i++)
                {
                    ref Rgb24 sp = ref Unsafe.Add(ref sourceRef, i);
                    ref Bgr24 dp = ref Unsafe.Add(ref destRef, i);

                    dp.FromRgb24(sp);
                }
            }

            /// <inheritdoc />
            internal override void ToBgra32(Configuration configuration, ReadOnlySpan<Rgb24> sourcePixels, Span<Bgra32> destPixels)
            {
                Guard.NotNull(configuration, nameof(configuration));
                Guard.DestinationShouldNotBeTooShort(sourcePixels, destPixels, nameof(destPixels));

                ref Rgb24 sourceRef = ref MemoryMarshal.GetReference(sourcePixels);
                ref Bgra32 destRef = ref MemoryMarshal.GetReference(destPixels);

                for (int i = 0; i < sourcePixels.Length; i++)
                {
                    ref Rgb24 sp = ref Unsafe.Add(ref sourceRef, i);
                    ref Bgra32 dp = ref Unsafe.Add(ref destRef, i);

                    dp.FromRgb24(sp);
                }
            }

            /// <inheritdoc />
            internal override void ToGray8(Configuration configuration, ReadOnlySpan<Rgb24> sourcePixels, Span<Gray8> destPixels)
            {
                Guard.NotNull(configuration, nameof(configuration));
                Guard.DestinationShouldNotBeTooShort(sourcePixels, destPixels, nameof(destPixels));

                ref Rgb24 sourceRef = ref MemoryMarshal.GetReference(sourcePixels);
                ref Gray8 destRef = ref MemoryMarshal.GetReference(destPixels);

                for (int i = 0; i < sourcePixels.Length; i++)
                {
                    ref Rgb24 sp = ref Unsafe.Add(ref sourceRef, i);
                    ref Gray8 dp = ref Unsafe.Add(ref destRef, i);

                    dp.FromRgb24(sp);
                }
            }

            /// <inheritdoc />
            internal override void ToGray16(Configuration configuration, ReadOnlySpan<Rgb24> sourcePixels, Span<Gray16> destPixels)
            {
                Guard.NotNull(configuration, nameof(configuration));
                Guard.DestinationShouldNotBeTooShort(sourcePixels, destPixels, nameof(destPixels));

                ref Rgb24 sourceRef = ref MemoryMarshal.GetReference(sourcePixels);
                ref Gray16 destRef = ref MemoryMarshal.GetReference(destPixels);

                for (int i = 0; i < sourcePixels.Length; i++)
                {
                    ref Rgb24 sp = ref Unsafe.Add(ref sourceRef, i);
                    ref Gray16 dp = ref Unsafe.Add(ref destRef, i);

                    dp.FromRgb24(sp);
                }
            }

            /// <inheritdoc />
            internal override void ToRgba32(Configuration configuration, ReadOnlySpan<Rgb24> sourcePixels, Span<Rgba32> destPixels)
            {
                Guard.NotNull(configuration, nameof(configuration));
                Guard.DestinationShouldNotBeTooShort(sourcePixels, destPixels, nameof(destPixels));

                ref Rgb24 sourceRef = ref MemoryMarshal.GetReference(sourcePixels);
                ref Rgba32 destRef = ref MemoryMarshal.GetReference(destPixels);

                for (int i = 0; i < sourcePixels.Length; i++)
                {
                    ref Rgb24 sp = ref Unsafe.Add(ref sourceRef, i);
                    ref Rgba32 dp = ref Unsafe.Add(ref destRef, i);

                    dp.FromRgb24(sp);
                }
            }

            /// <inheritdoc />
            internal override void ToRgb48(Configuration configuration, ReadOnlySpan<Rgb24> sourcePixels, Span<Rgb48> destPixels)
            {
                Guard.NotNull(configuration, nameof(configuration));
                Guard.DestinationShouldNotBeTooShort(sourcePixels, destPixels, nameof(destPixels));

                ref Rgb24 sourceRef = ref MemoryMarshal.GetReference(sourcePixels);
                ref Rgb48 destRef = ref MemoryMarshal.GetReference(destPixels);

                for (int i = 0; i < sourcePixels.Length; i++)
                {
                    ref Rgb24 sp = ref Unsafe.Add(ref sourceRef, i);
                    ref Rgb48 dp = ref Unsafe.Add(ref destRef, i);

                    dp.FromRgb24(sp);
                }
            }

            /// <inheritdoc />
            internal override void ToRgba64(Configuration configuration, ReadOnlySpan<Rgb24> sourcePixels, Span<Rgba64> destPixels)
            {
                Guard.NotNull(configuration, nameof(configuration));
                Guard.DestinationShouldNotBeTooShort(sourcePixels, destPixels, nameof(destPixels));

                ref Rgb24 sourceRef = ref MemoryMarshal.GetReference(sourcePixels);
                ref Rgba64 destRef = ref MemoryMarshal.GetReference(destPixels);

                for (int i = 0; i < sourcePixels.Length; i++)
                {
                    ref Rgb24 sp = ref Unsafe.Add(ref sourceRef, i);
                    ref Rgba64 dp = ref Unsafe.Add(ref destRef, i);

                    dp.FromRgb24(sp);
                }
            }
        }
    }
}