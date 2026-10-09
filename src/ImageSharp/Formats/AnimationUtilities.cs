// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats;

/// <summary>
/// Finds changed pixels and frame bounds for animated images.
/// </summary>
internal static class AnimationUtilities
{
    /// <summary>
    /// Selects replacement at JIT compilation time for each row-method specialization.
    /// </summary>
    private interface IReplacementOperator
    {
        /// <summary>
        /// Gets a value indicating whether matching pixels are replaced.
        /// </summary>
        static abstract bool IsEnabled { get; }
    }

    /// <summary>
    /// Supplies row conversion and comparison while keeping frame traversal shared.
    /// </summary>
    /// <typeparam name="TRowPixel">The representation used to compare pixels.</typeparam>
    private interface IRowOperator<TRowPixel>
        where TRowPixel : unmanaged
    {
        /// <summary>
        /// Converts the replacement after it has been packed into the frame's pixel format.
        /// </summary>
        /// <typeparam name="TPixel">The frame pixel format.</typeparam>
        /// <param name="replacement">The replacement in the frame's pixel format.</param>
        /// <returns>The replacement in the row representation.</returns>
        static abstract TRowPixel ConvertReplacement<TPixel>(TPixel replacement)
            where TPixel : unmanaged, IPixel<TPixel>;

        /// <summary>
        /// Converts pixels to the row representation while retaining their alpha representation.
        /// </summary>
        /// <typeparam name="TPixel">The frame pixel format.</typeparam>
        /// <param name="pixelOperations">The pixel operations shared by all rows in this frame.</param>
        /// <param name="configuration">The configuration.</param>
        /// <param name="source">The source pixels.</param>
        /// <param name="destination">The comparison row.</param>
        static abstract void ToRow<TPixel>(
            PixelOperations<TPixel> pixelOperations, Configuration configuration, ReadOnlySpan<TPixel> source, Span<TRowPixel> destination)
            where TPixel : unmanaged, IPixel<TPixel>;

        /// <summary>
        /// Writes the comparison row back into the frame's pixel format.
        /// </summary>
        /// <typeparam name="TPixel">The frame pixel format.</typeparam>
        /// <param name="pixelOperations">The pixel operations shared by all rows in this frame.</param>
        /// <param name="configuration">The configuration.</param>
        /// <param name="source">The comparison row. Its contents may be modified.</param>
        /// <param name="destination">The destination pixels.</param>
        static abstract void FromRow<TPixel>(
            PixelOperations<TPixel> pixelOperations, Configuration configuration, Span<TRowPixel> source, Span<TPixel> destination)
            where TPixel : unmanaged, IPixel<TPixel>;

        /// <summary>
        /// Compares the row, replaces duplicates when requested, and expands the changed bounds.
        /// </summary>
        /// <typeparam name="TReplacement">The replacement mode.</typeparam>
        /// <param name="previous">The previous row or repeated replacement.</param>
        /// <param name="current">The current row, which also stores the result.</param>
        /// <param name="next">The following row, or an empty span when no frame follows.</param>
        /// <param name="replacement">The replacement in the row representation.</param>
        /// <param name="left">The leftmost changed pixel.</param>
        /// <param name="right">The exclusive rightmost changed pixel.</param>
        /// <returns>Whether the row needs inclusion in the changed bounds.</returns>
        static abstract bool CompareRow<TReplacement>(
            ReadOnlySpan<TRowPixel> previous, Span<TRowPixel> current, ReadOnlySpan<TRowPixel> next, TRowPixel replacement, ref int left, ref int right)
            where TReplacement : struct, IReplacementOperator;
    }

    /// <summary>
    /// Copies the current frame and replaces matching pixels with the supplied color when blending is enabled.
    /// Returns the changed bounds, including pixels whose alpha decreases in the next frame.
    /// </summary>
    /// <typeparam name="TPixel">The type of pixel format.</typeparam>
    /// <param name="configuration">The configuration.</param>
    /// <param name="previousFrame">The previous frame, or null to compare against the replacement color.</param>
    /// <param name="currentFrame">The frame to copy and compare.</param>
    /// <param name="nextFrame">The next frame, or null when no frame follows.</param>
    /// <param name="resultFrame">The destination for the copied or replaced pixels.</param>
    /// <param name="replacement">The color to write for pixels that match the previous frame.</param>
    /// <param name="blend">Whether to replace matching pixels.</param>
    /// <param name="clampingMode">Whether the bounds require even left and top coordinates.</param>
    /// <returns>Whether pixels differ and the rectangle required to encode the frame.</returns>
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
        PixelTypeInfo info = TPixel.GetPixelTypeInfo();

