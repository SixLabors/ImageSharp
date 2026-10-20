// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Formats.Tiff.ColorConverters;
using SixLabors.ImageSharp.Formats.Tiff.Compression;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Writers;

/// <summary>
/// Writes unsigned lightness and signed chroma without changing the source frame.
/// </summary>
/// <typeparam name="TPixel">The source pixel format.</typeparam>
internal sealed class TiffLabWriter<TPixel> : TiffBaseColorWriter<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly ColorProfileConverter converter;
    private readonly TiffLabEncodeOperator packingOperator;
    private readonly bool clearTransparentPixels;

    private IMemoryOwner<byte>? stripBuffer;
    private IMemoryOwner<float>? conversionBuffer;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffLabWriter{TPixel}"/> class.
    /// </summary>
    /// <param name="image">The source frame in working sRGB.</param>
    /// <param name="encodingSize">The encoded region size.</param>
    /// <param name="memoryAllocator">The allocator for reusable row and strip storage.</param>
    /// <param name="configuration">The pixel conversion configuration.</param>
    /// <param name="entriesCollector">The frame's TIFF directory entries.</param>
    /// <param name="bitsPerPixel">The resolved total depth, 24 or 48.</param>
    /// <param name="converter">The converter to the retained Lab profile, or null for unprofiled D50 Lab.</param>
    /// <param name="clearTransparentPixels">Whether to clear fully transparent RGB before conversion.</param>
    public TiffLabWriter(
        ImageFrame<TPixel> image,
        Size encodingSize,
        MemoryAllocator memoryAllocator,
        Configuration configuration,
        TiffEncoderEntriesCollector entriesCollector,
        int bitsPerPixel,
        ColorProfileConverter? converter,
        bool clearTransparentPixels)
        : base(image, encodingSize, memoryAllocator, configuration, entriesCollector)
    {
        this.BitsPerPixel = bitsPerPixel;
        this.converter = converter ?? new ColorProfileConverter(new ColorConversionOptions { MemoryAllocator = memoryAllocator });
        this.packingOperator = new TiffLabEncodeOperator(bitsPerPixel == 48);
        this.clearTransparentPixels = clearTransparentPixels;
    }

    /// <inheritdoc />
    public override int BitsPerPixel { get; }

    /// <inheritdoc />
    protected override void EncodeStrip(int y, int height, TiffBaseCompressor compressor)
    {
        int width = this.Width;
        int bytesPerRow = this.BytesPerRow;
        int byteCount = height * bytesPerRow;

        // Keep strip and conversion storage for the writer's lifetime, rather than renting for each row.
        this.stripBuffer ??= this.MemoryAllocator.Allocate<byte>(byteCount);
        this.conversionBuffer ??= this.MemoryAllocator.Allocate<float>(width * 7);

        Span<byte> output = this.stripBuffer.GetSpan()[..byteCount];
        Span<float> storage = this.conversionBuffer.GetSpan();

        // The first four floats per pixel retain alpha until transparent RGB has been cleared.
        // The remaining three floats hold RGB, then Lab, then normalized integer codes in the same storage.
        Span<Vector4> vectors = MemoryMarshal.Cast<float, Vector4>(storage[..(width * 4)]);
        Span<float> components = storage.Slice(width * 4, width * 3);
        Span<Rgb> rgb = MemoryMarshal.Cast<float, Rgb>(components);
        Span<CieLab> lab = MemoryMarshal.Cast<float, CieLab>(components);
        TiffFloat32Source sourceLayout = new(isBigEndian: !BitConverter.IsLittleEndian, hasAlpha: false);

        for (int row = y; row < y + height; row++)
        {
            ReadOnlySpan<TPixel> source = this.Image.PixelBuffer.DangerousGetRowSpan(row)[..width];
            PixelOperations<TPixel>.Instance.ToUnassociatedScaledVector4(this.Configuration, source, vectors);

            if (this.clearTransparentPixels)
            {
                EncodingUtilities.ReplaceTransparentPixels(vectors);
            }

            Rgb.FromScaledVector4(vectors, rgb);
            this.converter.Convert<Rgb, CieLab>(rgb, lab);

            // Reuse TIFF's closed generic traversal for all SIMD widths and the scalar tail.
            // The three input floats are L*, a* and b*. The operator supplies unsigned codes, retaining signed chroma bits.
            TiffOperatedColorConverter<TiffFloat32Source, TiffLabEncodeOperator, TiffFloat3Store, float, float>.Convert(
                MemoryMarshal.AsBytes(lab),
                [],
                [],
                [],
                components,
                sourceLayout,
                this.packingOperator,
                default);

            Span<byte> samples = output.Slice((row - y) * bytesPerRow, bytesPerRow);

            if (this.BitsPerPixel == 24)
            {
                SimdUtils.NormalizedFloatToByteSaturate(components, samples);
            }
            else
            {
                // The file header declares host byte order, matching the shared native-word packer.
                SimdUtils.NormalizedFloatToUInt16Saturate(components, MemoryMarshal.Cast<byte, ushort>(samples));
            }
        }

        compressor.CompressStrip(output, height);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        this.stripBuffer?.Dispose();
        this.conversionBuffer?.Dispose();
    }
}
