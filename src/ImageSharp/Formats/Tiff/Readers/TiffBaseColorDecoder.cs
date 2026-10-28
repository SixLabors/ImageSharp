// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Icc;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Readers;

/// <summary>
/// Owns reusable color conversion rows for one TIFF strip or tile reader.
/// </summary>
internal abstract class TiffBaseColorDecoder : IDisposable
{
    private IMemoryOwner<Rgb>? rgbBuffer;
    private IMemoryOwner<Vector4>? vectorBuffer;
    private ColorProfileConverter? colorConverter;

    /// <summary>
    /// Gets a value indicating whether native RGB or grayscale samples require ICC conversion before destination packing.
    /// </summary>
    protected bool HasColorConverter => this.colorConverter is not null;

    /// <summary>
    /// Configures ICC conversion once when the decoder creates this ordinary RGB or grayscale reader.
    /// </summary>
    /// <param name="configuration">The image configuration.</param>
    /// <param name="profile">The frame's source profile.</param>
    public void ConfigureColorConversion(Configuration configuration, IccProfile? profile)
    {
        if (IccProfile.TryGetIccProfileForColorConversion(profile, out IccProfile? sourceProfile))
        {
            this.colorConverter = new ColorProfileConverter(new ColorConversionOptions
            {
                SourceIccProfile = sourceProfile,
                TargetIccProfile = CompactSrgbV4Profile.Profile,
                MemoryAllocator = configuration.MemoryAllocator
            });
        }
    }

    /// <summary>
    /// Gets a whole floating row for ICC, or the caller's bounded packing block when ICC is not required.
    /// </summary>
    /// <param name="configuration">The image configuration.</param>
    /// <param name="width">The decoded row width.</param>
    /// <param name="fallback">The existing stack buffer for direct pixel packing.</param>
    /// <returns>The reusable row or the supplied packing block.</returns>
    protected Span<Vector4> GetColorBuffer(Configuration configuration, int width, Span<Vector4> fallback)
        => this.HasColorConverter ? this.GetVectorBuffer(configuration.MemoryAllocator, width) : fallback;

    /// <summary>
    /// Selects a block in the floating ICC row, or reuses the start of the direct packing buffer.
    /// </summary>
    /// <param name="vectors">The row or bounded packing buffer.</param>
    /// <param name="offset">The horizontal position in the decoded row.</param>
    /// <param name="count">The number of pixels in this block.</param>
    /// <returns>The initialized block's destination.</returns>
    protected Span<Vector4> GetColorBlock(Span<Vector4> vectors, int offset, int count)
        => vectors.Slice(this.HasColorConverter ? offset : 0, count);

    /// <summary>
    /// Stages native pixel samples in the ICC row without reducing their precision to the requested destination.
    /// </summary>
    /// <typeparam name="TPixel">The native storage pixel format.</typeparam>
    /// <param name="configuration">The image configuration.</param>
    /// <param name="source">The native pixel block.</param>
    /// <param name="vectors">The reusable ICC row.</param>
    /// <param name="offset">The horizontal position of this block.</param>
    /// <returns>Whether ICC requires staging. Otherwise the caller uses its existing direct bulk conversion.</returns>
    protected bool TryStoreColorPixels<TPixel>(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Vector4> vectors, int offset)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (!this.HasColorConverter)
        {
            return false;
        }

        PixelOperations<TPixel>.Instance.ToUnassociatedScaledVector4(configuration, source, vectors.Slice(offset, source.Length));

        return true;
    }

    /// <summary>
    /// Packs a normalized block directly when ICC is absent. Profiled blocks remain in the reusable row.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel format.</typeparam>
    /// <param name="configuration">The image configuration.</param>
    /// <param name="vectors">The decoded component block.</param>
    /// <param name="destination">The destination pixels.</param>
    /// <param name="alphaRepresentation">The stored samples' alpha representation.</param>
    protected void StoreColorVectors<TPixel>(
        Configuration configuration,
        Span<Vector4> vectors,
        Span<TPixel> destination,
        PixelAlphaRepresentation alphaRepresentation)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (!this.HasColorConverter)
        {
            switch (alphaRepresentation)
            {
                case PixelAlphaRepresentation.Associated:
                    PixelOperations<TPixel>.Instance.FromAssociatedScaledVector4Destructive(configuration, vectors, destination);
                    break;
                case PixelAlphaRepresentation.Unassociated:
                    PixelOperations<TPixel>.Instance.FromUnassociatedScaledVector4Destructive(configuration, vectors, destination);
                    break;
                default:
                    PixelOperations<TPixel>.Instance.FromScaledVector4Destructive(configuration, vectors, destination);
                    break;
            }
        }
    }

    /// <summary>
    /// Converts the completed native row through ICC, then applies destination precision and alpha once.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel format.</typeparam>
    /// <param name="configuration">The image configuration.</param>
    /// <param name="vectors">The reusable floating row, or the direct packing buffer when ICC is absent.</param>
    /// <param name="destination">The destination row.</param>
    /// <param name="alphaRepresentation">The stored samples' alpha representation.</param>
    protected void CompleteColorRow<TPixel>(
        Configuration configuration,
        Span<Vector4> vectors,
        Span<TPixel> destination,
        PixelAlphaRepresentation alphaRepresentation)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (this.colorConverter is not null)
        {
            // ICC consumes straight RGB. The floating color API applies finite working-color bounds and alpha [0,1].
            // It also unassociates associated samples. TPixel has not discarded source precision or color components.
            if (alphaRepresentation == PixelAlphaRepresentation.Associated)
            {
                PixelOperations<RgbaVector>.Instance.FromAssociatedScaledVector4Destructive(
                    configuration, vectors, MemoryMarshal.Cast<Vector4, RgbaVector>(vectors));
            }
            else
            {
                PixelOperations<RgbaVector>.Instance.FromUnassociatedScaledVector4Destructive(
                    configuration, vectors, MemoryMarshal.Cast<Vector4, RgbaVector>(vectors));
            }

            // Both rows belong to this reader and are reused by later strips and tiles. Alpha stays in the vector row.
            this.colorConverter.Convert(vectors, this.GetRgbBuffer(configuration.MemoryAllocator, destination.Length));
            PixelOperations<TPixel>.Instance.FromUnassociatedScaledVector4Destructive(configuration, vectors, destination);
        }
    }

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
