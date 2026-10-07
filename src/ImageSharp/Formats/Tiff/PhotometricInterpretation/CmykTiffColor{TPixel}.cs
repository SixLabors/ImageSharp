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

internal class CmykTiffColor<TPixel> : TiffBaseColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly ColorProfileConverter colorProfileConverter;
    private readonly Configuration configuration;
    private readonly PixelConversionModifiers modifiers;

    /// <summary>
    /// Initializes a new instance of the <see cref="CmykTiffColor{TPixel}"/> class.
    /// </summary>
    /// <param name="configuration">The image configuration.</param>
    /// <param name="metadata">The frame metadata.</param>
    /// <param name="allocator">The allocator for color conversion buffers.</param>
    public CmykTiffColor(Configuration configuration, ImageFrameMetadata metadata, MemoryAllocator allocator)
    {
        this.configuration = configuration;
        bool hasProfile = IccProfile.TryGetIccProfileForColorConversion(metadata.IccProfile, out IccProfile? profile);
        ColorConversionOptions options = new() { SourceIccProfile = profile, TargetIccProfile = hasProfile ? CompactSrgbV4Profile.Profile : null, MemoryAllocator = allocator };

        this.colorProfileConverter = new ColorProfileConverter(options);
        this.modifiers = hasProfile ? PixelConversionModifiers.Scale | PixelConversionModifiers.UnPremultiply : PixelConversionModifiers.Scale;
    }

    /// <inheritdoc/>
    public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
    {
        int offset = 0;

        // The converter moves the complete CMYK input into its PCS buffers before writing RGB.
        // The first three floats per pixel can therefore receive RGB after the four-float source is consumed.
        using IMemoryOwner<Vector4> vectorBuffer = this.colorProfileConverter.Options.MemoryAllocator.Allocate<Vector4>(width);
        Span<Vector4> vectorRow = vectorBuffer.Memory.Span[..width];
        Span<Cmyk> cmykRow = MemoryMarshal.Cast<Vector4, Cmyk>(vectorRow);
        Span<float> floatRow = MemoryMarshal.Cast<Vector4, float>(vectorRow);
        Span<Rgb> rgbRow = MemoryMarshal.Cast<float, Rgb>(floatRow[..(width * 3)]);
        InlineArray16<Vector4> blockBuffer = default;
        Span<Vector4> vectors = blockBuffer;

        for (int y = top; y < top + height; y++)
        {
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);

            // Normalize packed component bytes into the typed row before the one whole-row profile conversion.
            SimdUtils.ByteToNormalizedFloat(data.Slice(offset, width * 4), floatRow);
            offset += width * 4;

            this.colorProfileConverter.Convert<Cmyk, Rgb>(cmykRow, rgbRow);

            for (int x = 0; x < width; x += vectors.Length)
            {
                int count = Math.Min(vectors.Length, width - x);
                Span<Vector4> block = vectors[..count];

                // Pixel packing may overwrite its input, so convert a bounded RGB block into separate scratch.
                Rgb.ToScaledVector4(rgbRow.Slice(x, count), block);
                PixelOperations<TPixel>.Instance.FromVector4Destructive(this.configuration, block, pixelRow.Slice(x, count), this.modifiers);
            }
        }
    }
}
