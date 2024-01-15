// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

// The JIT can detect and optimize rotation idioms ROTL (Rotate Left)
// and ROTR (Rotate Right) emitting efficient CPU instructions:
// https://github.com/dotnet/coreclr/pull/1830
namespace SixLabors.ImageSharp;

/// <summary>
/// Defines the contract for methods that allow the shuffling of pixel components.
/// Used for shuffling on platforms that do not support Hardware Intrinsics.
/// </summary>
internal interface IComponentShuffle
{
    /// <summary>
    /// Shuffles then slices 8-bit integers in <paramref name="source"/>
    /// using a byte control and store the results in <paramref name="destination"/>.
    /// If successful, this method will reduce the length of <paramref name="source"/> length
    /// by the shuffle amount.
    /// </summary>
    /// <param name="source">The source span of bytes.</param>
    /// <param name="destination">The destination span of bytes.</param>
    void ShuffleReduce(ref ReadOnlySpan<byte> source, ref Span<byte> destination);

    /// <summary>
    /// Shuffle 8-bit integers in <paramref name="source"/>
    /// using the control and store the results in <paramref name="destination"/>.
    /// </summary>
    /// <param name="source">The source span of bytes.</param>
    /// <param name="destination">The destination span of bytes.</param>
    /// <remarks>
    /// Implementation can assume that source.Length is less or equal than destination.Length.
    /// Loops should iterate using source.Length.
    /// </remarks>
    void Shuffle(ReadOnlySpan<byte> source, Span<byte> destination);
}
