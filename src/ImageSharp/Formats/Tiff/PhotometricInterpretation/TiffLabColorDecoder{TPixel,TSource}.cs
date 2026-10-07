// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Icc;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Decodes packed TIFF Lab samples through color profile conversion.
/// </summary>
/// <typeparam name="TPixel">The destination pixel format.</typeparam>
/// <typeparam name="TSource">The stored Lab sample format.</typeparam>
internal class TiffLabColorDecoder<TPixel, TSource> : TiffBaseColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
    where TSource : struct, ITiffColorSource<float>
{
    private readonly Configuration configuration;
    private readonly TSource source;
    private readonly ColorProfileConverter converter;
    private readonly PixelConversionModifiers modifiers;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffLabColorDecoder{TPixel, TSource}"/> class.
    /// </summary>
    /// <param name="configuration">The image configuration.</param>
    /// <param name="source">The packed Lab source.</param>
    /// <param name="metadata">The frame metadata.</param>
    /// <param name="allocator">The decoder memory allocator.</param>
    public TiffLabColorDecoder(Configuration configuration, TSource source, ImageFrameMetadata metadata, MemoryAllocator allocator)
    {
        this.configuration = configuration;
        this.source = source;
        bool hasProfile = IccProfile.TryGetIccProfileForColorConversion(metadata.IccProfile, out IccProfile? iccProfile);
        ColorConversionOptions options = new() { SourceIccProfile = iccProfile, TargetIccProfile = hasProfile ? CompactSrgbV4Profile.Profile : null, MemoryAllocator = allocator };

        this.converter = new ColorProfileConverter(options);
        this.modifiers = hasProfile ? PixelConversionModifiers.Scale | PixelConversionModifiers.UnPremultiply : PixelConversionModifiers.Scale;
    }

    /// <inheritdoc />
    public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
    {
        // The color converter reads the complete Lab row before writing RGB output.
        // Lab and RGB each use three floats, so the input and output can share one row buffer.
        using IMemoryOwner<Rgb> rgbBuffer = this.converter.Options.MemoryAllocator.Allocate<Rgb>(width);
        Span<Rgb> rgbRow = rgbBuffer.Memory.Span[..width];
        Span<CieLab> labRow = MemoryMarshal.Cast<Rgb, CieLab>(rgbRow);
        InlineArray16<Vector4> vectorBuffer = default;
        Span<Vector4> vectors = vectorBuffer;
        int offset = 0;

        for (int y = top; y < top + height; y++)
        {
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);
            int rowByteCount = width * this.source.BytesPerPixel;
            TiffLabRowReader<TSource>.Read(data.Slice(offset, rowByteCount), [], [], labRow, this.source);
            this.converter.Convert<CieLab, Rgb>(labRow, rgbRow);

            for (int x = 0; x < width; x += vectors.Length)
            {
                int count = Math.Min(vectors.Length, width - x);
                Span<Vector4> block = vectors[..count];

                // Pixel packing destructively edits the scratch block, leaving the converted RGB row intact.
                Rgb.ToScaledVector4(rgbRow.Slice(x, count), block);
                PixelOperations<TPixel>.Instance.FromVector4Destructive(this.configuration, block, pixelRow.Slice(x, count), this.modifiers);
            }

            offset += rowByteCount;
        }
    }
}
