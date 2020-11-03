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
        /// Stride of the prediction plane (=4*mb_w + 1)
        /// </summary>
        private readonly int predsWidth;

        /// <summary>
        /// Macroblock width.
        /// </summary>
        private readonly int mbw;

        /// <summary>
        /// Macroblock height.
        /// </summary>
        private readonly int mbh;

        /// <summary>
        /// The segment features.
        /// </summary>
        private Vp8EncSegmentHeader segmentHeader;

        /// <summary>
        /// Contextual macroblock infos.
        /// </summary>
        private readonly Vp8MacroBlockInfo[] mbInfo;

        /// <summary>
        /// Probabilities.
        /// </summary>
        private readonly Vp8EncProba proba;

        private readonly Vp8RdLevel rdOptLevel;

        private int dqUvDc;

        private int dqUvAc;

        private int maxI4HeaderBits;

        /// <summary>
        /// Global susceptibility.
        /// </summary>
        private int alpha;

        /// <summary>
        /// U/V quantization susceptibility.
        /// </summary>
        private int uvAlpha;

        /// <summary>
        /// Fixed-point precision for RGB->YUV.
        /// </summary>
        private const int YuvFix = 16;

        private const int YuvHalf = 1 << (YuvFix - 1);

        private const int KC1 = 20091 + (1 << 16);

        private const int KC2 = 35468;

        private const int MaxLevel = 2047;

        private readonly byte[] zigzag = { 0, 1, 4, 8, 5, 2, 3, 6, 9, 12, 13, 10, 7, 11, 14, 15 };

        private readonly byte[] averageBytesPerMb = { 50, 24, 16, 9, 7, 5, 3, 2 };

        private const int NumMbSegments = 4;

        private const int MaxItersKMeans = 6;

        // Convergence is considered reached if dq < DqLimit
        private const float DqLimit = 0.4f;

        private const ulong Partition0SizeLimit = (WebPConstants.Vp8MaxPartition0Size - 2048UL) << 11;

        private const long HeaderSizeEstimate = WebPConstants.RiffHeaderSize + WebPConstants.ChunkHeaderSize + WebPConstants.Vp8FrameHeaderSize;

        private const int QMin = 0;

        private const int QMax = 100;

        /// <summary>
        /// Initializes a new instance of the <see cref="Vp8Encoder"/> class.
        /// </summary>
        /// <param name="memoryAllocator">The memory allocator.</param>
        /// <param name="width">The width of the input image.</param>
        /// <param name="height">The height of the input image.</param>
        /// <param name="quality">The encoding quality.</param>
        /// <param name="method">Quality/speed trade-off (0=fast, 6=slower-better).</param>
        /// <param name="entropyPasses">Number of entropy-analysis passes (in [1..10]).</param>
        public Vp8Encoder(MemoryAllocator memoryAllocator, int width, int height, int quality, int method, int entropyPasses)
        {
            this.memoryAllocator = memoryAllocator;
            this.quality = quality.Clamp(0, 100);
            this.method = method.Clamp(0, 6);
            this.entropyPasses = entropyPasses.Clamp(1, 10);
            this.rdOptLevel = (method >= 6) ? Vp8RdLevel.RdOptTrellisAll
                : (method >= 5) ? Vp8RdLevel.RdOptTrellis
                : (method >= 3) ? Vp8RdLevel.RdOptBasic
                : Vp8RdLevel.RdOptNone;

            var pixelCount = width * height;
            this.mbw = (width + 15) >> 4;
            this.mbh = (height + 15) >> 4;
            var uvSize = ((width + 1) >> 1) * ((height + 1) >> 1);
            this.Y = this.memoryAllocator.Allocate<byte>(pixelCount);
            this.U = this.memoryAllocator.Allocate<byte>(uvSize);
            this.V = this.memoryAllocator.Allocate<byte>(uvSize);
            this.YTop = this.memoryAllocator.Allocate<byte>(this.mbw * 16);
            this.UvTop = this.memoryAllocator.Allocate<byte>(this.mbw * 16 * 2);
            this.Nz = this.memoryAllocator.Allocate<uint>(this.mbw + 1);
            this.MbHeaderLimit = 256 * 510 * 8 * 1024 / (this.mbw * this.mbh);
            int predSize = (((4 * this.mbw) + 1) * ((4 * this.mbh) + 1)) + this.predsWidth + 1;

            // TODO: make partition_limit configurable?
            int limit = 100; // original code: limit = 100 - config->partition_limit;
            this.maxI4HeaderBits =
                256 * 16 * 16 * // upper bound: up to 16bit per 4x4 block
                (limit * limit) / (100 * 100);  // ... modulated with a quadratic curve.

            this.mbInfo = new Vp8MacroBlockInfo[this.mbw * this.mbh];
            for (int i = 0; i < this.mbInfo.Length; i++)
            {
                this.mbInfo[i] = new Vp8MacroBlockInfo();
            }

            this.proba = new Vp8EncProba();

            // this.Preds = this.memoryAllocator.Allocate<byte>(predSize);
            this.Preds = this.memoryAllocator.Allocate<byte>(predSize * 2); // TODO: figure out how much mem we need here. This is too much.
            this.predsWidth = (4 * this.mbw) + 1;

            this.ResetBoundaryPredictions();

            // Initialize the bitwriter.
            var baseQuant = 36; // TODO: hardCoded for now.
            int averageBytesPerMacroBlock = this.averageBytesPerMb[baseQuant >> 4];
            int expectedSize = this.mbw * this.mbh * averageBytesPerMacroBlock;
            this.bitWriter = new Vp8BitWriter(expectedSize);
        }

        /// <summary>
        /// Gets or sets the global susceptibility.
        /// </summary>
        public int Alpha { get; set; }

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
            var segmentInfos = new Vp8SegmentInfo[4];
            for (int i = 0; i < 4; i++)
            {
                segmentInfos[i] = new Vp8SegmentInfo();
            }

            var it = new Vp8EncIterator(this.YTop, this.UvTop, this.Preds, this.Nz, this.mbInfo, this.mbw, this.mbh);
            var alphas = new int[WebPConstants.MaxAlpha + 1];
            this.alpha = this.MacroBlockAnalysis(width, height, it, y, u, v, yStride, uvStride, alphas, out this.uvAlpha);

            // Analysis is done, proceed to actual encoding.
            this.segmentHeader = new Vp8EncSegmentHeader(4);
            this.AssignSegments(segmentInfos, alphas);
            this.SetLoopParams(segmentInfos, this.quality);

            // TODO: EncodeAlpha();
            // Stats-collection loop.
            this.StatLoop(width, height, yStride, uvStride, segmentInfos);
            it.Init();
            it.InitFilter();
            do
            {
                var info = new Vp8ModeScore();
                it.Import(y, u, v, yStride, uvStride, width, height);
                if (!this.Decimate(it, segmentInfos, info, this.rdOptLevel))
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

            // Write bytes from the bitwriter buffer to the stream.
            this.bitWriter.WriteEncodedImageToStream(lossy: true, stream);
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

        /// <summary>
        /// Only collect statistics(number of skips, token usage, ...).
        /// This is used for deciding optimal probabilities. It also modifies the
        /// quantizer value if some target (size, PSNR) was specified.
        /// </summary>
        private void StatLoop(int width, int height, int yStride, int uvStride, Vp8SegmentInfo[] segmentInfos)
        {
            int targetSize = 0; // TODO: target size is hardcoded.
            float targetPsnr = 0.0f; // TDOO: targetPsnr is hardcoded.
            int method = this.method;
            bool doSearch = false; // TODO: doSearch hardcoded for now.
            bool fastProbe = (method == 0 || method == 3) && !doSearch;
            int numPassLeft = this.entropyPasses;
            Vp8RdLevel rdOpt = (method >= 3 || doSearch) ? Vp8RdLevel.RdOptBasic : Vp8RdLevel.RdOptNone;
            int nbMbs = this.mbw * this.mbh;

            var stats = new PassStats(targetSize, targetPsnr, QMin, QMax, this.quality);
            this.proba.ResetTokenStats();

            // Fast mode: quick analysis pass over few mbs. Better than nothing.
            if (fastProbe)
            {
                if (method == 3)
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
                var sizeP0 = this.OneStatPass(width, height, yStride, uvStride, rdOpt, nbMbs, stats, segmentInfos);
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
                this.proba.FinalizeSkipProba(this.mbw, this.mbh);
                this.proba.FinalizeTokenProbas();
            }

            this.proba.CalculateLevelCosts();  // finalize costs
        }

        private long OneStatPass(int width, int height, int yStride, int uvStride, Vp8RdLevel rdOpt, int nbMbs, PassStats stats, Vp8SegmentInfo[] segmentInfos)
        {
            Span<byte> y = this.Y.GetSpan();
            Span<byte> u = this.U.GetSpan();
            Span<byte> v = this.V.GetSpan();
            var it = new Vp8EncIterator(this.YTop, this.UvTop, this.Preds, this.Nz, this.mbInfo, this.mbw, this.mbh);
            long size = 0;
            long sizeP0 = 0;
            long distortion = 0;
            long pixelCount = nbMbs * 384;

            this.SetLoopParams(segmentInfos, stats.Q);
            do
            {
                var info = new Vp8ModeScore();
                it.Import(y, u, v, yStride, uvStride, width, height);
                if (this.Decimate(it, segmentInfos, info, rdOpt))
                {
                    // Just record the number of skips and act like skipProba is not used.
                    ++this.proba.NbSkip;
                }

                this.RecordResiduals(it, info);
                size += info.R + info.H;
                sizeP0 += info.H;
                distortion += info.D;

                it.SaveBoundary();
            }
            while (it.Next());

            sizeP0 += this.segmentHeader.Size;
            if (stats.DoSizeSearch)
            {
                size += this.proba.FinalizeSkipProba(this.mbw, this.mbh);
                size += this.proba.FinalizeTokenProbas();
                size = ((size + sizeP0 + 1024) >> 11) + HeaderSizeEstimate;
                stats.Value = size;
            }
            else
            {
                stats.Value = GetPsnr(distortion, pixelCount);
            }

            return sizeP0;
        }

        private void SetLoopParams(Vp8SegmentInfo[] dqm, float q)
        {
            // Setup segment quantizations and filters.
            this.SetSegmentParams(dqm, q);

            // Compute segment probabilities.
            this.SetSegmentProbas();

            this.ResetStats();
        }

        private void ResetBoundaryPredictions()
        {
            Span<byte> top = this.Preds.GetSpan();
            Span<byte> left = this.Preds.Slice(this.predsWidth - 1);
            for (int i = 0; i < 4 * this.mbw; ++i)
            {
                top[i] = (int)IntraPredictionMode.DcPrediction;
            }

            for (int i = 0; i < 4 * this.mbh; ++i)
            {
                left[i * this.predsWidth] = (int)IntraPredictionMode.DcPrediction;
            }

            // TODO: enc->nz_[-1] = 0;   // constant
        }

        // Simplified k-Means, to assign Nb segments based on alpha-histogram.
        private void AssignSegments(Vp8SegmentInfo[] dqm, int[] alphas)
        {
            int nb = (this.segmentHeader.NumSegments < NumMbSegments) ? this.segmentHeader.NumSegments : NumMbSegments;
            var centers = new int[NumMbSegments];
            int weightedAverage = 0;
            var map = new int[WebPConstants.MaxAlpha + 1];
            int a, n, k;
            int minA;
            int maxA;
            int rangeA;
            var accum = new int[NumMbSegments];
            var distAccum = new int[NumMbSegments];

            // Bracket the input.
            for (n = 0; n <= WebPConstants.MaxAlpha && alphas[n] == 0; ++n)
            {
            }

            minA = n;
            for (n = WebPConstants.MaxAlpha; n > minA && alphas[n] == 0; --n)
            {
            }

            maxA = n;
            rangeA = maxA - minA;

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
                var displaced = 0;
                weightedAverage = 0;
                var totalWeight = 0;
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
            for (n = 0; n < this.mbw * this.mbh; ++n)
            {
                Vp8MacroBlockInfo mb = this.mbInfo[n];
                int alpha = mb.Alpha;
                mb.Segment = map[alpha];
                mb.Alpha = centers[map[alpha]];  // for the record.
            }

            // TODO: add possibility for SmoothSegmentMap
            this.SetSegmentAlphas(dqm, centers, weightedAverage);
        }

        private void SetSegmentAlphas(Vp8SegmentInfo[] dqm, int[] centers, int mid)
        {
            int nb = this.segmentHeader.NumSegments;
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

        private void SetSegmentParams(Vp8SegmentInfo[] dqm, float quality)
        {
            int nb = this.segmentHeader.NumSegments;
            int snsStrength = 50; // TODO: Spatial Noise Shaping, hardcoded for now.
            double amp = WebPConstants.SnsToDq * snsStrength / 100.0d / 128.0d;
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

            // uvAlpha is normally spread around ~60. The useful range is
            // typically ~30 (quite bad) to ~100 (ok to decimate UV more).
            // We map it to the safe maximal range of MAX/MIN_DQ_UV for dq_uv.
            this.dqUvAc = (this.uvAlpha - WebPConstants.QuantEncMidAlpha) * (WebPConstants.QuantEncMaxDqUv - WebPConstants.QuantEncMinDqUv) / (WebPConstants.QuantEncMaxAlpha - WebPConstants.QuantEncMinAlpha);

            // We rescale by the user-defined strength of adaptation.
            this.dqUvAc = this.dqUvAc * snsStrength / 100;

            // and make it safe.
            this.dqUvAc = Clip(this.dqUvAc, WebPConstants.QuantEncMinDqUv, WebPConstants.QuantEncMaxDqUv);

            // We also boost the dc-uv-quant a little, based on sns-strength, since
            // U/V channels are quite more reactive to high quants (flat DC-blocks
            // tend to appear, and are unpleasant).
            this.dqUvDc = -4 * snsStrength / 100;
            this.dqUvDc = Clip(this.dqUvDc, -15, 15);   // 4bit-signed max allowed

            this.SetupMatrices(dqm);
        }

        private void SetSegmentProbas()
        {
            var p = new int[NumMbSegments];
            int n;

            for (n = 0; n < this.mbw * this.mbh; ++n)
            {
                Vp8MacroBlockInfo mb = this.mbInfo[n];
                ++p[mb.Segment];
            }

            if (this.segmentHeader.NumSegments > 1)
            {
                byte[] probas = this.proba.Segments;
                probas[0] = (byte)GetProba(p[0] + p[1], p[2] + p[3]);
                probas[1] = (byte)GetProba(p[0], p[1]);
                probas[2] = (byte)GetProba(p[2], p[3]);

                this.segmentHeader.UpdateMap = (probas[0] != 255) || (probas[1] != 255) || (probas[2] != 255);
                if (!this.segmentHeader.UpdateMap)
                {
                   this.ResetSegments();
                }

                this.segmentHeader.Size = (p[0] * (LossyUtils.Vp8BitCost(0, probas[0]) + LossyUtils.Vp8BitCost(0, probas[1]))) +
                                          (p[1] * (LossyUtils.Vp8BitCost(0, probas[0]) + LossyUtils.Vp8BitCost(1, probas[1]))) +
                                          (p[2] * (LossyUtils.Vp8BitCost(1, probas[0]) + LossyUtils.Vp8BitCost(0, probas[2]))) +
                                          (p[3] * (LossyUtils.Vp8BitCost(1, probas[0]) + LossyUtils.Vp8BitCost(1, probas[2])));
            }
            else
            {
                this.segmentHeader.UpdateMap = false;
                this.segmentHeader.Size = 0;
            }
        }

        private void ResetSegments()
        {
            int n;
            for (n = 0; n < this.mbw * this.mbh; ++n)
            {
                this.mbInfo[n].Segment = 0;
            }
        }

        private void ResetStats()
        {
            Vp8EncProba proba = this.proba;
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

                m.Y1.Q[0] = WebPLookupTables.DcTable[Clip(q, 0, 127)];
                m.Y1.Q[1] = WebPLookupTables.AcTable[Clip(q, 0, 127)];

                m.Y2.Q[0] = (ushort)(WebPLookupTables.DcTable[Clip(q, 0, 127)] * 2);
                m.Y2.Q[1] = WebPLookupTables.AcTable2[Clip(q, 0, 127)];

                m.Uv.Q[0] = WebPLookupTables.DcTable[Clip(q + this.dqUvDc, 0, 117)];
                m.Uv.Q[1] = WebPLookupTables.AcTable[Clip(q + this.dqUvAc, 0, 127)];

                var qi4 = m.Y1.Expand(0);
                var qi16 = m.Y2.Expand(1);
                var quv = m.Uv.Expand(2);

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
                    it.Import(y, u, v, yStride, uvStride, width, height);
                    int bestAlpha = this.MbAnalyze(it, alphas, out var bestUvAlpha);

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

            int bestAlpha;
            if (this.method <= 1)
            {
                bestAlpha = it.FastMbAnalyze(this.quality);
            }
            else
            {
                bestAlpha = it.MbAnalyzeBestIntra16Mode();
            }

            bestUvAlpha = it.MbAnalyzeBestUvMode();

            // Final susceptibility mix.
            bestAlpha = ((3 * bestAlpha) + bestUvAlpha + 2) >> 2;
            bestAlpha = FinalAlphaValue(bestAlpha);
            alphas[bestAlpha]++;
            it.CurrentMacroBlockInfo.Alpha = bestAlpha;   // For later remapping.

            return bestAlpha; // Mixed susceptibility (not just luma).
        }

        private bool Decimate(Vp8EncIterator it, Vp8SegmentInfo[] segmentInfos, Vp8ModeScore rd, Vp8RdLevel rdOpt)
        {
            rd.InitScore();

            it.MakeLuma16Preds();
            it.MakeChroma8Preds();

            // TODO: add support for Rate-distortion optimization levels
            // At this point we have heuristically decided intra16 / intra4.
            // For method >= 2, pick the best intra4/intra16 based on SSE (~tad slower).
            // For method <= 1, we don't re-examine the decision but just go ahead with
            // quantization/reconstruction.
            this.RefineUsingDistortion(it, segmentInfos, rd, this.method >= 2, this.method >= 1);

            bool isSkipped = rd.Nz == 0;
            it.SetSkip(isSkipped);

            return isSkipped;
        }

        // Refine intra16/intra4 sub-modes based on distortion only (not rate).
        private void RefineUsingDistortion(Vp8EncIterator it, Vp8SegmentInfo[] segmentInfos, Vp8ModeScore rd, bool tryBothModes, bool refineUvMode)
        {
            long bestScore = Vp8ModeScore.MaxCost;
            int nz = 0;
            int mode;
            bool isI16 = tryBothModes || (it.CurrentMacroBlockInfo.MacroBlockType == Vp8MacroBlockType.I16X16);
            Vp8SegmentInfo dqm = segmentInfos[it.CurrentMacroBlockInfo.Segment];

            // Some empiric constants, of approximate order of magnitude.
            int lambdaDi16 = 106;
            int lambdaDi4 = 11;
            int lambdaDuv = 120;
            long scoreI4 = dqm.I4Penalty;
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
                    long score = (Vp8Sse16X16(src, reference) * WebPConstants.RdDistoMult) + (WebPConstants.Vp8FixedCostsI16[mode] * lambdaDi16);

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
                    Span<byte> src = it.YuvIn.AsSpan(Vp8EncIterator.YOffEnc + WebPLookupTables.Vp8Scan[it.I4]);
                    short[] modeCosts = it.GetCostModeI4(rd.ModesI4);

                    it.MakeIntra4Preds();
                    for (mode = 0; mode < numBModes; ++mode)
                    {
                        Span<byte> reference = it.YuvP.AsSpan(Vp8EncIterator.Vp8I4ModeOffsets[mode]);
                        long score = (Vp8Sse4X4(src, reference) * WebPConstants.RdDistoMult) + (modeCosts[mode] * lambdaDi4);
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
                        nz |= this.ReconstructIntra4(it, dqm, rd.YAcLevels.AsSpan(it.I4, 16), src, tmpDst, bestI4Mode) << it.I4;
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
                nz = this.ReconstructIntra16(it, dqm, rd, it.YuvOut.AsSpan(Vp8EncIterator.YOffEnc), it.Preds.Slice(it.PredIdx)[0]);
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
                    long score = (Vp8Sse16X8(src, reference) * WebPConstants.RdDistoMult) + (WebPConstants.Vp8FixedCostsUv[mode] * lambdaDuv);
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
                residual.Init(0, 1, this.proba);
                residual.SetCoeffs(rd.YDcLevels);
                int res = this.bitWriter.PutCoeffs(it.TopNz[8] + it.LeftNz[8], residual);
                it.TopNz[8] = it.LeftNz[8] = res;
                residual.Init(1, 0, this.proba);
            }
            else
            {
                residual.Init(0, 3, this.proba);
            }

            // luma-AC
            for (y = 0; y < 4; ++y)
            {
                for (x = 0; x < 4; ++x)
                {
                    int ctx = it.TopNz[x] + it.LeftNz[y];
                    residual.SetCoeffs(rd.YAcLevels.AsSpan(16 * (x + (y * 4)), 16));
                    int res = this.bitWriter.PutCoeffs(ctx, residual);
                    it.TopNz[x] = it.LeftNz[y] = res;
                }
            }

            int pos2 = this.bitWriter.NumBytes();

            // U/V
            residual.Init(0, 2, this.proba);
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
            int segment = it.CurrentMacroBlockInfo.Segment;

            it.NzToBytes();

            if (i16)
            {
                // i16x16
                residual.Init(0, 1, this.proba);
                residual.SetCoeffs(rd.YDcLevels);
                var res = residual.RecordCoeffs(it.TopNz[8] + it.LeftNz[8]);
                it.TopNz[8] = res;
                it.LeftNz[8] = res;
                residual.Init(1, 0, this.proba);
            }
            else
            {
                residual.Init(0, 3, this.proba);
            }

            // luma-AC
            for (y = 0; y < 4; ++y)
            {
                for (x = 0; x < 4; ++x)
                {
                    int ctx = it.TopNz[x] + it.LeftNz[y];
                    residual.SetCoeffs(rd.YAcLevels.AsSpan(16 * (x + (y * 4)), 16));
                    var res = residual.RecordCoeffs(ctx);
                    it.TopNz[x] = res;
                    it.LeftNz[y] = res;
                }
            }

            // U/V
            residual.Init(0, 2, this.proba);
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
            Span<byte> reference = it.YuvP.AsSpan(Vp8EncIterator.Vp8I16ModeOffsets[mode]);
            Span<byte> src = it.YuvIn.AsSpan(Vp8EncIterator.YOffEnc);
            int nz = 0;
            int n;
            var dcTmp = new short[16];
            var tmp = new short[16 * 16];
            Span<short> tmpSpan = tmp.AsSpan();

            for (n = 0; n < 16; n += 2)
            {
                this.FTransform2(src.Slice(WebPLookupTables.Vp8Scan[n]), reference.Slice(WebPLookupTables.Vp8Scan[n]), tmpSpan.Slice(n * 16, 16), tmpSpan.Slice((n + 1) * 16, 16));
            }

            this.FTransformWht(tmp.AsSpan(0), dcTmp);
            nz |= this.QuantizeBlock(dcTmp, rd.YDcLevels, dqm.Y2) << 24;

            for (n = 0; n < 16; n += 2)
            {
                // Zero-out the first coeff, so that: a) nz is correct below, and
                // b) finding 'last' non-zero coeffs in SetResidualCoeffs() is simplified.
                tmp[n * 16] = tmp[(n + 1) * 16] = 0;
                nz |= this.Quantize2Blocks(tmpSpan.Slice(n * 16), rd.YAcLevels.AsSpan(n * 16, 32), dqm.Y1) << n;
            }

            // Transform back.
            LossyUtils.TransformWht(dcTmp, tmpSpan);
            for (n = 0; n < 16; n += 2)
            {
                this.ITransform(reference.Slice(WebPLookupTables.Vp8Scan[n]), tmpSpan.Slice(n * 16, 32), yuvOut.Slice(WebPLookupTables.Vp8Scan[n]), true);
            }

            return nz;
        }

        private int ReconstructIntra4(Vp8EncIterator it, Vp8SegmentInfo dqm, Span<short> levels, Span<byte> src, Span<byte> yuvOut, int mode)
        {
            Span<byte> reference = it.YuvP.AsSpan(Vp8EncIterator.Vp8I4ModeOffsets[mode]);
            var tmp = new short[16];
            this.FTransform(src, reference, tmp);
            var nz = this.QuantizeBlock(tmp, levels, dqm.Y1);
            this.ITransform(reference, tmp, yuvOut, false);

            return nz;
        }

        private int ReconstructUv(Vp8EncIterator it, Vp8SegmentInfo dqm, Vp8ModeScore rd, Span<byte> yuvOut, int mode)
        {
            Span<byte> reference = it.YuvP.AsSpan(Vp8EncIterator.Vp8UvModeOffsets[mode]);
            Span<byte> src = it.YuvIn.AsSpan(Vp8EncIterator.UOffEnc);
            int nz = 0;
            int n;
            var tmp = new short[8 * 16];

            for (n = 0; n < 8; n += 2)
            {
                this.FTransform2(
                    src.Slice(WebPLookupTables.Vp8ScanUv[n]),
                    reference.Slice(WebPLookupTables.Vp8ScanUv[n]),
                    tmp.AsSpan(n * 16, 16),
                    tmp.AsSpan((n + 1) * 16, 16));
            }

            /* TODO:
             if (it->top_derr_ != NULL)
            {
                CorrectDCValues(it, &dqm->uv_, tmp, rd);
            }*/

            for (n = 0; n < 8; n += 2)
            {
                nz |= this.Quantize2Blocks(tmp.AsSpan(n * 16, 32), rd.UvLevels.AsSpan(n * 16, 32), dqm.Uv) << n;
            }

            for (n = 0; n < 8; n += 2)
            {
                this.ITransform(reference.Slice(WebPLookupTables.Vp8ScanUv[n]), tmp.AsSpan(n * 16, 32), yuvOut.Slice(WebPLookupTables.Vp8ScanUv[n]), true);
            }

            return nz << 16;
        }

        private void FTransform2(Span<byte> src, Span<byte> reference, Span<short> output, Span<short> output2)
        {
            this.FTransform(src, reference, output);
            this.FTransform(src.Slice(4), reference.Slice(4), output2);
        }

        private void FTransform(Span<byte> src, Span<byte> reference, Span<short> output)
        {
            int i;
            var tmp = new int[16];
            int srcIdx = 0;
            int refIdx = 0;
            for (i = 0; i < 4; ++i)
            {
                int d0 = src[srcIdx] - reference[refIdx];   // 9bit dynamic range ([-255,255])
                int d1 = src[srcIdx + 1] - reference[refIdx + 1];
                int d2 = src[srcIdx + 2] - reference[refIdx + 2];
                int d3 = src[srcIdx + 3] - reference[refIdx + 3];
                int a0 = d0 + d3;         // 10b                      [-510,510]
                int a1 = d1 + d2;
                int a2 = d1 - d2;
                int a3 = d0 - d3;
                tmp[0 + (i * 4)] = (a0 + a1) * 8;   // 14b                      [-8160,8160]
                tmp[1 + (i * 4)] = ((a2 * 2217) + (a3 * 5352) + 1812) >> 9;      // [-7536,7542]
                tmp[2 + (i * 4)] = (a0 - a1) * 8;
                tmp[3 + (i * 4)] = ((a3 * 2217) - (a2 * 5352) + 937) >> 9;

                srcIdx += WebPConstants.Bps;
                refIdx += WebPConstants.Bps;
            }

            for (i = 0; i < 4; ++i)
            {
                int a0 = tmp[0 + i] + tmp[12 + i];  // 15b
                int a1 = tmp[4 + i] + tmp[8 + i];
                int a2 = tmp[4 + i] - tmp[8 + i];
                int a3 = tmp[0 + i] - tmp[12 + i];
                output[0 + i] = (short)((a0 + a1 + 7) >> 4);            // 12b
                output[4 + i] = (short)((((a2 * 2217) + (a3 * 5352) + 12000) >> 16) + (a3 != 0 ? 1 : 0));
                output[8 + i] = (short)((a0 - a1 + 7) >> 4);
                output[12 + i] = (short)(((a3 * 2217) - (a2 * 5352) + 51000) >> 16);
            }
        }

        private void FTransformWht(Span<short> input, Span<short> output)
        {
            var tmp = new int[16];
            int i;
            int inputIdx = 0;
            for (i = 0; i < 4; ++i)
            {
                int a0 = input[inputIdx + (0 * 16)] + input[inputIdx + (2 * 16)];  // 13b
                int a1 = input[inputIdx + (1 * 16)] + input[inputIdx + (3 * 16)];
                int a2 = input[inputIdx + (1 * 16)] - input[inputIdx + (3 * 16)];
                int a3 = input[inputIdx + (0 * 16)] - input[inputIdx + (2 * 16)];
                tmp[0 + (i * 4)] = a0 + a1;   // 14b
                tmp[1 + (i * 4)] = a3 + a2;
                tmp[2 + (i * 4)] = a3 - a2;
                tmp[3 + (i * 4)] = a0 - a1;

                inputIdx += 64;
            }

            for (i = 0; i < 4; ++i)
            {
                int a0 = tmp[0 + i] + tmp[8 + i];  // 15b
                int a1 = tmp[4 + i] + tmp[12 + i];
                int a2 = tmp[4 + i] - tmp[12 + i];
                int a3 = tmp[0 + i] - tmp[8 + i];
                int b0 = a0 + a1;    // 16b
                int b1 = a3 + a2;
                int b2 = a3 - a2;
                int b3 = a0 - a1;
                output[0 + i] = (short)(b0 >> 1);     // 15b
                output[4 + i] = (short)(b1 >> 1);
                output[8 + i] = (short)(b2 >> 1);
                output[12 + i] = (short)(b3 >> 1);
            }
        }

        private int Quantize2Blocks(Span<short> input, Span<short> output, Vp8Matrix mtx)
        {
            int nz;
            nz = this.QuantizeBlock(input, output, mtx) << 0;
            nz |= this.QuantizeBlock(input.Slice(1 * 16), output.Slice(1 * 16), mtx) << 1;
            return nz;
        }

        private int QuantizeBlock(Span<short> input, Span<short> output, Vp8Matrix mtx)
        {
            int last = -1;
            int n;
            for (n = 0; n < 16; ++n)
            {
                int j = this.zigzag[n];
                bool sign = input[j] < 0;
                uint coeff = (uint)((sign ? -input[j] : input[j]) + mtx.Sharpen[j]);
                if (coeff > mtx.ZThresh[j])
                {
                    uint q = mtx.Q[j];
                    uint iQ = mtx.IQ[j];
                    uint b = mtx.Bias[j];
                    int level = QuantDiv(coeff, iQ, b);
                    if (level > MaxLevel)
                    {
                        level = MaxLevel;
                    }

                    if (sign)
                    {
                        level = -level;
                    }

                    input[j] = (short)(level * (int)q);
                    output[n] = (short)level;
                    if (level != 0)
                    {
                        last = n;
                    }
                }
                else
                {
                    output[n] = 0;
                    input[j] = 0;
                }
            }

            return (last >= 0) ? 1 : 0;
        }

        private void ITransform(Span<byte> reference, Span<short> input, Span<byte> dst, bool doTwo)
        {
            this.ITransformOne(reference, input, dst);
            if (doTwo)
            {
                this.ITransformOne(reference.Slice(4), input.Slice(16), dst.Slice(4));
            }
        }

        private void ITransformOne(Span<byte> reference, Span<short> input, Span<byte> dst)
        {
            int i;
#pragma warning disable SA1312 // Variable names should begin with lower-case letter
            var C = new int[4 * 4];
#pragma warning restore SA1312 // Variable names should begin with lower-case letter
            Span<int> tmp = C.AsSpan();
            for (i = 0; i < 4; ++i)
            {
                // vertical pass.
                int a = input[0] + input[8];
                int b = input[0] - input[8];
                int c = Mul(input[4], KC2) - Mul(input[12], KC1);
                int d = Mul(input[4], KC1) + Mul(input[12], KC2);
                tmp[0] = a + d;
                tmp[1] = b + c;
                tmp[2] = b - c;
                tmp[3] = a - d;
                tmp = tmp.Slice(4);
                input = input.Slice(1);
            }

            tmp = C.AsSpan();
            for (i = 0; i < 4; ++i)
            {
                // horizontal pass.
                int dc = tmp[0] + 4;
                int a = dc + tmp[8];
                int b = dc - tmp[8];
                int c = Mul(tmp[4], KC2) - Mul(tmp[12], KC1);
                int d = Mul(tmp[4], KC1) + Mul(tmp[12], KC2);
                Store(dst, reference, 0, i, (byte)(a + d));
                Store(dst, reference, 1, i, (byte)(b + c));
                Store(dst, reference, 2, i, (byte)(b - c));
                Store(dst, reference, 3, i, (byte)(a - d));
                tmp = tmp.Slice(1);
            }
        }

        private void ConvertRgbToYuv<TPixel>(Image<TPixel> image)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int uvWidth = (image.Width + 1) >> 1;
            bool hasAlpha = this.CheckNonOpaque(image);

            // Temporary storage for accumulated R/G/B values during conversion to U/V.
            using IMemoryOwner<ushort> tmpRgb = this.memoryAllocator.Allocate<ushort>(4 * uvWidth);
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
                y[x] = (byte)RgbToY(rgba.R, rgba.G, rgba.B, YuvHalf);
            }
        }

        private void ConvertRgbaToUv(Span<ushort> rgb, Span<byte> u, Span<byte> v, int width)
        {
            int rgbIdx = 0;
            for (int i = 0; i < width; i += 1, rgbIdx += 4)
            {
                int r = rgb[rgbIdx], g = rgb[rgbIdx + 1], b = rgb[rgbIdx + 2];
                u[i] = (byte)RgbToU(r, g, b, YuvHalf << 2);
                v[i] = (byte)RgbToV(r, g, b, YuvHalf << 2);
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

                dst[dstIdx] = (ushort)LinearToGamma(
                    GammaToLinear(rgba0.R) +
                            GammaToLinear(rgba1.R) +
                            GammaToLinear(rgba2.R) +
                            GammaToLinear(rgba3.R), 0);
                dst[dstIdx + 1] = (ushort)LinearToGamma(
                    GammaToLinear(rgba0.G) +
                            GammaToLinear(rgba1.G) +
                            GammaToLinear(rgba2.G) +
                            GammaToLinear(rgba3.G), 0);
                dst[dstIdx + 2] = (ushort)LinearToGamma(
                    GammaToLinear(rgba0.B) +
                            GammaToLinear(rgba1.B) +
                            GammaToLinear(rgba2.B) +
                            GammaToLinear(rgba3.B), 0);
            }

            if ((width & 1) != 0)
            {
                TPixel color = rowSpan[j];
                color.ToRgba32(ref rgba0);
                color = nextRowSpan[j];
                color.ToRgba32(ref rgba1);

                dst[dstIdx] = (ushort)LinearToGamma(GammaToLinear(rgba0.R) + GammaToLinear(rgba1.R), 1);
                dst[dstIdx + 1] = (ushort)LinearToGamma(GammaToLinear(rgba0.G) + GammaToLinear(rgba1.G), 1);
                dst[dstIdx + 2] = (ushort)LinearToGamma(GammaToLinear(rgba0.B) + GammaToLinear(rgba1.B), 1);
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
                    r = (ushort)LinearToGamma(
                        GammaToLinear(rgba0.R) +
                        GammaToLinear(rgba1.R) +
                        GammaToLinear(rgba2.R) +
                        GammaToLinear(rgba3.R), 0);
                    g = (ushort)LinearToGamma(
                        GammaToLinear(rgba0.G) +
                        GammaToLinear(rgba1.G) +
                        GammaToLinear(rgba2.G) +
                        GammaToLinear(rgba3.G), 0);
                    b = (ushort)LinearToGamma(
                        GammaToLinear(rgba0.B) +
                        GammaToLinear(rgba1.B) +
                        GammaToLinear(rgba2.B) +
                        GammaToLinear(rgba3.B), 0);
                }
                else
                {
                    r = LinearToGammaWeighted(rgba0.R, rgba1.R, rgba2.R, rgba3.R, rgba0.A, rgba1.A, rgba2.A, rgba3.A, a);
                    g = LinearToGammaWeighted(rgba0.G, rgba1.G, rgba2.G, rgba3.G, rgba0.A, rgba1.A, rgba2.A, rgba3.A, a);
                    b = LinearToGammaWeighted(rgba0.B, rgba1.B, rgba2.B, rgba3.B, rgba0.A, rgba1.A, rgba2.A, rgba3.A, a);
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
                    r = (ushort)LinearToGamma(GammaToLinear(rgba0.R) + GammaToLinear(rgba1.R), 1);
                    g = (ushort)LinearToGamma(GammaToLinear(rgba0.G) + GammaToLinear(rgba1.G), 1);
                    b = (ushort)LinearToGamma(GammaToLinear(rgba0.B) + GammaToLinear(rgba1.B), 1);
                }
                else
                {
                    r = LinearToGammaWeighted(rgba0.R, rgba1.R, rgba2.R, rgba3.R, rgba0.A, rgba1.A, rgba2.A, rgba3.A, a);
                    g = LinearToGammaWeighted(rgba0.G, rgba1.G, rgba2.G, rgba3.G, rgba0.A, rgba1.A, rgba2.A, rgba3.A, a);
                    b = LinearToGammaWeighted(rgba0.B, rgba1.B, rgba2.B, rgba3.B, rgba0.A, rgba1.A, rgba2.A, rgba3.A, a);
                }

                dst[dstIdx] = (ushort)r;
                dst[dstIdx + 1] = (ushort)g;
                dst[dstIdx + 2] = (ushort)b;
                dst[dstIdx + 3] = (ushort)a;
            }
        }

        private static int LinearToGammaWeighted(byte rgb0, byte rgb1, byte rgb2, byte rgb3, byte a0, byte a1, byte a2, byte a3, uint totalA)
        {
            uint sum = (a0 * GammaToLinear(rgb0)) + (a1 * GammaToLinear(rgb1)) + (a2 * GammaToLinear(rgb2)) + (a3 * GammaToLinear(rgb3));
            return LinearToGamma((sum * WebPLookupTables.InvAlpha[totalA]) >> (WebPConstants.AlphaFix - 2), 0);
        }

        // Convert a linear value 'v' to YUV_FIX+2 fixed-point precision
        // U/V value, suitable for RGBToU/V calls.
        [MethodImpl(InliningOptions.ShortMethod)]
        private static int LinearToGamma(uint baseValue, int shift)
        {
            int y = Interpolate((int)(baseValue << shift));   // Final uplifted value.
            return (y + WebPConstants.GammaTabRounder) >> WebPConstants.GammaTabFix;    // Descale.
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static uint GammaToLinear(byte v)
        {
            return WebPLookupTables.GammaToLinearTab[v];
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static int Interpolate(int v)
        {
            int tabPos = v >> (WebPConstants.GammaTabFix + 2);    // integer part.
            int x = v & ((WebPConstants.GammaTabScale << 2) - 1);  // fractional part.
            int v0 = WebPLookupTables.LinearToGammaTab[tabPos];
            int v1 = WebPLookupTables.LinearToGammaTab[tabPos + 1];
            int y = (v1 * x) + (v0 * ((WebPConstants.GammaTabScale << 2) - x));   // interpolate

            return y;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static int RgbToY(byte r, byte g, byte b, int rounding)
        {
            int luma = (16839 * r) + (33059 * g) + (6420 * b);
            return (luma + rounding + (16 << YuvFix)) >> YuvFix;  // No need to clip.
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static int RgbToU(int r, int g, int b, int rounding)
        {
            int u = (-9719 * r) - (19081 * g) + (28800 * b);
            return ClipUv(u, rounding);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static int RgbToV(int r, int g, int b, int rounding)
        {
            int v = (+28800 * r) - (24116 * g) - (4684 * b);
            return ClipUv(v, rounding);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static int ClipUv(int uv, int rounding)
        {
            uv = (uv + rounding + (128 << (YuvFix + 2))) >> (YuvFix + 2);
            return ((uv & ~0xff) == 0) ? uv : (uv < 0) ? 0 : 255;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static int FinalAlphaValue(int alpha)
        {
            alpha = WebPConstants.MaxAlpha - alpha;
            return Clip(alpha, 0, WebPConstants.MaxAlpha);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static int Clip(int v, int min, int max)
        {
            return (v < min) ? min : (v > max) ? max : v;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static int Vp8Sse16X16(Span<byte> a, Span<byte> b)
        {
            return GetSse(a, b, 16, 16);
        }

        private static int Vp8Sse16X8(Span<byte> a, Span<byte> b)
        {
            return GetSse(a, b, 16, 8);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static int Vp8Sse4X4(Span<byte> a, Span<byte> b)
        {
            return GetSse(a, b, 4, 4);
        }

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

                aOffset += WebPConstants.Bps;
                bOffset += WebPConstants.Bps;
            }

            return count;
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

                src = src.Slice(WebPConstants.Bps);
            }

            return true;
        }

        /// <summary>
        /// We want to emulate jpeg-like behaviour where the expected "good" quality
        /// is around q=75. Internally, our "good" middle is around c=50. So we
        /// map accordingly using linear piece-wise function
        /// </summary>
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
        private static double GetPsnr(long mse, long size)
        {
            return (mse > 0 && size > 0) ? 10.0f * Math.Log10(255.0f * 255.0f * size / mse) : 99;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static int QuantDiv(uint n, uint iQ, uint b)
        {
            return (int)(((n * iQ) + b) >> WebPConstants.QFix);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static void Store(Span<byte> dst, Span<byte> reference, int x, int y, byte v)
        {
            dst[x + (y * WebPConstants.Bps)] = LossyUtils.Clip8B(reference[x + (y * WebPConstants.Bps)] + (v >> 3));
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static int Mul(int a, int b)
        {
            return (a * b) >> 16;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static int GetProba(int a, int b)
        {
            int total = a + b;
            return (total == 0) ? 255 // that's the default probability.
                : ((255 * a) + (total / 2)) / total;  // rounded proba
        }
    }
}
