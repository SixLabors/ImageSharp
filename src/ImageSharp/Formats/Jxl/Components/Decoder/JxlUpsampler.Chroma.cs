// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;

internal sealed partial class JxlUpsampler
{
    /// <summary>
    /// Expands one subsampled color plane in place to the coding-resolution rectangle.
    /// </summary>
    /// <param name="plane">The final-capacity plane, with source samples in its top-left rectangle.</param>
    /// <param name="sourceSize">The positive original sample dimensions.</param>
    /// <param name="targetSize">The positive coding dimensions after chroma expansion.</param>
    /// <param name="horizontalShift">The validated horizontal subsampling shift, zero or one.</param>
    /// <param name="verticalShift">The validated vertical subsampling shift, zero or one.</param>
    /// <param name="cancellationToken">The token checked between physical rows.</param>
    /// <remarks>
    /// Cached shifts select the operations even when a one-sample source and its output have equal extents.
    /// Each axis uses the format's three-tap quarter/three-quarter weights before restoration and color conversion.
    /// The kernels read every contributing input before overlapping output stores. No extra plane or row allocation is needed.
    /// </remarks>
    public static void ExpandChroma(
        Buffer2D<float> plane,
        Size sourceSize,
        Size targetSize,
        int horizontalShift,
        int verticalShift,
        CancellationToken cancellationToken)
    {
        Size current = sourceSize;
        if (horizontalShift != 0)
        {
            for (int y = 0; y < current.Height; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ExpandChromaHorizontal(plane.DangerousGetRowSpan(y)[..targetSize.Width], current.Width);
            }

            current = new Size(targetSize.Width, current.Height);
        }

        if (verticalShift != 0)
        {
            // Reverse expansion preserves all source rows until their last use. The vertical kernel
            // loads its three input values before either store, including aliases at the upper border.
            for (int y = current.Height - 1; y >= 0; y--)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ReadOnlySpan<float> top = plane.DangerousGetRowSpan(Math.Max(y - 1, 0))[..current.Width];
                ReadOnlySpan<float> middle = plane.DangerousGetRowSpan(y)[..current.Width];
                ReadOnlySpan<float> bottom = plane.DangerousGetRowSpan(Math.Min(y + 1, current.Height - 1))[..current.Width];
                Span<float> first = plane.DangerousGetRowSpan(2 * y)[..current.Width];
                Span<float> second = (2 * y) + 1 < targetSize.Height ? plane.DangerousGetRowSpan((2 * y) + 1)[..current.Width] : [];
                ExpandChromaVertical(top, middle, bottom, first, second);
            }
        }
    }

    /// <summary>
    /// Expands a subsampled row in place using the native three-quarter center and one-quarter adjacent weights.
    /// </summary>
    /// <param name="destination">The complete logical output row. Its first inputLength values contain the source.</param>
    /// <param name="inputLength">The positive source length, equal to the output width divided by two and rounded upward.</param>
    public static void ExpandChromaHorizontal(Span<float> destination, int inputLength)
    {
        ref float row = ref MemoryMarshal.GetReference(destination);
        if (inputLength == 1)
        {
            float value = float.MultiplyAddEstimate(row, 0.25F, row * 0.75F);
            destination.Fill(value);
            return;
        }

        // Process the last mirrored pair first, then vector batches toward the left. Every batch loads all neighbors
        // before its interleaved stores. Expanded samples therefore overwrite only input positions already consumed.
        int last = inputLength - 1;
        float final = Unsafe.Add(ref row, last);
        float previous = Unsafe.Add(ref row, last - 1);
        Unsafe.Add(ref row, 2 * last) = float.MultiplyAddEstimate(previous, 0.25F, final * 0.75F);
        if ((2 * last) + 1 < destination.Length)
        {
            Unsafe.Add(ref row, (2 * last) + 1) = float.MultiplyAddEstimate(final, 0.25F, final * 0.75F);
        }

        int end = last;
        if (Vector512.IsHardwareAccelerated)
        {
            nuint count = (end - 1).Vector512Count<float>();
            for (nuint block = 0; block < count; block++)
            {
                end -= Vector512<float>.Count;
                Vector512<float> left = Vector512.LoadUnsafe(ref row, (nuint)(end - 1));
                Vector512<float> center = Vector512.LoadUnsafe(ref row, (nuint)end) * Vector512.Create(0.75F);
                Vector512<float> right = Vector512.LoadUnsafe(ref row, (nuint)(end + 1));
                left = Vector512.MultiplyAddEstimate(left, Vector512.Create(0.25F), center);
                right = Vector512.MultiplyAddEstimate(right, Vector512.Create(0.25F), center);
                SimdUtils.Interleave2(left, right, ref Unsafe.Add(ref row, 2 * end));
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint count = (end - 1).Vector256Count<float>();
            for (nuint block = 0; block < count; block++)
            {
                end -= Vector256<float>.Count;
                Vector256<float> left = Vector256.LoadUnsafe(ref row, (nuint)(end - 1));
                Vector256<float> center = Vector256.LoadUnsafe(ref row, (nuint)end) * Vector256.Create(0.75F);
                Vector256<float> right = Vector256.LoadUnsafe(ref row, (nuint)(end + 1));
                left = Vector256.MultiplyAddEstimate(left, Vector256.Create(0.25F), center);
                right = Vector256.MultiplyAddEstimate(right, Vector256.Create(0.25F), center);
                SimdUtils.Interleave2(left, right, ref Unsafe.Add(ref row, 2 * end));
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint count = (end - 1).Vector128Count<float>();
            for (nuint block = 0; block < count; block++)
            {
                end -= Vector128<float>.Count;
                Vector128<float> left = Vector128.LoadUnsafe(ref row, (nuint)(end - 1));
                Vector128<float> center = Vector128.LoadUnsafe(ref row, (nuint)end) * Vector128.Create(0.75F);
                Vector128<float> right = Vector128.LoadUnsafe(ref row, (nuint)(end + 1));
                left = Vector128.MultiplyAddEstimate(left, Vector128.Create(0.25F), center);
                right = Vector128.MultiplyAddEstimate(right, Vector128.Create(0.25F), center);
                SimdUtils.Interleave2(left, right, ref Unsafe.Add(ref row, 2 * end));
            }
        }

        for (int x = end - 1; x >= 1; x--)
        {
            float left = Unsafe.Add(ref row, x - 1);
            float center = Unsafe.Add(ref row, x) * 0.75F;
            float right = Unsafe.Add(ref row, x + 1);
            Unsafe.Add(ref row, 2 * x) = float.MultiplyAddEstimate(left, 0.25F, center);
            Unsafe.Add(ref row, (2 * x) + 1) = float.MultiplyAddEstimate(right, 0.25F, center);
        }

        float first = row;
        float next = Unsafe.Add(ref row, 1);
        row = float.MultiplyAddEstimate(first, 0.25F, first * 0.75F);
        Unsafe.Add(ref row, 1) = float.MultiplyAddEstimate(next, 0.25F, first * 0.75F);
    }

    /// <summary>
    /// Expands one source row into its upper and lower weighted rows, permitting overlap with any input row.
    /// </summary>
    /// <param name="top">The preceding input row, mirrored to the first row at the border.</param>
    /// <param name="middle">The current input row.</param>
    /// <param name="bottom">The next input row, mirrored to the last row at the border.</param>
    /// <param name="first">The upper output row.</param>
    /// <param name="second">The lower output row, or an empty span when an odd output height excludes it.</param>
    public static void ExpandChromaVertical(
        ReadOnlySpan<float> top, ReadOnlySpan<float> middle, ReadOnlySpan<float> bottom, Span<float> first, Span<float> second)
    {
        ref float inputTop = ref MemoryMarshal.GetReference(top);
        ref float inputMiddle = ref MemoryMarshal.GetReference(middle);
        ref float inputBottom = ref MemoryMarshal.GetReference(bottom);
        ref float outputFirst = ref MemoryMarshal.GetReference(first);
        ref float outputSecond = ref MemoryMarshal.GetReference(second);
        int x = 0;

        // The final odd-height row has no lower output. Dispatch before the loop so ordinary rows do not branch per batch.
        if (second.IsEmpty)
        {
            BlendChromaRow(top, middle, first);
            return;
        }

        // A destination can alias middle or bottom. Load all inputs and compute both results before either store.
        // Each lane stays in its original column; later widths consume only the unprocessed remainder.
        if (Vector512.IsHardwareAccelerated)
        {
            nuint count = (first.Length - x).Vector512Count<float>();
            for (nuint block = 0; block < count; block++, x += Vector512<float>.Count)
            {
                Vector512<float> center = Vector512.LoadUnsafe(ref inputMiddle, (nuint)x) * Vector512.Create(0.75F);
                Vector512<float> upper = Vector512.MultiplyAddEstimate(
                    Vector512.LoadUnsafe(ref inputTop, (nuint)x), Vector512.Create(0.25F), center);

                Vector512<float> lower = Vector512.MultiplyAddEstimate(
                    Vector512.LoadUnsafe(ref inputBottom, (nuint)x), Vector512.Create(0.25F), center);

                upper.StoreUnsafe(ref outputFirst, (nuint)x);
                lower.StoreUnsafe(ref outputSecond, (nuint)x);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint count = (first.Length - x).Vector256Count<float>();
            for (nuint block = 0; block < count; block++, x += Vector256<float>.Count)
            {
                Vector256<float> center = Vector256.LoadUnsafe(ref inputMiddle, (nuint)x) * Vector256.Create(0.75F);
                Vector256<float> upper = Vector256.MultiplyAddEstimate(
                    Vector256.LoadUnsafe(ref inputTop, (nuint)x), Vector256.Create(0.25F), center);

                Vector256<float> lower = Vector256.MultiplyAddEstimate(
                    Vector256.LoadUnsafe(ref inputBottom, (nuint)x), Vector256.Create(0.25F), center);

                upper.StoreUnsafe(ref outputFirst, (nuint)x);
                lower.StoreUnsafe(ref outputSecond, (nuint)x);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint count = (first.Length - x).Vector128Count<float>();
            for (nuint block = 0; block < count; block++, x += Vector128<float>.Count)
            {
                Vector128<float> center = Vector128.LoadUnsafe(ref inputMiddle, (nuint)x) * Vector128.Create(0.75F);
                Vector128<float> upper = Vector128.MultiplyAddEstimate(
                    Vector128.LoadUnsafe(ref inputTop, (nuint)x), Vector128.Create(0.25F), center);

                Vector128<float> lower = Vector128.MultiplyAddEstimate(
                    Vector128.LoadUnsafe(ref inputBottom, (nuint)x), Vector128.Create(0.25F), center);

                upper.StoreUnsafe(ref outputFirst, (nuint)x);
                lower.StoreUnsafe(ref outputSecond, (nuint)x);
            }
        }

        for (; x < first.Length; x++)
        {
            float center = Unsafe.Add(ref inputMiddle, x) * 0.75F;
            float upper = float.MultiplyAddEstimate(Unsafe.Add(ref inputTop, x), 0.25F, center);
            float lower = float.MultiplyAddEstimate(Unsafe.Add(ref inputBottom, x), 0.25F, center);
            Unsafe.Add(ref outputFirst, x) = upper;
            Unsafe.Add(ref outputSecond, x) = lower;
        }
    }

    /// <summary>
    /// Blends the final upper chroma row when an odd output height omits its matching lower row.
    /// </summary>
    /// <param name="adjacent">The preceding mirrored input row.</param>
    /// <param name="middle">The current input row.</param>
    /// <param name="destination">The complete output row.</param>
    private static void BlendChromaRow(ReadOnlySpan<float> adjacent, ReadOnlySpan<float> middle, Span<float> destination)
    {
        ref float neighbor = ref MemoryMarshal.GetReference(adjacent);
        ref float current = ref MemoryMarshal.GetReference(middle);
        ref float output = ref MemoryMarshal.GetReference(destination);
        int x = 0;
        if (Vector512.IsHardwareAccelerated)
        {
            nuint count = (destination.Length - x).Vector512Count<float>();
            for (nuint block = 0; block < count; block++, x += Vector512<float>.Count)
            {
                Vector512<float> center = Vector512.LoadUnsafe(ref current, (nuint)x) * Vector512.Create(0.75F);
                Vector512.MultiplyAddEstimate(Vector512.LoadUnsafe(ref neighbor, (nuint)x), Vector512.Create(0.25F), center).StoreUnsafe(ref output, (nuint)x);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint count = (destination.Length - x).Vector256Count<float>();
            for (nuint block = 0; block < count; block++, x += Vector256<float>.Count)
            {
                Vector256<float> center = Vector256.LoadUnsafe(ref current, (nuint)x) * Vector256.Create(0.75F);
                Vector256.MultiplyAddEstimate(Vector256.LoadUnsafe(ref neighbor, (nuint)x), Vector256.Create(0.25F), center).StoreUnsafe(ref output, (nuint)x);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint count = (destination.Length - x).Vector128Count<float>();
            for (nuint block = 0; block < count; block++, x += Vector128<float>.Count)
            {
                Vector128<float> center = Vector128.LoadUnsafe(ref current, (nuint)x) * Vector128.Create(0.75F);
                Vector128.MultiplyAddEstimate(Vector128.LoadUnsafe(ref neighbor, (nuint)x), Vector128.Create(0.25F), center).StoreUnsafe(ref output, (nuint)x);
            }
        }

        for (; x < destination.Length; x++)
        {
            Unsafe.Add(ref output, x) = float.MultiplyAddEstimate(Unsafe.Add(ref neighbor, x), 0.25F, Unsafe.Add(ref current, x) * 0.75F);
        }
    }
}
