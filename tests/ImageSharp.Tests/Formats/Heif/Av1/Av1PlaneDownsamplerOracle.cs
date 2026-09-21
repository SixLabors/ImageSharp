// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Halves a plane with a direct transcription of the reference decoder's separable passes.
/// </summary>
/// <remarks>
/// This is the oracle the halving filter and the image pyramid are both measured against, so it is
/// deliberately written as the plainest possible form of the reference rather than as fast code.
/// Reference: av1_resize_plane_to_half() and down2_symeven().
/// </remarks>
internal static class Av1PlaneDownsamplerOracle
{
    /// <summary>
    /// Gets one half of the normative symmetric-even kernel, as the reference decoder states it.
    /// </summary>
    private static ReadOnlySpan<short> HalfFilter => [56, 12, -3, -1];

    /// <summary>
    /// Halves both axes of one plane.
    /// </summary>
    /// <param name="source">The source samples.</param>
    /// <param name="sourceStride">The source row stride.</param>
    /// <param name="width">The source width.</param>
    /// <param name="height">The source height.</param>
    /// <param name="destination">The halved samples.</param>
    /// <param name="destinationStride">The destination row stride.</param>
    public static void Halve(
        ReadOnlySpan<byte> source,
        int sourceStride,
        int width,
        int height,
        Span<byte> destination,
        int destinationStride)
    {
        int halvedWidth = width >> 1;
        int halvedHeight = height >> 1;
        byte[] intermediate = new byte[halvedWidth * height];
        for (int y = 0; y < height; y++)
        {
            HalveLine(
                source.Slice(y * sourceStride, width),
                intermediate.AsSpan(y * halvedWidth, halvedWidth));
        }

        byte[] column = new byte[height];
        byte[] halvedColumn = new byte[halvedHeight];
        for (int x = 0; x < halvedWidth; x++)
        {
            for (int y = 0; y < height; y++)
            {
                column[y] = intermediate[(y * halvedWidth) + x];
            }

            HalveLine(column, halvedColumn);

            for (int y = 0; y < halvedHeight; y++)
            {
                destination[(y * destinationStride) + x] = halvedColumn[y];
            }
        }
    }

    /// <summary>
    /// Halves one line with the reference's clamped symmetric kernel.
    /// </summary>
    /// <param name="source">The source samples.</param>
    /// <param name="destination">The halved samples.</param>
    /// <remarks>
    /// One clamped form covers every output, which is the result the reference reaches through its
    /// three-part split of the line.
    /// </remarks>
    private static void HalveLine(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        ReadOnlySpan<short> filter = HalfFilter;
        int last = source.Length - 1;
        int index = 0;
        for (int position = 0; position < source.Length; position += 2)
        {
            int sum = 1 << 6;
            for (int tap = 0; tap < filter.Length; tap++)
            {
                sum += (source[Math.Max(position - tap, 0)] + source[Math.Min(position + 1 + tap, last)]) * filter[tap];
            }

            destination[index++] = (byte)Math.Clamp(sum >> 7, 0, 255);
        }
    }
}
