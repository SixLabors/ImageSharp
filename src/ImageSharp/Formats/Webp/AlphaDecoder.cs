// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Compression;
using SixLabors.ImageSharp.Formats.Webp.BitReader;
using SixLabors.ImageSharp.Formats.Webp.Lossless;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Webp;

/// <summary>
/// Implements decoding for lossy alpha chunks which may be compressed.
/// </summary>
internal class AlphaDecoder : IDisposable
{
    private readonly MemoryAllocator memoryAllocator;

    /// <summary>
    /// The image data integrity mode. It controls damaged or truncated alpha data.
    /// </summary>
    private readonly SegmentIntegrityHandling segmentIntegrityHandling;

    /// <summary>
    /// Initializes a new instance of the <see cref="AlphaDecoder"/> class.
    /// </summary>
    /// <param name="width">The width of the image.</param>
    /// <param name="height">The height of the image.</param>
    /// <param name="data">The alpha data. It can be compressed.</param>
    /// <param name="alphaChunkHeader">
    /// The first byte of the alpha image stream. It tells how to decode the stream. <see cref="WebpChunkParsingUtils.TryReadAlphaChunk"/> checked that it has
    /// only defined values.
    /// </param>
    /// <param name="memoryAllocator">The memory allocator.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="segmentIntegrityHandling">The image data integrity mode. It controls damaged or truncated alpha data.</param>
    public AlphaDecoder(
        int width,
        int height,
        IMemoryOwner<byte> data,
        byte alphaChunkHeader,
        MemoryAllocator memoryAllocator,
        Configuration configuration,
        SegmentIntegrityHandling segmentIntegrityHandling)
    {
        this.Width = width;
        this.Height = height;
        this.Data = data;
        this.memoryAllocator = memoryAllocator;
        this.segmentIntegrityHandling = segmentIntegrityHandling;
        this.LastRow = 0;
        int totalPixels = width * height;

        // Header bits 0 and 1 are the compression method, and bits 2 and 3 are the filter.
        this.Compressed = (WebpAlphaCompressionMethod)(alphaChunkHeader & 0x03) == WebpAlphaCompressionMethod.WebpLosslessCompression;

        // The allocation is not clean. Decode() writes each decoded row and sets the other alpha values to 0, the
        // transparent fill of the other codecs. A complete decode writes every value, so it clears nothing.
        this.Alpha = memoryAllocator.Allocate<byte>(totalPixels);
        this.AlphaFilterType = (WebpAlphaFilterType)((alphaChunkHeader >> 2) & 0x03);
        this.Vp8LDec = new Vp8LDecoder(width, height);
        this.Vp8LDec.AllocatePixels(memoryAllocator);

        if (this.Compressed)
        {
            // Decode() reads the lossless header, because damage in the header is image data damage. Decode() runs
            // inside the using statement of the caller. Thus, that statement disposes the buffers when the integrity
            // policy throws.
            Vp8LBitReader bitReader = new(data);
            this.LosslessDecoder = new WebpLosslessDecoder(bitReader, memoryAllocator, configuration);
        }
    }

    /// <summary>
    /// Gets the width of the image.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the height of the image.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Gets the used filter type.
    /// </summary>
    public WebpAlphaFilterType AlphaFilterType { get; }

    /// <summary>
    /// Gets or sets the last decoded row.
    /// </summary>
    public int LastRow { get; set; }

    /// <summary>
    /// Gets or sets the row before the last decoded row.
    /// </summary>
    public int PrevRow { get; set; }

    /// <summary>
    /// Gets information for decoding Vp8L compressed alpha data.
    /// </summary>
    public Vp8LDecoder Vp8LDec { get; }

    /// <summary>
    /// Gets the decoded alpha data.
    /// </summary>
    public IMemoryOwner<byte> Alpha { get; }

    /// <summary>
    /// Gets a value indicating whether the alpha channel uses compression.
    /// </summary>
    [MemberNotNullWhen(true, nameof(LosslessDecoder))]
    private bool Compressed { get; }

    /// <summary>
    /// Gets the (maybe compressed) alpha data.
    /// </summary>
    private IMemoryOwner<byte> Data { get; }

    /// <summary>
    /// Gets the Vp8L decoder which is used to de compress the alpha channel, if needed.
    /// </summary>
    private WebpLosslessDecoder? LosslessDecoder { get; }

