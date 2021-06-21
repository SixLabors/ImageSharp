// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;

using SixLabors.ImageSharp.Formats.Webp.BitWriter;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Webp.Lossy
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
        /// The global configuration.
        /// </summary>
        private readonly Configuration configuration;

        /// <summary>
        /// The quality, that will be used to encode the image.
        /// </summary>
        private readonly int quality;

        /// <summary>
        /// Quality/speed trade-off (0=fast, 6=slower-better).
        /// </summary>
        private readonly int method;

        /// <summary>
        /// Number of entropy-analysis passes (in [1..10]).
        /// </summary>
        private readonly int entropyPasses;

        /// <summary>
        /// A bit writer for writing lossy webp streams.
        /// </summary>
        private Vp8BitWriter bitWriter;

        private readonly Vp8RdLevel rdOptLevel;

        private int maxI4HeaderBits;

        /// <summary>
        /// Global susceptibility.
        /// </summary>
        private int alpha;

        /// <summary>
        /// U/V quantization susceptibility.
        /// </summary>
        private int uvAlpha;

        private readonly byte[] averageBytesPerMb = { 50, 24, 16, 9, 7, 5, 3, 2 };

        private const int NumMbSegments = 4;

        private const int NumBModes = 10;

        /// <summary>
        /// The number of prediction modes.
        /// </summary>
        private const int NumPredModes = 4;

        private const int MaxItersKMeans = 6;

        // Convergence is considered reached if dq < DqLimit
        private const float DqLimit = 0.4f;

        private const ulong Partition0SizeLimit = (WebpConstants.Vp8MaxPartition0Size - 2048UL) << 11;

        private const long HeaderSizeEstimate = WebpConstants.RiffHeaderSize + WebpConstants.ChunkHeaderSize + WebpConstants.Vp8FrameHeaderSize;

        private const int QMin = 0;

        private const int QMax = 100;

        // TODO: filterStrength is hardcoded, should be configurable.
        private const int FilterStrength = 60;

        /// <summary>
        /// Initializes a new instance of the <see cref="Vp8Encoder"/> class.
        /// </summary>
        /// <param name="memoryAllocator">The memory allocator.</param>
        /// <param name="configuration">The global configuration.</param>
        /// <param name="width">The width of the input image.</param>
        /// <param name="height">The height of the input image.</param>
        /// <param name="quality">The encoding quality.</param>
        /// <param name="method">Quality/speed trade-off (0=fast, 6=slower-better).</param>
        /// <param name="entropyPasses">Number of entropy-analysis passes (in [1..10]).</param>
        public Vp8Encoder(MemoryAllocator memoryAllocator, Configuration configuration, int width, int height, int quality, int method, int entropyPasses)
        {
            this.memoryAllocator = memoryAllocator;
            this.configuration = configuration;
            this.Width = width;
            this.Height = height;
            this.quality = Numerics.Clamp(quality, 0, 100);
            this.method = Numerics.Clamp(method, 0, 6);
            this.entropyPasses = Numerics.Clamp(entropyPasses, 1, 10);
            this.rdOptLevel = (method >= 6) ? Vp8RdLevel.RdOptTrellisAll
                : (method >= 5) ? Vp8RdLevel.RdOptTrellis
                : (method >= 3) ? Vp8RdLevel.RdOptBasic
                : Vp8RdLevel.RdOptNone;

            int pixelCount = width * height;
            this.Mbw = (width + 15) >> 4;
            this.Mbh = (height + 15) >> 4;
            int uvSize = ((width + 1) >> 1) * ((height + 1) >> 1);
            this.Y = this.memoryAllocator.Allocate<byte>(pixelCount);
            this.U = this.memoryAllocator.Allocate<byte>(uvSize);
            this.V = this.memoryAllocator.Allocate<byte>(uvSize);
            this.YTop = new byte[this.Mbw * 16];
            this.UvTop = new byte[this.Mbw * 16 * 2];
            this.Nz = new uint[this.Mbw + 1];
            this.MbHeaderLimit = 256 * 510 * 8 * 1024 / (this.Mbw * this.Mbh);
            this.TopDerr = new sbyte[this.Mbw * 4];

            // TODO: make partition_limit configurable?
            int limit = 100; // original code: limit = 100 - config->partition_limit;
            this.maxI4HeaderBits =
                256 * 16 * 16 * limit * limit / (100 * 100);  // ... modulated with a quadratic curve.

            this.MbInfo = new Vp8MacroBlockInfo[this.Mbw * this.Mbh];
            for (int i = 0; i < this.MbInfo.Length; i++)
            {
                this.MbInfo[i] = new Vp8MacroBlockInfo();
            }

            this.SegmentInfos = new Vp8SegmentInfo[4];
            for (int i = 0; i < 4; i++)
            {
                this.SegmentInfos[i] = new Vp8SegmentInfo();
            }

            this.FilterHeader = new Vp8FilterHeader();
            int predSize = (((4 * this.Mbw) + 1) * ((4 * this.Mbh) + 1)) + this.PredsWidth + 1;
            this.PredsWidth = (4 * this.Mbw) + 1;
            this.Proba = new Vp8EncProba();
            this.Preds = new byte[predSize + this.PredsWidth + this.Mbw];

            // Initialize with default values, which the reference c implementation uses,
            // to be able to compare to the original and spot differences.
            this.Preds.AsSpan().Fill(205);
            this.Nz.AsSpan().Fill(3452816845);

            this.ResetBoundaryPredictions();
        }

        public int BaseQuant { get; set; }

        /// <summary>
        /// Gets the probabilities.
        /// </summary>
        public Vp8EncProba Proba { get; }

        /// <summary>
        /// Gets the segment features.
        /// </summary>
        public Vp8EncSegmentHeader SegmentHeader { get; private set; }

        /// <summary>
        /// Gets the segment infos.
        /// </summary>
        public Vp8SegmentInfo[] SegmentInfos { get; }

        /// <summary>
        /// Gets the macro block info's.
        /// </summary>
        public Vp8MacroBlockInfo[] MbInfo { get; }

        /// <summary>
        /// Gets the filter header.
        /// </summary>
        public Vp8FilterHeader FilterHeader { get; }

        /// <summary>
        /// Gets or sets the global susceptibility.
        /// </summary>
        public int Alpha { get; set; }

        /// <summary>
        /// Gets the width of the image.
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Gets the height of the image.
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// Gets the stride of the prediction plane (=4*mb_w + 1)
        /// </summary>
        public int PredsWidth { get; }

        /// <summary>
        /// Gets the macroblock width.
        /// </summary>
        public int Mbw { get; }

        /// <summary>
        /// Gets the macroblock height.
        /// </summary>
        public int Mbh { get; }

        public int DqY1Dc { get; private set; }

        public int DqY2Ac { get; private set; }

        public int DqY2Dc { get; private set; }

        public int DqUvAc { get; private set; }

        public int DqUvDc { get; private set; }

        /// <summary>
        /// Gets the luma component.
        /// </summary>
        private IMemoryOwner<byte> Y { get; }

        /// <summary>
        /// Gets the chroma U component.
        /// </summary>
        private IMemoryOwner<byte> U { get; }

        /// <summary>
        /// Gets the chroma U component.
        /// </summary>
        private IMemoryOwner<byte> V { get; }

        /// <summary>
        /// Gets the top luma samples.
        /// </summary>
        public byte[] YTop { get; }

        /// <summary>
        /// Gets the top u/v samples. U and V are packed into 16 bytes (8 U + 8 V).
        /// </summary>
        public byte[] UvTop { get; }

        /// <summary>
        /// Gets the non-zero pattern.
        /// </summary>
        public uint[] Nz { get; }

        /// <summary>
        /// Gets the prediction modes: (4*mbw+1) * (4*mbh+1).
        /// </summary>
        public byte[] Preds { get; }

        /// <summary>
        /// Gets the diffusion error.
        /// </summary>
        public sbyte[] TopDerr { get; }

        /// <summary>
        /// Gets a rough limit for header bits per MB.
        /// </summary>
        private int MbHeaderLimit { get; }

        private readonly ushort[] weightY = { 38, 32, 20, 9, 32, 28, 17, 7, 20, 17, 10, 4, 9, 7, 4, 2 };

        /// <summary>
        /// Encodes the image to the specified stream from the <see cref="Image{TPixel}"/>.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="image">The <see cref="Image{TPixel}"/> to encode from.</param>
        /// <param name="stream">The <see cref="Stream"/> to encode the image data to.</param>
        public void Encode<TPixel>(Image<TPixel> image, Stream stream)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int width = image.Width;
            int height = image.Height;
            this.ConvertRgbToYuv(image);
            Span<byte> y = this.Y.GetSpan();
            Span<byte> u = this.U.GetSpan();
            Span<byte> v = this.V.GetSpan();

            int yStride = width;
            int uvStride = (yStride + 1) >> 1;

            var it = new Vp8EncIterator(this.YTop, this.UvTop, this.Nz, this.MbInfo, this.Preds, this.TopDerr, this.Mbw, this.Mbh);
            int[] alphas = new int[WebpConstants.MaxAlpha + 1];
            this.alpha = this.MacroBlockAnalysis(width, height, it, y, u, v, yStride, uvStride, alphas, out this.uvAlpha);
            int totalMb = this.Mbw * this.Mbw;
            this.alpha /= totalMb;
            this.uvAlpha /= totalMb;

            // Analysis is done, proceed to actual encoding.
            this.SegmentHeader = new Vp8EncSegmentHeader(4);
            this.AssignSegments(alphas);
            this.SetLoopParams(this.quality);

            // Initialize the bitwriter.
            int averageBytesPerMacroBlock = this.averageBytesPerMb[this.BaseQuant >> 4];
            int expectedSize = this.Mbw * this.Mbh * averageBytesPerMacroBlock;
            this.bitWriter = new Vp8BitWriter(expectedSize, this);

            // TODO: EncodeAlpha();
            // Stats-collection loop.
            this.StatLoop(width, height, yStride, uvStride);
            it.Init();
            it.InitFilter();
            do
            {
                bool dontUseSkip = !this.Proba.UseSkipProba;

                var info = new Vp8ModeScore();
                it.Import(y, u, v, yStride, uvStride, width, height, false);

                // Warning! order is important: first call VP8Decimate() and
                // *then* decide how to code the skip decision if there's one.
                if (!this.Decimate(it, info, this.rdOptLevel) || dontUseSkip)
                {
                    this.CodeResiduals(it, info);
                }
                else
                {
                    it.ResetAfterSkip();
                }

                it.SaveBoundary();
            }
            while (it.Next());

            // Store filter stats.
            this.AdjustFilterStrength();

            // Write bytes from the bitwriter buffer to the stream.
            image.Metadata.SyncProfiles();
            this.bitWriter.WriteEncodedImageToStream(stream, image.Metadata.ExifProfile, (uint)width, (uint)height);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.Y.Dispose();
            this.U.Dispose();
            this.V.Dispose();
        }

        /// <summary>
        /// Only collect statistics(number of skips, token usage, ...).
        /// This is used for deciding optimal probabilities. It also modifies the
        /// quantizer value if some target (size, PSNR) was specified.
        /// </summary>
        private void StatLoop(int width, int height, int yStride, int uvStride)
        {
            int targetSize = 0; // TODO: target size is hardcoded.
            float targetPsnr = 0.0f; // TODO: targetPsnr is hardcoded.
            bool doSearch = false; // TODO: doSearch hardcoded for now.
            bool fastProbe = (this.method == 0 || this.method == 3) && !doSearch;
            int numPassLeft = this.entropyPasses;
            Vp8RdLevel rdOpt = (this.method >= 3 || doSearch) ? Vp8RdLevel.RdOptBasic : Vp8RdLevel.RdOptNone;
            int nbMbs = this.Mbw * this.Mbh;

            var stats = new PassStats(targetSize, targetPsnr, QMin, QMax, this.quality);
            this.Proba.ResetTokenStats();

            // Fast mode: quick analysis pass over few mbs. Better than nothing.
            if (fastProbe)
            {
                if (this.method == 3)
                {
                    // We need more stats for method 3 to be reliable.
                    nbMbs = (nbMbs > 200) ? nbMbs >> 1 : 100;
                }
                else
                {
                    nbMbs = (nbMbs > 200) ? nbMbs >> 2 : 50;
                }
            }

            while (numPassLeft-- > 0)
            {
                bool isLastPass = (MathF.Abs(stats.Dq) <= DqLimit) || (numPassLeft == 0) || (this.maxI4HeaderBits == 0);
                long sizeP0 = this.OneStatPass(width, height, yStride, uvStride, rdOpt, nbMbs, stats);
                if (sizeP0 == 0)
                {
                    return;
                }

                if (this.maxI4HeaderBits > 0 && sizeP0 > (long)Partition0SizeLimit)
                {
                    ++numPassLeft;
                    this.maxI4HeaderBits >>= 1;  // strengthen header bit limitation...
                    continue;                        // ...and start over
                }

                if (isLastPass)
                {
                    break;
                }

                // If no target size: just do several pass without changing 'q'
                if (doSearch)
                {
                    stats.ComputeNextQ();
                    if (MathF.Abs(stats.Dq) <= DqLimit)
                    {
                        break;
                    }
                }
            }

            if (!doSearch || !stats.DoSizeSearch)
            {
                // Need to finalize probas now, since it wasn't done during the search.
                this.Proba.FinalizeSkipProba(this.Mbw, this.Mbh);
                this.Proba.FinalizeTokenProbas();
            }

            this.Proba.CalculateLevelCosts();  // Finalize costs.
        }

        private long OneStatPass(int width, int height, int yStride, int uvStride, Vp8RdLevel rdOpt, int nbMbs, PassStats stats)
        {
            Span<byte> y = this.Y.GetSpan();
            Span<byte> u = this.U.GetSpan();
            Span<byte> v = this.V.GetSpan();
            var it = new Vp8EncIterator(this.YTop, this.UvTop, this.Nz, this.MbInfo, this.Preds, this.TopDerr, this.Mbw, this.Mbh);
            long size = 0;
            long sizeP0 = 0;
            long distortion = 0;
            long pixelCount = nbMbs * 384;

            this.SetLoopParams(stats.Q);
            do
            {
                var info = new Vp8ModeScore();
                it.Import(y, u, v, yStride, uvStride, width, height, false);
                if (this.Decimate(it, info, rdOpt))
                {
                    // Just record the number of skips and act like skipProba is not used.
                    ++this.Proba.NbSkip;
                }

                this.RecordResiduals(it, info);
                size += info.R + info.H;
                sizeP0 += info.H;
                distortion += info.D;

                it.SaveBoundary();
            }
            while (it.Next());

            sizeP0 += this.SegmentHeader.Size;
            if (stats.DoSizeSearch)
            {
                size += this.Proba.FinalizeSkipProba(this.Mbw, this.Mbh);
                size += this.Proba.FinalizeTokenProbas();
                size = ((size + sizeP0 + 1024) >> 11) + HeaderSizeEstimate;
                stats.Value = size;
            }
            else
            {
                stats.Value = GetPsnr(distortion, pixelCount);
            }

            return sizeP0;
        }

        private void SetLoopParams(float q)
        {
            // Setup segment quantizations and filters.
            this.SetSegmentParams(q);

            // Compute segment probabilities.
            this.SetSegmentProbas();

            this.ResetStats();
        }

        private void AdjustFilterStrength()
        {
            if (FilterStrength > 0)
            {
                int maxLevel = 0;
                for (int s = 0; s < WebpConstants.NumMbSegments; s++)
                {
                    Vp8SegmentInfo dqm = this.SegmentInfos[s];

                    // this '>> 3' accounts for some inverse WHT scaling
                    int delta = (dqm.MaxEdge * dqm.Y2.Q[1]) >> 3;
                    int level = this.FilterStrengthFromDelta(this.FilterHeader.Sharpness, delta);
                    if (level > dqm.FStrength)
                    {
                        dqm.FStrength = level;
                    }

                    if (maxLevel < dqm.FStrength)
                    {
                        maxLevel = dqm.FStrength;
                    }
                }

                this.FilterHeader.FilterLevel = maxLevel;
            }
        }

        private void ResetBoundaryPredictions()
        {
            Span<byte> top = this.Preds.AsSpan(); // original source top starts at: enc->preds_ - enc->preds_w_
            Span<byte> left = this.Preds.AsSpan(this.PredsWidth - 1);
            for (int i = 0; i < 4 * this.Mbw; ++i)
            {
                top[i] = (int)IntraPredictionMode.DcPrediction;
            }

            for (int i = 0; i < 4 * this.Mbh; ++i)
            {
                left[i * this.PredsWidth] = (int)IntraPredictionMode.DcPrediction;
            }

            int predsW = (4 * this.Mbw) + 1;
            int predsH = (4 * this.Mbh) + 1;
            int predsSize = predsW * predsH;
            this.Preds.AsSpan(predsSize + this.PredsWidth - 4, 4).Fill(0);

            this.Nz[0] = 0;   // constant
        }

        // Simplified k-Means, to assign Nb segments based on alpha-histogram.
        private void AssignSegments(int[] alphas)
        {
            int nb = (this.SegmentHeader.NumSegments < NumMbSegments) ? this.SegmentHeader.NumSegments : NumMbSegments;
            int[] centers = new int[NumMbSegments];
            int weightedAverage = 0;
            int[] map = new int[WebpConstants.MaxAlpha + 1];
            int n, k;
            int[] accum = new int[NumMbSegments];
            int[] distAccum = new int[NumMbSegments];

            // Bracket the input.
            for (n = 0; n <= WebpConstants.MaxAlpha && alphas[n] == 0; ++n)
            {
            }

            int minA = n;
            for (n = WebpConstants.MaxAlpha; n > minA && alphas[n] == 0; --n)
            {
            }

            int maxA = n;
            int rangeA = maxA - minA;

            // Spread initial centers evenly.
            for (k = 0, n = 1; k < nb; ++k, n += 2)
            {
                centers[k] = minA + (n * rangeA / (2 * nb));
            }

            for (k = 0; k < MaxItersKMeans; ++k)
            {
                // Reset stats.
                for (n = 0; n < nb; ++n)
                {
                    accum[n] = 0;
                    distAccum[n] = 0;
                }

                // Assign nearest center for each 'a'
                n = 0;    // track the nearest center for current 'a'
                int a;
                for (a = minA; a <= maxA; ++a)
                {
                    if (alphas[a] != 0)
                    {
                        while (n + 1 < nb && Math.Abs(a - centers[n + 1]) < Math.Abs(a - centers[n]))
                        {
                            n++;
                        }

                        map[a] = n;

                        // Accumulate contribution into best centroid.
                        distAccum[n] += a * alphas[a];
                        accum[n] += alphas[a];
                    }
                }

                // All point are classified. Move the centroids to the center of their respective cloud.
                int displaced = 0;
                weightedAverage = 0;
                int totalWeight = 0;
                for (n = 0; n < nb; ++n)
                {
                    if (accum[n] != 0)
                    {
                        int newCenter = (distAccum[n] + (accum[n] / 2)) / accum[n];
                        displaced += Math.Abs(centers[n] - newCenter);
                        centers[n] = newCenter;
                        weightedAverage += newCenter * accum[n];
                        totalWeight += accum[n];
                    }
                }

                weightedAverage = (weightedAverage + (totalWeight / 2)) / totalWeight;
                if (displaced < 5)
                {
                    break;   // no need to keep on looping...
                }
            }

            // Map each original value to the closest centroid
            for (n = 0; n < this.Mbw * this.Mbh; ++n)
            {
                Vp8MacroBlockInfo mb = this.MbInfo[n];
                int alpha = mb.Alpha;
                mb.Segment = map[alpha];
                mb.Alpha = centers[map[alpha]];  // for the record.
            }

            // TODO: add possibility for SmoothSegmentMap
            this.SetSegmentAlphas(centers, weightedAverage);
        }

        private void SetSegmentAlphas(int[] centers, int mid)
        {
            int nb = this.SegmentHeader.NumSegments;
            Vp8SegmentInfo[] dqm = this.SegmentInfos;
            int min = centers[0], max = centers[0];
            int n;

            if (nb > 1)
            {
                for (n = 0; n < nb; ++n)
                {
                    if (min > centers[n])
                    {
                        min = centers[n];
                    }

                    if (max < centers[n])
                    {
                        max = centers[n];
                    }
                }
            }

            if (max == min)
            {
                max = min + 1;
            }

            for (n = 0; n < nb; ++n)
            {
                int alpha = 255 * (centers[n] - mid) / (max - min);
                int beta = 255 * (centers[n] - min) / (max - min);
                dqm[n].Alpha = Clip(alpha, -127, 127);
                dqm[n].Beta = Clip(beta, 0, 255);
            }
        }

        private void SetSegmentParams(float quality)
        {
            int nb = this.SegmentHeader.NumSegments;
            Vp8SegmentInfo[] dqm = this.SegmentInfos;
            int snsStrength = 50; // TODO: Spatial Noise Shaping, hardcoded for now.
            double amp = WebpConstants.SnsToDq * snsStrength / 100.0d / 128.0d;
            double cBase = QualityToCompression(quality / 100.0d);
            for (int i = 0; i < nb; ++i)
            {
                // We modulate the base coefficient to accommodate for the quantization
                // susceptibility and allow denser segments to be quantized more.
                double expn = 1.0d - (amp * dqm[i].Alpha);
                double c = Math.Pow(cBase, expn);
                int q = (int)(127.0d * (1.0d - c));
                dqm[i].Quant = Clip(q, 0, 127);
            }

            // Purely indicative in the bitstream (except for the 1-segment case).
            this.BaseQuant = dqm[0].Quant;

            // uvAlpha is normally spread around ~60. The useful range is
            // typically ~30 (quite bad) to ~100 (ok to decimate UV more).
            // We map it to the safe maximal range of MAX/MIN_DQ_UV for dq_uv.
            this.DqUvAc = (this.uvAlpha - WebpConstants.QuantEncMidAlpha) * (WebpConstants.QuantEncMaxDqUv - WebpConstants.QuantEncMinDqUv) / (WebpConstants.QuantEncMaxAlpha - WebpConstants.QuantEncMinAlpha);

            // We rescale by the user-defined strength of adaptation.
            this.DqUvAc = this.DqUvAc * snsStrength / 100;

            // and make it safe.
            this.DqUvAc = Clip(this.DqUvAc, WebpConstants.QuantEncMinDqUv, WebpConstants.QuantEncMaxDqUv);

            // We also boost the dc-uv-quant a little, based on sns-strength, since
            // U/V channels are quite more reactive to high quants (flat DC-blocks
            // tend to appear, and are unpleasant).
            this.DqUvDc = -4 * snsStrength / 100;
            this.DqUvDc = Clip(this.DqUvDc, -15, 15);   // 4bit-signed max allowed

            this.DqY1Dc = 0;
            this.DqY2Dc = 0;
            this.DqY2Ac = 0;

            // Initialize segments' filtering
            this.SetupFilterStrength();

            this.SetupMatrices(dqm);
        }

        private void SetupFilterStrength()
        {
            int filterSharpness = 0; // TODO: filterSharpness is hardcoded
            var filterType = 1; // TODO: filterType is hardcoded

            // level0 is in [0..500]. Using '-f 50' as filter_strength is mid-filtering.
            int level0 = 5 * FilterStrength;
            for (int i = 0; i < WebpConstants.NumMbSegments; ++i)
            {
                Vp8SegmentInfo m = this.SegmentInfos[i];

                // We focus on the quantization of AC coeffs.
                int qstep = WebpLookupTables.AcTable[Clip(m.Quant, 0, 127)] >> 2;
                int baseStrength = this.FilterStrengthFromDelta(this.FilterHeader.Sharpness, qstep);

                // Segments with lower complexity ('beta') will be less filtered.
                int f = baseStrength * level0 / (256 + m.Beta);
                m.FStrength = (f < WebpConstants.FilterStrengthCutoff) ? 0 : (f > 63) ? 63 : f;
            }

            // We record the initial strength (mainly for the case of 1-segment only).
            this.FilterHeader.FilterLevel = this.SegmentInfos[0].FStrength;
            this.FilterHeader.Simple = filterType == 0;
            this.FilterHeader.Sharpness = filterSharpness;
        }

        private void SetSegmentProbas()
        {
            int[] p = new int[NumMbSegments];
            int n;

            for (n = 0; n < this.Mbw * this.Mbh; ++n)
            {
                Vp8MacroBlockInfo mb = this.MbInfo[n];
                ++p[mb.Segment];
            }

            if (this.SegmentHeader.NumSegments > 1)
            {
                byte[] probas = this.Proba.Segments;
                probas[0] = (byte)GetProba(p[0] + p[1], p[2] + p[3]);
                probas[1] = (byte)GetProba(p[0], p[1]);
                probas[2] = (byte)GetProba(p[2], p[3]);

                this.SegmentHeader.UpdateMap = (probas[0] != 255) || (probas[1] != 255) || (probas[2] != 255);
                if (!this.SegmentHeader.UpdateMap)
                {
                    this.ResetSegments();
                }

                this.SegmentHeader.Size = (p[0] * (LossyUtils.Vp8BitCost(0, probas[0]) + LossyUtils.Vp8BitCost(0, probas[1]))) +
                                          (p[1] * (LossyUtils.Vp8BitCost(0, probas[0]) + LossyUtils.Vp8BitCost(1, probas[1]))) +
                                          (p[2] * (LossyUtils.Vp8BitCost(1, probas[0]) + LossyUtils.Vp8BitCost(0, probas[2]))) +
                                          (p[3] * (LossyUtils.Vp8BitCost(1, probas[0]) + LossyUtils.Vp8BitCost(1, probas[2])));
            }
            else
            {
                this.SegmentHeader.UpdateMap = false;
                this.SegmentHeader.Size = 0;
            }
        }

        private void ResetSegments()
        {
            int n;
            for (n = 0; n < this.Mbw * this.Mbh; ++n)
            {
                this.MbInfo[n].Segment = 0;
            }
        }

        private void ResetStats()
        {
            Vp8EncProba proba = this.Proba;
            proba.CalculateLevelCosts();
            proba.NbSkip = 0;
        }

        private void SetupMatrices(Vp8SegmentInfo[] dqm)
        {
            for (int i = 0; i < dqm.Length; ++i)
            {
                Vp8SegmentInfo m = dqm[i];
                int q = m.Quant;

                m.Y1 = new Vp8Matrix();
                m.Y2 = new Vp8Matrix();
                m.Uv = new Vp8Matrix();

                m.Y1.Q[0] = WebpLookupTables.DcTable[Clip(q, 0, 127)];
                m.Y1.Q[1] = WebpLookupTables.AcTable[Clip(q, 0, 127)];

                m.Y2.Q[0] = (ushort)(WebpLookupTables.DcTable[Clip(q, 0, 127)] * 2);
                m.Y2.Q[1] = WebpLookupTables.AcTable2[Clip(q, 0, 127)];

                m.Uv.Q[0] = WebpLookupTables.DcTable[Clip(q + this.DqUvDc, 0, 117)];
                m.Uv.Q[1] = WebpLookupTables.AcTable[Clip(q + this.DqUvAc, 0, 127)];

                int qi4 = m.Y1.Expand(0);
                m.Y2.Expand(1); // qi16
                m.Uv.Expand(2); // quv

                m.I4Penalty = 1000 * qi4 * qi4;
            }
        }

        private int MacroBlockAnalysis(int width, int height, Vp8EncIterator it, Span<byte> y, Span<byte> u, Span<byte> v, int yStride, int uvStride, int[] alphas, out int uvAlpha)
        {
            int alpha = 0;
            uvAlpha = 0;
            if (!it.IsDone())
            {
                do
                {
                    it.Import(y, u, v, yStride, uvStride, width, height, true);
                    int bestAlpha = this.MbAnalyze(it, alphas, out int bestUvAlpha);

                    // Accumulate for later complexity analysis.
                    alpha += bestAlpha;
                    uvAlpha += bestUvAlpha;
                }
                while (it.Next());
            }

            return alpha;
        }

        private int MbAnalyze(Vp8EncIterator it, int[] alphas, out int bestUvAlpha)
        {
            it.SetIntra16Mode(0);    // default: Intra16, DC_PRED
            it.SetSkip(false);       // not skipped.
            it.SetSegment(0);        // default segment, spec-wise.

            int bestAlpha = this.method <= 1 ? it.FastMbAnalyze(this.quality) : it.MbAnalyzeBestIntra16Mode();

            bestUvAlpha = it.MbAnalyzeBestUvMode();

            // Final susceptibility mix.
            bestAlpha = ((3 * bestAlpha) + bestUvAlpha + 2) >> 2;
            bestAlpha = FinalAlphaValue(bestAlpha);
            alphas[bestAlpha]++;
            it.CurrentMacroBlockInfo.Alpha = bestAlpha;   // For later remapping.

            return bestAlpha; // Mixed susceptibility (not just luma).
        }

        private bool Decimate(Vp8EncIterator it, Vp8ModeScore rd, Vp8RdLevel rdOpt)
        {
            rd.InitScore();

            // We can perform predictions for Luma16x16 and Chroma8x8 already.
            // Luma4x4 predictions needs to be done as-we-go.
            it.MakeLuma16Preds();
            it.MakeChroma8Preds();

            if (rdOpt > Vp8RdLevel.RdOptNone)
            {
                this.PickBestIntra16(it, rd);
                if (this.method >= 2)
                {
                    this.PickBestIntra4(it, rd);
                }

                this.PickBestUv(it, rd);
            }
            else
            {
                // At this point we have heuristically decided intra16 / intra4.
                // For method >= 2, pick the best intra4/intra16 based on SSE (~tad slower).
                // For method <= 1, we don't re-examine the decision but just go ahead with
                // quantization/reconstruction.
                this.RefineUsingDistortion(it, rd, this.method >= 2, this.method >= 1);
            }

            bool isSkipped = rd.Nz == 0;
            it.SetSkip(isSkipped);

            return isSkipped;
        }

        private void PickBestIntra16(Vp8EncIterator it, Vp8ModeScore rd)
        {
            const int numBlocks = 16;
            Vp8SegmentInfo dqm = this.SegmentInfos[it.CurrentMacroBlockInfo.Segment];
            int lambda = dqm.LambdaI16;
            int tlambda = dqm.TLambda;
            Span<byte> src = it.YuvIn.AsSpan(Vp8EncIterator.YOffEnc);
            var rdTmp = new Vp8ModeScore();
            Vp8ModeScore rdCur = rdTmp;
            Vp8ModeScore rdBest = rd;
            int mode;
            bool isFlat = IsFlatSource16(src);
            rd.ModeI16 = -1;
            for (mode = 0; mode < NumPredModes; ++mode)
            {
                // scratch buffer.
                Span<byte> tmpDst = it.YuvOut2.AsSpan(Vp8EncIterator.YOffEnc);
                rdCur.ModeI16 = mode;

                // Reconstruct.
                rdCur.Nz = (uint)this.ReconstructIntra16(it, dqm, rdCur, tmpDst, mode);

                // Measure RD-score.
                rdCur.D = Vp8Sse16X16(src, tmpDst);
                rdCur.SD = tlambda != 0 ? Mult8B(tlambda, Vp8Disto16x16(src, tmpDst, this.weightY)) : 0;
                rdCur.H = WebpConstants.Vp8FixedCostsI16[mode];
                rdCur.R = this.GetCostLuma16(it, rdCur);

                if (isFlat)
                {
                    // Refine the first impression (which was in pixel space).
                    isFlat = IsFlat(rdCur.YAcLevels, numBlocks, WebpConstants.FlatnessLimitI16);
                    if (isFlat)
                    {
                        // Block is very flat. We put emphasis on the distortion being very low!
                        rdCur.D *= 2;
                        rdCur.SD *= 2;
                    }
                }

                // Since we always examine Intra16 first, we can overwrite *rd directly.
                rdCur.SetRdScore(lambda);

                if (mode == 0 || rdCur.Score < rdBest.Score)
                {
                    Vp8ModeScore tmp = rdCur;
                    rdCur = rdBest;
                    rdBest = tmp;
                    it.SwapOut();
                }
            }

            if (rdBest != rd)
            {
                rd = rdBest;
            }

            // Finalize score for mode decision.
            rd.SetRdScore(dqm.LambdaMode);
            it.SetIntra16Mode(rd.ModeI16);

            // We have a blocky macroblock (only DCs are non-zero) with fairly high
            // distortion, record max delta so we can later adjust the minimal filtering
            // strength needed to smooth these blocks out.
            if ((rd.Nz & 0x100ffff) == 0x1000000 && rd.D > dqm.MinDisto)
            {
                dqm.StoreMaxDelta(rd.YDcLevels);
            }
        }

        private bool PickBestIntra4(Vp8EncIterator it, Vp8ModeScore rd)
        {
            Vp8SegmentInfo dqm = this.SegmentInfos[it.CurrentMacroBlockInfo.Segment];
            int lambda = dqm.LambdaI16;
            int tlambda = dqm.TLambda;
            Span<byte> src0 = it.YuvIn.AsSpan(Vp8EncIterator.YOffEnc);
            Span<byte> bestBlocks = it.YuvOut2.AsSpan(Vp8EncIterator.YOffEnc);
            int totalHeaderBits = 0;
            var rdBest = new Vp8ModeScore();
            IMemoryOwner<byte> scratchBuffer = this.memoryAllocator.Allocate<byte>(512);

            if (this.maxI4HeaderBits == 0)
            {
                return false;
            }

            rdBest.InitScore();
            rdBest.H = 211;  // '211' is the value of VP8BitCost(0, 145)
            rdBest.SetRdScore(dqm.LambdaMode);
            it.StartI4();
            do
            {
                int numBlocks = 1;
                var rdi4 = new Vp8ModeScore();
                int mode;
                int bestMode = -1;
                Span<byte> src = src0.Slice(WebpLookupTables.Vp8Scan[it.I4]);
                short[] modeCosts = it.GetCostModeI4(rd.ModesI4);
                Span<byte> bestBlock = bestBlocks.Slice(WebpLookupTables.Vp8Scan[it.I4]);
                Span<byte> tmpDst = scratchBuffer.GetSpan();

                rdi4.InitScore();
                it.MakeIntra4Preds();
                for (mode = 0; mode < NumBModes; ++mode)
                {
                    var rdTmp = new Vp8ModeScore();
                    short[] tmpLevels = new short[16];

                    // Reconstruct.
                    rdTmp.Nz = (uint)this.ReconstructIntra4(it, dqm, tmpLevels, src, tmpDst, mode);

                    // Compute RD-score.
                    rdTmp.D = Vp8Sse4X4(src, tmpDst);
                    rdTmp.SD = tlambda != 0 ? Mult8B(tlambda, Vp8Disto4x4(src, tmpDst, this.weightY)) : 0;
                    rdTmp.H = modeCosts[mode];

                    // Add flatness penalty, to avoid flat area to be mispredicted by a complex mode.
                    if (mode > 0 && IsFlat(tmpLevels, numBlocks, WebpConstants.FlatnessLimitI4))
                    {
                        rdTmp.R = WebpConstants.FlatnessPenality * numBlocks;
                    }
                    else
                    {
                        rdTmp.R = 0;
                    }

                    // early-out check.
                    rdTmp.SetRdScore(lambda);
                    if (bestMode >= 0 && rdTmp.Score >= rdi4.Score)
                    {
                        continue;
                    }

                    // finish computing score.
                    rdTmp.R += it.GetCostLuma4(tmpLevels, this.Proba);
                    rdTmp.SetRdScore(lambda);

                    if (bestMode < 0 || rdTmp.Score < rdi4.Score)
                    {
                        rdi4.CopyScore(rdTmp);
                        bestMode = mode;
                        Span<byte> tmp = tmpDst;
                        tmpDst = bestBlock;
                        bestBlock = tmp;
                        tmpLevels.AsSpan(0, rdBest.YAcLevels[it.I4]).CopyTo(rdBest.YAcLevels);
                    }
                }

                rdi4.SetRdScore(lambda);
                rdBest.AddScore(rdi4);
                if (rdBest.Score >= rd.Score)
                {
                    return false;
                }

                totalHeaderBits += (int)rdi4.H;   // <- equal to modeCosts[bestMode];
                if (totalHeaderBits > this.maxI4HeaderBits)
                {
                    return false;
                }

                // Copy selected samples if not in the right place already.
                if (bestBlock != bestBlocks.Slice(WebpLookupTables.Vp8Scan[it.I4]))
                {
                    Vp8Copy4x4(bestBlock, bestBlocks.Slice(WebpLookupTables.Vp8Scan[it.I4]));
                }

                rd.ModesI4[it.I4] = (byte)bestMode;
                it.TopNz[it.I4 & 3] = it.LeftNz[it.I4 >> 2] = rdi4.Nz != 0 ? 1 : 0;
            }
            while (it.RotateI4(bestBlocks));

            // Finalize state.
            rd.CopyScore(rdBest);
            it.SetIntra4Mode(rd.ModesI4);
            it.SwapOut();
            rdBest.YAcLevels.AsSpan().CopyTo(rd.YAcLevels);

            // Select intra4x4 over intra16x16.
            return true;
        }

        private void PickBestUv(Vp8EncIterator it, Vp8ModeScore rd)
        {

        }

        // TODO: move to Vp8EncIterator
        private int GetCostLuma16(Vp8EncIterator it, Vp8ModeScore rd)
        {
            var res = new Vp8Residual();
            int r = 0;

            // re-import the non-zero context.
            it.NzToBytes();

            // DC
            res.Init(0, 1, this.Proba);
            res.SetCoeffs(rd.YDcLevels);
            r += res.GetResidualCost(it.TopNz[8] + it.LeftNz[8]);

            // AC
            res.Init(1, 0, this.Proba);
            for (int y = 0; y < 4; ++y)
            {
                for (int x = 0; x < 4; ++x)
                {
                    int ctx = it.TopNz[x] + it.LeftNz[y];
                    res.SetCoeffs(rd.YAcLevels.AsSpan(x + (y * 4)));
                    r += res.GetResidualCost(ctx);
                    it.TopNz[x] = it.LeftNz[y] = (res.Last >= 0) ? 1 : 0;
                }
            }

            return r;
        }

        // Refine intra16/intra4 sub-modes based on distortion only (not rate).
        private void RefineUsingDistortion(Vp8EncIterator it, Vp8ModeScore rd, bool tryBothModes, bool refineUvMode)
        {
            long bestScore = Vp8ModeScore.MaxCost;
            int nz = 0;
            int mode;
            bool isI16 = tryBothModes || (it.CurrentMacroBlockInfo.MacroBlockType == Vp8MacroBlockType.I16X16);
            Vp8SegmentInfo dqm = this.SegmentInfos[it.CurrentMacroBlockInfo.Segment];

            // Some empiric constants, of approximate order of magnitude.
            const int lambdaDi16 = 106;
            const int lambdaDi4 = 11;
            const int lambdaDuv = 120;
            long scoreI4 = dqm.I4Penalty;
            long i4BitSum = 0;
            long bitLimit = tryBothModes
                ? this.MbHeaderLimit
                : Vp8ModeScore.MaxCost; // no early-out allowed.

            if (isI16)
            {
                int bestMode = -1;
                Span<byte> src = it.YuvIn.AsSpan(Vp8EncIterator.YOffEnc);
                for (mode = 0; mode < NumPredModes; ++mode)
                {
                    Span<byte> reference = it.YuvP.AsSpan(Vp8Encoding.Vp8I16ModeOffsets[mode]);
                    long score = (Vp8Sse16X16(src, reference) * WebpConstants.RdDistoMult) + (WebpConstants.Vp8FixedCostsI16[mode] * lambdaDi16);

                    if (mode > 0 && WebpConstants.Vp8FixedCostsI16[mode] > bitLimit)
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
                    if (IsFlatSource16(src))
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
                    Span<byte> src = it.YuvIn.AsSpan(Vp8EncIterator.YOffEnc + WebpLookupTables.Vp8Scan[it.I4]);
                    short[] modeCosts = it.GetCostModeI4(rd.ModesI4);

                    it.MakeIntra4Preds();
                    for (mode = 0; mode < NumBModes; ++mode)
                    {
                        Span<byte> reference = it.YuvP.AsSpan(Vp8Encoding.Vp8I4ModeOffsets[mode]);
                        long score = (Vp8Sse4X4(src, reference) * WebpConstants.RdDistoMult) + (modeCosts[mode] * lambdaDi4);
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
                        // Reconstruct partial block inside YuvOut2 buffer
                        Span<byte> tmpDst = it.YuvOut2.AsSpan(Vp8EncIterator.YOffEnc + WebpLookupTables.Vp8Scan[it.I4]);
                        nz |= this.ReconstructIntra4(it, dqm, rd.YAcLevels.AsSpan(it.I4 * 16, 16), src, tmpDst, bestI4Mode) << it.I4;
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
                int intra16Mode = it.Preds[it.PredIdx];
                nz = this.ReconstructIntra16(it, dqm, rd, it.YuvOut.AsSpan(Vp8EncIterator.YOffEnc), intra16Mode);
            }

            // ... and UV!
            if (refineUvMode)
            {
                int bestMode = -1;
                long bestUvScore = Vp8ModeScore.MaxCost;
                Span<byte> src = it.YuvIn.AsSpan(Vp8EncIterator.UOffEnc);
                for (mode = 0; mode < NumPredModes; ++mode)
                {
                    Span<byte> reference = it.YuvP.AsSpan(Vp8Encoding.Vp8UvModeOffsets[mode]);
                    long score = (Vp8Sse16X8(src, reference) * WebpConstants.RdDistoMult) + (WebpConstants.Vp8FixedCostsUv[mode] * lambdaDuv);
                    if (score < bestUvScore)
                    {
                        bestMode = mode;
                        bestUvScore = score;
                    }
                }

                it.SetIntraUvMode(bestMode);
            }

            nz |= this.ReconstructUv(it, dqm, rd, it.YuvOut.AsSpan(Vp8EncIterator.UOffEnc), it.CurrentMacroBlockInfo.UvMode);

            rd.Nz = (uint)nz;
            rd.Score = bestScore;
        }

        private void CodeResiduals(Vp8EncIterator it, Vp8ModeScore rd)
        {
            int x, y, ch;
            var residual = new Vp8Residual();
            bool i16 = it.CurrentMacroBlockInfo.MacroBlockType == Vp8MacroBlockType.I16X16;
            int segment = it.CurrentMacroBlockInfo.Segment;

            it.NzToBytes();

            int pos1 = this.bitWriter.NumBytes();
            if (i16)
            {
                residual.Init(0, 1, this.Proba);
                residual.SetCoeffs(rd.YDcLevels);
                int res = this.bitWriter.PutCoeffs(it.TopNz[8] + it.LeftNz[8], residual);
                it.TopNz[8] = it.LeftNz[8] = res;
                residual.Init(1, 0, this.Proba);
            }
            else
            {
                residual.Init(0, 3, this.Proba);
            }

            // luma-AC
            for (y = 0; y < 4; ++y)
            {
                for (x = 0; x < 4; ++x)
                {
                    int ctx = it.TopNz[x] + it.LeftNz[y];
                    Span<short> coeffs = rd.YAcLevels.AsSpan(16 * (x + (y * 4)), 16);
                    residual.SetCoeffs(coeffs);
                    int res = this.bitWriter.PutCoeffs(ctx, residual);
                    it.TopNz[x] = it.LeftNz[y] = res;
                }
            }

            int pos2 = this.bitWriter.NumBytes();

            // U/V
            residual.Init(0, 2, this.Proba);
            for (ch = 0; ch <= 2; ch += 2)
            {
                for (y = 0; y < 2; ++y)
                {
                    for (x = 0; x < 2; ++x)
                    {
                        int ctx = it.TopNz[4 + ch + x] + it.LeftNz[4 + ch + y];
                        residual.SetCoeffs(rd.UvLevels.AsSpan(16 * ((ch * 2) + x + (y * 2)), 16));
                        var res = this.bitWriter.PutCoeffs(ctx, residual);
                        it.TopNz[4 + ch + x] = it.LeftNz[4 + ch + y] = res;
                    }
                }
            }

            int pos3 = this.bitWriter.NumBytes();
            it.LumaBits = pos2 - pos1;
            it.UvBits = pos3 - pos2;
            it.BitCount[segment, i16 ? 1 : 0] += it.LumaBits;
            it.BitCount[segment, 2] += it.UvBits;
            it.BytesToNz();
        }

        /// <summary>
        /// Same as CodeResiduals, but doesn't actually write anything.
        /// Instead, it just records the event distribution.
        /// </summary>
        private void RecordResiduals(Vp8EncIterator it, Vp8ModeScore rd)
        {
            int x, y, ch;
            var residual = new Vp8Residual();
            bool i16 = it.CurrentMacroBlockInfo.MacroBlockType == Vp8MacroBlockType.I16X16;

            it.NzToBytes();

            if (i16)
            {
                // i16x16
                residual.Init(0, 1, this.Proba);
                residual.SetCoeffs(rd.YDcLevels);
                int res = residual.RecordCoeffs(it.TopNz[8] + it.LeftNz[8]);
                it.TopNz[8] = res;
                it.LeftNz[8] = res;
                residual.Init(1, 0, this.Proba);
            }
            else
            {
                residual.Init(0, 3, this.Proba);
            }

            // luma-AC
            for (y = 0; y < 4; ++y)
            {
                for (x = 0; x < 4; ++x)
                {
                    int ctx = it.TopNz[x] + it.LeftNz[y];
                    Span<short> coeffs = rd.YAcLevels.AsSpan(16 * (x + (y * 4)), 16);
                    residual.SetCoeffs(coeffs);
                    var res = residual.RecordCoeffs(ctx);
                    it.TopNz[x] = res;
                    it.LeftNz[y] = res;
                }
            }

            // U/V
            residual.Init(0, 2, this.Proba);
            for (ch = 0; ch <= 2; ch += 2)
            {
                for (y = 0; y < 2; ++y)
                {
                    for (x = 0; x < 2; ++x)
                    {
                        int ctx = it.TopNz[4 + ch + x] + it.LeftNz[4 + ch + y];
                        residual.SetCoeffs(rd.UvLevels.AsSpan(16 * ((ch * 2) + x + (y * 2)), 16));
                        var res = residual.RecordCoeffs(ctx);
                        it.TopNz[4 + ch + x] = res;
                        it.LeftNz[4 + ch + y] = res;
                    }
                }
            }

            it.BytesToNz();
        }

        private int ReconstructIntra16(Vp8EncIterator it, Vp8SegmentInfo dqm, Vp8ModeScore rd, Span<byte> yuvOut, int mode)
        {
            Span<byte> reference = it.YuvP.AsSpan(Vp8Encoding.Vp8I16ModeOffsets[mode]);
            Span<byte> src = it.YuvIn.AsSpan(Vp8EncIterator.YOffEnc);
            int nz = 0;
            int n;
            short[] dcTmp = new short[16];
            short[] tmp = new short[16 * 16];
            Span<short> tmpSpan = tmp.AsSpan();

            for (n = 0; n < 16; n += 2)
            {
                Vp8Encoding.FTransform2(src.Slice(WebpLookupTables.Vp8Scan[n]), reference.Slice(WebpLookupTables.Vp8Scan[n]), tmpSpan.Slice(n * 16, 16), tmpSpan.Slice((n + 1) * 16, 16));
            }

            Vp8Encoding.FTransformWht(tmp, dcTmp);
            nz |= QuantEnc.QuantizeBlock(dcTmp, rd.YDcLevels, dqm.Y2) << 24;

            for (n = 0; n < 16; n += 2)
            {
                // Zero-out the first coeff, so that: a) nz is correct below, and
                // b) finding 'last' non-zero coeffs in SetResidualCoeffs() is simplified.
                tmp[n * 16] = tmp[(n + 1) * 16] = 0;
                nz |= QuantEnc.Quantize2Blocks(tmpSpan.Slice(n * 16, 32), rd.YAcLevels.AsSpan(n * 16, 32), dqm.Y1) << n;
            }

            // Transform back.
            LossyUtils.TransformWht(dcTmp, tmpSpan);
            for (n = 0; n < 16; n += 2)
            {
                Vp8Encoding.ITransform(reference.Slice(WebpLookupTables.Vp8Scan[n]), tmpSpan.Slice(n * 16, 32), yuvOut.Slice(WebpLookupTables.Vp8Scan[n]), true);
            }

            return nz;
        }

        private int ReconstructIntra4(Vp8EncIterator it, Vp8SegmentInfo dqm, Span<short> levels, Span<byte> src, Span<byte> yuvOut, int mode)
        {
            Span<byte> reference = it.YuvP.AsSpan(Vp8Encoding.Vp8I4ModeOffsets[mode]);
            short[] tmp = new short[16];
            Vp8Encoding.FTransform(src, reference, tmp);
            int nz = QuantEnc.QuantizeBlock(tmp, levels, dqm.Y1);
            Vp8Encoding.ITransform(reference, tmp, yuvOut, false);

            return nz;
        }

        private int ReconstructUv(Vp8EncIterator it, Vp8SegmentInfo dqm, Vp8ModeScore rd, Span<byte> yuvOut, int mode)
        {
            Span<byte> reference = it.YuvP.AsSpan(Vp8Encoding.Vp8UvModeOffsets[mode]);
            Span<byte> src = it.YuvIn.AsSpan(Vp8EncIterator.UOffEnc);
            int nz = 0;
            int n;
            short[] tmp = new short[8 * 16];

            for (n = 0; n < 8; n += 2)
            {
                Vp8Encoding.FTransform2(
                    src.Slice(WebpLookupTables.Vp8ScanUv[n]),
                    reference.Slice(WebpLookupTables.Vp8ScanUv[n]),
                    tmp.AsSpan(n * 16, 16),
                    tmp.AsSpan((n + 1) * 16, 16));
            }

            QuantEnc.CorrectDcValues(it, dqm.Uv, tmp, rd);

            for (n = 0; n < 8; n += 2)
            {
                nz |= QuantEnc.Quantize2Blocks(tmp.AsSpan(n * 16, 32), rd.UvLevels.AsSpan(n * 16, 32), dqm.Uv) << n;
            }

            for (n = 0; n < 8; n += 2)
            {
                Vp8Encoding.ITransform(reference.Slice(WebpLookupTables.Vp8ScanUv[n]), tmp.AsSpan(n * 16, 32), yuvOut.Slice(WebpLookupTables.Vp8ScanUv[n]), true);
            }

            return nz << 16;
        }

        /// <summary>
        /// Converts the RGB values of the image to YUV.
        /// </summary>
        /// <typeparam name="TPixel">The pixel type of the image.</typeparam>
        /// <param name="image">The image to convert.</param>
        private void ConvertRgbToYuv<TPixel>(Image<TPixel> image)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int width = image.Width;
            int height = image.Height;
            int uvWidth = (width + 1) >> 1;

            // Temporary storage for accumulated R/G/B values during conversion to U/V.
            using IMemoryOwner<ushort> tmpRgb = this.memoryAllocator.Allocate<ushort>(4 * uvWidth);
            using IMemoryOwner<Rgba32> rgbaRow0Buffer = this.memoryAllocator.Allocate<Rgba32>(width);
            using IMemoryOwner<Rgba32> rgbaRow1Buffer = this.memoryAllocator.Allocate<Rgba32>(width);
            Span<ushort> tmpRgbSpan = tmpRgb.GetSpan();
            Span<Rgba32> rgbaRow0 = rgbaRow0Buffer.GetSpan();
            Span<Rgba32> rgbaRow1 = rgbaRow1Buffer.GetSpan();
            int uvRowIndex = 0;
            int rowIndex;
            bool rowsHaveAlpha = false;
            for (rowIndex = 0; rowIndex < height - 1; rowIndex += 2)
            {
                Span<TPixel> rowSpan = image.GetPixelRowSpan(rowIndex);
                Span<TPixel> nextRowSpan = image.GetPixelRowSpan(rowIndex + 1);
                PixelOperations<TPixel>.Instance.ToRgba32(this.configuration, rowSpan, rgbaRow0);
                PixelOperations<TPixel>.Instance.ToRgba32(this.configuration, nextRowSpan, rgbaRow1);

                rowsHaveAlpha = YuvConversion.CheckNonOpaque(rgbaRow0) && YuvConversion.CheckNonOpaque(rgbaRow1);

                // Downsample U/V planes, two rows at a time.
                if (!rowsHaveAlpha)
                {
                    YuvConversion.AccumulateRgb(rgbaRow0, rgbaRow1, tmpRgbSpan, width);
                }
                else
                {
                    YuvConversion.AccumulateRgba(rgbaRow0, rgbaRow1, tmpRgbSpan, width);
                }

                YuvConversion.ConvertRgbaToUv(tmpRgbSpan, this.U.Slice(uvRowIndex * uvWidth), this.V.Slice(uvRowIndex * uvWidth), uvWidth);
                uvRowIndex++;

                YuvConversion.ConvertRgbaToY(rgbaRow0, this.Y.Slice(rowIndex * width), width);
                YuvConversion.ConvertRgbaToY(rgbaRow1, this.Y.Slice((rowIndex + 1) * width), width);
            }

            // Extra last row.
            if ((height & 1) != 0)
            {
                if (!rowsHaveAlpha)
                {
                    YuvConversion.AccumulateRgb(rgbaRow0, rgbaRow0, tmpRgbSpan, width);
                }
                else
                {
                    YuvConversion.AccumulateRgba(rgbaRow0, rgbaRow0, tmpRgbSpan, width);
                }

                YuvConversion.ConvertRgbaToY(rgbaRow0, this.Y.Slice(rowIndex * width), width);
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static int FinalAlphaValue(int alpha)
        {
            alpha = WebpConstants.MaxAlpha - alpha;
            return Clip(alpha, 0, WebpConstants.MaxAlpha);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static int Clip(int v, int min, int max) => (v < min) ? min : (v > max) ? max : v;

        [MethodImpl(InliningOptions.ShortMethod)]
        private static int Vp8Sse16X16(Span<byte> a, Span<byte> b) => GetSse(a, b, 16, 16);

        [MethodImpl(InliningOptions.ShortMethod)]
        private static int Vp8Sse16X8(Span<byte> a, Span<byte> b) => GetSse(a, b, 16, 8);

        [MethodImpl(InliningOptions.ShortMethod)]
        private static int Vp8Sse4X4(Span<byte> a, Span<byte> b) => GetSse(a, b, 4, 4);

        [MethodImpl(InliningOptions.ShortMethod)]
        private static int GetSse(Span<byte> a, Span<byte> b, int w, int h)
        {
            int count = 0;
            int aOffset = 0;
            int bOffset = 0;
            for (int y = 0; y < h; ++y)
            {
                for (int x = 0; x < w; ++x)
                {
                    int diff = a[aOffset + x] - b[bOffset + x];
                    count += diff * diff;
                }

                aOffset += WebpConstants.Bps;
                bOffset += WebpConstants.Bps;
            }

            return count;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static void Vp8Copy4x4(Span<byte> src, Span<byte> dst) => Copy(src, dst, 4, 4);

        [MethodImpl(InliningOptions.ShortMethod)]
        private static void Copy(Span<byte> src, Span<byte> dst, int w, int h)
        {
            for (int y = 0; y < h; ++y)
            {
                src.Slice(0, w).CopyTo(dst);
                src = src.Slice(WebpConstants.Bps);
                dst = dst.Slice(WebpConstants.Bps);
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static int Vp8Disto16x16(Span<byte> a, Span<byte> b, Span<ushort> w)
        {
            int D = 0;
            int x, y;
            for (y = 0; y < 16 * WebpConstants.Bps; y += 4 * WebpConstants.Bps)
            {
                for (x = 0; x < 16; x += 4)
                {
                    D += Vp8Disto4x4(a.Slice(x + y), b.Slice(x + y), w);
                }
            }

            return D;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static int Vp8Disto4x4(Span<byte> a, Span<byte> b, Span<ushort> w)
        {
            int sum1 = LossyUtils.TTransform(a, w);
            int sum2 = LossyUtils.TTransform(b, w);
            return Math.Abs(sum2 - sum1) >> 5;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static bool IsFlat(Span<short> levels, int numBlocks, int thresh)
        {
            int score = 0;
            while (numBlocks-- > 0)
            {
                for (int i = 1; i < 16; ++i)
                {
                    // omit DC, we're only interested in AC
                    score += (levels[i] != 0) ? 1 : 0;
                    if (score > thresh)
                    {
                        return false;
                    }
                }

                levels = levels.Slice(16, 16);
            }

            return true;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static bool IsFlatSource16(Span<byte> src)
        {
            uint v = src[0] * 0x01010101u;
            Span<byte> vSpan = BitConverter.GetBytes(v).AsSpan();
            for (int i = 0; i < 16; ++i)
            {
                if (!src.Slice(0, 4).SequenceEqual(vSpan) || !src.Slice(4, 4).SequenceEqual(vSpan) ||
                    !src.Slice(8, 4).SequenceEqual(vSpan) || !src.Slice(12, 4).SequenceEqual(vSpan))
                {
                    return false;
                }

                src = src.Slice(WebpConstants.Bps);
            }

            return true;
        }

        /// <summary>
        /// We want to emulate jpeg-like behaviour where the expected "good" quality
        /// is around q=75. Internally, our "good" middle is around c=50. So we
        /// map accordingly using linear piece-wise function
        /// </summary>
        [MethodImpl(InliningOptions.ShortMethod)]
        private static double QualityToCompression(double c)
        {
            double linearC = (c < 0.75) ? c * (2.0d / 3.0d) : (2.0d * c) - 1.0d;

            // The file size roughly scales as pow(quantizer, 3.). Actually, the
            // exponent is somewhere between 2.8 and 3.2, but we're mostly interested
            // in the mid-quant range. So we scale the compressibility inversely to
            // this power-law: quant ~= compression ^ 1/3. This law holds well for
            // low quant. Finer modeling for high-quant would make use of AcTable[]
            // more explicitly.
            double v = Math.Pow(linearC, 1 / 3.0d);

            return v;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private int FilterStrengthFromDelta(int sharpness, int delta)
        {
            int pos = (delta < WebpConstants.MaxDelzaSize) ? delta : WebpConstants.MaxDelzaSize - 1;
            return WebpLookupTables.LevelsFromDelta[sharpness, pos];
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static int Mult8B(int a, int b) => ((a * b) + 128) >> 8;

        [MethodImpl(InliningOptions.ShortMethod)]
        private static double GetPsnr(long mse, long size) => (mse > 0 && size > 0) ? 10.0f * Math.Log10(255.0f * 255.0f * size / mse) : 99;

        [MethodImpl(InliningOptions.ShortMethod)]
        private static int GetProba(int a, int b)
        {
            int total = a + b;
            return (total == 0) ? 255 // that's the default probability.
                : ((255 * a) + (total / 2)) / total;  // rounded proba
        }
    }
}
