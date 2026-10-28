// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Icc;
using SixLabors.ImageSharp.Formats.Tiff.Utils;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Readers;

internal class CmykTiffColor<TPixel> : TiffBaseColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly ColorProfileConverter colorProfileConverter;
    private readonly Configuration configuration;
    private readonly bool hasProfile;
    private readonly int bitsPerSample;
    private readonly bool isBigEndian;

    /// <summary>
    /// Initializes a new instance of the <see cref="CmykTiffColor{TPixel}"/> class.
    /// </summary>
    /// <param name="configuration">The image configuration.</param>
    /// <param name="metadata">The frame metadata.</param>
    /// <param name="allocator">The allocator for color conversion buffers.</param>
    /// <param name="bitsPerSample">The validated ink depth, 8 or 16.</param>
    /// <param name="isBigEndian">Whether the stored sample words use big-endian byte order.</param>
    public CmykTiffColor(Configuration configuration, ImageFrameMetadata metadata, MemoryAllocator allocator, int bitsPerSample, bool isBigEndian)
    {
        this.configuration = configuration;
        this.bitsPerSample = bitsPerSample;
        this.isBigEndian = isBigEndian;
        bool hasProfile = IccProfile.TryGetIccProfileForColorConversion(metadata.IccProfile, out IccProfile? profile);
        ColorConversionOptions options = new()
        {
            SourceIccProfile = profile,
            TargetIccProfile = hasProfile ? CompactSrgbV4Profile.Profile : null,
            MemoryAllocator = allocator
        };

        this.colorProfileConverter = new ColorProfileConverter(options);
        this.hasProfile = hasProfile;
    }

    /// <inheritdoc/>
    public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
    {
        int offset = 0;

        // Each conversion block reads four ink components per pixel before it writes three RGB components.
        // RGB output stays before the next unread CMYK block.
        Span<Vector4> vectorRow = this.GetVectorBuffer(this.colorProfileConverter.Options.MemoryAllocator, width);
        Span<Cmyk> cmykRow = MemoryMarshal.Cast<Vector4, Cmyk>(vectorRow);
        Span<float> floatRow = MemoryMarshal.Cast<Vector4, float>(vectorRow);
        Span<Rgb> rgbRow = MemoryMarshal.Cast<float, Rgb>(floatRow[..(width * 3)]);
        InlineArray16<Vector4> blockBuffer = default;
        Span<Vector4> vectors = blockBuffer;

        for (int y = top; y < top + height; y++)
        {
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);

            // Normalize all four ink channels before the whole-row color conversion. K never represents alpha.
            if (this.bitsPerSample == 8)
            {
                SimdUtils.ByteToNormalizedFloat(data.Slice(offset, width * 4), floatRow);
                offset += width * 4;
            }
            else
            {
                ReadOnlySpan<byte> samples = data.Slice(offset, width * 8);
                if (this.isBigEndian != BitConverter.IsLittleEndian)
                {
                    // The stored words already have the host byte order, so normalize directly into the floating row.
                    SimdUtils.UInt16ToNormalizedFloat(MemoryMarshal.Cast<byte, ushort>(samples), floatRow);
                }
                else
                {
                    // Reuse the existing pixel-packing block for byte-order conversion before ICC uses it.
                    // This bounded block needs no additional allocation or full-row copy.
                    Span<ushort> words = MemoryMarshal.Cast<Vector4, ushort>(vectors);
                    Span<float> remaining = floatRow;
                    while (!remaining.IsEmpty)
                    {
                        int count = Math.Min(remaining.Length, words.Length);
                        Span<ushort> block = words[..count];
                        TiffUtilities.CopyUShorts(samples[..(count * sizeof(ushort))], block, this.isBigEndian, invert: false);
                        SimdUtils.UInt16ToNormalizedFloat(block, remaining[..count]);
                        remaining = remaining[count..];
                        samples = samples[(count * sizeof(ushort))..];
                    }
                }

                offset += width * 8;
            }

            this.colorProfileConverter.Convert<Cmyk, Rgb>(cmykRow, rgbRow);

            for (int x = 0; x < width; x += vectors.Length)
            {
                int count = Math.Min(vectors.Length, width - x);
                Span<Vector4> block = vectors[..count];

                // Pixel packing may overwrite its input, so convert a bounded RGB block into separate scratch.
                Rgb.ToScaledVector4(rgbRow.Slice(x, count), block);

                // ICC output is straight RGB; without ICC, retain the existing native-alpha packing path.
                if (this.hasProfile)
                {
                    PixelOperations<TPixel>.Instance.FromUnassociatedScaledVector4Destructive(this.configuration, block, pixelRow.Slice(x, count));
                }
                else
                {
                    PixelOperations<TPixel>.Instance.FromScaledVector4Destructive(this.configuration, block, pixelRow.Slice(x, count));
                }
            }
        }
    }
}
