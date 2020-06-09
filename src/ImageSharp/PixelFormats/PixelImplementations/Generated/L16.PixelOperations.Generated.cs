﻿// Copyright (c) Six Labors.
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
    public partial struct L16
    {
        /// <summary>
        /// Provides optimized overrides for bulk operations.
        /// </summary>
        internal class PixelOperations : PixelOperations<L16>
        {
            /// <inheritdoc />
            public override void FromL16(Configuration configuration, ReadOnlySpan<L16> source, Span<L16> destinationPixels)
            {
                Guard.NotNull(configuration, nameof(configuration));
                Guard.DestinationShouldNotBeTooShort(source, destinationPixels, nameof(destinationPixels));

                source.CopyTo(destinationPixels);
            }

            /// <inheritdoc />
            public override void ToL16(Configuration configuration, ReadOnlySpan<L16> sourcePixels, Span<L16> destinationPixels)
            {
                Guard.NotNull(configuration, nameof(configuration));
                Guard.DestinationShouldNotBeTooShort(sourcePixels, destinationPixels, nameof(destinationPixels));

                sourcePixels.CopyTo(destinationPixels);
            }


            /// <inheritdoc />
            public override void ToArgb32(Configuration configuration, ReadOnlySpan<L16> sourcePixels, Span<Argb32> destinationPixels)
            {
                Guard.NotNull(configuration, nameof(configuration));
                Guard.DestinationShouldNotBeTooShort(sourcePixels, destinationPixels, nameof(destinationPixels));

                ref L16 sourceRef = ref MemoryMarshal.GetReference(sourcePixels);
                ref Argb32 destRef = ref MemoryMarshal.GetReference(destinationPixels);

                for (int i = 0; i < sourcePixels.Length; i++)
                {
                    ref L16 sp = ref Unsafe.Add(ref sourceRef, i);
                    ref Argb32 dp = ref Unsafe.Add(ref destRef, i);

                    dp.FromL16(sp);
                }
            }

            /// <inheritdoc />
            public override void ToBgr24(Configuration configuration, ReadOnlySpan<L16> sourcePixels, Span<Bgr24> destinationPixels)
            {
                Guard.NotNull(configuration, nameof(configuration));
                Guard.DestinationShouldNotBeTooShort(sourcePixels, destinationPixels, nameof(destinationPixels));

                ref L16 sourceRef = ref MemoryMarshal.GetReference(sourcePixels);
                ref Bgr24 destRef = ref MemoryMarshal.GetReference(destinationPixels);

                for (int i = 0; i < sourcePixels.Length; i++)
                {
                    ref L16 sp = ref Unsafe.Add(ref sourceRef, i);
                    ref Bgr24 dp = ref Unsafe.Add(ref destRef, i);

                    dp.FromL16(sp);
                }
            }

            /// <inheritdoc />
            public override void ToBgra32(Configuration configuration, ReadOnlySpan<L16> sourcePixels, Span<Bgra32> destinationPixels)
            {
                Guard.NotNull(configuration, nameof(configuration));
                Guard.DestinationShouldNotBeTooShort(sourcePixels, destinationPixels, nameof(destinationPixels));

                ref L16 sourceRef = ref MemoryMarshal.GetReference(sourcePixels);
                ref Bgra32 destRef = ref MemoryMarshal.GetReference(destinationPixels);

                for (int i = 0; i < sourcePixels.Length; i++)
                {
                    ref L16 sp = ref Unsafe.Add(ref sourceRef, i);
                    ref Bgra32 dp = ref Unsafe.Add(ref destRef, i);

                    dp.FromL16(sp);
                }
            }

            /// <inheritdoc />
            public override void ToL8(Configuration configuration, ReadOnlySpan<L16> sourcePixels, Span<L8> destinationPixels)
            {
                Guard.NotNull(configuration, nameof(configuration));
                Guard.DestinationShouldNotBeTooShort(sourcePixels, destinationPixels, nameof(destinationPixels));

                ref L16 sourceRef = ref MemoryMarshal.GetReference(sourcePixels);
                ref L8 destRef = ref MemoryMarshal.GetReference(destinationPixels);

                for (int i = 0; i < sourcePixels.Length; i++)
                {
                    ref L16 sp = ref Unsafe.Add(ref sourceRef, i);
                    ref L8 dp = ref Unsafe.Add(ref destRef, i);

                    dp.FromL16(sp);
                }
            }

            /// <inheritdoc />
            public override void ToLa16(Configuration configuration, ReadOnlySpan<L16> sourcePixels, Span<La16> destinationPixels)
            {
                Guard.NotNull(configuration, nameof(configuration));
                Guard.DestinationShouldNotBeTooShort(sourcePixels, destinationPixels, nameof(destinationPixels));

                ref L16 sourceRef = ref MemoryMarshal.GetReference(sourcePixels);
                ref La16 destRef = ref MemoryMarshal.GetReference(destinationPixels);

                for (int i = 0; i < sourcePixels.Length; i++)
                {
                    ref L16 sp = ref Unsafe.Add(ref sourceRef, i);
                    ref La16 dp = ref Unsafe.Add(ref destRef, i);

                    dp.FromL16(sp);
                }
            }

            /// <inheritdoc />
            public override void ToLa32(Configuration configuration, ReadOnlySpan<L16> sourcePixels, Span<La32> destinationPixels)
            {
                Guard.NotNull(configuration, nameof(configuration));
                Guard.DestinationShouldNotBeTooShort(sourcePixels, destinationPixels, nameof(destinationPixels));

                ref L16 sourceRef = ref MemoryMarshal.GetReference(sourcePixels);
                ref La32 destRef = ref MemoryMarshal.GetReference(destinationPixels);

                for (int i = 0; i < sourcePixels.Length; i++)
                {
                    ref L16 sp = ref Unsafe.Add(ref sourceRef, i);
                    ref La32 dp = ref Unsafe.Add(ref destRef, i);

                    dp.FromL16(sp);
                }
            }

            /// <inheritdoc />
            public override void ToRgb24(Configuration configuration, ReadOnlySpan<L16> sourcePixels, Span<Rgb24> destinationPixels)
            {
                Guard.NotNull(configuration, nameof(configuration));
                Guard.DestinationShouldNotBeTooShort(sourcePixels, destinationPixels, nameof(destinationPixels));

                ref L16 sourceRef = ref MemoryMarshal.GetReference(sourcePixels);
                ref Rgb24 destRef = ref MemoryMarshal.GetReference(destinationPixels);

                for (int i = 0; i < sourcePixels.Length; i++)
                {
                    ref L16 sp = ref Unsafe.Add(ref sourceRef, i);
                    ref Rgb24 dp = ref Unsafe.Add(ref destRef, i);

                    dp.FromL16(sp);
                }
            }

            /// <inheritdoc />
            public override void ToRgba32(Configuration configuration, ReadOnlySpan<L16> sourcePixels, Span<Rgba32> destinationPixels)
            {
                Guard.NotNull(configuration, nameof(configuration));
                Guard.DestinationShouldNotBeTooShort(sourcePixels, destinationPixels, nameof(destinationPixels));

                ref L16 sourceRef = ref MemoryMarshal.GetReference(sourcePixels);
                ref Rgba32 destRef = ref MemoryMarshal.GetReference(destinationPixels);

                for (int i = 0; i < sourcePixels.Length; i++)
                {
                    ref L16 sp = ref Unsafe.Add(ref sourceRef, i);
                    ref Rgba32 dp = ref Unsafe.Add(ref destRef, i);

                    dp.FromL16(sp);
                }
            }

            /// <inheritdoc />
            public override void ToRgb48(Configuration configuration, ReadOnlySpan<L16> sourcePixels, Span<Rgb48> destinationPixels)
            {
                Guard.NotNull(configuration, nameof(configuration));
                Guard.DestinationShouldNotBeTooShort(sourcePixels, destinationPixels, nameof(destinationPixels));

                ref L16 sourceRef = ref MemoryMarshal.GetReference(sourcePixels);
                ref Rgb48 destRef = ref MemoryMarshal.GetReference(destinationPixels);

                for (int i = 0; i < sourcePixels.Length; i++)
                {
                    ref L16 sp = ref Unsafe.Add(ref sourceRef, i);
                    ref Rgb48 dp = ref Unsafe.Add(ref destRef, i);

                    dp.FromL16(sp);
                }
            }

            /// <inheritdoc />
            public override void ToRgba64(Configuration configuration, ReadOnlySpan<L16> sourcePixels, Span<Rgba64> destinationPixels)
            {
                Guard.NotNull(configuration, nameof(configuration));
                Guard.DestinationShouldNotBeTooShort(sourcePixels, destinationPixels, nameof(destinationPixels));

                ref L16 sourceRef = ref MemoryMarshal.GetReference(sourcePixels);
                ref Rgba64 destRef = ref MemoryMarshal.GetReference(destinationPixels);

                for (int i = 0; i < sourcePixels.Length; i++)
                {
                    ref L16 sp = ref Unsafe.Add(ref sourceRef, i);
                    ref Rgba64 dp = ref Unsafe.Add(ref destRef, i);

                    dp.FromL16(sp);
                }
            }

            /// <inheritdoc />
            public override void ToBgra5551(Configuration configuration, ReadOnlySpan<L16> sourcePixels, Span<Bgra5551> destinationPixels)
            {
                Guard.NotNull(configuration, nameof(configuration));
                Guard.DestinationShouldNotBeTooShort(sourcePixels, destinationPixels, nameof(destinationPixels));

                ref L16 sourceRef = ref MemoryMarshal.GetReference(sourcePixels);
                ref Bgra5551 destRef = ref MemoryMarshal.GetReference(destinationPixels);

                for (int i = 0; i < sourcePixels.Length; i++)
                {
                    ref L16 sp = ref Unsafe.Add(ref sourceRef, i);
                    ref Bgra5551 dp = ref Unsafe.Add(ref destRef, i);

                    dp.FromL16(sp);
                }
            }
            /// <inheritdoc />
            public override void From<TSourcePixel>(
                Configuration configuration,
                ReadOnlySpan<TSourcePixel> sourcePixels,
                Span<L16> destinationPixels)
            {
                PixelOperations<TSourcePixel>.Instance.ToL16(configuration, sourcePixels, destinationPixels);
            }
        }
    }
}
