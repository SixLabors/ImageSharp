// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Webp.Lossy;

/// <summary>
/// Iterator structure to iterate through macroblocks, pointing to the
/// right neighbouring data (samples, predictions, contexts, ...)
/// </summary>
internal class Vp8EncIterator
{
    public const int YOffEnc = 0;

    public const int UOffEnc = 16;

    public const int VOffEnc = 16 + 8;

    private const int MaxIntra16Mode = 2;

    private const int MaxIntra4Mode = 2;

    private const int MaxUvMode = 2;

    private const int DefaultAlpha = -1;

    private readonly int mbw;

    private readonly int mbh;

    /// <summary>
    /// Stride of the prediction plane(=4*mbw + 1).
    /// </summary>
    private readonly int predsWidth;

    /// <summary>
    /// Array to record the position of the top sample to pass to the prediction functions.
    /// </summary>
    private readonly byte[] vp8TopLeftI4 =
    {
        17, 21, 25, 29,
        13, 17, 21, 25,
        9,  13, 17, 21,
        5,   9, 13, 17
    };

    private int currentMbIdx;

    private int nzIdx;
    private int yTopIdx;

    private int uvTopIdx;

    public Vp8EncIterator(Vp8Encoder enc)
        : this(enc.YTop, enc.UvTop, enc.Nz, enc.MbInfo, enc.Preds, enc.TopDerr, enc.Mbw, enc.Mbh)
    {
    }