        // Component precision selects row storage. PixelOperations handles the channel layout and numeric range.
        // Unknown precision requires native vectors; it cannot safely select eight-bit storage.
        if (info.ComponentInfo?.GetMaximumComponentPrecision() <= 8)
        {
            // Keep associated byte samples associated throughout comparison and packing.
            if (info.AlphaRepresentation == PixelAlphaRepresentation.Associated)
            {
                return blend
                    ? DeDuplicatePixels<TPixel, Rgba32P, AssociatedByteOperator, ReplaceDuplicatesOperator>(
                        configuration, previousFrame, currentFrame, nextFrame, resultFrame, replacement, clampingMode)
                    : DeDuplicatePixels<TPixel, Rgba32P, AssociatedByteOperator, KeepPixelsOperator>(
                        configuration, previousFrame, currentFrame, nextFrame, resultFrame, replacement, clampingMode);
            }

            return blend
                ? DeDuplicatePixels<TPixel, Rgba32, UnassociatedByteOperator, ReplaceDuplicatesOperator>(
                    configuration, previousFrame, currentFrame, nextFrame, resultFrame, replacement, clampingMode)
                : DeDuplicatePixels<TPixel, Rgba32, UnassociatedByteOperator, KeepPixelsOperator>(
                    configuration, previousFrame, currentFrame, nextFrame, resultFrame, replacement, clampingMode);
        }

