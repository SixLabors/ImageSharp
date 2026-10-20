// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Tiff.Readers;

/// <summary>
/// Owns reusable color conversion rows for one TIFF strip or tile reader.
/// </summary>
internal abstract class TiffBaseColorDecoder : IDisposable
{
    private IMemoryOwner<Rgb>? rgbBuffer;
    private IMemoryOwner<Vector4>? vectorBuffer;

    /// <inheritdoc />
    public void Dispose()
    {
        this.rgbBuffer?.Dispose();
        this.vectorBuffer?.Dispose();
        this.rgbBuffer = null;
        this.vectorBuffer = null;
    }

    /// <summary>
    /// Gets reusable storage for a row of RGB color samples.
    /// </summary>
    /// <param name="allocator">The decoder memory allocator.</param>
    /// <param name="length">The number of samples in the current row.</param>
    /// <returns>A row span that remains valid until this reader is disposed.</returns>
    /// <remarks>
    /// The decoder processes a full-width strip or tile first. Later edge tiles can request a shorter row.
    /// The first request therefore determines the required capacity for this reader.
    /// </remarks>
    protected Span<Rgb> GetRgbBuffer(MemoryAllocator allocator, int length)
    {
        // Keep the row for the reader lifetime. Each strip or tile reuses the same allocator-owned storage.
        this.rgbBuffer ??= allocator.Allocate<Rgb>(length);
        return this.rgbBuffer.GetSpan()[..length];
    }

    /// <summary>
    /// Gets reusable storage for a row of four-component color samples.
    /// </summary>
    /// <param name="allocator">The decoder memory allocator.</param>
    /// <param name="length">The number of samples in the current row.</param>
    /// <returns>A row span that remains valid until this reader is disposed.</returns>
    /// <remarks>
    /// The decoder processes a full-width strip or tile first. Later edge tiles can request a shorter row.
    /// The first request therefore determines the required capacity for this reader.
    /// </remarks>
    protected Span<Vector4> GetVectorBuffer(MemoryAllocator allocator, int length)
    {
        // RGB and four-component rows have separate owners so neither requires a larger contiguous allocation.
        this.vectorBuffer ??= allocator.Allocate<Vector4>(length);
        return this.vectorBuffer.GetSpan()[..length];
    }
}