    /// <summary>
    /// Gets a value indicating whether the decoding needs 1 byte per pixel for decoding.
    /// Although Alpha Channel requires only 1 byte per pixel, sometimes Vp8LDecoder may need to allocate
    /// 4 bytes per pixel internally during decode.
    /// <see cref="Decode"/> sets the value after it reads the lossless header. Before that, the value is
    /// <see langword="false"/>.
    /// </summary>
    public bool Use8BDecode { get; private set; }

    /// <summary>
    /// Decodes and filters the maybe compressed alpha data.
    /// </summary>
    public void Decode()
    {
        if (!this.Compressed)
        {
            Span<byte> dataSpan = this.Data.Memory.Span;
            int rows = this.Height;
            Span<byte> alphaSpan = this.Alpha.Memory.Span;
            if (dataSpan.Length < this.Width * this.Height)
            {
                // Raw alpha that is too short is truncated image data. The decoder keeps the complete rows.
                this.segmentIntegrityHandling.ThrowOrIgnoreImageDataError("Not enough data in the ALPH chunk.");
                rows = dataSpan.Length / this.Width;
                alphaSpan[(rows * this.Width)..].Clear();
            }

            if (this.AlphaFilterType == WebpAlphaFilterType.None)
            {
                dataSpan[..(rows * this.Width)].CopyTo(alphaSpan);
                return;
            }

            Span<byte> deltas = dataSpan;
            Span<byte> dst = alphaSpan;
            Span<byte> prev = default;
            for (int y = 0; y < rows; y++)
            {
                switch (this.AlphaFilterType)
                {
                    case WebpAlphaFilterType.Horizontal:
                        HorizontalUnfilter(prev, deltas, dst, this.Width);
                        break;
                    case WebpAlphaFilterType.Vertical:
                        VerticalUnfilter(prev, deltas, dst, this.Width);
                        break;
                    case WebpAlphaFilterType.Gradient:
                        GradientUnfilter(prev, deltas, dst, this.Width);
                        break;
                }

                prev = dst;
                deltas = deltas[this.Width..];
                dst = dst[this.Width..];
            }
        }
        else
        {
            // The color image exists before the alpha data. Thus, in the IgnoreImageData mode, the decoder does not throw for damaged headers, and the alpha
            // values are 0.
            if (!this.LosslessDecoder.TryReadHeaders(this.Vp8LDec, this.segmentIntegrityHandling, true))
            {
                // The header is damaged, so the decoder cannot decode an alpha value.
                this.Alpha.Memory.Span.Clear();
                return;
            }

            // If the alpha data uses only the color indexing transform, no color cache and trivial red, blue and
            // alpha codes, DecodeAlphaData() decodes it. This frequent case needs only 1 byte per pixel.
            this.Use8BDecode = this.Vp8LDec.Transforms.Count is 1
                               && this.Vp8LDec.Transforms[0].TransformType == Vp8LTransformType.ColorIndexingTransform
                               && Is8BOptimizable(this.Vp8LDec.Metadata);

            int rows;
            if (this.Use8BDecode)
            {
                this.LosslessDecoder.DecodeAlphaData(this, this.segmentIntegrityHandling);
                rows = this.LastRow;
            }
            else
            {
                rows = this.LosslessDecoder.DecodeImageRows(
                    this.Vp8LDec,
                    this.segmentIntegrityHandling,
                    "The WebP alpha data is damaged or ends before the last value.");

                this.ExtractAlphaRows(this.Vp8LDec, this.Width, rows);
            }

            // After damaged data, the rows after the decoded rows get 0.
            this.Alpha.Memory.Span[(rows * this.Width)..].Clear();
        }
    }

    /// <summary>
    /// Applies filtering to a set of rows.
    /// </summary>
    /// <param name="firstRow">The first row index to start filtering.</param>
    /// <param name="lastRow">The last row index for filtering.</param>
    /// <param name="dst">The destination to store the filtered data.</param>
    /// <param name="stride">The stride to use.</param>
    public void AlphaApplyFilter(int firstRow, int lastRow, Span<byte> dst, int stride)
    {
        if (this.AlphaFilterType == WebpAlphaFilterType.None)
        {
            return;
        }

        Span<byte> alphaSpan = this.Alpha.Memory.Span;
        Span<byte> prev = this.PrevRow == 0 ? null : alphaSpan[(this.Width * this.PrevRow)..];
        for (int y = firstRow; y < lastRow; y++)
        {
            switch (this.AlphaFilterType)
            {
                case WebpAlphaFilterType.Horizontal:
                    HorizontalUnfilter(prev, dst, dst, this.Width);
                    break;
                case WebpAlphaFilterType.Vertical:
                    VerticalUnfilter(prev, dst, dst, this.Width);
                    break;
                case WebpAlphaFilterType.Gradient:
                    GradientUnfilter(prev, dst, dst, this.Width);
                    break;
            }

            prev = dst;
            dst = dst[stride..];
        }

        this.PrevRow = lastRow - 1;
    }

