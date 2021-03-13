// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using SixLabors.ImageSharp.PixelFormats;

using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats
{
    [Trait("Category", "PixelFormats")]
    public abstract partial class PixelConverterTests
    {
        public static class ReferenceImplementations
        {
            public static byte[] MakeRgba32ByteArray(byte r, byte g, byte b, byte a)
            {
                var buffer = new byte[256];

                for (int i = 0; i < buffer.Length; i += 4)
                {
                    buffer[i] = r;
                    buffer[i + 1] = g;
                    buffer[i + 2] = b;
                    buffer[i + 3] = a;
                }

                return buffer;
            }

            public static byte[] MakeArgb32ByteArray(byte r, byte g, byte b, byte a)
            {
                var buffer = new byte[256];

                for (int i = 0; i < buffer.Length; i += 4)
                {
                    buffer[i] = a;
                    buffer[i + 1] = r;
                    buffer[i + 2] = g;
                    buffer[i + 3] = b;
                }

                return buffer;
            }

            public static byte[] MakeBgra32ByteArray(byte r, byte g, byte b, byte a)
            {
                var buffer = new byte[256];

                for (int i = 0; i < buffer.Length; i += 4)
                {
                    buffer[i] = b;
                    buffer[i + 1] = g;
                    buffer[i + 2] = r;
                    buffer[i + 3] = a;
                }

                return buffer;
            }

            internal static void To<TSourcePixel, TDestinationPixel>(
                Configuration configuration,
                ReadOnlySpan<TSourcePixel> sourcePixels,
                Span<TDestinationPixel> destinationPixels)
                where TSourcePixel : unmanaged, IPixel<TSourcePixel>
                where TDestinationPixel : unmanaged, IPixel<TDestinationPixel>
            {
                Guard.NotNull(configuration, nameof(configuration));
                Guard.DestinationShouldNotBeTooShort(sourcePixels, destinationPixels, nameof(destinationPixels));

                int count = sourcePixels.Length;
                ref TSourcePixel sourceRef = ref MemoryMarshal.GetReference(sourcePixels);

                if (typeof(TSourcePixel) == typeof(TDestinationPixel))
                {
                    Span<TSourcePixel> uniformDest =
                        MemoryMarshal.Cast<TDestinationPixel, TSourcePixel>(destinationPixels);
                    sourcePixels.CopyTo(uniformDest);
                    return;
                }

                // L8 and L16 are special implementations of IPixel in that they do not conform to the
                // standard RGBA colorspace format and must be converted from RGBA using the special ITU BT709 algorithm.
                // One of the requirements of FromScaledVector4/ToScaledVector4 is that it unaware of this and
                // packs/unpacks the pixel without and conversion so we employ custom methods do do this.
                if (typeof(TDestinationPixel) == typeof(L16))
                {
                    ref L16 l16Ref = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<TDestinationPixel, L16>(destinationPixels));
                    for (int i = 0; i < count; i++)
                    {
                        ref TSourcePixel sp = ref Unsafe.Add(ref sourceRef, i);
                        ref L16 dp = ref Unsafe.Add(ref l16Ref, i);
                        dp.ConvertFromRgbaScaledVector4(sp.ToScaledVector4());
                    }

                    return;
                }

                if (typeof(TDestinationPixel) == typeof(L8))
                {
                    ref L8 l8Ref = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<TDestinationPixel, L8>(destinationPixels));
                    for (int i = 0; i < count; i++)
                    {
                        ref TSourcePixel sp = ref Unsafe.Add(ref sourceRef, i);
                        ref L8 dp = ref Unsafe.Add(ref l8Ref, i);
                        dp.ConvertFromRgbaScaledVector4(sp.ToScaledVector4());
                    }

                    return;
                }

                // Normal conversion
                ref TDestinationPixel destRef = ref MemoryMarshal.GetReference(destinationPixels);
                for (int i = 0; i < count; i++)
                {
                    ref TSourcePixel sp = ref Unsafe.Add(ref sourceRef, i);
                    ref TDestinationPixel dp = ref Unsafe.Add(ref destRef, i);
                    dp.FromScaledVector4(sp.ToScaledVector4());
                }
            }
        }
    }
}
