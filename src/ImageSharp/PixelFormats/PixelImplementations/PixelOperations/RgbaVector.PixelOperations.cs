// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.PixelFormats;

/// <content>
/// Selects the shared storage-family implementation.
/// </content>
public partial struct RgbaVector
{
    /// <summary>
    /// Inherits the shared bulk conversions for this pixel's storage.
    /// </summary>
    internal partial class PixelOperations : RgbaVectorPixelOperations<RgbaVector>
    {
        /// <inheritdoc />
        public override void From<TPixelFrom>(Configuration configuration, ReadOnlySpan<TPixelFrom> source, Span<RgbaVector> destination)
        {
            // RgbaVector stores straight RGBA floats in Vector4 order. Write into that final storage without a temporary row or copy.
            // The single-pixel conversions take the straight scaled vector of the source. An associated source unassociates its
            // own storage, so a byte source divides the stored magnitudes one time. The source kernel does the same.
            Span<Vector4> vectors = MemoryMarshal.Cast<RgbaVector, Vector4>(destination);
            PixelOperations<TPixelFrom>.Instance.ToUnassociatedScaledVector4(configuration, source, vectors);
        }
    }
}