    public Vp8EncIterator(byte[] yTop, byte[] uvTop, uint[] nz, Vp8MacroBlockInfo[] mb, byte[] preds, sbyte[] topDerr, int mbw, int mbh)
    {
        this.YTop = yTop;
        this.UvTop = uvTop;
        this.Nz = nz;
        this.Mb = mb;
        this.Preds = preds;
        this.TopDerr = topDerr;
        this.LeftDerr = new sbyte[2 * 2];
        this.mbw = mbw;
        this.mbh = mbh;
        this.currentMbIdx = 0;
        this.nzIdx = 1;
        this.yTopIdx = 0;
        this.uvTopIdx = 0;
        this.predsWidth = (4 * mbw) + 1;
        this.PredIdx = this.predsWidth;
        this.YuvIn = new byte[WebpConstants.Bps * 16];
        this.YuvOut = new byte[WebpConstants.Bps * 16];
        this.YuvOut2 = new byte[WebpConstants.Bps * 16];
        this.YuvP = new byte[(32 * WebpConstants.Bps) + (16 * WebpConstants.Bps) + (8 * WebpConstants.Bps)]; // I16+Chroma+I4 preds
        this.YLeft = new byte[32];
        this.UvLeft = new byte[32];
        this.TopNz = new int[9];
        this.LeftNz = new int[9];
        this.I4Boundary = new byte[37];
        this.BitCount = new long[4, 3];
        this.Scratch = new byte[WebpConstants.Bps * 16];
        this.Scratch2 = new short[17 * 16];
        this.Scratch3 = new int[16];

        // To match the C initial values of the reference implementation, initialize all with 204.
        const byte defaultInitVal = 204;
        this.YuvIn.AsSpan().Fill(defaultInitVal);
        this.YuvOut.AsSpan().Fill(defaultInitVal);
        this.YuvOut2.AsSpan().Fill(defaultInitVal);
        this.YuvP.AsSpan().Fill(defaultInitVal);
        this.YLeft.AsSpan().Fill(defaultInitVal);
        this.UvLeft.AsSpan().Fill(defaultInitVal);
        this.Scratch.AsSpan().Fill(defaultInitVal);

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
    /// Gets the left error diffusion (u/v).
    /// </summary>
    public sbyte[] LeftDerr { get; }

    /// <summary>
    /// Gets the top luma samples at position 'X'.
    /// </summary>
    public byte[] YTop { get; }

    /// <summary>
    /// Gets the top u/v samples at position 'X', packed as 16 bytes.
    /// </summary>
    public byte[] UvTop { get; }

    /// <summary>
    /// Gets the intra mode predictors (4x4 blocks).
    /// </summary>
    public byte[] Preds { get; }

    /// <summary>
    /// Gets the current start index of the intra mode predictors.
    /// </summary>
    public int PredIdx { get; private set; }

    /// <summary>
    /// Gets the non-zero pattern.
    /// </summary>
    public uint[] Nz { get; }

    /// <summary>
    /// Gets the top diffusion error.
    /// </summary>
    public sbyte[] TopDerr { get; }

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

    /// <summary>
    /// Gets the byte scratch buffer.
    /// </summary>
    public byte[] Scratch { get; }

    /// <summary>
    /// Gets the short scratch buffer.
    /// </summary>
    public short[] Scratch2 { get; }

    /// <summary>
    /// Gets the int scratch buffer.
    /// </summary>
    public int[] Scratch3 { get; }

    public Vp8MacroBlockInfo CurrentMacroBlockInfo => this.Mb[this.currentMbIdx];

    private Vp8MacroBlockInfo[] Mb { get; }

    public void Init() => this.Reset();

    public static void InitFilter()
    {
        // TODO: add support for autofilter
    }

    public void StartI4()
    {
        int i;
        this.I4 = 0;    // first 4x4 sub-block.
        this.I4BoundaryIdx = this.vp8TopLeftI4[0];

        // Import the boundary samples.
        for (i = 0; i < 17; i++)
        {
            // left
            this.I4Boundary[i] = this.YLeft[15 - i + 1];
        }

        Span<byte> yTop = this.YTop.AsSpan(this.yTopIdx);
        for (i = 0; i < 16; i++)
        {
            // top
            this.I4Boundary[17 + i] = yTop[i];
        }

        // top-right samples have a special case on the far right of the picture.
        if (this.X < this.mbw - 1)
        {
            for (i = 16; i < 16 + 4; i++)
            {
                this.I4Boundary[17 + i] = yTop[i];
            }
        }
        else
        {
            // else, replicate the last valid pixel four times
            for (i = 16; i < 16 + 4; i++)
            {
                this.I4Boundary[17 + i] = this.I4Boundary[17 + 15];
            }
        }

        this.NzToBytes();  // import the non-zero context.
    }

    // Import uncompressed samples from source.
    public void Import(Span<byte> y, Span<byte> u, Span<byte> v, int yStride, int uvStride, int width, int height, bool importBoundarySamples)
    {
        int yStartIdx = ((this.Y * yStride) + this.X) * 16;
        int uvStartIdx = ((this.Y * uvStride) + this.X) * 8;
        Span<byte> ySrc = y[yStartIdx..];
        Span<byte> uSrc = u[uvStartIdx..];
        Span<byte> vSrc = v[uvStartIdx..];
        int w = Math.Min(width - (this.X * 16), 16);
        int h = Math.Min(height - (this.Y * 16), 16);
        int uvw = (w + 1) >> 1;
        int uvh = (h + 1) >> 1;

        Span<byte> yuvIn = this.YuvIn.AsSpan(YOffEnc);
        Span<byte> uIn = this.YuvIn.AsSpan(UOffEnc);
        Span<byte> vIn = this.YuvIn.AsSpan(VOffEnc);
        ImportBlock(ySrc, yStride, yuvIn, w, h, 16);
        ImportBlock(uSrc, uvStride, uIn, uvw, uvh, 8);
        ImportBlock(vSrc, uvStride, vIn, uvw, uvh, 8);

        if (!importBoundarySamples)
        {
            return;
        }

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

            ImportLine(y[(yStartIdx - 1)..], yStride, yLeft[1..], h, 16);
            ImportLine(u[(uvStartIdx - 1)..], uvStride, uLeft[1..], uvh, 8);
            ImportLine(v[(uvStartIdx - 1)..], uvStride, vLeft[1..], uvh, 8);
        }

        Span<byte> yTop = this.YTop.AsSpan(this.yTopIdx, 16);
        if (this.Y == 0)
        {
            yTop.Fill(127);
            this.UvTop.AsSpan(this.uvTopIdx, 16).Fill(127);
        }
        else
        {
            ImportLine(y[(yStartIdx - yStride)..], 1, yTop, w, 16);
            ImportLine(u[(uvStartIdx - uvStride)..], 1, this.UvTop.AsSpan(this.uvTopIdx, 8), uvw, 8);
            ImportLine(v[(uvStartIdx - uvStride)..], 1, this.UvTop.AsSpan(this.uvTopIdx + 8, 8), uvw, 8);
        }
    }