        return blend
            ? DeDuplicatePixels<TPixel, Vector4, VectorOperator, ReplaceDuplicatesOperator>(
                configuration, previousFrame, currentFrame, nextFrame, resultFrame, replacement, clampingMode)
            : DeDuplicatePixels<TPixel, Vector4, VectorOperator, KeepPixelsOperator>(
                configuration, previousFrame, currentFrame, nextFrame, resultFrame, replacement, clampingMode);
    }

    /// <summary>
    /// Traverses animation frames using the selected row representation without changing the bounds or replacement rules.
    /// </summary>
    /// <typeparam name="TPixel">The frame pixel format.</typeparam>
    /// <typeparam name="TRowPixel">The row comparison format.</typeparam>
    /// <typeparam name="TRowOperator">The conversion and comparison operations for the row.</typeparam>
    /// <typeparam name="TReplacement">The replacement mode selected once for this frame.</typeparam>
    /// <param name="configuration">The configuration.</param>
    /// <param name="previousFrame">The previous frame, or null to compare against the replacement color.</param>
    /// <param name="currentFrame">The frame to copy and compare.</param>
    /// <param name="nextFrame">The next frame, or null when no frame follows.</param>
    /// <param name="resultFrame">The destination for the copied or replaced pixels.</param>
    /// <param name="replacement">The replacement color.</param>
    /// <param name="clampingMode">Whether the bounds require even left and top coordinates.</param>
    /// <returns>Whether pixels differ and the rectangle required to encode the frame.</returns>
    // Keep this loop out of callers so each row representation has its own compiled frame traversal.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (bool Difference, Rectangle Bounds) DeDuplicatePixels<TPixel, TRowPixel, TRowOperator, TReplacement>(
        Configuration configuration,
        ImageFrame<TPixel>? previousFrame,
        ImageFrame<TPixel> currentFrame,
        ImageFrame<TPixel>? nextFrame,
        ImageFrame<TPixel> resultFrame,
        Color replacement,
        ClampingMode clampingMode)
        where TPixel : unmanaged, IPixel<TPixel>
        where TRowPixel : unmanaged
        where TRowOperator : struct, IRowOperator<TRowPixel>
        where TReplacement : struct, IReplacementOperator
    {
        // Resolve the pixel operations once; every row uses the same instance.
        PixelOperations<TPixel> pixelOperations = PixelOperations<TPixel>.Instance;

        // Convert the replacement through TPixel so its channels and alpha match the frame's pixel format.
        TRowPixel background = TRowOperator.ConvertReplacement(replacement.ToPixel<TPixel>());

        // One pooled allocation holds previous, current and next rows. The current row also holds the output,
        // so replacement needs no fourth row buffer.
        using IMemoryOwner<TRowPixel> buffers = configuration.MemoryAllocator.Allocate<TRowPixel>(currentFrame.Width * 3);
        Span<TRowPixel> rows = buffers.GetSpan();
        Span<TRowPixel> previous = rows[..currentFrame.Width];
        Span<TRowPixel> current = rows.Slice(currentFrame.Width, currentFrame.Width);
        Span<TRowPixel> next = rows.Slice(currentFrame.Width * 2, currentFrame.Width);

        if (previousFrame is null)
        {
            // With no previous frame, compare the current pixels against the replacement color.
            previous.Fill(background);
        }

        // An empty following row tells both comparison paths to skip next-frame alpha work.
        ReadOnlySpan<TRowPixel> following = nextFrame is null ? default : next;

        int top = int.MinValue;
        int bottom = int.MaxValue;
        int left = int.MaxValue;
        int right = int.MinValue;
        bool hasDiff = false;

        for (int y = 0; y < currentFrame.Height; y++)
        {
            if (previousFrame is not null)
            {
                TRowOperator.ToRow(pixelOperations, configuration, previousFrame.DangerousGetPixelRowMemory(y).Span, previous);
            }

            ReadOnlySpan<TPixel> sourceRow = currentFrame.DangerousGetPixelRowMemory(y).Span;
            TRowOperator.ToRow(pixelOperations, configuration, sourceRow, current);

            if (nextFrame is not null)
            {
                TRowOperator.ToRow(pixelOperations, configuration, nextFrame.DangerousGetPixelRowMemory(y).Span, next);
            }

            Span<TPixel> resultRow = resultFrame.DangerousGetPixelRowMemory(y).Span;
            bool hasRowDiff = TRowOperator.CompareRow<TReplacement>(previous, current, following, background, ref left, ref right);

            // The frame's replacement operator is constant. The JIT removes the unused output path and its condition.
            if (TReplacement.IsEnabled)
            {
                // Use the packing API that matches the row's numeric range and alpha representation.
                TRowOperator.FromRow(pixelOperations, configuration, current, resultRow);
            }
            else
            {
                // With replacement disabled, the output equals the source. A direct copy avoids another pixel conversion.
                sourceRow.CopyTo(resultRow);
            }

            if (hasRowDiff)
            {
                hasDiff = true;
                if (top == int.MinValue)
                {
                    top = y;
                }

                bottom = y + 1;
            }
        }

        // An unchanged frame still needs a nonempty rectangle to encode its timing and disposal settings.
        Rectangle bounds = Rectangle.FromLTRB(
            left = Numerics.Clamp(left, 0, resultFrame.Width - 1),
            top = Numerics.Clamp(top, 0, resultFrame.Height - 1),
            Numerics.Clamp(right, left + 1, resultFrame.Width),
            Numerics.Clamp(bottom, top + 1, resultFrame.Height));

        // WebP requires even left and top coordinates. Expand the rectangle to include the original changed pixels.
        if (clampingMode == ClampingMode.Even)
        {
            bounds.Width = Math.Min(resultFrame.Width, bounds.Width + (bounds.X & 1));
            bounds.Height = Math.Min(resultFrame.Height, bounds.Height + (bounds.Y & 1));
            bounds.X = Math.Max(0, bounds.X - (bounds.X & 1));
            bounds.Y = Math.Max(0, bounds.Y - (bounds.Y & 1));
        }

        return (hasDiff, bounds);
    }

    /// <summary>
    /// Compares four channels per pixel, replaces matching pixels, and updates the changed bounds.
    /// </summary>
    /// <typeparam name="TOperator">The replacement mode selected for the row.</typeparam>
    /// <param name="previous">The previous row, or a row filled with the replacement color.</param>
    /// <param name="current">The current row. Matching pixels are replaced in this row.</param>
    /// <param name="next">The next row, or an empty span when there is no next frame.</param>
    /// <param name="replacement">The replacement in the same pixel format as the frame.</param>
    /// <param name="left">The leftmost changed pixel found so far.</param>
    /// <param name="right">The exclusive rightmost changed pixel found so far.</param>
    /// <returns>Whether pixels differ or their alpha decreases in the next frame.</returns>
    private static bool DeDuplicatePixelRow<TOperator>(
        ReadOnlySpan<Vector4> previous, Span<Vector4> current, ReadOnlySpan<Vector4> next, Vector4 replacement, ref int left, ref int right)
        where TOperator : struct, IReplacementOperator
    {
        ReadOnlySpan<float> components = MemoryMarshal.Cast<Vector4, float>(current);
        ref float previousRef = ref Unsafe.As<Vector4, float>(ref MemoryMarshal.GetReference(previous));
        ref float currentRef = ref Unsafe.As<Vector4, float>(ref MemoryMarshal.GetReference(current));
        ref float nextRef = ref Unsafe.As<Vector4, float>(ref MemoryMarshal.GetReference(next));
        bool hasNextFrame = !next.IsEmpty;
        nuint offset = 0;
        uint rowDifferences = 0;

        // Keep both bounds in registers throughout the row. Only changed blocks update them.
        // Only lane zero is used; copy it back to the frame bounds after all blocks have been compared.
        Vector128<int> leftBound = Vector128.CreateScalar(left);
        Vector128<int> rightBound = Vector128.CreateScalar(right);

        // Repeat the replacement's R, G, B and alpha across each vector's pixels.
        Vector128<float> background = replacement.AsVector128();

        if (Vector512.IsHardwareAccelerated)
        {
            // A 512-bit vector holds four pixels. Each shuffle stays inside the four lanes of its pixel.
            Vector512<float> backgroundValues = Vector512.Create(Vector256.Create(background, background), Vector256.Create(background, background));
            nuint end = components.Vector512Count<float>() * (nuint)Vector512<float>.Count;

            for (; offset < end; offset += (nuint)Vector512<float>.Count)
            {
                Vector512<float> value = Vector512.LoadUnsafe(ref currentRef, offset);

                // Compare R, G, B and alpha separately; matching components produce all-one bit masks.
                Vector512<uint> equal = Vector512.Equals(Vector512.LoadUnsafe(ref previousRef, offset), value).AsUInt32();

                // Bounds need only one different component to include a pixel. Skip the shuffles when no pixels are replaced.
                if (TOperator.IsEnabled)
                {
                    // Replacement must select all four channels together. Pair R/G and B/alpha, then combine the pairs.
                    // AND leaves all four masks set only when the whole pixel matches. AsSingle reinterprets the mask bits.
                    equal &= Vector512_.ShuffleNative(equal.AsSingle(), SimdUtils.Shuffle.MMShuffle2301).AsUInt32();
                    equal &= Vector512_.ShuffleNative(equal.AsSingle(), SimdUtils.Shuffle.MMShuffle1032).AsUInt32();
                }

                // Only the first 16 mask bits describe the four RGBA pixels in this block.
                uint differences = ~(uint)equal.ExtractMostSignificantBits() & 0xFFFFU;

                // Bits 3, 7, 11 and 15 are alpha. Compare them only when a following frame exists.
                if (hasNextFrame)
                {
                    differences |= (uint)Vector512.GreaterThan(value, Vector512.LoadUnsafe(ref nextRef, offset)).ExtractMostSignificantBits() & 0x8888U;
                }

                rowDifferences |= differences;

                // Unchanged blocks need no bit scans or bounds updates.
                if (differences != 0)
                {
                    UpdateBounds(differences, (int)(offset / 4), ref leftBound, ref rightBound);
                }

                // Each operator supplies a constant. The copy-only specialization has no selection, store or blend branch.
                if (TOperator.IsEnabled)
                {
                    Vector512.ConditionalSelect(equal.AsSingle(), backgroundValues, value).StoreUnsafe(ref currentRef, offset);
                }
            }
        }
        else if (Vector256.IsHardwareAccelerated)
        {
            // A 256-bit vector holds two pixels. The shuffles combine comparisons separately for each pixel.
            Vector256<float> backgroundValues = Vector256.Create(background, background);
            nuint end = components.Vector256Count<float>() * (nuint)Vector256<float>.Count;

            for (; offset < end; offset += (nuint)Vector256<float>.Count)
            {
                Vector256<float> value = Vector256.LoadUnsafe(ref currentRef, offset);

                // Compare R, G, B and alpha separately; matching components produce all-one bit masks.
                Vector256<uint> equal = Vector256.Equals(Vector256.LoadUnsafe(ref previousRef, offset), value).AsUInt32();

                // Bounds need only one different component to include a pixel. Skip the shuffles when no pixels are replaced.
                if (TOperator.IsEnabled)
                {
                    // Replacement must select all four channels together. Pair R/G and B/alpha, then combine the pairs.
                    // AND leaves all four masks set only when the whole pixel matches. AsSingle reinterprets the mask bits.
                    equal &= Vector256_.ShuffleNative(equal.AsSingle(), SimdUtils.Shuffle.MMShuffle2301).AsUInt32();
                    equal &= Vector256_.ShuffleNative(equal.AsSingle(), SimdUtils.Shuffle.MMShuffle1032).AsUInt32();
                }

                // Only the first eight mask bits describe the two RGBA pixels in this block.
                uint differences = ~equal.ExtractMostSignificantBits() & 0xFFU;

                // Bits 3 and 7 are alpha. Skip their comparison when there is no following frame.
                if (hasNextFrame)
                {
                    differences |= Vector256.GreaterThan(value, Vector256.LoadUnsafe(ref nextRef, offset)).ExtractMostSignificantBits() & 0x88U;
                }

                rowDifferences |= differences;

                // Unchanged blocks need no bit scans or bounds updates.
                if (differences != 0)
                {
                    UpdateBounds(differences, (int)(offset / 4), ref leftBound, ref rightBound);
                }

                // The operator's constant removes this condition and removes the store from the copy-only specialization.
                if (TOperator.IsEnabled)
                {
                    Vector256.ConditionalSelect(equal.AsSingle(), backgroundValues, value).StoreUnsafe(ref currentRef, offset);
                }
            }
        }

        // A 128-bit vector holds one pixel. Process pixels left after the wider loop here.
        // With hardware intrinsics disabled, the runtime executes these operations in software for every pixel.
        for (; offset < (nuint)components.Length; offset += (nuint)Vector128<float>.Count)
        {
            Vector128<float> value = Vector128.LoadUnsafe(ref currentRef, offset);

            // Compare R, G, B and alpha separately; matching components produce all-one bit masks.
            Vector128<uint> equal = Vector128.Equals(Vector128.LoadUnsafe(ref previousRef, offset), value).AsUInt32();

            // Bounds need only one different component to include a pixel. Skip the shuffles when no pixels are replaced.
            if (TOperator.IsEnabled)
            {
                // Replacement must select all four channels together. Pair R/G and B/alpha, then combine the pairs.
                // AND leaves all four masks set only when the whole pixel matches. AsSingle reinterprets the mask bits.
                equal &= Vector128_.ShuffleNative(equal.AsSingle(), SimdUtils.Shuffle.MMShuffle2301).AsUInt32();
                equal &= Vector128_.ShuffleNative(equal.AsSingle(), SimdUtils.Shuffle.MMShuffle1032).AsUInt32();
            }

            // The four low bits describe this pixel. Bit 3 is alpha.
            uint differences = ~equal.ExtractMostSignificantBits() & 0xFU;
            if (hasNextFrame)
            {
                differences |= Vector128.GreaterThan(value, Vector128.LoadUnsafe(ref nextRef, offset)).ExtractMostSignificantBits() & 0x8U;
            }

            rowDifferences |= differences;

            // Unchanged blocks need no bit scans or bounds updates.
            if (differences != 0)
            {
                UpdateBounds(differences, (int)(offset / 4), ref leftBound, ref rightBound);
            }

            // The copy-only path also skips replacement for the final pixels, including the software fallback.
            if (TOperator.IsEnabled)
            {
                Vector128.ConditionalSelect(equal.AsSingle(), background, value).StoreUnsafe(ref currentRef, offset);
            }
        }

        left = leftBound.ToScalar();
        right = rightBound.ToScalar();
        return rowDifferences != 0;
    }

    /// <summary>
    /// Compares packed RGBA words without changing the stored alpha representation.
    /// </summary>
    /// <typeparam name="TOperator">The replacement mode selected for the row.</typeparam>
    /// <param name="previous">The previous row, or the replacement color repeated across the row.</param>
    /// <param name="current">The current row. Matching pixels are replaced in this row.</param>
    /// <param name="next">The next row, or an empty span when there is no next frame.</param>
    /// <param name="replacement">The replacement packed in the same alpha representation as the row.</param>
    /// <param name="left">The leftmost changed pixel found so far.</param>
    /// <param name="right">The exclusive rightmost changed pixel found so far.</param>
    /// <returns>Whether pixels differ or their alpha decreases in the next frame.</returns>
    private static bool DeDuplicateByteRow<TOperator>(
        ReadOnlySpan<uint> previous, Span<uint> current, ReadOnlySpan<uint> next, uint replacement, ref int left, ref int right)
        where TOperator : struct, IReplacementOperator
    {
        ref uint previousRef = ref MemoryMarshal.GetReference(previous);
        ref uint currentRef = ref MemoryMarshal.GetReference(current);
        ref uint nextRef = ref MemoryMarshal.GetReference(next);
        bool hasNextFrame = !next.IsEmpty;
        nuint offset = 0;
        uint rowDifferences = 0;
        int leftBound = left;
        int rightBound = right;

        if (Vector512.IsHardwareAccelerated)
        {
            // Each uint lane contains one RGBA pixel. Sixteen pixels fit in a complete block.
            Vector512<uint> background = Vector512.Create(replacement);
            nuint end = current.Vector512Count<uint>() * (nuint)Vector512<uint>.Count;
            for (; offset < end; offset += (nuint)Vector512<uint>.Count)
            {
                Vector512<uint> value = Vector512.LoadUnsafe(ref currentRef, offset);
                Vector512<uint> equal = Vector512.Equals(Vector512.LoadUnsafe(ref previousRef, offset), value);

                // Alpha occupies the high byte. Decreasing next-frame alpha requires this pixel in the bounds.
                // Skip the load and comparison when no next frame exists.
                // Invert the extracted mask, not the vector. Only the low sixteen bits represent pixels.
                uint differences = ~(uint)equal.ExtractMostSignificantBits() & 0xFFFFU;
                if (hasNextFrame)
                {
                    Vector512<uint> lowerAlpha = Vector512.GreaterThan(value >> 24, Vector512.LoadUnsafe(ref nextRef, offset) >> 24);
                    differences |= (uint)lowerAlpha.ExtractMostSignificantBits();
                }

                rowDifferences |= differences;

                // Each mask bit represents one pixel. Skip both bit scans when the entire block is unchanged.
                if (differences != 0)
                {
                    leftBound = Math.Min(leftBound, (int)offset + BitOperations.TrailingZeroCount(differences));
                    rightBound = Math.Max(rightBound, (int)offset + 32 - BitOperations.LeadingZeroCount(differences));
                }

                // Equality controls replacement; decreasing alpha changes only the bounds.
                // The copy-only specialization removes these stores.
                if (TOperator.IsEnabled)
                {
                    Vector512.ConditionalSelect(equal, background, value).StoreUnsafe(ref currentRef, offset);
                }
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            // Process eight pixels per block, including any complete block left by the wider loop.
            Vector256<uint> background = Vector256.Create(replacement);
            nuint end = current.Vector256Count<uint>() * (nuint)Vector256<uint>.Count;
            for (; offset < end; offset += (nuint)Vector256<uint>.Count)
            {
                Vector256<uint> value = Vector256.LoadUnsafe(ref currentRef, offset);
                Vector256<uint> equal = Vector256.Equals(Vector256.LoadUnsafe(ref previousRef, offset), value);

                // The low eight mask bits represent the eight packed pixels in this block.
                uint differences = ~equal.ExtractMostSignificantBits() & 0xFFU;
                if (hasNextFrame)
                {
                    Vector256<uint> lowerAlpha = Vector256.GreaterThan(value >> 24, Vector256.LoadUnsafe(ref nextRef, offset) >> 24);
                    differences |= (uint)lowerAlpha.ExtractMostSignificantBits();
                }

                rowDifferences |= differences;

                // Only changed blocks need bounds calculations. The first and last set bits identify their pixel positions.
                if (differences != 0)
                {
                    leftBound = Math.Min(leftBound, (int)offset + BitOperations.TrailingZeroCount(differences));
                    rightBound = Math.Max(rightBound, (int)offset + 32 - BitOperations.LeadingZeroCount(differences));
                }

                if (TOperator.IsEnabled)
                {
                    Vector256.ConditionalSelect(equal, background, value).StoreUnsafe(ref currentRef, offset);
                }
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            // Process four pixels per block. The scalar loop handles the remaining one to three pixels.
            Vector128<uint> background = Vector128.Create(replacement);
            nuint end = current.Vector128Count<uint>() * (nuint)Vector128<uint>.Count;
            for (; offset < end; offset += (nuint)Vector128<uint>.Count)
            {
                Vector128<uint> value = Vector128.LoadUnsafe(ref currentRef, offset);
                Vector128<uint> equal = Vector128.Equals(Vector128.LoadUnsafe(ref previousRef, offset), value);

                // The low four mask bits represent this block's four packed pixels.
                uint differences = ~equal.ExtractMostSignificantBits() & 0xFU;
                if (hasNextFrame)
                {
                    Vector128<uint> lowerAlpha = Vector128.GreaterThan(value >> 24, Vector128.LoadUnsafe(ref nextRef, offset) >> 24);
                    differences |= (uint)lowerAlpha.ExtractMostSignificantBits();
                }

                rowDifferences |= differences;

                if (differences != 0)
                {
                    leftBound = Math.Min(leftBound, (int)offset + BitOperations.TrailingZeroCount(differences));
                    rightBound = Math.Max(rightBound, (int)offset + 32 - BitOperations.LeadingZeroCount(differences));
                }

                if (TOperator.IsEnabled)
                {
                    Vector128.ConditionalSelect(equal, background, value).StoreUnsafe(ref currentRef, offset);
                }
            }
        }

        // With SIMD disabled, this loop handles the whole row using the same equality and alpha rules.
        for (; offset < (nuint)current.Length; offset++)
        {
            uint value = Unsafe.Add(ref currentRef, offset);
            bool equal = value == Unsafe.Add(ref previousRef, offset);

            // A pixel needs bounds when its color differs or its alpha decreases in the following frame.
            uint differences = equal && (!hasNextFrame || (value >> 24) <= (Unsafe.Add(ref nextRef, offset) >> 24)) ? 0U : 1U;
            rowDifferences |= differences;
            if (differences != 0)
            {
                leftBound = Math.Min(leftBound, (int)offset);
                rightBound = Math.Max(rightBound, (int)offset + 1);
            }

            if (TOperator.IsEnabled)
            {
                Unsafe.Add(ref currentRef, offset) = equal ? replacement : value;
            }
        }

        left = leftBound;
        right = rightBound;
        return rowDifferences != 0;
    }

    /// <summary>
    /// Expands the left and right bounds to include the changed pixels in one SIMD block.
    /// </summary>
    /// <param name="differences">A nonzero mask. Each pixel uses four component bits; a set bit marks a change.</param>
    /// <param name="offset">The first pixel index in the block.</param>
    /// <param name="left">The leftmost changed pixel in lane zero.</param>
    /// <param name="right">The exclusive rightmost changed pixel in lane zero.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void UpdateBounds(uint differences, int offset, ref Vector128<int> left, ref Vector128<int> right)
    {
        // Four adjacent mask bits represent R, G, B and alpha for one pixel.
        // Dividing the first set-bit index by four selects the first changed pixel.
        int first = offset + (BitOperations.TrailingZeroCount(differences) / 4);

        // Round the significant-bit count up to a whole pixel to obtain the exclusive right edge.
        int last = offset + ((32 - BitOperations.LeadingZeroCount(differences) + 3) / 4);
        left = Vector128.Min(left, Vector128.CreateScalar(first));
        right = Vector128.Max(right, Vector128.CreateScalar(last));
    }

    /// <summary>
    /// Uses Rgba32 rows to retain unassociated byte channels.
    /// </summary>
    private readonly struct UnassociatedByteOperator : IRowOperator<Rgba32>
    {
        /// <inheritdoc />
        public static Rgba32 ConvertReplacement<TPixel>(TPixel replacement)
            where TPixel : unmanaged, IPixel<TPixel>
            => replacement.ToRgba32();

        /// <inheritdoc />
        public static void ToRow<TPixel>(
            PixelOperations<TPixel> pixelOperations, Configuration configuration, ReadOnlySpan<TPixel> source, Span<Rgba32> destination)
            where TPixel : unmanaged, IPixel<TPixel>
            => pixelOperations.ToRgba32(configuration, source, destination);

        /// <inheritdoc />
        public static void FromRow<TPixel>(PixelOperations<TPixel> pixelOperations, Configuration configuration, Span<Rgba32> source, Span<TPixel> destination)
            where TPixel : unmanaged, IPixel<TPixel>
            => pixelOperations.FromRgba32(configuration, source, destination);

        /// <inheritdoc />
        public static bool CompareRow<TReplacement>(
            ReadOnlySpan<Rgba32> previous, Span<Rgba32> current, ReadOnlySpan<Rgba32> next, Rgba32 replacement, ref int left, ref int right)
            where TReplacement : struct, IReplacementOperator
            => DeDuplicateByteRow<TReplacement>(
                MemoryMarshal.Cast<Rgba32, uint>(previous),
                MemoryMarshal.Cast<Rgba32, uint>(current),
                MemoryMarshal.Cast<Rgba32, uint>(next),
                replacement.PackedValue,
                ref left,
                ref right);
    }

    /// <summary>
    /// Uses Rgba32P rows to retain associated byte channels.
    /// </summary>
    private readonly struct AssociatedByteOperator : IRowOperator<Rgba32P>
    {
        /// <inheritdoc />
        public static Rgba32P ConvertReplacement<TPixel>(TPixel replacement)
            where TPixel : unmanaged, IPixel<TPixel>
            => replacement.ToRgba32P();

        /// <inheritdoc />
        public static void ToRow<TPixel>(
            PixelOperations<TPixel> pixelOperations, Configuration configuration, ReadOnlySpan<TPixel> source, Span<Rgba32P> destination)
            where TPixel : unmanaged, IPixel<TPixel>
            => pixelOperations.ToRgba32P(configuration, source, destination);

        /// <inheritdoc />
        public static void FromRow<TPixel>(PixelOperations<TPixel> pixelOperations, Configuration configuration, Span<Rgba32P> source, Span<TPixel> destination)
            where TPixel : unmanaged, IPixel<TPixel>
            => pixelOperations.FromRgba32P(configuration, source, destination);

        /// <inheritdoc />
        public static bool CompareRow<TReplacement>(
            ReadOnlySpan<Rgba32P> previous, Span<Rgba32P> current, ReadOnlySpan<Rgba32P> next, Rgba32P replacement, ref int left, ref int right)
            where TReplacement : struct, IReplacementOperator
            => DeDuplicateByteRow<TReplacement>(
                MemoryMarshal.Cast<Rgba32P, uint>(previous),
                MemoryMarshal.Cast<Rgba32P, uint>(current),
                MemoryMarshal.Cast<Rgba32P, uint>(next),
                replacement.PackedValue,
                ref left,
                ref right);
    }

    /// <summary>
    /// Retains native component precision and alpha in floating-point rows.
    /// </summary>
    private readonly struct VectorOperator : IRowOperator<Vector4>
    {
        /// <inheritdoc />
        public static Vector4 ConvertReplacement<TPixel>(TPixel replacement)
            where TPixel : unmanaged, IPixel<TPixel>
            => replacement.ToVector4();

        /// <inheritdoc />
        public static void ToRow<TPixel>(
            PixelOperations<TPixel> pixelOperations, Configuration configuration, ReadOnlySpan<TPixel> source, Span<Vector4> destination)
            where TPixel : unmanaged, IPixel<TPixel>
            => pixelOperations.ToVector4(configuration, source, destination);

        /// <inheritdoc />
        public static void FromRow<TPixel>(PixelOperations<TPixel> pixelOperations, Configuration configuration, Span<Vector4> source, Span<TPixel> destination)
            where TPixel : unmanaged, IPixel<TPixel>
            => pixelOperations.FromVector4Destructive(configuration, source, destination);

        /// <inheritdoc />
        public static bool CompareRow<TReplacement>(
            ReadOnlySpan<Vector4> previous, Span<Vector4> current, ReadOnlySpan<Vector4> next, Vector4 replacement, ref int left, ref int right)
            where TReplacement : struct, IReplacementOperator
            => DeDuplicatePixelRow<TReplacement>(previous, current, next, replacement, ref left, ref right);
    }

    /// <summary>
    /// Retains current pixels while calculating the changed bounds.
    /// </summary>
    private readonly struct KeepPixelsOperator : IReplacementOperator
    {
        /// <inheritdoc />
        public static bool IsEnabled => false;
    }

    /// <summary>
    /// Replaces matching pixels while calculating the changed bounds.
    /// </summary>
    private readonly struct ReplaceDuplicatesOperator : IReplacementOperator
    {
        /// <inheritdoc />
        public static bool IsEnabled => true;
    }
}

#pragma warning disable SA1201 // Elements should appear in the correct order
internal enum ClampingMode
#pragma warning restore SA1201 // Elements should appear in the correct order
{
    None,

    Even,
}
