// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.PixelFormats;

[Trait("Category", "PixelFormats")]
public abstract partial class PixelConverterTests
{
    public static class ReferenceImplementations
    {
        public static byte[] MakeRgba32ByteArray(byte r, byte g, byte b, byte a)
        {
            byte[] buffer = new byte[256];

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
            byte[] buffer = new byte[256];

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
            byte[] buffer = new byte[256];

            for (int i = 0; i < buffer.Length; i += 4)
            {
                buffer[i] = b;
                buffer[i + 1] = g;
                buffer[i + 2] = r;
                buffer[i + 3] = a;
            }

            return buffer;
        }

        public static byte[] MakeAbgr32ByteArray(byte r, byte g, byte b, byte a)
        {
            byte[] buffer = new byte[256];

            for (int i = 0; i < buffer.Length; i += 4)
            {
                buffer[i] = a;
                buffer[i + 1] = b;
                buffer[i + 2] = g;
                buffer[i + 3] = r;
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
                Span<TSourcePixel> uniformDest = MemoryMarshal.Cast<TDestinationPixel, TSourcePixel>(destinationPixels);
                sourcePixels.CopyTo(uniformDest);
                return;
            }

            // Normal conversion
            ref TDestinationPixel destRef = ref MemoryMarshal.GetReference(destinationPixels);
            for (int i = 0; i < count; i++)
            {
                ref TSourcePixel sp = ref Unsafe.Add(ref sourceRef, i);
                ref TDestinationPixel dp = ref Unsafe.Add(ref destRef, i);
                dp = TDestinationPixel.FromScaledVector4(sp.ToScaledVector4());
            }
        }
    }
}
