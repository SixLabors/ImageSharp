// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;

using SixLabors.ImageSharp.Formats.WebP.BitWriter;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.WebP.Lossy
{
    /// <summary>
    /// Encoder for lossy webp images.
    /// </summary>
    internal class Vp8Encoder : IDisposable
    {
        /// <summary>
        /// The <see cref="MemoryAllocator"/> to use for buffer allocations.
        /// </summary>
        private readonly MemoryAllocator memoryAllocator;

        /// <summary>
        /// A bit writer for writing lossy webp streams.
        /// </summary>
        private readonly Vp8BitWriter bitWriter;

        /// <summary>
        /// Fixed-point precision for RGB->YUV.
        /// </summary>
        private const int YuvFix = 16;

        private const int YuvHalf = 1 << (YuvFix - 1);

        /// <summary>
        /// Initializes a new instance of the <see cref="Vp8Encoder"/> class.
        /// </summary>
        /// <param name="memoryAllocator">The memory allocator.</param>
        /// <param name="width">The width of the input image.</param>
        /// <param name="height">The height of the input image.</param>
        public Vp8Encoder(MemoryAllocator memoryAllocator, int width, int height)
        {
            this.memoryAllocator = memoryAllocator;

            var pixelCount = width * height;
            int mbw = (width + 15) >> 4;
            int mbh = (height + 15) >> 4;
            var uvSize = ((width + 1) >> 1) * ((height + 1) >> 1);
            this.Y = this.memoryAllocator.Allocate<byte>(pixelCount);
            this.U = this.memoryAllocator.Allocate<byte>(uvSize);
            this.V = this.memoryAllocator.Allocate<byte>(uvSize);
            this.YTop = this.memoryAllocator.Allocate<byte>(mbw * 16);
            this.UvTop = this.memoryAllocator.Allocate<byte>(mbw * 16 * 2);
            this.Preds = this.memoryAllocator.Allocate<byte>(((4 * mbw) + 1) * ((4 * mbh) + 1));
            this.Nz = this.memoryAllocator.Allocate<uint>(mbw + 1);
            this.MbHeaderLimit = 256 * 510 * 8 * 1024 / (mbw * mbh);

            // TODO: properly initialize the bitwriter
            this.bitWriter = new Vp8BitWriter();
        }

        /// <summary>
        /// Gets or sets the global susceptibility.
        /// </summary>
        public int Alpha { get; set; }

        private IMemoryOwner<byte> Y { get; }

        private IMemoryOwner<byte> U { get; }

        private IMemoryOwner<byte> V { get; }

        /// <summary>
        /// Gets the top luma samples.
        /// </summary>
        private IMemoryOwner<byte> YTop { get; }

        /// <summary>
        /// Gets the top u/v samples. U and V are packed into 16 bytes (8 U + 8 V).
        /// </summary>
        private IMemoryOwner<byte> UvTop { get; }

        /// <summary>
        /// Gets the prediction modes: (4*mbw+1) * (4*mbh+1).
        /// </summary>
        private IMemoryOwner<byte> Preds { get; }

        /// <summary>
        /// Gets the non-zero pattern.
        /// </summary>
        private IMemoryOwner<uint> Nz { get; }

        /// <summary>
        /// Gets a rough limit for header bits per MB.
        /// </summary>
        private int MbHeaderLimit { get; }

        public void Encode<TPixel>(Image<TPixel> image, Stream stream)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int width = image.Width;
            int height = image.Height;
            this.ConvertRgbToYuv(image);
            Span<byte> y = this.Y.GetSpan();
            Span<byte> u = this.U.GetSpan();
            Span<byte> v = this.V.GetSpan();

            int mbw = (width + 15) >> 4;
            int mbh = (height + 15) >> 4;
            int yStride = width;
            int uvStride = (yStride + 1) >> 1;
            var mb = new Vp8MacroBlockInfo[mbw * mbh];
            for (int i = 0; i < mb.Length; i++)
            {
                mb[i] = new Vp8MacroBlockInfo();
            }

            var it = new Vp8EncIterator(this.YTop, this.UvTop, this.Preds, this.Nz, mb, mbw, mbh);
            int method = 4; // TODO: hardcoded for now
            int quality = 100; // TODO: hardcoded for now
            var alphas = new int[WebPConstants.MaxAlpha + 1];
            int alpha = this.MacroBlockAnalysis(width, height, it, y, u, v, yStride, uvStride, method, quality, alphas, out int uvAlpha);

            // Analysis is done, proceed to actual coding.

            // TODO: EncodeAlpha();
            it.Init();
            it.InitFilter();
            do
            {
                var info = new Vp8ModeScore();
                it.Import(y, u, v, yStride, uvStride, width, height);
                if (!this.Decimate(it, info, method))
                {
                    this.CodeResiduals(it);
                }
                else
                {
                    it.ResetAfterSkip();
                }

                it.SaveBoundary();
            }
            while (it.Next());

            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.Y.Dispose();
            this.U.Dispose();
            this.V.Dispose();
            this.YTop.Dispose();
            this.UvTop.Dispose();
            this.Preds.Dispose();
        }

        private int MacroBlockAnalysis(int width, int height, Vp8EncIterator it, Span<byte> y, Span<byte> u, Span<byte> v, int yStride, int uvStride, int method, int quality, int[] alphas, out int uvAlpha)
        {
            int alpha = 0;
            uvAlpha = 0;
            if (!it.IsDone())
            {
                do
                {
                    it.Import(y, u, v, yStride, uvStride, width, height);
                    int bestAlpha = this.MbAnalyze(it, method, quality, alphas, out var bestUvAlpha);

                    // Accumulate for later complexity analysis.
                    alpha += bestAlpha;
                    uvAlpha += bestUvAlpha;
                }
                while (it.Next());
            }

            return alpha;
        }

        private int MbAnalyze(Vp8EncIterator it, int method, int quality, int[] alphas, out int bestUvAlpha)
        {
            it.SetIntra16Mode(0);    // default: Intra16, DC_PRED
            it.SetSkip(false);       // not skipped.
            it.SetSegment(0);        // default segment, spec-wise.

            int bestAlpha;
            if (method <= 1)
            {
                bestAlpha = it.FastMbAnalyze(quality);
            }
            else
            {
                bestAlpha = it.MbAnalyzeBestIntra16Mode();
            }

            bestUvAlpha = it.MbAnalyzeBestUvMode();

            // Final susceptibility mix.
            bestAlpha = ((3 * bestAlpha) + bestUvAlpha + 2) >> 2;
            bestAlpha = this.FinalAlphaValue(bestAlpha);
            alphas[bestAlpha]++;
            it.CurrentMacroBlockInfo.Alpha = bestAlpha;   // For later remapping.

            return bestAlpha; // Mixed susceptibility (not just luma).
        }

        private bool Decimate(Vp8EncIterator it, Vp8ModeScore rd, int method)
        {
            rd.InitScore();

            it.MakeLuma16Preds();
            it.MakeChroma8Preds();

            // TODO: add support for Rate-distortion optimization levels
            // At this point we have heuristically decided intra16 / intra4.
            // For method >= 2, pick the best intra4/intra16 based on SSE (~tad slower).
            // For method <= 1, we don't re-examine the decision but just go ahead with
            // quantization/reconstruction.
            this.RefineUsingDistortion(it, rd, method >= 2, method >= 1);

            bool isSkipped = rd.Nz == 0;
            it.SetSkip(isSkipped);

            return isSkipped;
        }

        // Refine intra16/intra4 sub-modes based on distortion only (not rate).
        private void RefineUsingDistortion(Vp8EncIterator it, Vp8ModeScore rd, bool tryBothModes, bool refineUvMode)
        {
            long bestScore = Vp8ModeScore.MaxCost;
            int nz = 0;
            int mode;
            bool isI16 = tryBothModes || (it.CurrentMacroBlockInfo.MacroBlockType == Vp8MacroBlockType.I16X16);

            // TODO: VP8SegmentInfo* const dqm = &it->enc_->dqm_[it->mb_->segment_];

            // Some empiric constants, of approximate order of magnitude.
            int lambdaDi16 = 106;
            int lambdaDi4 = 11;
            int lambdaDuv = 120;
            long scoreI4 = 676000; // TODO: hardcoded for now: long scoreI4 = dqm->i4_penalty_;
            long i4BitSum = 0;
            long bitLimit = tryBothModes
                ? this.MbHeaderLimit
                : Vp8ModeScore.MaxCost; // no early-out allowed.
            int numPredModes = 4;
            int numBModes = 10;

            if (isI16)
            {
                int bestMode = -1;
                Span<byte> src = it.YuvIn.AsSpan(Vp8EncIterator.YOffEnc);
                for (mode = 0; mode < numPredModes; ++mode)
                {
                    Span<byte> reference = it.YuvP.AsSpan(Vp8EncIterator.Vp8I16ModeOffsets[mode]);
                    long score = (this.Vp8Sse16X16(src, reference) * WebPConstants.RdDistoMult) + (WebPConstants.Vp8FixedCostsI16[mode] * lambdaDi16);

                    if (mode > 0 && WebPConstants.Vp8FixedCostsI16[mode] > bitLimit)
                    {
                        continue;
                    }

                    if (score < bestScore)
                    {
                        bestMode = mode;
                        bestScore = score;
                    }
                }

                if (it.X == 0 || it.Y == 0)
                {
                    // Avoid starting a checkerboard resonance from the border. See bug #432 of libwebp.
                    if (this.IsFlatSource16(src))
                    {
                        bestMode = (it.X == 0) ? 0 : 2;
                        tryBothModes = false; // Stick to i16.
                    }
                }

                it.SetIntra16Mode(bestMode);

                // We'll reconstruct later, if i16 mode actually gets selected.
            }

            // Next, evaluate Intra4.
            if (tryBothModes || !isI16)
            {
                // We don't evaluate the rate here, but just account for it through a
                // constant penalty (i4 mode usually needs more bits compared to i16).
                isI16 = false;
                it.StartI4();
                do
                {
                    int bestI4Mode = -1;
                    long bestI4Score = Vp8ModeScore.MaxCost;
                    Span<byte> src = it.YuvIn.AsSpan(Vp8EncIterator.YOffEnc + WebPLookupTables.Vp8Scan[it.I4]);
                    short[] modeCosts = it.GetCostModeI4(rd.ModesI4);

                    it.MakeIntra4Preds();
                    for (mode = 0; mode < numBModes; ++mode)
                    {
                        Span<byte> reference = it.YuvP.AsSpan(Vp8EncIterator.Vp8I4ModeOffsets[mode]);
                        long score = (this.Vp8Sse4X4(src, reference) * WebPConstants.RdDistoMult) + (modeCosts[mode] * lambdaDi4);
                        if (score < bestI4Score)
                        {
                            bestI4Mode = mode;
                            bestI4Score = score;
                        }
                    }

                    i4BitSum += modeCosts[bestI4Mode];
                    rd.ModesI4[it.I4] = (byte)bestI4Mode;
                    scoreI4 += bestI4Score;
                    if (scoreI4 >= bestScore || i4BitSum > bitLimit)
                    {
                        // Intra4 won't be better than Intra16. Bail out and pick Intra16.
                        isI16 = true;
                        break;
                    }
                    else
                    {
                        // Reconstruct partial block inside yuv_out2 buffer
                        Span<byte> tmpDst = it.YuvOut2.AsSpan(Vp8EncIterator.YOffEnc + WebPLookupTables.Vp8Scan[it.I4]);
                        // TODO: nz |= ReconstructIntra4(it, rd.YAcLevels[it.I4], src, tmpDst, bestI4Mode) << it.I4;
                    }
                }
                while (it.RotateI4(it.YuvOut2.AsSpan(Vp8EncIterator.YOffEnc)));
            }

            // Final reconstruction, depending on which mode is selected.
            if (!isI16)
            {
                it.SetIntra4Mode(rd.ModesI4);
                it.SwapOut();
                bestScore = scoreI4;
            }
            else
            {
                // TODO: nz = ReconstructIntra16(it, rd, it.YuvOut.AsSpan(Vp8EncIterator.YOffEnc), it.Preds.GetSpan()[0]);
            }

            // ... and UV!
            if (refineUvMode)
            {
                int bestMode = -1;
                long bestUvScore = Vp8ModeScore.MaxCost;
                Span<byte> src = it.YuvIn.AsSpan(Vp8EncIterator.UOffEnc);
                for (mode = 0; mode < numPredModes; ++mode)
                {
                    Span<byte> reference = it.YuvP.AsSpan(Vp8EncIterator.Vp8UvModeOffsets[mode]);
                    long score = (this.Vp8Sse16X8(src, reference) * WebPConstants.RdDistoMult) + (WebPConstants.Vp8FixedCostsUv[mode] * lambdaDuv);
                    if (score < bestUvScore)
                    {
                        bestMode = mode;
                        bestUvScore = score;
                    }
                }

                it.SetIntraUvMode(bestMode);
            }

            // TODO: nz |= ReconstructUv(it, rd, it.YuvOut.AsSpan(Vp8EncIterator.UOffEnc), it.CurrentMacroBlockInfo.UvMode);

            rd.Nz = (uint)nz;
            rd.Score = bestScore;
        }

        private void CodeResiduals(Vp8EncIterator it)
        {

        }

        private void ReconstructIntra16()
        {

        }

        private void ReconstructIntra4()
        {

        }

        private void ReconstructUv()
        {

        }

        private void ConvertRgbToYuv<TPixel>(Image<TPixel> image)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int uvWidth = (image.Width + 1) >> 1;
            bool hasAlpha = this.CheckNonOpaque(image);

            // Temporary storage for accumulated R/G/B values during conversion to U/V.
            using (IMemoryOwner<ushort> tmpRgb = this.memoryAllocator.Allocate<ushort>(4 * uvWidth))
            {
                Span<ushort> tmpRgbSpan = tmpRgb.GetSpan();
                int uvRowIndex = 0;
                int rowIndex;
                for (rowIndex = 0; rowIndex < image.Height - 1; rowIndex += 2)
                {
                    // Downsample U/V planes, two rows at a time.
                    Span<TPixel> rowSpan = image.GetPixelRowSpan(rowIndex);
                    Span<TPixel> nextRowSpan = image.GetPixelRowSpan(rowIndex + 1);
                    if (!hasAlpha)
                    {
                        this.AccumulateRgb(rowSpan, nextRowSpan, tmpRgbSpan, image.Width);
                    }
                    else
                    {
                        this.AccumulateRgba(rowSpan, nextRowSpan, tmpRgbSpan, image.Width);
                    }

                    this.ConvertRgbaToUv(tmpRgbSpan, this.U.Slice(uvRowIndex * uvWidth), this.V.Slice(uvRowIndex * uvWidth), uvWidth);
                    uvRowIndex++;

                    this.ConvertRgbaToY(rowSpan, this.Y.Slice(rowIndex * image.Width), image.Width);
                    this.ConvertRgbaToY(nextRowSpan, this.Y.Slice((rowIndex + 1) * image.Width), image.Width);
                }

                // Extra last row.
                if ((image.Height & 1) != 0)
                {
                    Span<TPixel> rowSpan = image.GetPixelRowSpan(rowIndex);
                    if (!hasAlpha)
                    {
                        this.AccumulateRgb(rowSpan, rowSpan, tmpRgbSpan, image.Width);
                    }
                    else
                    {
                        this.AccumulateRgba(rowSpan, rowSpan, tmpRgbSpan, image.Width);
                    }

                    this.ConvertRgbaToY(rowSpan, this.Y.Slice(rowIndex * image.Width), image.Width);
                }
            }
        }

        // Returns true if alpha has non-0xff values.
        private bool CheckNonOpaque<TPixel>(Image<TPixel> image)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Rgba32 rgba = default;
            for (int rowIndex = 0; rowIndex < image.Height; rowIndex++)
            {
                Span<TPixel> rowSpan = image.GetPixelRowSpan(rowIndex);
                for (int x = 0; x < image.Width; x++)
                {
                    TPixel color = rowSpan[x];
                    color.ToRgba32(ref rgba);
                    if (rgba.A != 255)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void ConvertRgbaToY<TPixel>(Span<TPixel> rowSpan, Span<byte> y, int width)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Rgba32 rgba = default;
            for (int x = 0; x < width; x++)
            {
                TPixel color = rowSpan[x];
                color.ToRgba32(ref rgba);
                y[x] = (byte)this.RgbToY(rgba.R, rgba.G, rgba.B, YuvHalf);
            }
        }

        private void ConvertRgbaToUv(Span<ushort> rgb, Span<byte> u, Span<byte> v, int width)
        {
            int rgbIdx = 0;
            for (int i = 0; i < width; i += 1, rgbIdx += 4)
            {
                int r = rgb[rgbIdx], g = rgb[rgbIdx + 1], b = rgb[rgbIdx + 2];
                u[i] = (byte)this.RgbToU(r, g, b, YuvHalf << 2);
                v[i] = (byte)this.RgbToV(r, g, b, YuvHalf << 2);
            }
        }

        private void AccumulateRgb<TPixel>(Span<TPixel> rowSpan, Span<TPixel> nextRowSpan, Span<ushort> dst, int width)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Rgba32 rgba0 = default;
            Rgba32 rgba1 = default;
            Rgba32 rgba2 = default;
            Rgba32 rgba3 = default;
            int i, j;
            int dstIdx = 0;
            for (i = 0, j = 0; i < (width >> 1); i += 1, j += 2, dstIdx += 4)
            {
                TPixel color = rowSpan[j];
                color.ToRgba32(ref rgba0);
                color = rowSpan[j + 1];
                color.ToRgba32(ref rgba1);
                color = nextRowSpan[j];
                color.ToRgba32(ref rgba2);
                color = nextRowSpan[j + 1];
                color.ToRgba32(ref rgba3);

                dst[dstIdx] = (ushort)this.LinearToGamma(
                    this.GammaToLinear(rgba0.R) +
                            this.GammaToLinear(rgba1.R) +
                            this.GammaToLinear(rgba2.R) +
                            this.GammaToLinear(rgba3.R), 0);
                dst[dstIdx + 1] = (ushort)this.LinearToGamma(
                    this.GammaToLinear(rgba0.G) +
                            this.GammaToLinear(rgba1.G) +
                            this.GammaToLinear(rgba2.G) +
                            this.GammaToLinear(rgba3.G), 0);
                dst[dstIdx + 2] = (ushort)this.LinearToGamma(
                    this.GammaToLinear(rgba0.B) +
                            this.GammaToLinear(rgba1.B) +
                            this.GammaToLinear(rgba2.B) +
                            this.GammaToLinear(rgba3.B), 0);
            }

            if ((width & 1) != 0)
            {
                TPixel color = rowSpan[j];
                color.ToRgba32(ref rgba0);
                color = nextRowSpan[j];
                color.ToRgba32(ref rgba1);

                dst[dstIdx] = (ushort)this.LinearToGamma(this.GammaToLinear(rgba0.R) + this.GammaToLinear(rgba1.R), 1);
                dst[dstIdx + 1] = (ushort)this.LinearToGamma(this.GammaToLinear(rgba0.G) + this.GammaToLinear(rgba1.G), 1);
                dst[dstIdx + 2] = (ushort)this.LinearToGamma(this.GammaToLinear(rgba0.B) + this.GammaToLinear(rgba1.B), 1);
            }
        }

        private void AccumulateRgba<TPixel>(Span<TPixel> rowSpan, Span<TPixel> nextRowSpan, Span<ushort> dst, int width)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Rgba32 rgba0 = default;
            Rgba32 rgba1 = default;
            Rgba32 rgba2 = default;
            Rgba32 rgba3 = default;
            int i, j;
            int dstIdx = 0;
            for (i = 0, j = 0; i < (width >> 1); i += 1, j += 2, dstIdx += 4)
            {
                TPixel color = rowSpan[j];
                color.ToRgba32(ref rgba0);
                color = rowSpan[j + 1];
                color.ToRgba32(ref rgba1);
                color = nextRowSpan[j];
                color.ToRgba32(ref rgba2);
                color = nextRowSpan[j + 1];
                color.ToRgba32(ref rgba3);
                uint a = (uint)(rgba0.A + rgba1.A + rgba2.A + rgba3.A);
                int r, g, b;
                if (a == 4 * 0xff || a == 0)
                {
                    r = (ushort)this.LinearToGamma(
                        this.GammaToLinear(rgba0.R) +
                        this.GammaToLinear(rgba1.R) +
                        this.GammaToLinear(rgba2.R) +
                        this.GammaToLinear(rgba3.R), 0);
                    g = (ushort)this.LinearToGamma(
                        this.GammaToLinear(rgba0.G) +
                        this.GammaToLinear(rgba1.G) +
                        this.GammaToLinear(rgba2.G) +
                        this.GammaToLinear(rgba3.G), 0);
                    b = (ushort)this.LinearToGamma(
                        this.GammaToLinear(rgba0.B) +
                        this.GammaToLinear(rgba1.B) +
                        this.GammaToLinear(rgba2.B) +
                        this.GammaToLinear(rgba3.B), 0);
                }
                else
                {
                    r = this.LinearToGammaWeighted(rgba0.R, rgba1.R, rgba2.R, rgba3.R, rgba0.A, rgba1.A, rgba2.A, rgba3.A, a);
                    g = this.LinearToGammaWeighted(rgba0.G, rgba1.G, rgba2.G, rgba3.G, rgba0.A, rgba1.A, rgba2.A, rgba3.A, a);
                    b = this.LinearToGammaWeighted(rgba0.B, rgba1.B, rgba2.B, rgba3.B, rgba0.A, rgba1.A, rgba2.A, rgba3.A, a);
                }

                dst[dstIdx] = (ushort)r;
                dst[dstIdx + 1] = (ushort)g;
                dst[dstIdx + 2] = (ushort)b;
                dst[dstIdx + 3] = (ushort)a;
            }

            if ((width & 1) != 0)
            {
                TPixel color = rowSpan[j];
                color.ToRgba32(ref rgba0);
                color = nextRowSpan[j];
                color.ToRgba32(ref rgba1);
                uint a = (uint)(2u * (rgba0.A + rgba1.A));
                int r, g, b;
                if (a == 4 * 0xff || a == 0)
                {
                    r = (ushort)this.LinearToGamma(this.GammaToLinear(rgba0.R) + this.GammaToLinear(rgba1.R), 1);
                    g = (ushort)this.LinearToGamma(this.GammaToLinear(rgba0.G) + this.GammaToLinear(rgba1.G), 1);
                    b = (ushort)this.LinearToGamma(this.GammaToLinear(rgba0.B) + this.GammaToLinear(rgba1.B), 1);
                }
                else
                {
                    r = this.LinearToGammaWeighted(rgba0.R, rgba1.R, rgba2.R, rgba3.R, rgba0.A, rgba1.A, rgba2.A, rgba3.A, a);
                    g = this.LinearToGammaWeighted(rgba0.G, rgba1.G, rgba2.G, rgba3.G, rgba0.A, rgba1.A, rgba2.A, rgba3.A, a);
                    b = this.LinearToGammaWeighted(rgba0.B, rgba1.B, rgba2.B, rgba3.B, rgba0.A, rgba1.A, rgba2.A, rgba3.A, a);
                }

                dst[dstIdx] = (ushort)r;
                dst[dstIdx + 1] = (ushort)g;
                dst[dstIdx + 2] = (ushort)b;
                dst[dstIdx + 3] = (ushort)a;
            }
        }

        private int LinearToGammaWeighted(byte rgb0, byte rgb1, byte rgb2, byte rgb3, byte a0, byte a1, byte a2, byte a3, uint totalA)
        {
            uint sum = (a0 * this.GammaToLinear(rgb0)) + (a1 * this.GammaToLinear(rgb1)) + (a2 * this.GammaToLinear(rgb2)) + (a3 * this.GammaToLinear(rgb3));
            return this.LinearToGamma((sum * WebPLookupTables.InvAlpha[totalA]) >> (WebPConstants.AlphaFix - 2), 0);
        }

        // Convert a linear value 'v' to YUV_FIX+2 fixed-point precision
        // U/V value, suitable for RGBToU/V calls.
        [MethodImpl(InliningOptions.ShortMethod)]
        private int LinearToGamma(uint baseValue, int shift)
        {
            int y = this.Interpolate((int)(baseValue << shift));   // Final uplifted value.
            return (y + WebPConstants.GammaTabRounder) >> WebPConstants.GammaTabFix;    // Descale.
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private uint GammaToLinear(byte v)
        {
            return WebPLookupTables.GammaToLinearTab[v];
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private int Interpolate(int v)
        {
            int tabPos = v >> (WebPConstants.GammaTabFix + 2);    // integer part.
            int x = v & ((WebPConstants.GammaTabScale << 2) - 1);  // fractional part.
            int v0 = WebPLookupTables.LinearToGammaTab[tabPos];
            int v1 = WebPLookupTables.LinearToGammaTab[tabPos + 1];
            int y = (v1 * x) + (v0 * ((WebPConstants.GammaTabScale << 2) - x));   // interpolate

            return y;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private int RgbToY(byte r, byte g, byte b, int rounding)
        {
            int luma = (16839 * r) + (33059 * g) + (6420 * b);
            return (luma + rounding + (16 << YuvFix)) >> YuvFix;  // No need to clip.
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private int RgbToU(int r, int g, int b, int rounding)
        {
            int u = (-9719 * r) - (19081 * g) + (28800 * b);
            return this.ClipUv(u, rounding);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private int RgbToV(int r, int g, int b, int rounding)
        {
            int v = (+28800 * r) - (24116 * g) - (4684 * b);
            return this.ClipUv(v, rounding);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private int ClipUv(int uv, int rounding)
        {
            uv = (uv + rounding + (128 << (YuvFix + 2))) >> (YuvFix + 2);
            return ((uv & ~0xff) == 0) ? uv : (uv < 0) ? 0 : 255;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private int FinalAlphaValue(int alpha)
        {
            alpha = WebPConstants.MaxAlpha - alpha;
            return this.Clip(alpha, 0, WebPConstants.MaxAlpha);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private int Clip(int v, int min, int max)
        {
            return (v < min) ? min : (v > max) ? max : v;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private int Vp8Sse16X16(Span<byte> a, Span<byte> b)
        {
            return this.GetSse(a, b, 16, 16);
        }

        private int Vp8Sse16X8(Span<byte> a, Span<byte> b)
        {
            return this.GetSse(a, b, 16, 8);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private int Vp8Sse4X4(Span<byte> a, Span<byte> b)
        {
            return this.GetSse(a, b, 4, 4);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private int GetSse(Span<byte> a, Span<byte> b, int w, int h)
        {
            int count = 0;
            for (int y = 0; y < h; ++y)
            {
                for (int x = 0; x < w; ++x)
                {
                    int diff = a[x] - b[x];
                    count += diff * diff;
                }

                a = a.Slice(WebPConstants.Bps);
                b = b.Slice(WebPConstants.Bps);
            }

            return count;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private bool IsFlatSource16(Span<byte> src)
        {
            uint v = src[0] * 0x01010101u;
            Span<byte> vSpan = BitConverter.GetBytes(v).AsSpan();
            for (int i = 0; i < 16; ++i)
            {
                if (src.Slice(0, 4).SequenceEqual(vSpan) || src.Slice(4, 4).SequenceEqual(vSpan) ||
                    src.Slice(0, 8).SequenceEqual(vSpan) || src.Slice(12, 4).SequenceEqual(vSpan))
                {
                    return false;
                }

                src = src.Slice(WebPConstants.Bps);
            }

            return true;
        }
    }
}
