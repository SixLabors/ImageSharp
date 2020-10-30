// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Buffers.Binary;
using SixLabors.ImageSharp.Formats.WebP.Lossless;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.WebP.Lossy
{
    /// <summary>
    /// Iterator structure to iterate through macroblocks, pointing to the
    /// right neighbouring data (samples, predictions, contexts, ...)
    /// </summary>
    internal class Vp8EncIterator
    {
        public const int YOffEnc = 0;

        public const int UOffEnc = 16;

        public const int VOffEnc = 16 + 8;

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

        public static readonly int[] Vp8I16ModeOffsets = { I16DC16, I16TM16, I16VE16, I16HE16 };

        public static readonly int[] Vp8UvModeOffsets = { C8DC8, C8TM8, C8VE8, C8HE8 };

        private const int I4DC4 = (3 * 16 * WebPConstants.Bps) + 0;

        private const int I4TM4 = I4DC4 + 4;

        private const int I4VE4 = I4DC4 + 8;

        private const int I4HE4 = I4DC4 + 12;

        private const int I4RD4 = I4DC4 + 16;

        private const int I4VR4 = I4RD4 + 20;

        private const int I4LD4 = I4RD4 + 24;

        private const int I4VL4 = I4RD4 + 28;

        private const int I4HD4 = (3 * 16 * WebPConstants.Bps) + (4 * WebPConstants.Bps);

        private const int I4HU4 = I4HD4 + 4;

        public static readonly int[] Vp8I4ModeOffsets = { I4DC4, I4TM4, I4VE4, I4HE4, I4RD4, I4VR4, I4LD4, I4VL4, I4HD4, I4HU4 };

        private readonly byte[] clip1 = new byte[255 + 510 + 1]; // clips [-255,510] to [0,255]

        // Array to record the position of the top sample to pass to the prediction functions.
        private readonly byte[] vp8TopLeftI4 =
        {
            17, 21, 25, 29,
            13, 17, 21, 25,
            9,  13, 17, 21,
            5,   9, 13, 17
        };

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
            this.UvLeft = new byte[32];
            this.TopNz = new int[9];
            this.LeftNz = new int[9];
            this.I4Boundary = new byte[37];
            this.BitCount = new long[4, 3];

            // To match the C++ initial values of the reference implementation, initialize all with 204.
            byte defaultInitVal = 204;
            this.YuvIn.AsSpan().Fill(defaultInitVal);
            this.YuvOut.AsSpan().Fill(defaultInitVal);
            this.YuvOut2.AsSpan().Fill(defaultInitVal);
            this.YuvP.AsSpan().Fill(defaultInitVal);
            this.YLeft.AsSpan().Fill(defaultInitVal);
            this.UvLeft.AsSpan().Fill(defaultInitVal);
            this.Preds.GetSpan().Fill(defaultInitVal);

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
        /// Gets the input samples.
        /// </summary>
        public byte[] YuvIn { get; }

        /// <summary>
        /// Gets or sets the output samples.
        /// </summary>
        public byte[] YuvOut { get; set; }

        /// <summary>
        /// Gets or sets the secondary buffer swapped with YuvOut.
        /// </summary>
        public byte[] YuvOut2 { get; set; }

        /// <summary>
        /// Gets the scratch buffer for prediction.
        /// </summary>
        public byte[] YuvP { get; }

        /// <summary>
        /// Gets the left luma samples.
        /// </summary>
        public byte[] YLeft { get; }

        /// <summary>
        /// Gets the left uv samples.
        /// </summary>
        public byte[] UvLeft { get; }

        /// <summary>
        /// Gets the top luma samples at position 'X'.
        /// </summary>
        public IMemoryOwner<byte> YTop { get; }

        /// <summary>
        /// Gets the top u/v samples at position 'X', packed as 16 bytes.
        /// </summary>
        public IMemoryOwner<byte> UvTop { get; }

        /// <summary>
        /// Gets the intra mode predictors (4x4 blocks).
        /// </summary>
        public IMemoryOwner<byte> Preds { get; }

        /// <summary>
        /// Gets the current start index of the intra mode predictors.
        /// </summary>
        public int PredIdx
        {
            get
            {
                return this.predIdx;
            }
        }

        /// <summary>
        /// Gets the non-zero pattern.
        /// </summary>
        public IMemoryOwner<uint> Nz { get; }

        /// <summary>
        /// Gets 32+5 boundary samples needed by intra4x4.
        /// </summary>
        public byte[] I4Boundary { get; }

        /// <summary>
        /// Gets or sets the index to the current top boundary sample.
        /// </summary>
        public int I4BoundaryIdx { get; set; }

        /// <summary>
        /// Gets or sets the current intra4x4 mode being tested.
        /// </summary>
        public int I4 { get; set; }

        /// <summary>
        /// Gets the top-non-zero context.
        /// </summary>
        public int[] TopNz { get; }

        /// <summary>
        /// Gets the left-non-zero. leftNz[8] is independent.
        /// </summary>
        public int[] LeftNz { get; }

        /// <summary>
        /// Gets or sets the macroblock bit-cost for luma.
        /// </summary>
        public long LumaBits { get; set; }

        /// <summary>
        /// Gets the bit counters for coded levels.
        /// </summary>
        public long[,] BitCount { get; }

        /// <summary>
        /// Gets or sets the macroblock bit-cost for chroma.
        /// </summary>
        public long UvBits { get; set; }

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

        public void Init()
        {
            this.Reset();
        }

        public void InitFilter()
        {
            // TODO: add support for autofilter
        }

        public void StartI4()
        {
            int i;
            this.I4 = 0;    // first 4x4 sub-block.
            this.I4BoundaryIdx = this.vp8TopLeftI4[0];

            // Import the boundary samples.
            for (i = 0; i < 17; ++i)
            {
                // left
                this.I4Boundary[i] = this.YLeft[15 - i + 1];
            }

            Span<byte> yTop = this.YTop.GetSpan();
            for (i = 0; i < 16; ++i)
            {
                // top
                this.I4Boundary[17 + i] = yTop[i];
            }

            // top-right samples have a special case on the far right of the picture.
            if (this.X < this.mbw - 1)
            {
                for (i = 16; i < 16 + 4; ++i)
                {
                    this.I4Boundary[17 + i] = yTop[i];
                }
            }
            else
            {
                // else, replicate the last valid pixel four times
                for (i = 16; i < 16 + 4; ++i)
                {
                    this.I4Boundary[17 + i] = this.I4Boundary[17 + 15];
                }
            }

            this.NzToBytes();  // import the non-zero context.
        }

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
                Span<byte> uLeft = this.UvLeft.AsSpan(0, 16);
                Span<byte> vLeft = this.UvLeft.AsSpan(16, 16);
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
                histo.CollectHistogram(this.YuvIn.AsSpan(YOffEnc), this.YuvP.AsSpan(Vp8I16ModeOffsets[mode]), 0, 16);
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
                histo.CollectHistogram(this.YuvIn.AsSpan(UOffEnc), this.YuvP.AsSpan(Vp8UvModeOffsets[mode]), 16, 16 + 4 + 4);
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

        public void SetIntra4Mode(byte[] modes)
        {
            int modesIdx = 0;
            int predIdx = this.predIdx;
            for (int y = 4; y > 0; --y)
            {
                modes.AsSpan(modesIdx, 4).CopyTo(this.Preds.Slice(predIdx));
                predIdx += this.predsWidth;
                modesIdx += 4;
            }

            this.CurrentMacroBlockInfo.MacroBlockType = Vp8MacroBlockType.I4X4;
        }

        public short[] GetCostModeI4(byte[] modes)
        {
            int predsWidth = this.predsWidth;
            int predIdx = this.predIdx;
            int x = this.I4 & 3;
            int y = this.I4 >> 2;
            int left = (x == 0) ? this.Preds.Slice(predIdx)[(y * predsWidth) - 1] : modes[this.I4 - 1];
            int top = (y == 0) ? this.Preds.Slice(predIdx)[-predsWidth + x] : modes[this.I4 - 4];
            return WebPLookupTables.Vp8FixedCostsI4[top, left];
        }

        public void SetIntraUvMode(int mode)
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

        public void SaveBoundary()
        {
            int x = this.X;
            int y = this.Y;
            Span<byte> ySrc = this.YuvOut.AsSpan(YOffEnc);
            Span<byte> uvSrc = this.YuvOut.AsSpan(UOffEnc);
            if (x < this.mbw - 1)
            {
                // left
                for (int i = 0; i < 16; ++i)
                {
                    this.YLeft[i + 1] = ySrc[15 + (i * WebPConstants.Bps)];
                }

                for (int i = 0; i < 8; ++i)
                {
                    this.UvLeft[i + 1] = uvSrc[7 + (i * WebPConstants.Bps)];
                    this.UvLeft[i + 16 + 1] = uvSrc[15 + (i * WebPConstants.Bps)];
                }

                // top-left (before 'top'!)
                this.YLeft[0] = this.YTop.GetSpan()[15];
                this.UvLeft[0] = this.UvTop.GetSpan()[0 + 7];
                this.UvLeft[16] = this.UvTop.GetSpan()[8 + 7];
            }

            if (y < this.mbh - 1)
            {
                // top
                ySrc.Slice(15 * WebPConstants.Bps, 16).CopyTo(this.YTop.GetSpan());
                uvSrc.Slice(7 * WebPConstants.Bps, 8 + 8).CopyTo(this.UvTop.GetSpan());
            }
        }

        public bool RotateI4(Span<byte> yuvOut)
        {
            Span<byte> blk = yuvOut.Slice(WebPLookupTables.Vp8Scan[this.I4]);
            Span<byte> top = this.I4Boundary.AsSpan(this.I4BoundaryIdx);
            int i;

            // Update the cache with 7 fresh samples.
            for (i = 0; i <= 3; ++i)
            {
                top[-4 + i] = blk[i + (3 * WebPConstants.Bps)];   // Store future top samples.
            }

            if ((this.I4 & 3) != 3)
            {
                // if not on the right sub-blocks #3, #7, #11, #15
                for (i = 0; i <= 2; ++i)
                {
                    // store future left samples
                    top[i] = blk[3 + ((2 - i) * WebPConstants.Bps)];
                }
            }
            else
            {
                // else replicate top-right samples, as says the specs.
                for (i = 0; i <= 3; ++i)
                {
                    top[i] = top[i + 4];
                }
            }

            // move pointers to next sub-block
            ++this.I4;
            if (this.I4 == 16)
            {
                // we're done
                return false;
            }

            this.I4BoundaryIdx = this.vp8TopLeftI4[this.I4];

            return true;
        }

        public void ResetAfterSkip()
        {
            if (this.CurrentMacroBlockInfo.MacroBlockType == Vp8MacroBlockType.I16X16)
            {
                // Reset all predictors.
                this.Nz.GetSpan()[0] = 0;
                this.LeftNz[8] = 0;
            }
            else
            {
                this.Nz.GetSpan()[0] &= 1 << 24;  // Preserve the dc_nz bit.
            }
        }

        public void MakeLuma16Preds()
        {
            Span<byte> left = this.X != 0 ? this.YLeft.AsSpan() : null;
            Span<byte> top = this.Y != 0 ? this.YTop.Slice(this.yTopIdx) : null;
            this.EncPredLuma16(this.YuvP, left, top);
        }

        public void MakeChroma8Preds()
        {
            Span<byte> left = this.X != 0 ? this.UvLeft.AsSpan() : null;
            Span<byte> top = this.Y != 0 ? this.UvTop.Slice(this.uvTopIdx) : null;
            this.EncPredChroma8(this.YuvP, left, top);
        }

        public void MakeIntra4Preds()
        {
            this.EncPredLuma4(this.YuvP, this.I4Boundary.AsSpan(this.I4BoundaryIdx));
        }

        public void SwapOut()
        {
            byte[] tmp = this.YuvOut;
            this.YuvOut = this.YuvOut2;
            this.YuvOut2 = tmp;
        }

        public void NzToBytes()
        {
            Span<uint> nz = this.Nz.GetSpan();

            uint lnz = 0; // TODO: -1?
            uint tnz = nz[0];
            Span<int> topNz = this.TopNz;
            Span<int> leftNz = this.LeftNz;

            // Top-Y
            topNz[0] = this.Bit(tnz, 12);
            topNz[1] = this.Bit(tnz, 13);
            topNz[2] = this.Bit(tnz, 14);
            topNz[3] = this.Bit(tnz, 15);

            // Top-U
            topNz[4] = this.Bit(tnz, 18);
            topNz[5] = this.Bit(tnz, 19);

            // Top-V
            topNz[6] = this.Bit(tnz, 22);
            topNz[7] = this.Bit(tnz, 23);

            // DC
            topNz[8] = this.Bit(tnz, 24);

            // left-Y
            leftNz[0] = this.Bit(lnz, 3);
            leftNz[1] = this.Bit(lnz, 7);
            leftNz[2] = this.Bit(lnz, 11);
            leftNz[3] = this.Bit(lnz, 15);

            // left-U
            leftNz[4] = this.Bit(lnz, 17);
            leftNz[5] = this.Bit(lnz, 19);

            // left-V
            leftNz[6] = this.Bit(lnz, 21);
            leftNz[7] = this.Bit(lnz, 23);

            // left-DC is special, iterated separately.
        }

        public void BytesToNz()
        {
            uint nz = 0;
            int[] topNz = this.TopNz;
            int[] leftNz = this.LeftNz;

            // top
            nz |= (uint)((topNz[0] << 12) | (topNz[1] << 13));
            nz |= (uint)((topNz[2] << 14) | (topNz[3] << 15));
            nz |= (uint)((topNz[4] << 18) | (topNz[5] << 19));
            nz |= (uint)((topNz[6] << 22) | (topNz[7] << 23));
            nz |= (uint)(topNz[8] << 24);  // we propagate the top bit, esp. for intra4

            // left
            nz |= (uint)((leftNz[0] << 3) | (leftNz[1] << 7));
            nz |= (uint)(leftNz[2] << 11);
            nz |= (uint)((leftNz[4] << 17) | (leftNz[6] << 21));
        }

        private void Mean16x4(Span<byte> input, Span<uint> dc)
        {
            for (int k = 0; k < 4; ++k)
            {
                uint avg = 0;
                for (int y = 0; y < 4; ++y)
                {
                    for (int x = 0; x < 4; ++x)
                    {
                        avg += input[x + (y * WebPConstants.Bps)];
                    }
                }

                dc[k] = avg;
                input = input.Slice(4);   // go to next 4x4 block.
            }
        }

        // luma 16x16 prediction (paragraph 12.3).
        private void EncPredLuma16(Span<byte> dst, Span<byte> left, Span<byte> top)
        {
            this.DcMode(dst.Slice(I16DC16), left, top, 16, 16, 5);
            this.VerticalPred(dst.Slice(I16VE16), top, 16);
            this.HorizontalPred(dst.Slice(I16HE16), left, 16);
            this.TrueMotion(dst.Slice(I16TM16), left, top, 16);
        }

        // Chroma 8x8 prediction (paragraph 12.2).
        private void EncPredChroma8(Span<byte> dst, Span<byte> left, Span<byte> top)
        {
            // U block.
            this.DcMode(dst.Slice(C8DC8), left, top, 8, 8, 4);
            this.VerticalPred(dst.Slice(C8VE8), top, 8);
            this.HorizontalPred(dst.Slice(C8HE8), left, 8);
            this.TrueMotion(dst.Slice(C8TM8), left, top, 8);

            // V block.
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

        // Left samples are top[-5 .. -2], top_left is top[-1], top are
        // located at top[0..3], and top right is top[4..7]
        private void EncPredLuma4(Span<byte> dst, Span<byte> top)
        {
            this.Dc4(dst.Slice(I4DC4), top);
            this.Tm4(dst.Slice(I4TM4), top);
            this.Ve4(dst.Slice(I4VE4), top);
            this.He4(dst.Slice(I4HE4), top);
            this.Rd4(dst.Slice(I4RD4), top);
            this.Vr4(dst.Slice(I4VR4), top);
            this.Ld4(dst.Slice(I4LD4), top);
            this.Vl4(dst.Slice(I4VL4), top);
            this.Hd4(dst.Slice(I4HD4), top);
            this.Hu4(dst.Slice(I4HU4), top);
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
                left = left.Slice(1); // in the reference implementation, left starts at -1.
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

        private void Dc4(Span<byte> dst, Span<byte> top)
        {
            uint dc = 4;
            int i;
            for (i = 0; i < 4; ++i)
            {
                dc += (uint)(top[i] + top[-5 + i]);
            }

            this.Fill(dst, (int)(dc >> 3), 4);
        }

        private void Tm4(Span<byte> dst, Span<byte> top)
        {
            Span<byte> clip = this.clip1.AsSpan(255 - top[-1]);
            for (int y = 0; y < 4; ++y)
            {
                Span<byte> clipTable = clip.Slice(top[-2 - y]);
                for (int x = 0; x < 4; ++x)
                {
                    dst[x] = clipTable[top[x]];
                }

                dst = dst.Slice(WebPConstants.Bps);
            }
        }

        private void Ve4(Span<byte> dst, Span<byte> top)
        {
            // vertical
            byte[] vals =
            {
                LossyUtils.Avg3(top[-1], top[0], top[1]),
                LossyUtils.Avg3(top[0], top[1], top[2]),
                LossyUtils.Avg3(top[1], top[2], top[3]),
                LossyUtils.Avg3(top[2], top[3], top[4])
            };

            for (int i = 0; i < 4; ++i)
            {
                vals.AsSpan().CopyTo(dst.Slice(i * WebPConstants.Bps));
            }
        }

        private void He4(Span<byte> dst, Span<byte> top)
        {
            // horizontal
            byte x = top[-1];
            byte i = top[-2];
            byte j = top[-3];
            byte k = top[-4];
            byte l = top[-5];

            uint val = 0x01010101U * LossyUtils.Avg3(x, i, j);
            BinaryPrimitives.WriteUInt32BigEndian(dst, val);
            val = 0x01010101U * LossyUtils.Avg3(i, j, k);
            BinaryPrimitives.WriteUInt32BigEndian(dst.Slice(1 * WebPConstants.Bps), val);
            val = 0x01010101U * LossyUtils.Avg3(j, k, l);
            BinaryPrimitives.WriteUInt32BigEndian(dst.Slice(2 * WebPConstants.Bps), val);
            val = 0x01010101U * LossyUtils.Avg3(k, l, l);
            BinaryPrimitives.WriteUInt32BigEndian(dst.Slice(1 * WebPConstants.Bps), val);
        }

        private void Rd4(Span<byte> dst, Span<byte> top)
        {
            byte x = top[-1];
            byte i = top[-2];
            byte j = top[-3];
            byte k = top[-4];
            byte l = top[-5];
            byte a = top[0];
            byte b = top[1];
            byte c = top[2];
            byte d = top[3];

            LossyUtils.Dst(dst, 0, 3, LossyUtils.Avg3(j, k, l));
            var ijk = LossyUtils.Avg3(i, j, k);
            LossyUtils.Dst(dst, 0, 2, ijk);
            LossyUtils.Dst(dst, 1, 3, ijk);
            var xij = LossyUtils.Avg3(x, i, j);
            LossyUtils.Dst(dst, 0, 1, xij);
            LossyUtils.Dst(dst, 1, 2, xij);
            LossyUtils.Dst(dst, 2, 3, xij);
            var axi = LossyUtils.Avg3(a, x, i);
            LossyUtils.Dst(dst, 0, 0, axi);
            LossyUtils.Dst(dst, 1, 1, axi);
            LossyUtils.Dst(dst, 2, 2, axi);
            LossyUtils.Dst(dst, 3, 3, axi);
            var bax = LossyUtils.Avg3(b, a, x);
            LossyUtils.Dst(dst, 1, 0, bax);
            LossyUtils.Dst(dst, 2, 1, bax);
            LossyUtils.Dst(dst, 3, 2, bax);
            var cba = LossyUtils.Avg3(c, b, a);
            LossyUtils.Dst(dst, 2, 0, cba);
            LossyUtils.Dst(dst, 3, 1, cba);
            LossyUtils.Dst(dst, 3, 0, LossyUtils.Avg3(d, c, b));
        }

        private void Vr4(Span<byte> dst, Span<byte> top)
        {
            byte x = top[-1];
            byte i = top[-2];
            byte j = top[-3];
            byte k = top[-4];
            byte a = top[0];
            byte b = top[1];
            byte c = top[2];
            byte d = top[3];

            var xa = LossyUtils.Avg2(x, a);
            LossyUtils.Dst(dst, 0, 0, xa);
            LossyUtils.Dst(dst, 1, 2, xa);
            var ab = LossyUtils.Avg2(a, b);
            LossyUtils.Dst(dst, 1, 0, ab);
            LossyUtils.Dst(dst, 2, 2, ab);
            var bc = LossyUtils.Avg2(b, c);
            LossyUtils.Dst(dst, 2, 0, bc);
            LossyUtils.Dst(dst, 3, 2, bc);
            LossyUtils.Dst(dst, 3, 0, LossyUtils.Avg2(c, d));
            LossyUtils.Dst(dst, 0, 3, LossyUtils.Avg3(k, j, i));
            LossyUtils.Dst(dst, 0, 2, LossyUtils.Avg3(j, i, x));
            var ixa = LossyUtils.Avg3(i, x, a);
            LossyUtils.Dst(dst, 0, 1, ixa);
            LossyUtils.Dst(dst, 1, 3, ixa);
            var xab = LossyUtils.Avg3(x, a, b);
            LossyUtils.Dst(dst, 1, 1, xab);
            LossyUtils.Dst(dst, 2, 3, xab);
            var abc = LossyUtils.Avg3(a, b, c);
            LossyUtils.Dst(dst, 2, 1, abc);
            LossyUtils.Dst(dst, 3, 3, abc);
            LossyUtils.Dst(dst, 3, 1, LossyUtils.Avg3(b, c, d));
        }

        private void Ld4(Span<byte> dst, Span<byte> top)
        {
            byte a = top[0];
            byte b = top[1];
            byte c = top[2];
            byte d = top[3];
            byte e = top[4];
            byte f = top[5];
            byte g = top[6];
            byte h = top[7];

            LossyUtils.Dst(dst, 0, 0, LossyUtils.Avg3(a, b, c));
            var bcd = LossyUtils.Avg3(b, c, d);
            LossyUtils.Dst(dst, 1, 0, bcd);
            LossyUtils.Dst(dst, 0, 1, bcd);
            var cde = LossyUtils.Avg3(c, d, e);
            LossyUtils.Dst(dst, 2, 0, cde);
            LossyUtils.Dst(dst, 1, 1, cde);
            LossyUtils.Dst(dst, 0, 2, cde);
            var def = LossyUtils.Avg3(d, e, f);
            LossyUtils.Dst(dst, 3, 0, def);
            LossyUtils.Dst(dst, 2, 1, def);
            LossyUtils.Dst(dst, 1, 2, def);
            LossyUtils.Dst(dst, 0, 3, def);
            var efg = LossyUtils.Avg3(e, f, g);
            LossyUtils.Dst(dst, 3, 1, efg);
            LossyUtils.Dst(dst, 2, 2, efg);
            LossyUtils.Dst(dst, 1, 3, efg);
            var fgh = LossyUtils.Avg3(f, g, h);
            LossyUtils.Dst(dst, 3, 2, fgh);
            LossyUtils.Dst(dst, 2, 3, fgh);
            LossyUtils.Dst(dst, 3, 3, LossyUtils.Avg3(g, h, h));
        }

        private void Vl4(Span<byte> dst, Span<byte> top)
        {
            byte a = top[0];
            byte b = top[1];
            byte c = top[2];
            byte d = top[3];
            byte e = top[4];
            byte f = top[5];
            byte g = top[6];
            byte h = top[7];

            LossyUtils.Dst(dst, 0, 0, LossyUtils.Avg2(a, b));
            var bc = LossyUtils.Avg2(b, c);
            LossyUtils.Dst(dst, 1, 0, bc);
            LossyUtils.Dst(dst, 0, 2, bc);
            var cd = LossyUtils.Avg2(c, d);
            LossyUtils.Dst(dst, 2, 0, cd);
            LossyUtils.Dst(dst, 1, 2, cd);
            var de = LossyUtils.Avg2(d, e);
            LossyUtils.Dst(dst, 3, 0, de);
            LossyUtils.Dst(dst, 2, 2, de);
            LossyUtils.Dst(dst, 0, 1, LossyUtils.Avg3(a, b, c));
            var bcd = LossyUtils.Avg3(b, c, d);
            LossyUtils.Dst(dst, 1, 1, bcd);
            LossyUtils.Dst(dst, 0, 3, bcd);
            var cde = LossyUtils.Avg3(c, d, e);
            LossyUtils.Dst(dst, 2, 1, cde);
            LossyUtils.Dst(dst, 1, 3, cde);
            var def = LossyUtils.Avg3(d, e, f);
            LossyUtils.Dst(dst, 3, 1, def);
            LossyUtils.Dst(dst, 2, 3, def);
            LossyUtils.Dst(dst, 3, 2, LossyUtils.Avg3(e, f, g));
            LossyUtils.Dst(dst, 3, 3, LossyUtils.Avg3(f, g, h));
        }

        private void Hd4(Span<byte> dst, Span<byte> top)
        {
            byte x = top[-1];
            byte i = top[-2];
            byte j = top[-3];
            byte k = top[-4];
            byte l = top[-5];
            byte a = top[0];
            byte b = top[1];
            byte c = top[2];

            var ix = LossyUtils.Avg2(i, x);
            LossyUtils.Dst(dst, 0, 0, ix);
            LossyUtils.Dst(dst, 2, 1, ix);
            var ji = LossyUtils.Avg2(j, i);
            LossyUtils.Dst(dst, 0, 1, ji);
            LossyUtils.Dst(dst, 2, 2, ji);
            var kj = LossyUtils.Avg2(k, j);
            LossyUtils.Dst(dst, 0, 2, kj);
            LossyUtils.Dst(dst, 2, 3, kj);
            LossyUtils.Dst(dst, 0, 3, LossyUtils.Avg2(l, k));
            LossyUtils.Dst(dst, 3, 0, LossyUtils.Avg3(a, b, c));
            LossyUtils.Dst(dst, 2, 0, LossyUtils.Avg3(x, a, b));
            var ixa = LossyUtils.Avg3(i, x, a);
            LossyUtils.Dst(dst, 1, 0, ixa);
            LossyUtils.Dst(dst, 3, 1, ixa);
            var jix = LossyUtils.Avg3(j, i, x);
            LossyUtils.Dst(dst, 1, 1, jix);
            LossyUtils.Dst(dst, 3, 2, jix);
            var kji = LossyUtils.Avg3(k, j, i);
            LossyUtils.Dst(dst, 1, 2, kji);
            LossyUtils.Dst(dst, 3, 3, kji);
            LossyUtils.Dst(dst, 1, 3, LossyUtils.Avg3(l, k, j));
        }

        private void Hu4(Span<byte> dst, Span<byte> top)
        {
            byte i = top[-2];
            byte j = top[-3];
            byte k = top[-4];
            byte l = top[-5];

            LossyUtils.Dst(dst, 0, 0, LossyUtils.Avg2(i, j));
            var jk = LossyUtils.Avg2(j, k);
            LossyUtils.Dst(dst, 2, 0, jk);
            LossyUtils.Dst(dst, 0, 1, jk);
            var kl = LossyUtils.Avg2(k, l);
            LossyUtils.Dst(dst, 2, 1, kl);
            LossyUtils.Dst(dst, 0, 2, kl);
            LossyUtils.Dst(dst, 1, 0, LossyUtils.Avg3(i, j, k));
            var jkl = LossyUtils.Avg3(j, k, l);
            LossyUtils.Dst(dst, 3, 0, jkl);
            LossyUtils.Dst(dst, 1, 1, jkl);
            var kll = LossyUtils.Avg3(k, l, l);
            LossyUtils.Dst(dst, 3, 1, kll);
            LossyUtils.Dst(dst, 1, 2, kll);
            LossyUtils.Dst(dst, 3, 2, l);
            LossyUtils.Dst(dst, 2, 2, l);
            LossyUtils.Dst(dst, 0, 3, l);
            LossyUtils.Dst(dst, 1, 3, l);
            LossyUtils.Dst(dst, 2, 3, l);
            LossyUtils.Dst(dst, 3, 3, l);
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
            int srcIdx = 0;
            for (int i = 0; i < h; ++i)
            {
                src.Slice(srcIdx, w).CopyTo(dst.Slice(dstIdx));
                if (w < size)
                {
                    dst.Slice(dstIdx, size - w).Fill(dst[dstIdx + w - 1]);
                }

                dstIdx += WebPConstants.Bps;
                srcIdx += srcStride;
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
            int srcIdx = 0;
            for (i = 0; i < len; ++i)
            {
                dst[i] = src[srcIdx];
                srcIdx += srcStride;
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
            this.predIdx = this.predsWidth + (y * 4 * this.predsWidth);

            this.InitLeft();
        }

        private void InitLeft()
        {
            Span<byte> yLeft = this.YLeft.AsSpan();
            Span<byte> uLeft = this.UvLeft.AsSpan(0, 16);
            Span<byte> vLeft = this.UvLeft.AsSpan(16, 16);
            byte val = (byte)((this.Y > 0) ? 129 : 127);
            yLeft[0] = val;
            uLeft[0] = val;
            vLeft[0] = val;

            yLeft.Slice(1, 16).Fill(129);
            uLeft.Slice(1, 8).Fill(129);
            vLeft.Slice(1, 8).Fill(129);

            this.LeftNz[8] = 0;
        }

        private void InitTop()
        {
            int topSize = this.mbw * 16;
            this.YTop.Slice(0, topSize).Fill(127);
            this.Nz.GetSpan().Fill(0);
        }

        // Convert packed context to byte array.
        private int Bit(uint nz, int n)
        {
            return (int)(nz & (1 << n));
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
