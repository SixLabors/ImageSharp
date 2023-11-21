// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats;

/// <summary>
/// Utility methods for animated formats.
/// </summary>
internal static class AnimationUtilities
{
    /// <summary>
    /// Deduplicates pixels between the previous and current frame returning only the changed pixels and bounds.
    /// </summary>
    /// <typeparam name="TPixel">The type of pixel format.</typeparam>
    /// <param name="configuration">The configuration.</param>
    /// <param name="previousFrame">The previous frame if present.</param>
    /// <param name="currentFrame">The current frame.</param>
    /// <param name="resultFrame">The resultant output.</param>
    /// <param name="replacement">The value to use when replacing duplicate pixels.</param>
    /// <returns>The <see cref="ValueTuple{Boolean, Rectangle}"/> representing the operation result.</returns>
    public static (bool Difference, Rectangle Bounds) DeDuplicatePixels<TPixel>(
        Configuration configuration,
        ImageFrame<TPixel>? previousFrame,
        ImageFrame<TPixel> currentFrame,
        ImageFrame<TPixel> resultFrame,
        Vector4 replacement)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        MemoryAllocator memoryAllocator = configuration.MemoryAllocator;
        IMemoryOwner<Vector4> buffers = memoryAllocator.Allocate<Vector4>(currentFrame.Width * 3, AllocationOptions.Clean);
        Span<Vector4> previous = buffers.GetSpan()[..currentFrame.Width];
        Span<Vector4> current = buffers.GetSpan().Slice(currentFrame.Width, currentFrame.Width);
        Span<Vector4> result = buffers.GetSpan()[(currentFrame.Width * 2)..];

        int top = int.MinValue;
        int bottom = int.MaxValue;
        int left = int.MaxValue;
        int right = int.MinValue;

        bool hasDiff = false;
        for (int y = 0; y < currentFrame.Height; y++)
        {
            if (previousFrame != null)
            {
                PixelOperations<TPixel>.Instance.ToVector4(configuration, previousFrame.DangerousGetPixelRowMemory(y).Span, previous, PixelConversionModifiers.Scale);
            }

            PixelOperations<TPixel>.Instance.ToVector4(configuration, currentFrame.DangerousGetPixelRowMemory(y).Span, current, PixelConversionModifiers.Scale);

            ref Vector256<float> previousBase = ref Unsafe.As<Vector4, Vector256<float>>(ref MemoryMarshal.GetReference(previous));
            ref Vector256<float> currentBase = ref Unsafe.As<Vector4, Vector256<float>>(ref MemoryMarshal.GetReference(current));
            ref Vector256<float> resultBase = ref Unsafe.As<Vector4, Vector256<float>>(ref MemoryMarshal.GetReference(result));

            Vector256<float> replacement256 = Vector256.Create(replacement.X, replacement.Y, replacement.Z, replacement.W, replacement.X, replacement.Y, replacement.Z, replacement.W);

            int size = Unsafe.SizeOf<Vector4>();

            bool hasRowDiff = false;
            int i = 0;
            uint x = 0;
            int length = current.Length;
            int remaining = current.Length;
            if (Avx2.IsSupported && remaining >= 2)
            {
                while (remaining >= 2)
                {
                    Vector256<float> p = Unsafe.Add(ref previousBase, x);
                    Vector256<float> c = Unsafe.Add(ref currentBase, x);

                    // Compare the previous and current pixels
                    Vector256<float> neq = Avx.CompareEqual(p, c);
                    Vector256<int> mask = neq.AsInt32();

                    neq = Avx.Xor(neq, Vector256<float>.AllBitsSet);
                    int m = Avx2.MoveMask(neq.AsByte());
                    if (m != 0)
                    {
                        // If is diff is found, the left side is marked by the min of previously found left side and the diff position.
                        // The right is the max of the previously found right side and the diff position + 1.
                        int diff = (int)(i + (uint)(BitOperations.TrailingZeroCount(m) / size));
                        left = Math.Min(left, diff);
                        right = Math.Max(right, diff + 1);
                        hasRowDiff = true;
                        hasDiff = true;
                    }

                    // Capture the original alpha values.
                    mask = Avx2.HorizontalAdd(mask, mask);
                    mask = Avx2.HorizontalAdd(mask, mask);
                    mask = Avx2.CompareEqual(mask, Vector256.Create(-4));

                    Vector256<float> r = Avx.BlendVariable(c, replacement256, mask.AsSingle());
                    Unsafe.Add(ref resultBase, x) = r;

                    x++;
                    i += 2;
                    remaining -= 2;
                }
            }

            for (i = remaining; i > 0; i--)
            {
                x = (uint)(length - i);

                Vector4 p = Unsafe.Add(ref Unsafe.As<Vector256<float>, Vector4>(ref previousBase), x);
                Vector4 c = Unsafe.Add(ref Unsafe.As<Vector256<float>, Vector4>(ref currentBase), x);
                ref Vector4 r = ref Unsafe.Add(ref Unsafe.As<Vector256<float>, Vector4>(ref resultBase), x);

                if (p != c)
                {
                    r = c;

                    // If is diff is found, the left side is marked by the min of previously found left side and the diff position.
                    // The right is the max of the previously found right side and the diff position + 1.
                    left = Math.Min(left, (int)x);
                    right = Math.Max(right, (int)x + 1);
                    hasRowDiff = true;
                    hasDiff = true;
                }
                else
                {
                    r = replacement;
                }
            }

            if (hasRowDiff)
            {
                if (top == int.MinValue)
                {
                    top = y;
                }

                bottom = y + 1;
            }

            PixelOperations<TPixel>.Instance.FromVector4Destructive(configuration, result, resultFrame.DangerousGetPixelRowMemory(y).Span, PixelConversionModifiers.Scale);
        }

        Rectangle bounds = Rectangle.FromLTRB(
            left = Numerics.Clamp(left, 0, resultFrame.Width - 1),
            top = Numerics.Clamp(top, 0, resultFrame.Height - 1),
            Numerics.Clamp(right, left + 1, resultFrame.Width),
            Numerics.Clamp(bottom, top + 1, resultFrame.Height));

        return new(hasDiff, bounds);
    }
}