    public void ExtractPalettedAlphaRows(int lastRow)
    {
        // For vertical and gradient filtering, we need to decode the part above the
        // cropTop row, in order to have the correct spatial predictors.
        int topRow = this.AlphaFilterType is WebpAlphaFilterType.None or WebpAlphaFilterType.Horizontal ? 0 : this.LastRow;
        int firstRow = this.LastRow < topRow ? topRow : this.LastRow;
        if (lastRow > firstRow)
        {
            // Special method for paletted alpha data.
            Span<byte> output = this.Alpha.Memory.Span;
            Span<uint> pixelData = this.Vp8LDec.Pixels.Memory.Span;
            Span<byte> pixelDataAsBytes = MemoryMarshal.Cast<uint, byte>(pixelData);
            Span<byte> dst = output[(this.Width * firstRow)..];
            Span<byte> input = pixelDataAsBytes[(this.Vp8LDec.Width * firstRow)..];

            // Decode() uses this path only for a stream with one transform, the color indexing transform.
            Vp8LTransform transform = this.Vp8LDec.Transforms[0];
            ColorIndexInverseTransformAlpha(transform, firstRow, lastRow, input, dst);
            this.AlphaApplyFilter(firstRow, lastRow, dst, this.Width);
        }

        this.LastRow = lastRow;
    }

    /// <summary>
    /// Once the image-stream is decoded into ARGB color values, the transparency information will be extracted from the green channel of the ARGB quadruplet.
    /// </summary>
    /// <param name="dec">The VP8L decoder.</param>
    /// <param name="width">The image width.</param>
    /// <param name="numRowsToProcess">The number of decoded rows. The alpha values of the other rows stay 0.</param>
    private void ExtractAlphaRows(Vp8LDecoder dec, int width, int numRowsToProcess)
    {
        Span<uint> input = dec.Pixels.Memory.Span;
        Span<byte> output = this.Alpha.Memory.Span;

        // Extract alpha (which is stored in the green plane).
        // the final width (!= dec->width_)
        int pixelCount = width * numRowsToProcess;
        WebpLosslessDecoder.ApplyInverseTransforms(dec, input, this.memoryAllocator);
        ExtractGreen(input, output, pixelCount);
        this.AlphaApplyFilter(0, numRowsToProcess, output, width);
    }

    private static void ColorIndexInverseTransformAlpha(
        Vp8LTransform transform,
        int yStart,
        int yEnd,
        ReadOnlySpan<byte> src,
        Span<byte> dst)
    {
        int bitsPerPixel = 8 >> transform.Bits;
        int width = transform.XSize;
        Span<uint> colorMap = transform.Data.Memory.Span;
        if (bitsPerPixel < 8)
        {
            int srcOffset = 0;
            int dstOffset = 0;
            int pixelsPerByte = 1 << transform.Bits;
            int countMask = pixelsPerByte - 1;
            int bitMask = (1 << bitsPerPixel) - 1;
            for (int y = yStart; y < yEnd; y++)
            {
                int packedPixels = 0;
                for (int x = 0; x < width; x++)
                {
                    if ((x & countMask) == 0)
                    {
                        packedPixels = src[srcOffset];
                        srcOffset++;
                    }

                    dst[dstOffset] = GetAlphaValue((int)colorMap[packedPixels & bitMask]);
                    dstOffset++;
                    packedPixels >>= bitsPerPixel;
                }
            }
        }
        else
        {
            MapAlpha(src, colorMap, dst, yStart, yEnd, width);
        }
    }

