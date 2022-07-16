// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Webp.BitReader;
using SixLabors.ImageSharp.Formats.Webp.Lossless;
using SixLabors.ImageSharp.Memory;
#if SUPPORTS_RUNTIME_INTRINSICS
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

namespace SixLabors.ImageSharp.Formats.Webp
{
    /// <summary>
    /// Implements decoding for lossy alpha chunks which may be compressed.
    /// </summary>
    internal class AlphaDecoder : IDisposable
    {
        private readonly MemoryAllocator memoryAllocator;

        /// <summary>
        /// Initializes a new instance of the <see cref="AlphaDecoder"/> class.
        /// </summary>
        /// <param name="width">The width of the image.</param>
        /// <param name="height">The height of the image.</param>
        /// <param name="data">The (maybe compressed) alpha data.</param>
        /// <param name="alphaChunkHeader">The first byte of the alpha image stream contains information on how to decode the stream.</param>
        /// <param name="memoryAllocator">Used for allocating memory during decoding.</param>
        /// <param name="configuration">The configuration.</param>
        public AlphaDecoder(int width, int height, IMemoryOwner<byte> data, byte alphaChunkHeader, MemoryAllocator memoryAllocator, Configuration configuration)
        {
            this.Width = width;
            this.Height = height;
            this.Data = data;
            this.memoryAllocator = memoryAllocator;
            this.LastRow = 0;
            int totalPixels = width * height;

            var compression = (WebpAlphaCompressionMethod)(alphaChunkHeader & 0x03);
            if (compression is not WebpAlphaCompressionMethod.NoCompression and not WebpAlphaCompressionMethod.WebpLosslessCompression)
            {
                WebpThrowHelper.ThrowImageFormatException($"unexpected alpha compression method {compression} found");
            }

            this.Compressed = compression == WebpAlphaCompressionMethod.WebpLosslessCompression;

            // The filtering method used. Only values between 0 and 3 are valid.
            int filter = (alphaChunkHeader >> 2) & 0x03;
            if (filter is < (int)WebpAlphaFilterType.None or > (int)WebpAlphaFilterType.Gradient)
            {
                WebpThrowHelper.ThrowImageFormatException($"unexpected alpha filter method {filter} found");
            }

            this.Alpha = memoryAllocator.Allocate<byte>(totalPixels);
            this.AlphaFilterType = (WebpAlphaFilterType)filter;
            this.Vp8LDec = new Vp8LDecoder(width, height, memoryAllocator);

            if (this.Compressed)
            {
                var bitReader = new Vp8LBitReader(data);
                this.LosslessDecoder = new WebpLosslessDecoder(bitReader, memoryAllocator, configuration);
                this.LosslessDecoder.DecodeImageStream(this.Vp8LDec, width, height, true);
                this.Use8BDecode = this.Vp8LDec.Transforms.Count > 0 && Is8BOptimizable(this.Vp8LDec.Metadata);
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
        private bool Compressed { get; }

        /// <summary>
        /// Gets the (maybe compressed) alpha data.
        /// </summary>
        private IMemoryOwner<byte> Data { get; }

        /// <summary>
        /// Gets the Vp8L decoder which is used to de compress the alpha channel, if needed.
        /// </summary>
        private WebpLosslessDecoder LosslessDecoder { get; }

        /// <summary>
        /// Gets a value indicating whether the decoding needs 1 byte per pixel for decoding.
        /// Although Alpha Channel requires only 1 byte per pixel, sometimes Vp8LDecoder may need to allocate
        /// 4 bytes per pixel internally during decode.
        /// </summary>
        public bool Use8BDecode { get; }

        /// <summary>
        /// Decodes and filters the maybe compressed alpha data.
        /// </summary>
        public void Decode()
        {
            if (!this.Compressed)
            {
                Span<byte> dataSpan = this.Data.Memory.Span;
                int pixelCount = this.Width * this.Height;
                if (dataSpan.Length < pixelCount)
                {
                    WebpThrowHelper.ThrowImageFormatException("not enough data in the ALPH chunk");
                }

                Span<byte> alphaSpan = this.Alpha.Memory.Span;
                if (this.AlphaFilterType == WebpAlphaFilterType.None)
                {
                    dataSpan.Slice(0, pixelCount).CopyTo(alphaSpan);
                    return;
                }

                Span<byte> deltas = dataSpan;
                Span<byte> dst = alphaSpan;
                Span<byte> prev = default;
                for (int y = 0; y < this.Height; y++)
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
                    deltas = deltas.Slice(this.Width);
                    dst = dst.Slice(this.Width);
                }
            }
            else
            {
                if (this.Use8BDecode)
                {
                    this.LosslessDecoder.DecodeAlphaData(this);
                }
                else
                {
                    this.LosslessDecoder.DecodeImageData(this.Vp8LDec, this.Vp8LDec.Pixels.Memory.Span);
                    this.ExtractAlphaRows(this.Vp8LDec);
                }
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
            Span<byte> prev = this.PrevRow == 0 ? null : alphaSpan.Slice(this.Width * this.PrevRow);
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
                dst = dst.Slice(stride);
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
                Span<byte> dst = output.Slice(this.Width * firstRow);
                Span<byte> input = pixelDataAsBytes.Slice(this.Vp8LDec.Width * firstRow);

                if (this.Vp8LDec.Transforms.Count == 0 || this.Vp8LDec.Transforms[0].TransformType != Vp8LTransformType.ColorIndexingTransform)
                {
                    WebpThrowHelper.ThrowImageFormatException("error while decoding alpha channel, expected color index transform data is missing");
                }

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
        private void ExtractAlphaRows(Vp8LDecoder dec)
        {
            int numRowsToProcess = dec.Height;
            int width = dec.Width;
            Span<uint> pixels = dec.Pixels.Memory.Span;
            Span<uint> input = pixels;
            Span<byte> output = this.Alpha.Memory.Span;

            // Extract alpha (which is stored in the green plane).
            int pixelCount = width * numRowsToProcess;
            WebpLosslessDecoder.ApplyInverseTransforms(dec, input, this.memoryAllocator);
            ExtractGreen(input, output, pixelCount);
            this.AlphaApplyFilter(0, numRowsToProcess, output, width);
        }

        private static void ColorIndexInverseTransformAlpha(
            Vp8LTransform transform,
            int yStart,
            int yEnd,
            Span<byte> src,
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

        private static void HorizontalUnfilter(Span<byte> prev, Span<byte> input, Span<byte> dst, int width)
        {
#if SUPPORTS_RUNTIME_INTRINSICS
            if (Sse2.IsSupported)
            {
                dst[0] = (byte)(input[0] + (prev.IsEmpty ? 0 : prev[0]));
                if (width <= 1)
                {
                    return;
                }

                nint i;
                Vector128<int> last = Vector128<int>.Zero.WithElement(0, dst[0]);
                ref byte srcRef = ref MemoryMarshal.GetReference(input);
                for (i = 1; i + 8 <= width; i += 8)
                {
                    var a0 = Vector128.Create(Unsafe.As<byte, long>(ref Unsafe.Add(ref srcRef, i)), 0);
                    Vector128<byte> a1 = Sse2.Add(a0.AsByte(), last.AsByte());
                    Vector128<byte> a2 = Sse2.ShiftLeftLogical128BitLane(a1, 1);
                    Vector128<byte> a3 = Sse2.Add(a1, a2);
                    Vector128<byte> a4 = Sse2.ShiftLeftLogical128BitLane(a3, 2);
                    Vector128<byte> a5 = Sse2.Add(a3, a4);
                    Vector128<byte> a6 = Sse2.ShiftLeftLogical128BitLane(a5, 4);
                    Vector128<byte> a7 = Sse2.Add(a5, a6);

                    ref byte outputRef = ref Unsafe.Add(ref MemoryMarshal.GetReference(dst), i);
                    Unsafe.As<byte, Vector64<byte>>(ref outputRef) = a7.GetLower();
                    last = Sse2.ShiftRightLogical(a7.AsInt64(), 56).AsInt32();
                }

                for (; i < width; ++i)
                {
                    dst[(int)i] = (byte)(input[(int)i] + dst[(int)i - 1]);
                }
            }
            else
#endif
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
#if SUPPORTS_RUNTIME_INTRINSICS
                if (Avx2.IsSupported)
                {
                    nint i;
                    int maxPos = width & ~31;
                    for (i = 0; i < maxPos; i += 32)
                    {
                        Vector256<int> a0 = Unsafe.As<byte, Vector256<int>>(ref Unsafe.Add(ref MemoryMarshal.GetReference(input), i));
                        Vector256<int> b0 = Unsafe.As<byte, Vector256<int>>(ref Unsafe.Add(ref MemoryMarshal.GetReference(prev), i));
                        Vector256<byte> c0 = Avx2.Add(a0.AsByte(), b0.AsByte());
                        ref byte outputRef = ref Unsafe.Add(ref MemoryMarshal.GetReference(dst), i);
                        Unsafe.As<byte, Vector256<byte>>(ref outputRef) = c0;
                    }

                    for (; i < width; i++)
                    {
                        dst[(int)i] = (byte)(prev[(int)i] + input[(int)i]);
                    }
                }
                else
#endif
                {
                    for (int i = 0; i < width; i++)
                    {
                        dst[i] = (byte)(prev[i] + input[i]);
                    }
                }
            }
        }

        private static void GradientUnfilter(Span<byte> prev, Span<byte> input, Span<byte> dst, int width)
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

        private static void MapAlpha(Span<byte> src, Span<uint> colorMap, Span<byte> dst, int yStart, int yEnd, int width)
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
        private static void ExtractGreen(Span<uint> argb, Span<byte> alpha, int size)
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
}
