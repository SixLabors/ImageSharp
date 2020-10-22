// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using SixLabors.ImageSharp.Formats.WebP.Lossless;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.WebP.Lossy
{
    /// <summary>
    /// Iterator structure to iterate through macroblocks, pointing to the
    /// right neighbouring data (samples, predictions, contexts, ...)
    /// </summary>
    internal class Vp8EncIterator : IDisposable
    {
        private const int YOffEnc = 0;

        private const int UOffEnc = 16;

        private const int VOffEnc = 16 + 8;

        private const int MaxUvMode = 2;

        private const int MaxIntra16Mode = 2;

        private const int MaxIntra4Mode = 2;

        private readonly int mbw;

        private readonly int mbh;

        /// <summary>
        /// Stride of the prediction plane(=4*mbw + 1).
        /// </summary>
        private readonly int predsWidth;

        private const int I16DC16 = 0 * 16 * WebPConstants.Bps;

        private const int I16TM16 = I16DC16 + 16;

        private const int I16VE16 = 1 * 16 * WebPConstants.Bps;

        private const int I16HE16 = I16VE16 + 16;

        private const int C8DC8 = 2 * 16 * WebPConstants.Bps;

        private const int C8TM8 = C8DC8 + (1 * 16);

        private const int C8VE8 = (2 * 16 * WebPConstants.Bps) + (8 * WebPConstants.Bps);

        private const int C8HE8 = C8VE8 + (1 * 16);

        private readonly int[] vp8I16ModeOffsets = { I16DC16, I16TM16, I16VE16, I16HE16 };

        private readonly int[] vp8UvModeOffsets = { C8DC8, C8TM8, C8VE8, C8HE8 };

        private readonly byte[] clip1 = new byte[255 + 510 + 1]; // clips [-255,510] to [0,255]

        private int currentMbIdx;

        private int nzIdx;

        private int predIdx;

        private int yTopIdx;

        private int uvTopIdx;

        public Vp8EncIterator(IMemoryOwner<byte> yTop, IMemoryOwner<byte> uvTop, IMemoryOwner<byte> preds, IMemoryOwner<uint> nz, Vp8MacroBlockInfo[] mb, int mbw, int mbh)
        {
            this.mbw = mbw;
            this.mbh = mbh;
            this.Mb = mb;
            this.currentMbIdx = 0;
            this.nzIdx = 0;
            this.predIdx = 0;
            this.yTopIdx = 0;
            this.uvTopIdx = 0;
            this.YTop = yTop;
            this.UvTop = uvTop;
            this.Preds = preds;
            this.Nz = nz;
            this.predsWidth = (4 * mbw) + 1;
            this.YuvIn = new byte[WebPConstants.Bps * 16];
            this.YuvOut = new byte[WebPConstants.Bps * 16];
            this.YuvOut2 = new byte[WebPConstants.Bps * 16];
            this.YuvP = new byte[(32 * WebPConstants.Bps) + (16 * WebPConstants.Bps) + (8 * WebPConstants.Bps)]; // I16+Chroma+I4 preds
            this.YLeft = new byte[32];
            this.ULeft = new byte[32];
            this.VLeft = new byte[32];
            this.TopNz = new int[9];
            this.LeftNz = new int[9];

            // To match the C++ initial values, initialize all with 204.
            byte defaultInitVal = 204;
            this.YuvIn.AsSpan().Fill(defaultInitVal);
            this.YuvOut.AsSpan().Fill(defaultInitVal);
            this.YuvOut2.AsSpan().Fill(defaultInitVal);
            this.YuvP.AsSpan().Fill(defaultInitVal);
            this.YLeft.AsSpan().Fill(defaultInitVal);
            this.ULeft.AsSpan().Fill(defaultInitVal);
            this.VLeft.AsSpan().Fill(defaultInitVal);

            for (int i = -255; i <= 255 + 255; ++i)
            {
                this.clip1[255 + i] = this.Clip8b(i);
            }

            this.Reset();
        }

        /// <summary>
        /// Gets or sets the current macroblock X value.
        /// </summary>
        public int X { get; set; }

        /// <summary>
        /// Gets or sets the current macroblock Y.
        /// </summary>
        public int Y { get; set; }

        /// <summary>
        /// Gets or sets the input samples.
        /// </summary>
        public byte[] YuvIn { get; set; }

        /// <summary>
        /// Gets or sets the output samples.
        /// </summary>
        public byte[] YuvOut { get; set; }

        /// <summary>
        /// Gets or sets the secondary buffer swapped with YuvOut.
        /// </summary>
        public byte[] YuvOut2 { get; set; }

        /// <summary>
        /// Gets or sets the scratch buffer for prediction.
        /// </summary>
        public byte[] YuvP { get; set; }

        /// <summary>
        /// Gets or sets the left luma samples.
        /// </summary>
        public byte[] YLeft { get; set; }

        /// <summary>
        /// Gets or sets the left u samples.
        /// </summary>
        public byte[] ULeft { get; set; }

        /// <summary>
        /// Gets or sets the left v samples.
        /// </summary>
        public byte[] VLeft { get; set; }

        /// <summary>
        /// Gets or sets the top luma samples at position 'X'.
        /// </summary>
        public IMemoryOwner<byte> YTop { get; set; }

        /// <summary>
        /// Gets or sets the top u/v samples at position 'X', packed as 16 bytes.
        /// </summary>
        public IMemoryOwner<byte> UvTop { get; set; }

        /// <summary>
        /// Gets or sets the non-zero pattern.
        /// </summary>
        public IMemoryOwner<uint> Nz { get; set; }

        /// <summary>
        /// Gets or sets the intra mode predictors (4x4 blocks).
        /// </summary>
        public IMemoryOwner<byte> Preds { get; set; }

        /// <summary>
        /// Gets or sets the top-non-zero context.
        /// </summary>
        public int[] TopNz { get; set; }

        /// <summary>
        /// Gets or sets the left-non-zero. leftNz[8] is independent.
        /// </summary>
        public int[] LeftNz { get; set; }

        /// <summary>
        /// Gets or sets the number of mb still to be processed.
        /// </summary>
        public int CountDown { get; set; }

        public Vp8MacroBlockInfo CurrentMacroBlockInfo
        {
            get
            {
                return this.Mb[this.currentMbIdx];
            }
        }

        private Vp8MacroBlockInfo[] Mb { get; }

        // Import uncompressed samples from source.
        public void Import(Span<byte> y, Span<byte> u, Span<byte> v, int yStride, int uvStride, int width, int height)
        {
            int yStartIdx = ((this.Y * yStride) + this.X) * 16;
            int uvStartIdx = ((this.Y * uvStride) + this.X) * 8;
            Span<byte> ySrc = y.Slice(yStartIdx);
            Span<byte> uSrc = u.Slice(uvStartIdx);
            Span<byte> vSrc = v.Slice(uvStartIdx);
            int w = Math.Min(width - (this.X * 16), 16);
            int h = Math.Min(height - (this.Y * 16), 16);
            int uvw = (w + 1) >> 1;
            int uvh = (h + 1) >> 1;

            Span<byte> yuvIn = this.YuvIn.AsSpan(YOffEnc);
            Span<byte> uIn = this.YuvIn.AsSpan(UOffEnc);
            Span<byte> vIn = this.YuvIn.AsSpan(VOffEnc);
            this.ImportBlock(ySrc, yStride, yuvIn, w, h, 16);
            this.ImportBlock(uSrc, uvStride, uIn, uvw, uvh, 8);
            this.ImportBlock(vSrc, uvStride, vIn, uvw, uvh, 8);

            // Import source (uncompressed) samples into boundary.
            if (this.X == 0)
            {
                this.InitLeft();
            }
            else
            {
                Span<byte> yLeft = this.YLeft.AsSpan();
                Span<byte> uLeft = this.ULeft.AsSpan();
                Span<byte> vLeft = this.VLeft.AsSpan();
                if (this.Y == 0)
                {
                    yLeft[0] = 127;
                    uLeft[0] = 127;
                    vLeft[0] = 127;
                }
                else
                {
                    yLeft[0] = y[yStartIdx - 1 - yStride];
                    uLeft[0] = u[uvStartIdx - 1 - uvStride];
                    vLeft[0] = v[uvStartIdx - 1 - uvStride];
                }

                this.ImportLine(y.Slice(yStartIdx - 1), yStride, yLeft.Slice(1), h, 16);
                this.ImportLine(u.Slice(uvStartIdx - 1), uvStride, uLeft.Slice(1), uvh, 8);
                this.ImportLine(v.Slice(uvStartIdx - 1), uvStride, vLeft.Slice(1), uvh, 8);
            }

            Span<byte> yTop = this.YTop.Slice(this.yTopIdx);
            if (this.Y == 0)
            {
                yTop.Fill(127);
                this.UvTop.GetSpan().Fill(127);
            }
            else
            {
                this.ImportLine(y.Slice(yStartIdx - yStride), 1, yTop, w, 16);
                this.ImportLine(u.Slice(uvStartIdx - uvStride), 1, this.UvTop.GetSpan(), uvw, 8);
                this.ImportLine(v.Slice(uvStartIdx - uvStride), 1, this.UvTop.GetSpan().Slice(8), uvw, 8);
            }
        }

        public int FastMbAnalyze(int quality)
        {
            // Empirical cut-off value, should be around 16 (~=block size). We use the
            // [8-17] range and favor intra4 at high quality, intra16 for low quality.
            int q = quality;
            int kThreshold = 8 + ((17 - 8) * q / 100);
            int k;
            var dc = new uint[16];
            uint m;
            uint m2;
            for (k = 0; k < 16; k += 4)
            {
                this.Mean16x4(this.YuvIn.AsSpan(YOffEnc + (k * WebPConstants.Bps)), dc.AsSpan(k));
            }

            for (m = 0, m2 = 0, k = 0; k < 16; ++k)
            {
                m += dc[k];
                m2 += dc[k] * dc[k];
            }

            if (kThreshold * m2 < m * m)
            {
                this.SetIntra16Mode(0);   // DC16
            }
            else
            {
                var modes = new byte[16];  // DC4
                this.SetIntra4Mode(modes);
            }

            return 0;
        }

        public int MbAnalyzeBestIntra16Mode()
        {
            int maxMode = MaxIntra16Mode;
            int mode;
            int bestAlpha = -1;
            int bestMode = 0;

            this.MakeLuma16Preds();
            for (mode = 0; mode < maxMode; ++mode)
            {
                var histo = new Vp8LHistogram();
                histo.CollectHistogram(this.YuvIn.AsSpan(YOffEnc), this.YuvP.AsSpan(this.vp8I16ModeOffsets[mode]), 0, 16);
                int alpha = histo.GetAlpha();
                if (alpha > bestAlpha)
                {
                    bestAlpha = alpha;
                    bestMode = mode;
                }
            }

            this.SetIntra16Mode(bestMode);
            return bestAlpha;
        }

        public int MbAnalyzeBestUvMode()
        {
            int bestAlpha = -1;
            int smallestAlpha = 0;
            int bestMode = 0;
            int maxMode = MaxUvMode;
            int mode;

            this.MakeChroma8Preds();
            for (mode = 0; mode < maxMode; ++mode)
            {
                var histo = new Vp8LHistogram();
                histo.CollectHistogram(this.YuvIn.AsSpan(UOffEnc), this.YuvP.AsSpan(this.vp8UvModeOffsets[mode]), 16, 16 + 4 + 4);
                int alpha = histo.GetAlpha();
                if (alpha > bestAlpha)
                {
                    bestAlpha = alpha;
                }

                // The best prediction mode tends to be the one with the smallest alpha.
                if (mode == 0 || alpha < smallestAlpha)
                {
                    smallestAlpha = alpha;
                    bestMode = mode;
                }
            }

            this.SetIntraUvMode(bestMode);
            return bestAlpha;
        }

        public void SetIntra16Mode(int mode)
        {
            Span<byte> preds = this.Preds.Slice(this.predIdx);
            for (int y = 0; y < 4; ++y)
            {
                preds.Slice(0, 4).Fill((byte)mode);
                preds = preds.Slice(this.predsWidth);
            }

            this.CurrentMacroBlockInfo.MacroBlockType = Vp8MacroBlockType.I16X16;
        }

        private void SetIntra4Mode(byte[] modes)
        {
            int modesIdx = 0;
            Span<byte> preds = this.Preds.Slice(this.predIdx);
            for (int y = 4; y > 0; --y)
            {
                // TODO:
                // memcpy(preds, modes, 4 * sizeof(*modes));
                preds = preds.Slice(this.predsWidth);
                modesIdx += 4;
            }

            this.CurrentMacroBlockInfo.MacroBlockType = Vp8MacroBlockType.I4X4;
        }

        private void SetIntraUvMode(int mode)
        {
            this.CurrentMacroBlockInfo.UvMode = mode;
        }

        public void SetSkip(bool skip)
        {
            this.CurrentMacroBlockInfo.Skip = skip;
        }

        public void SetSegment(int segment)
        {
            this.CurrentMacroBlockInfo.Segment = segment;
        }

        /// <summary>
        /// Returns true if iteration is finished.
        /// </summary>
        /// <returns>True if iterator is finished.</returns>
        public bool IsDone()
        {
            return this.CountDown <= 0;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
        }

        /// <summary>
        /// Go to next macroblock.
        /// </summary>
        /// <returns>Returns false if not finished.</returns>
        public bool Next()
        {
            if (++this.X == this.mbw)
            {
                this.SetRow(++this.Y);
            }
            else
            {
                this.currentMbIdx++;
                this.nzIdx++;
                this.predIdx += 4;
                this.yTopIdx += 16;
                this.uvTopIdx += 16;
            }

            return --this.CountDown > 0;
        }

        private void Mean16x4(Span<byte> input, Span<uint> dc)
        {
            int x;
            for (int k = 0; k < 4; ++k)
            {
                uint avg = 0;
                for (int y = 0; y < 4; ++y)
                {
                    for (x = 0; x < 4; ++x)
                    {
                        avg += input[x + (y * WebPConstants.Bps)];
                    }
                }

                dc[k] = avg;
                input = input.Slice(4);   // go to next 4x4 block.
            }
        }

        private void MakeLuma16Preds()
        {
            Span<byte> left = this.X != 0 ? this.YLeft.AsSpan() : null;
            Span<byte> top = this.Y != 0 ? this.YTop.Slice(this.yTopIdx) : null;
            this.EncPredLuma16(this.YuvP, left, top);
        }

        private void MakeChroma8Preds()
        {
            Span<byte> left = this.X != 0 ? this.ULeft.AsSpan() : null;
            Span<byte> top = this.Y != 0 ? this.UvTop.Slice(this.uvTopIdx) : null;
            this.EncPredChroma8(this.YuvP, left, top);
        }

        // luma 16x16 prediction (paragraph 12.3)
        private void EncPredLuma16(Span<byte> dst, Span<byte> left, Span<byte> top)
        {
            this.DcMode(dst.Slice(I16DC16), left, top, 16, 16, 5);
            this.VerticalPred(dst.Slice(I16VE16), top, 16);
            this.HorizontalPred(dst.Slice(I16HE16), left, 16);
            this.TrueMotion(dst.Slice(I16TM16), left, top, 16);
        }

        // Chroma 8x8 prediction (paragraph 12.2)
        private void EncPredChroma8(Span<byte> dst, Span<byte> left, Span<byte> top)
        {
            // U block
            this.DcMode(dst.Slice(C8DC8), left, top, 8, 8, 4);
            this.VerticalPred(dst.Slice(C8VE8), top, 8);
            this.HorizontalPred(dst.Slice(C8HE8), left, 8);
            this.TrueMotion(dst.Slice(C8TM8), left, top, 8);

            // V block
            dst = dst.Slice(8);
            if (top != null)
            {
                top = top.Slice(8);
            }

            if (left != null)
            {
                left = left.Slice(16);
            }

            this.DcMode(dst.Slice(C8DC8), left, top, 8, 8, 4);
            this.VerticalPred(dst.Slice(C8VE8), top, 8);
            this.HorizontalPred(dst.Slice(C8HE8), left, 8);
            this.TrueMotion(dst.Slice(C8TM8), left, top, 8);
        }

        private void DcMode(Span<byte> dst, Span<byte> left, Span<byte> top, int size, int round, int shift)
        {
            int dc = 0;
            int j;
            if (top != null)
            {
                for (j = 0; j < size; ++j)
                {
                    dc += top[j];
                }

                if (left != null)
                {
                    // top and left present.
                    left = left.Slice(1); // in the reference implementation, left starts at -1.
                    for (j = 0; j < size; ++j)
                    {
                        dc += left[j];
                    }
                }
                else
                {
                    // top, but no left.
                    dc += dc;
                }

                dc = (dc + round) >> shift;
            }
            else if (left != null)
            {
                // left but no top.
                for (j = 0; j < size; ++j)
                {
                    dc += left[j];
                }

                dc += dc;
                dc = (dc + round) >> shift;
            }
            else
            {
                // no top, no left, nothing.
                dc = 0x80;
            }

            this.Fill(dst, dc, size);
        }

        private void VerticalPred(Span<byte> dst, Span<byte> top, int size)
        {
            if (top != null)
            {
                for (int j = 0; j < size; ++j)
                {
                    top.Slice(0, size).CopyTo(dst.Slice(j * WebPConstants.Bps));
                }
            }
            else
            {
                this.Fill(dst, 127, size);
            }
        }

        private void HorizontalPred(Span<byte> dst, Span<byte> left, int size)
        {
            if (left != null)
            {
                left = left.Slice(1); // in the reference implementation, left starts at - 1.
                for (int j = 0; j < size; ++j)
                {
                    dst.Slice(j * WebPConstants.Bps, size).Fill(left[j]);
                }
            }
            else
            {
               this.Fill(dst, 129, size);
            }
        }

        private void TrueMotion(Span<byte> dst, Span<byte> left, Span<byte> top, int size)
        {
            if (left != null)
            {
                if (top != null)
                {
                    Span<byte> clip = this.clip1.AsSpan(255 - left[0]); // left [0] instead of left[-1], original left starts at -1
                    for (int y = 0; y < size; ++y)
                    {
                        Span<byte> clipTable = clip.Slice(left[y + 1]); // left[y]
                        for (int x = 0; x < size; ++x)
                        {
                            dst[x] = clipTable[top[x]];
                        }

                        dst = dst.Slice(WebPConstants.Bps);
                    }
                }
                else
                {
                    this.HorizontalPred(dst, left, size);
                }
            }
            else
            {
                // true motion without left samples (hence: with default 129 value)
                // is equivalent to VE prediction where you just copy the top samples.
                // Note that if top samples are not available, the default value is
                // then 129, and not 127 as in the VerticalPred case.
                if (top != null)
                {
                    this.VerticalPred(dst, top, size);
                }
                else
                {
                    this.Fill(dst, 129, size);
                }
            }
        }

        private void Fill(Span<byte> dst, int value, int size)
        {
            for (int j = 0; j < size; ++j)
            {
                dst.Slice(j * WebPConstants.Bps, size).Fill((byte)value);
            }
        }

        private void ImportBlock(Span<byte> src, int srcStride, Span<byte> dst, int w, int h, int size)
        {
            int dstIdx = 0;
            for (int i = 0; i < h; ++i)
            {
                src.Slice(0, w).CopyTo(dst.Slice(dstIdx));
                if (w < size)
                {
                    dst.Slice(dstIdx, size - w).Fill(dst[dstIdx + w - 1]);
                }

                dstIdx += WebPConstants.Bps;
                src = src.Slice(srcStride);
            }

            for (int i = h; i < size; ++i)
            {
                dst.Slice(dstIdx - WebPConstants.Bps, size).CopyTo(dst);
                dstIdx += WebPConstants.Bps;
            }
        }

        private void ImportLine(Span<byte> src, int srcStride, Span<byte> dst, int len, int totalLen)
        {
            int i;
            for (i = 0; i < len; ++i)
            {
                dst[i] = src[i];
                src = src.Slice(srcStride);
            }

            for (; i < totalLen; ++i)
            {
                dst[i] = dst[len - 1];
            }
        }

        /// <summary>
        /// Restart a scan.
        /// </summary>
        private void Reset()
        {
            this.SetRow(0);
            this.SetCountDown(this.mbw * this.mbh);
            this.InitTop();

            // TODO: memset(it->bit_count_, 0, sizeof(it->bit_count_));
        }

        /// <summary>
        /// Reset iterator position to row 'y'.
        /// </summary>
        /// <param name="y">The y position.</param>
        private void SetRow(int y)
        {
            this.X = 0;
            this.Y = y;
            this.currentMbIdx = y * this.mbw;
            this.nzIdx = 0;
            this.yTopIdx = 0;
            this.uvTopIdx = 0;

            this.InitLeft();

            // TODO:
            // it->preds_ = enc->preds_ + y * 4 * enc->preds_w_;
        }

        private void InitLeft()
        {
            Span<byte> yLeft = this.YLeft.AsSpan();
            Span<byte> uLeft = this.ULeft.AsSpan();
            Span<byte> vLeft = this.VLeft.AsSpan();
            byte val = (byte)((this.Y > 0) ? 129 : 127);
            yLeft[0] = val;
            uLeft[0] = val;
            vLeft[0] = val;
            uLeft[16] = val;
            vLeft[16] = val;

            yLeft.Slice(1, 16).Fill(129);
            uLeft.Slice(1, 8).Fill(129);
            vLeft.Slice(1, 8).Fill(129);
            uLeft.Slice(1 + 16, 8).Fill(129);
            vLeft.Slice(1 + 16, 8).Fill(129);

            this.LeftNz[8] = 0;
        }

        private void InitTop()
        {
            int topSize = this.mbw * 16;
            this.YTop.Slice(0, topSize).Fill(127);
            this.Nz.GetSpan().Fill(0);
        }

        /// <summary>
        /// Set count down.
        /// </summary>
        /// <param name="countDown">Number of iterations to go.</param>
        private void SetCountDown(int countDown)
        {
            this.CountDown = countDown;
        }

        private byte Clip8b(int v)
        {
            return ((v & ~0xff) == 0) ? (byte)v : (v < 0) ? (byte)0 : (byte)255;
        }
    }
}