    private static void HorizontalUnfilter(ReadOnlySpan<byte> prev, Span<byte> input, Span<byte> dst, int width)
    {
        if (Vector128.IsHardwareAccelerated && width >= 9)
        {
            dst[0] = (byte)(input[0] + (prev.IsEmpty ? 0 : prev[0]));
            nuint i;
            Vector128<int> last = Vector128<int>.Zero.WithElement(0, dst[0]);
            ref byte srcRef = ref MemoryMarshal.GetReference(input);
            ref byte dstRef = ref MemoryMarshal.GetReference(dst);

            for (i = 1; i <= (uint)width - 8; i += 8)
            {
                Vector128<long> a0 = Vector128.Create(Unsafe.As<byte, long>(ref Unsafe.Add(ref srcRef, i)), 0);
                Vector128<byte> a1 = a0.AsByte() + last.AsByte();
                Vector128<byte> a2 = Vector128_.ShiftLeftBytesInVector(a1, 1);
                Vector128<byte> a3 = a1 + a2;
                Vector128<byte> a4 = Vector128_.ShiftLeftBytesInVector(a3, 2);
                Vector128<byte> a5 = a3 + a4;
                Vector128<byte> a6 = Vector128_.ShiftLeftBytesInVector(a5, 4);
                Vector128<byte> a7 = a5 + a6;

                ref byte outputRef = ref Unsafe.Add(ref dstRef, i);
                Unsafe.As<byte, Vector64<byte>>(ref outputRef) = a7.GetLower();
                last = Vector128.ShiftRightLogical(a7.AsInt64(), 56).AsInt32();
            }

            for (; i < (uint)width; ++i)
            {
                dst[(int)i] = (byte)(input[(int)i] + dst[(int)i - 1]);
            }
        }
        else
        {
            byte pred = (byte)(prev.IsEmpty ? 0 : prev[0]);

            for (int i = 0; i < width; i++)
            {
                byte val = (byte)(pred + input[i]);
                pred = val;
                dst[i] = val;
            }
        }
    }

    private static void VerticalUnfilter(Span<byte> prev, Span<byte> input, Span<byte> dst, int width)
    {
        if (prev.IsEmpty)
        {
            HorizontalUnfilter(null, input, dst, width);
        }
        else
        {
            // Byte addition intentionally wraps modulo 256, matching the WebP alpha predictor.
            TensorPrimitives.Add(input[..width], prev[..width], dst[..width]);
        }
    }

    private static void GradientUnfilter(ReadOnlySpan<byte> prev, Span<byte> input, Span<byte> dst, int width)
    {
        if (prev.IsEmpty)
        {
            HorizontalUnfilter(null, input, dst, width);
        }
        else
        {
            byte prev0 = prev[0];
            byte topLeft = prev0;
            byte left = prev0;
            for (int i = 0; i < width; i++)
            {
                byte top = prev[i];
                left = (byte)(input[i] + GradientPredictor(left, top, topLeft));
                topLeft = top;
                dst[i] = left;
            }
        }
    }

    /// <summary>
    /// Row-processing for the special case when alpha data contains only one
    /// transform (color indexing), and trivial non-green literals.
    /// </summary>
    /// <param name="hdr">The VP8L meta data.</param>
    /// <returns>True, if alpha channel needs one byte per pixel, otherwise 4.</returns>
    private static bool Is8BOptimizable(Vp8LMetadata hdr)
    {
        if (hdr.ColorCacheSize > 0)
        {
            return false;
        }

        for (int i = 0; i < hdr.NumHTreeGroups; i++)
        {
            List<HuffmanCode[]> htrees = hdr.HTreeGroups[i].HTrees;
            if (htrees[HuffIndex.Red][0].BitsUsed > 0)
            {
                return false;
            }

            if (htrees[HuffIndex.Blue][0].BitsUsed > 0)
            {
                return false;
            }

            if (htrees[HuffIndex.Alpha][0].BitsUsed > 0)
            {
                return false;
            }
        }

        return true;
    }

    private static void MapAlpha(ReadOnlySpan<byte> src, ReadOnlySpan<uint> colorMap, Span<byte> dst, int yStart, int yEnd, int width)
    {
        int offset = 0;
        for (int y = yStart; y < yEnd; y++)
        {
            for (int x = 0; x < width; x++)
            {
                dst[offset] = GetAlphaValue((int)colorMap[src[offset]]);
                offset++;
            }
        }
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    private static byte GetAlphaValue(int val) => (byte)((val >> 8) & 0xff);

    [MethodImpl(InliningOptions.ShortMethod)]
    private static int GradientPredictor(byte a, byte b, byte c)
    {
        int g = a + b - c;
        return (g & ~0xff) == 0 ? g : g < 0 ? 0 : 255;  // clip to 8bit.
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    private static void ExtractGreen(ReadOnlySpan<uint> argb, Span<byte> alpha, int size)
    {
        for (int i = 0; i < size; i++)
        {
            alpha[i] = (byte)(argb[i] >> 8);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.Vp8LDec?.Dispose();
        this.Data.Dispose();
        this.Alpha?.Dispose();
    }
}
