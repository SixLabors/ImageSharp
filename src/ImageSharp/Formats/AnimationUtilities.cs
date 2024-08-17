// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
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
    /// <param name="nextFrame">The next frame if present.</param>
    /// <param name="resultFrame">The resultant output.</param>
    /// <param name="replacement">The value to use when replacing duplicate pixels.</param>
    /// <param name="blend">Whether the resultant frame represents an animation blend.</param>
    /// <param name="clampingMode">The clamping bound to apply when calculating difference bounds.</param>
    /// <returns>The <see cref="ValueTuple{Boolean, Rectangle}"/> representing the operation result.</returns>
    public static (bool Difference, Rectangle Bounds) DeDuplicatePixels<TPixel>(
        Configuration configuration,
        ImageFrame<TPixel>? previousFrame,
        ImageFrame<TPixel> currentFrame,
        ImageFrame<TPixel>? nextFrame,
        ImageFrame<TPixel> resultFrame,
        Color replacement,
        bool blend,
        ClampingMode clampingMode = ClampingMode.None)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        MemoryAllocator memoryAllocator = configuration.MemoryAllocator;
        using IMemoryOwner<Rgba32> buffers = memoryAllocator.Allocate<Rgba32>(currentFrame.Width * 4, AllocationOptions.Clean);
        Span<Rgba32> previous = buffers.GetSpan()[..currentFrame.Width];
        Span<Rgba32> current = buffers.GetSpan().Slice(currentFrame.Width, currentFrame.Width);
        Span<Rgba32> next = buffers.GetSpan().Slice(currentFrame.Width * 2, currentFrame.Width);
        Span<Rgba32> result = buffers.GetSpan()[(currentFrame.Width * 3)..];

        Rgba32 bg = replacement.ToPixel<Rgba32>();

        int top = int.MinValue;
        int bottom = int.MaxValue;
        int left = int.MaxValue;
        int right = int.MinValue;

        bool hasDiff = false;
        for (int y = 0; y < currentFrame.Height; y++)
        {
            if (previousFrame != null)
            {
                PixelOperations<TPixel>.Instance.ToRgba32(configuration, previousFrame.DangerousGetPixelRowMemory(y).Span, previous);
            }

            PixelOperations<TPixel>.Instance.ToRgba32(configuration, currentFrame.DangerousGetPixelRowMemory(y).Span, current);

            if (nextFrame != null)
            {
                PixelOperations<TPixel>.Instance.ToRgba32(configuration, nextFrame.DangerousGetPixelRowMemory(y).Span, next);
            }

            ref Vector256<byte> previousBase256 = ref Unsafe.As<Rgba32, Vector256<byte>>(ref MemoryMarshal.GetReference(previous));
            ref Vector256<byte> currentBase256 = ref Unsafe.As<Rgba32, Vector256<byte>>(ref MemoryMarshal.GetReference(current));
            ref Vector256<byte> nextBase256 = ref Unsafe.As<Rgba32, Vector256<byte>>(ref MemoryMarshal.GetReference(next));
            ref Vector256<byte> resultBase256 = ref Unsafe.As<Rgba32, Vector256<byte>>(ref MemoryMarshal.GetReference(result));

            int i = 0;
            uint x = 0;
            bool hasRowDiff = false;
            int length = current.Length;
            int remaining = current.Length;

            if (Avx2.IsSupported && remaining >= 8)
            {
                Vector256<uint> r256 = previousFrame != null ? Vector256.Create(bg.PackedValue) : Vector256<uint>.Zero;
                Vector256<uint> vmb256 = Vector256<uint>.Zero;
                if (blend)
                {
                    vmb256 = Avx2.CompareEqual(vmb256, vmb256);
                }

                while (remaining >= 8)
                {
                    Vector256<uint> p = Unsafe.Add(ref previousBase256, x).AsUInt32();
                    Vector256<uint> c = Unsafe.Add(ref currentBase256, x).AsUInt32();

                    Vector256<uint> eq = Avx2.CompareEqual(p, c);
                    Vector256<uint> r = Avx2.BlendVariable(c, r256, Avx2.And(eq, vmb256));

                    if (nextFrame != null)
                    {
                        Vector256<int> n = Avx2.ShiftRightLogical(Unsafe.Add(ref nextBase256, x).AsUInt32(), 24).AsInt32();
                        eq = Avx2.AndNot(Avx2.CompareGreaterThan(Avx2.ShiftRightLogical(c, 24).AsInt32(), n).AsUInt32(), eq);
                    }

                    Unsafe.Add(ref resultBase256, x) = r.AsByte();

                    uint msk = (uint)Avx2.MoveMask(eq.AsByte());
                    msk = ~msk;

                    if (msk != 0)
                    {
                        // If is diff is found, the left side is marked by the min of previously found left side and the start position.
                        // The right is the max of the previously found right side and the end position.
                        int start = i + (BitOperations.TrailingZeroCount(msk) / sizeof(uint));
                        int end = i + (8 - (BitOperations.LeadingZeroCount(msk) / sizeof(uint)));
                        left = Math.Min(left, start);
                        right = Math.Max(right, end);
                        hasRowDiff = true;
                        hasDiff = true;
                    }

                    x++;
                    i += 8;
                    remaining -= 8;
                }
            }

            if (Sse2.IsSupported && remaining >= 4)
            {
                // Update offset since we may be operating on the remainder previously incremented by pixel steps of 8.
                x *= 2;
                Vector128<uint> r128 = previousFrame != null ? Vector128.Create(bg.PackedValue) : Vector128<uint>.Zero;
                Vector128<uint> vmb128 = Vector128<uint>.Zero;
                if (blend)
                {
                    vmb128 = Sse2.CompareEqual(vmb128, vmb128);
                }

                while (remaining >= 4)
                {
                    Vector128<uint> p = Unsafe.Add(ref Unsafe.As<Vector256<byte>, Vector128<uint>>(ref previousBase256), x);
                    Vector128<uint> c = Unsafe.Add(ref Unsafe.As<Vector256<byte>, Vector128<uint>>(ref currentBase256), x);

                    Vector128<uint> eq = Sse2.CompareEqual(p, c);
                    Vector128<uint> r = SimdUtils.HwIntrinsics.BlendVariable(c, r128, Sse2.And(eq, vmb128));

                    if (nextFrame != null)
                    {
                        Vector128<int> n = Sse2.ShiftRightLogical(Unsafe.Add(ref Unsafe.As<Vector256<byte>, Vector128<uint>>(ref nextBase256), x), 24).AsInt32();
                        eq = Sse2.AndNot(Sse2.CompareGreaterThan(Sse2.ShiftRightLogical(c, 24).AsInt32(), n).AsUInt32(), eq);
                    }

                    Unsafe.Add(ref Unsafe.As<Vector256<byte>, Vector128<uint>>(ref resultBase256), x) = r;

                    ushort msk = (ushort)(uint)Sse2.MoveMask(eq.AsByte());
                    msk = (ushort)~msk;
                    if (msk != 0)
                    {
                        // If is diff is found, the left side is marked by the min of previously found left side and the start position.
                        // The right is the max of the previously found right side and the end position.
                        int start = i + (SimdUtils.HwIntrinsics.TrailingZeroCount(msk) / sizeof(uint));
                        int end = i + (4 - (SimdUtils.HwIntrinsics.LeadingZeroCount(msk) / sizeof(uint)));
                        left = Math.Min(left, start);
                        right = Math.Max(right, end);
                        hasRowDiff = true;
                        hasDiff = true;
                    }

                    x++;
                    i += 4;
                    remaining -= 4;
                }
            }

            if (AdvSimd.IsSupported && remaining >= 4)
            {
                // Update offset since we may be operating on the remainder previously incremented by pixel steps of 8.
                x *= 2;
                Vector128<uint> r128 = previousFrame != null ? Vector128.Create(bg.PackedValue) : Vector128<uint>.Zero;
                Vector128<uint> vmb128 = Vector128<uint>.Zero;
                if (blend)
                {
                    vmb128 = AdvSimd.CompareEqual(vmb128, vmb128);
                }

                while (remaining >= 4)
                {
                    Vector128<uint> p = Unsafe.Add(ref Unsafe.As<Vector256<byte>, Vector128<uint>>(ref previousBase256), x);
                    Vector128<uint> c = Unsafe.Add(ref Unsafe.As<Vector256<byte>, Vector128<uint>>(ref currentBase256), x);

                    Vector128<uint> eq = AdvSimd.CompareEqual(p, c);
                    Vector128<uint> r = SimdUtils.HwIntrinsics.BlendVariable(c, r128, AdvSimd.And(eq, vmb128));

                    if (nextFrame != null)
                    {
                        Vector128<int> n = AdvSimd.ShiftRightLogical(Unsafe.Add(ref Unsafe.As<Vector256<byte>, Vector128<uint>>(ref nextBase256), x), 24).AsInt32();
                        eq = AdvSimd.BitwiseClear(eq, AdvSimd.CompareGreaterThan(AdvSimd.ShiftRightLogical(c, 24).AsInt32(), n).AsUInt32());
                    }

                    Unsafe.Add(ref Unsafe.As<Vector256<byte>, Vector128<uint>>(ref resultBase256), x) = r;

                    ulong msk = ~AdvSimd.ExtractNarrowingLower(eq).AsUInt64().ToScalar();
                    if (msk != 0)
                    {
                        // If is diff is found, the left side is marked by the min of previously found left side and the start position.
                        // The right is the max of the previously found right side and the end position.
                        int start = i + (BitOperations.TrailingZeroCount(msk) / 16);
                        int end = i + (4 - (BitOperations.LeadingZeroCount(msk) / 16));
                        left = Math.Min(left, start);
                        right = Math.Max(right, end);
                        hasRowDiff = true;
                        hasDiff = true;
                    }

                    x++;
                    i += 4;
                    remaining -= 4;
                }
            }

            for (i = remaining; i > 0; i--)
            {
                x = (uint)(length - i);

                Rgba32 p = Unsafe.Add(ref MemoryMarshal.GetReference(previous), x);
                Rgba32 c = Unsafe.Add(ref MemoryMarshal.GetReference(current), x);
                Rgba32 n = Unsafe.Add(ref MemoryMarshal.GetReference(next), x);
                ref Rgba32 r = ref Unsafe.Add(ref MemoryMarshal.GetReference(result), x);

                bool peq = c.Rgba == (previousFrame != null ? p.Rgba : bg.Rgba);
                Rgba32 val = (blend & peq) ? bg : c;

                peq &= nextFrame == null || (n.Rgba >> 24 >= c.Rgba >> 24);
                r = val;

                if (!peq)
                {
                    // If is diff is found, the left side is marked by the min of previously found left side and the diff position.
                    // The right is the max of the previously found right side and the diff position + 1.
                    left = Math.Min(left, (int)x);
                    right = Math.Max(right, (int)x + 1);
                    hasRowDiff = true;
                    hasDiff = true;
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

            PixelOperations<TPixel>.Instance.FromRgba32(configuration, result, resultFrame.DangerousGetPixelRowMemory(y).Span);
        }

        Rectangle bounds = Rectangle.FromLTRB(
            left = Numerics.Clamp(left, 0, resultFrame.Width - 1),
            top = Numerics.Clamp(top, 0, resultFrame.Height - 1),
            Numerics.Clamp(right, left + 1, resultFrame.Width),
            Numerics.Clamp(bottom, top + 1, resultFrame.Height));

        // Webp requires even bounds
        if (clampingMode == ClampingMode.Even)
        {
            bounds.Width = Math.Min(resultFrame.Width, bounds.Width + (bounds.X & 1));
            bounds.Height = Math.Min(resultFrame.Height, bounds.Height + (bounds.Y & 1));
            bounds.X = Math.Max(0, bounds.X - (bounds.X & 1));
            bounds.Y = Math.Max(0, bounds.Y - (bounds.Y & 1));
        }

        return (hasDiff, bounds);
    }
}

#pragma warning disable SA1201 // Elements should appear in the correct order
internal enum ClampingMode
#pragma warning restore SA1201 // Elements should appear in the correct order
{
    None,

    Even,
}