    public int FastMbAnalyze(uint quality)
    {
        // Empirical cut-off value, should be around 16 (~=block size). We use the
        // [8-17] range and favor intra4 at high quality, intra16 for low quality.
        uint q = quality;
        uint kThreshold = 8 + ((17 - 8) * q / 100);
        int k;
        Span<uint> dc = stackalloc uint[16];
        uint m;
        uint m2;
        for (k = 0; k < 16; k += 4)
        {
            LossyUtils.Mean16x4(this.YuvIn.AsSpan(YOffEnc + (k * WebpConstants.Bps)), dc.Slice(k, 4));
        }

        for (m = 0, m2 = 0, k = 0; k < 16; k++)
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
            Span<byte> modes = stackalloc byte[16];  // DC4
            this.SetIntra4Mode(modes);
        }

        return 0;
    }

    public int MbAnalyzeBestIntra16Mode()
    {
        const int maxMode = MaxIntra16Mode;
        int mode;
        int bestAlpha = DefaultAlpha;
        int bestMode = 0;

        this.MakeLuma16Preds();
        for (mode = 0; mode < maxMode; mode++)
        {
            Vp8Histogram histo = new();
            histo.CollectHistogram(this.YuvIn.AsSpan(YOffEnc), this.YuvP.AsSpan(Vp8Encoding.Vp8I16ModeOffsets[mode]), 0, 16);
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

    public int MbAnalyzeBestIntra4Mode(int bestAlpha)
    {
        Span<byte> modes = stackalloc byte[16];
        const int maxMode = MaxIntra4Mode;
        Vp8Histogram totalHisto = new();
        int curHisto = 0;
        this.StartI4();
        do
        {
            int mode;
            int bestModeAlpha = DefaultAlpha;
            Vp8Histogram[] histos = new Vp8Histogram[2];
            Span<byte> src = this.YuvIn.AsSpan(YOffEnc + WebpLookupTables.Vp8Scan[this.I4]);

            this.MakeIntra4Preds();
            for (mode = 0; mode < maxMode; ++mode)
            {
                histos[curHisto] = new Vp8Histogram();
                histos[curHisto].CollectHistogram(src, this.YuvP.AsSpan(Vp8Encoding.Vp8I4ModeOffsets[mode]), 0, 1);

                int alpha = histos[curHisto].GetAlpha();
                if (alpha > bestModeAlpha)
                {
                    bestModeAlpha = alpha;
                    modes[this.I4] = (byte)mode;

                    // Keep track of best histo so far.
                    curHisto ^= 1;
                }
            }

            // Accumulate best histogram.
            histos[curHisto ^ 1].Merge(totalHisto);
        }
        while (this.RotateI4(this.YuvIn.AsSpan(YOffEnc))); // Note: we reuse the original samples for predictors.

        int i4Alpha = totalHisto.GetAlpha();
        if (i4Alpha > bestAlpha)
        {
            this.SetIntra4Mode(modes);
            bestAlpha = i4Alpha;
        }

        return bestAlpha;
    }

    public int MbAnalyzeBestUvMode()
    {
        int bestAlpha = DefaultAlpha;
        int smallestAlpha = 0;
        int bestMode = 0;
        const int maxMode = MaxUvMode;
        int mode;

        this.MakeChroma8Preds();
        for (mode = 0; mode < maxMode; ++mode)
        {
            Vp8Histogram histo = new();
            histo.CollectHistogram(this.YuvIn.AsSpan(UOffEnc), this.YuvP.AsSpan(Vp8Encoding.Vp8UvModeOffsets[mode]), 16, 16 + 4 + 4);
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
        Span<byte> preds = this.Preds.AsSpan(this.PredIdx);
        for (int y = 0; y < 4; y++)
        {
            preds[..4].Fill((byte)mode);
            preds = preds[this.predsWidth..];
        }

        this.CurrentMacroBlockInfo.MacroBlockType = Vp8MacroBlockType.I16X16;
    }

    public void SetIntra4Mode(ReadOnlySpan<byte> modes)
    {
        int modesIdx = 0;
        int predIdx = this.PredIdx;
        for (int y = 4; y > 0; y--)
        {
            modes.Slice(modesIdx, 4).CopyTo(this.Preds.AsSpan(predIdx));
            predIdx += this.predsWidth;
            modesIdx += 4;
        }

        this.CurrentMacroBlockInfo.MacroBlockType = Vp8MacroBlockType.I4X4;
    }

    public int GetCostLuma16(Vp8ModeScore rd, Vp8EncProba proba, Vp8Residual res)
    {
        int r = 0;

        // re-import the non-zero context.
        this.NzToBytes();

        // DC
        res.Init(0, 1, proba);
        res.SetCoeffs(rd.YDcLevels);
        r += res.GetResidualCost(this.TopNz[8] + this.LeftNz[8]);

        // AC
        res.Init(1, 0, proba);
        for (int y = 0; y < 4; y++)
        {
            for (int x = 0; x < 4; x++)
            {
                int ctx = this.TopNz[x] + this.LeftNz[y];
                res.SetCoeffs(rd.YAcLevels.AsSpan((x + (y * 4)) * 16, 16));
                r += res.GetResidualCost(ctx);
                this.TopNz[x] = this.LeftNz[y] = res.Last >= 0 ? 1 : 0;
            }
        }

        return r;
    }

    public short[] GetCostModeI4(byte[] modes)
    {
        int predsWidth = this.predsWidth;
        int predIdx = this.PredIdx;
        int x = this.I4 & 3;
        int y = this.I4 >> 2;
        int left = x == 0 ? this.Preds[predIdx + (y * predsWidth) - 1] : modes[this.I4 - 1];
        int top = y == 0 ? this.Preds[predIdx - predsWidth + x] : modes[this.I4 - 4];
        return WebpLookupTables.Vp8FixedCostsI4[top, left];
    }

    public int GetCostLuma4(Span<short> levels, Vp8EncProba proba, Vp8Residual res)
    {
        int x = this.I4 & 3;
        int y = this.I4 >> 2;
        int r = 0;

        res.Init(0, 3, proba);
        int ctx = this.TopNz[x] + this.LeftNz[y];
        res.SetCoeffs(levels);
        r += res.GetResidualCost(ctx);
        return r;
    }

    public int GetCostUv(Vp8ModeScore rd, Vp8EncProba proba, Vp8Residual res)
    {
        int r = 0;

        // re-import the non-zero context.
        this.NzToBytes();

        res.Init(0, 2, proba);
        for (int ch = 0; ch <= 2; ch += 2)
        {
            for (int y = 0; y < 2; y++)
            {
                for (int x = 0; x < 2; x++)
                {
                    int ctx = this.TopNz[4 + ch + x] + this.LeftNz[4 + ch + y];
                    res.SetCoeffs(rd.UvLevels.AsSpan(((ch * 2) + x + (y * 2)) * 16, 16));
                    r += res.GetResidualCost(ctx);
                    this.TopNz[4 + ch + x] = this.LeftNz[4 + ch + y] = res.Last >= 0 ? 1 : 0;
                }
            }
        }

        return r;
    }

    public void SetIntraUvMode(int mode) => this.CurrentMacroBlockInfo.UvMode = mode;

    public void SetSkip(bool skip) => this.CurrentMacroBlockInfo.Skip = skip;

    public void SetSegment(int segment) => this.CurrentMacroBlockInfo.Segment = segment;

    public void StoreDiffusionErrors(Vp8ModeScore rd)
    {
        for (int ch = 0; ch <= 1; ++ch)
        {
            Span<sbyte> top = this.TopDerr.AsSpan((this.X * 4) + ch, 2);
            Span<sbyte> left = this.LeftDerr.AsSpan(ch, 2);

            // restore err1
            left[0] = (sbyte)rd.Derr[ch, 0];

            // 3/4th of err3
            left[1] = (sbyte)((3 * rd.Derr[ch, 2]) >> 2);

            // err2
            top[0] = (sbyte)rd.Derr[ch, 1];

            // 1/4th of err3.
            top[1] = (sbyte)(rd.Derr[ch, 2] - left[1]);
        }
    }

    /// <summary>
    /// Returns true if iteration is finished.
    /// </summary>
    /// <returns>True if iterator is finished.</returns>
    public bool IsDone() => this.CountDown <= 0;

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
            this.PredIdx += 4;
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
            for (int i = 0; i < 16; i++)
            {
                this.YLeft[i + 1] = ySrc[15 + (i * WebpConstants.Bps)];
            }

            for (int i = 0; i < 8; i++)
            {
                this.UvLeft[i + 1] = uvSrc[7 + (i * WebpConstants.Bps)];
                this.UvLeft[i + 16 + 1] = uvSrc[15 + (i * WebpConstants.Bps)];
            }

            // top-left (before 'top'!)
            this.YLeft[0] = this.YTop[this.yTopIdx + 15];
            this.UvLeft[0] = this.UvTop[this.uvTopIdx + 0 + 7];
            this.UvLeft[16] = this.UvTop[this.uvTopIdx + 8 + 7];
        }

        if (y < this.mbh - 1)
        {
            // top
            ySrc.Slice(15 * WebpConstants.Bps, 16).CopyTo(this.YTop.AsSpan(this.yTopIdx));
            uvSrc.Slice(7 * WebpConstants.Bps, 8 + 8).CopyTo(this.UvTop.AsSpan(this.uvTopIdx));
        }
    }

    public bool RotateI4(Span<byte> yuvOut)
    {
        Span<byte> blk = yuvOut[WebpLookupTables.Vp8Scan[this.I4]..];
        Span<byte> top = this.I4Boundary.AsSpan();
        int topOffset = this.I4BoundaryIdx;
        int i;

        // Update the cache with 7 fresh samples.
        for (i = 0; i <= 3; i++)
        {
            top[topOffset - 4 + i] = blk[i + (3 * WebpConstants.Bps)];   // Store future top samples.
        }

        if ((this.I4 & 3) != 3)
        {
            // if not on the right sub-blocks #3, #7, #11, #15
            for (i = 0; i <= 2; i++)
            {
                // store future left samples
                top[topOffset + i] = blk[3 + ((2 - i) * WebpConstants.Bps)];
            }
        }
        else
        {
            // else replicate top-right samples, as says the specs.
            for (i = 0; i <= 3; i++)
            {
                top[topOffset + i] = top[topOffset + i + 4];
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
            this.Nz[this.nzIdx] = 0;
            this.LeftNz[8] = 0;
        }
        else
        {
            // Preserve the dc_nz bit.
            this.Nz[this.nzIdx] &= 1 << 24;
        }
    }

    public void MakeLuma16Preds()
    {
        Span<byte> left = this.X != 0 ? this.YLeft.AsSpan() : null;
        Span<byte> top = this.Y != 0 ? this.YTop.AsSpan(this.yTopIdx) : null;
        Vp8Encoding.EncPredLuma16(this.YuvP, left, top);
    }

    public void MakeChroma8Preds()
    {
        Span<byte> left = this.X != 0 ? this.UvLeft.AsSpan() : null;
        Span<byte> top = this.Y != 0 ? this.UvTop.AsSpan(this.uvTopIdx) : null;
        Vp8Encoding.EncPredChroma8(this.YuvP, left, top);
    }

    public void MakeIntra4Preds() => Vp8Encoding.EncPredLuma4(this.YuvP, this.I4Boundary, this.I4BoundaryIdx, this.Scratch.AsSpan(0, 4));

    public void SwapOut()
    {
        // Tuple swap uses 2 more IL bytes
#pragma warning disable IDE0180 // Use tuple to swap values
        byte[] tmp = this.YuvOut;
        this.YuvOut = this.YuvOut2;
        this.YuvOut2 = tmp;
#pragma warning restore IDE0180 // Use tuple to swap values
    }

    public void NzToBytes()
    {
        Span<uint> nz = this.Nz.AsSpan();

        uint lnz = nz[this.nzIdx - 1];
        uint tnz = nz[this.nzIdx];
        Span<int> topNz = this.TopNz;
        Span<int> leftNz = this.LeftNz;

        // Top-Y
        topNz[0] = Bit(tnz, 12);
        topNz[1] = Bit(tnz, 13);
        topNz[2] = Bit(tnz, 14);
        topNz[3] = Bit(tnz, 15);

        // Top-U
        topNz[4] = Bit(tnz, 18);
        topNz[5] = Bit(tnz, 19);

        // Top-V
        topNz[6] = Bit(tnz, 22);
        topNz[7] = Bit(tnz, 23);

        // DC
        topNz[8] = Bit(tnz, 24);

        // left-Y
        leftNz[0] = Bit(lnz, 3);
        leftNz[1] = Bit(lnz, 7);
        leftNz[2] = Bit(lnz, 11);
        leftNz[3] = Bit(lnz, 15);

        // left-U
        leftNz[4] = Bit(lnz, 17);
        leftNz[5] = Bit(lnz, 19);

        // left-V
        leftNz[6] = Bit(lnz, 21);
        leftNz[7] = Bit(lnz, 23);

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

        this.Nz[this.nzIdx] = nz;
    }

    private static void ImportBlock(Span<byte> src, int srcStride, Span<byte> dst, int w, int h, int size)
    {
        int dstIdx = 0;
        int srcIdx = 0;
        for (int i = 0; i < h; i++)
        {
            // memcpy(dst, src, w);
            src.Slice(srcIdx, w).CopyTo(dst[dstIdx..]);
            if (w < size)
            {
                // memset(dst + w, dst[w - 1], size - w);
                dst.Slice(dstIdx + w, size - w).Fill(dst[dstIdx + w - 1]);
            }

            dstIdx += WebpConstants.Bps;
            srcIdx += srcStride;
        }

        for (int i = h; i < size; i++)
        {
            // memcpy(dst, dst - BPS, size);
            dst.Slice(dstIdx - WebpConstants.Bps, size).CopyTo(dst[dstIdx..]);
            dstIdx += WebpConstants.Bps;
        }
    }

    private static void ImportLine(Span<byte> src, int srcStride, Span<byte> dst, int len, int totalLen)
    {
        int i;
        int srcIdx = 0;
        for (i = 0; i < len; i++)
        {
            dst[i] = src[srcIdx];
            srcIdx += srcStride;
        }

        for (; i < totalLen; i++)
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

        Array.Clear(this.BitCount);
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
        this.nzIdx = 1; // note: in reference source nz starts at -1.
        this.yTopIdx = 0;
        this.uvTopIdx = 0;
        this.PredIdx = this.predsWidth + (y * 4 * this.predsWidth);

        this.InitLeft();
    }

    private void InitLeft()
    {
        Span<byte> yLeft = this.YLeft.AsSpan();
        Span<byte> uLeft = this.UvLeft.AsSpan(0, 16);
        Span<byte> vLeft = this.UvLeft.AsSpan(16, 16);
        byte val = (byte)(this.Y > 0 ? 129 : 127);
        yLeft[0] = val;
        uLeft[0] = val;
        vLeft[0] = val;

        yLeft.Slice(1, 16).Fill(129);
        uLeft.Slice(1, 8).Fill(129);
        vLeft.Slice(1, 8).Fill(129);

        this.LeftNz[8] = 0;

        this.LeftDerr.AsSpan().Clear();
    }

    private void InitTop()
    {
        int topSize = this.mbw * 16;
        this.YTop.AsSpan(0, topSize).Fill(127);
        this.UvTop.AsSpan().Fill(127);
        this.Nz.AsSpan().Clear();

        int predsW = (4 * this.mbw) + 1;
        int predsH = (4 * this.mbh) + 1;
        int predsSize = predsW * predsH;
        this.Preds.AsSpan(predsSize + this.predsWidth, this.mbw).Clear();

        this.TopDerr.AsSpan().Clear();
    }

    private static int Bit(uint nz, int n) => (nz & (1 << n)) != 0 ? 1 : 0;

    /// <summary>
    /// Set count down.
    /// </summary>
    /// <param name="countDown">Number of iterations to go.</param>
    private void SetCountDown(int countDown) => this.CountDown = countDown;
}
