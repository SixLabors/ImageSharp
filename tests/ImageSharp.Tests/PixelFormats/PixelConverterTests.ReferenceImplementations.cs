// // Copyright (c) Six Labors and contributors.
// // Licensed under the Apache License, Version 2.0.

// // Copyright (c) Six Labors and contributors.
// // Licensed under the Apache License, Version 2.0.

// // Copyright (c) Six Labors and contributors.
// // Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.PixelFormats
{
    public abstract partial class PixelConverterTests
    {
        public static class ReferenceImplementations
        {
            public static Rgba32 MakeRgba32(byte r, byte g, byte b, byte a)
            {
                Rgba32 d = default;
                d.R = r;
                d.G = g;
                d.B = b;
                d.A = a;
                return d;
            }

            public static Argb32 MakeArgb32(byte r, byte g, byte b, byte a)
            {
                Argb32 d = default;
                d.R = r;
                d.G = g;
                d.B = b;
                d.A = a;
                return d;
            }

            public static Bgra32 MakeBgra32(byte r, byte g, byte b, byte a)
            {
                Bgra32 d = default;
                d.R = r;
                d.G = g;
                d.B = b;
                d.A = a;
                return d;
            }

            internal static void To<TSourcePixel, TDestinationPixel>(
                Configuration configuration,
                ReadOnlySpan<TSourcePixel> sourcePixels,
                Span<TDestinationPixel> destinationPixels)
                where TSourcePixel : struct, IPixel<TSourcePixel> where TDestinationPixel : struct, IPixel<TDestinationPixel>
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

                // Gray8 and Gray16 are special implementations of IPixel in that they do not conform to the
                // standard RGBA colorspace format and must be converted from RGBA using the special ITU BT709 algorithm.
                // One of the requirements of FromScaledVector4/ToScaledVector4 is that it unaware of this and
                // packs/unpacks the pixel without and conversion so we employ custom methods do do this.
                if (typeof(TDestinationPixel) == typeof(Gray16))
                {
                    ref Gray16 gray16Ref = ref MemoryMarshal.GetReference(
                                               MemoryMarshal.Cast<TDestinationPixel, Gray16>(destinationPixels));
                    for (int i = 0; i < count; i++)
                    {
                        ref TSourcePixel sp = ref Unsafe.Add(ref sourceRef, i);
                        ref Gray16 dp = ref Unsafe.Add(ref gray16Ref, i);
                        dp.ConvertFromRgbaScaledVector4(sp.ToScaledVector4());
                    }

                    return;
                }

                if (typeof(TDestinationPixel) == typeof(Gray8))
                {
                    ref Gray8 gray8Ref = ref MemoryMarshal.GetReference(
                                             MemoryMarshal.Cast<TDestinationPixel, Gray8>(destinationPixels));
                    for (int i = 0; i < count; i++)
                    {
                        ref TSourcePixel sp = ref Unsafe.Add(ref sourceRef, i);
                        ref Gray8 dp = ref Unsafe.Add(ref gray8Ref, i);
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
