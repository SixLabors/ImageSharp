// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Webp.BitWriter;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
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
        private readonly WebpEncodingMethod method;

        /// <summary>
        /// Number of entropy-analysis passes (in [1..10]).
        /// </summary>
        private readonly int entropyPasses;

        /// <summary>
        /// Specify the strength of the deblocking filter, between 0 (no filtering) and 100 (maximum filtering). A value of 0 will turn off any filtering.
        /// </summary>
        private readonly int filterStrength;

        /// <summary>
        /// The spatial noise shaping. 0=off, 100=maximum.
        /// </summary>
        private readonly int spatialNoiseShaping;

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

        private readonly bool alphaCompression;

        private const int NumMbSegments = 4;

        private const int MaxItersKMeans = 6;

        // Convergence is considered reached if dq < DqLimit
        private const float DqLimit = 0.4f;

        private const ulong Partition0SizeLimit = (WebpConstants.Vp8MaxPartition0Size - 2048UL) << 11;

        private const long HeaderSizeEstimate = WebpConstants.RiffHeaderSize + WebpConstants.ChunkHeaderSize + WebpConstants.Vp8FrameHeaderSize;

        private const int QMin = 0;

        private const int QMax = 100;

        /// <summary>
        /// Initializes a new instance of the <see cref="Vp8Encoder" /> class.
        /// </summary>
        /// <param name="memoryAllocator">The memory allocator.</param>
        /// <param name="configuration">The global configuration.</param>
        /// <param name="width">The width of the input image.</param>
        /// <param name="height">The height of the input image.</param>
        /// <param name="quality">The encoding quality.</param>
        /// <param name="method">Quality/speed trade-off (0=fast, 6=slower-better).</param>
        /// <param name="entropyPasses">Number of entropy-analysis passes (in [1..10]).</param>
        /// <param name="filterStrength">The filter the strength of the deblocking filter, between 0 (no filtering) and 100 (maximum filtering).</param>
        /// <param name="spatialNoiseShaping">The spatial noise shaping. 0=off, 100=maximum.</param>
        /// <param name="alphaCompression">If true, the alpha channel will be compressed with the lossless compression.</param>
        public Vp8Encoder(
            MemoryAllocator memoryAllocator,
            Configuration configuration,
            int width,
            int height,
            int quality,
            WebpEncodingMethod method,
            int entropyPasses,
            int filterStrength,
            int spatialNoiseShaping,
            bool alphaCompression)
        {
            this.memoryAllocator = memoryAllocator;
            this.configuration = configuration;
            this.Width = width;
            this.Height = height;
            this.quality = Numerics.Clamp(quality, 0, 100);
            this.method = method;
            this.entropyPasses = Numerics.Clamp(entropyPasses, 1, 10);
            this.filterStrength = Numerics.Clamp(filterStrength, 0, 100);
            this.spatialNoiseShaping = Numerics.Clamp(spatialNoiseShaping, 0, 100);
            this.alphaCompression = alphaCompression;
            this.rdOptLevel = method is WebpEncodingMethod.BestQuality ? Vp8RdLevel.RdOptTrellisAll
                : method >= WebpEncodingMethod.Level5 ? Vp8RdLevel.RdOptTrellis
                : method >= WebpEncodingMethod.Level3 ? Vp8RdLevel.RdOptBasic
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

        // This uses C#'s optimization to refer to the static data segment of the assembly, no allocation occurs.
        private static ReadOnlySpan<byte> AverageBytesPerMb => new byte[] { 50, 24, 16, 9, 7, 5, 3, 2 };

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
            int pixelCount = width * height;
            Span<byte> y = this.Y.GetSpan();
            Span<byte> u = this.U.GetSpan();
            Span<byte> v = this.V.GetSpan();
            bool hasAlpha = YuvConversion.ConvertRgbToYuv(image, this.configuration, this.memoryAllocator, y, u, v);

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
            int averageBytesPerMacroBlock = AverageBytesPerMb[this.BaseQuant >> 4];
            int expectedSize = this.Mbw * this.Mbh * averageBytesPerMacroBlock;
            this.bitWriter = new Vp8BitWriter(expectedSize, this);

            // Extract and encode alpha channel data, if present.
            int alphaDataSize = 0;
            bool alphaCompressionSucceeded = false;
            using var alphaEncoder = new AlphaEncoder();
            Span<byte> alphaData = Span<byte>.Empty;
            if (hasAlpha)
            {
                // TODO: This can potentially run in an separate task.
                IMemoryOwner<byte> encodedAlphaData = alphaEncoder.EncodeAlpha(image, this.configuration, this.memoryAllocator, this.alphaCompression, out alphaDataSize);
                alphaData = encodedAlphaData.GetSpan();
                if (alphaDataSize < pixelCount)
                {
                    // Only use compressed data, if the compressed data is actually smaller then the uncompressed data.
                    alphaCompressionSucceeded = true;
                }
            }

            // Stats-collection loop.
            this.StatLoop(width, height, yStride, uvStride);
            it.Init();
            it.InitFilter();
            var info = new Vp8ModeScore();
            var residual = new Vp8Residual();
            do
            {
                bool dontUseSkip = !this.Proba.UseSkipProba;
                info.Clear();
                it.Import(y, u, v, yStride, uvStride, width, height, false);

                // Warning! order is important: first call VP8Decimate() and
                // *then* decide how to code the skip decision if there's one.
                if (!this.Decimate(it, ref info, this.rdOptLevel) || dontUseSkip)
                {
                    this.CodeResiduals(it, info, residual);
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
            ImageMetadata metadata = image.Metadata;
            metadata.SyncProfiles();
            this.bitWriter.WriteEncodedImageToStream(
                stream,
                metadata.ExifProfile,
                metadata.XmpProfile,
                metadata.IccProfile,
                (uint)width,
                (uint)height,
                hasAlpha,
                alphaData.Slice(0, alphaDataSize),
                this.alphaCompression && alphaCompressionSucceeded);
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
            bool doSearch = targetSize > 0 || targetPsnr > 0;
            bool fastProbe = (this.method == 0 || this.method == WebpEncodingMethod.Level3) && !doSearch;
            int numPassLeft = this.entropyPasses;
            Vp8RdLevel rdOpt = this.method >= WebpEncodingMethod.Level3 || doSearch ? Vp8RdLevel.RdOptBasic : Vp8RdLevel.RdOptNone;
            int nbMbs = this.Mbw * this.Mbh;

            var stats = new PassStats(targetSize, targetPsnr, QMin, QMax, this.quality);
            this.Proba.ResetTokenStats();

            // Fast mode: quick analysis pass over few mbs. Better than nothing.
            if (fastProbe)
            {
                if (this.method == WebpEncodingMethod.Level3)
                {
                    // We need more stats for method 3 to be reliable.
                    nbMbs = nbMbs > 200 ? nbMbs >> 1 : 100;
                }
                else
                {
                    nbMbs = nbMbs > 200 ? nbMbs >> 2 : 50;
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

            // Finalize costs.
            this.Proba.CalculateLevelCosts();
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

            it.Init();
            this.SetLoopParams(stats.Q);
            var info = new Vp8ModeScore();
            do
            {
                info.Clear();
                it.Import(y, u, v, yStride, uvStride, width, height, false);
                if (this.Decimate(it, ref info, rdOpt))
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
            while (it.Next() && --nbMbs > 0);

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

        private unsafe void AdjustFilterStrength()
        {
            if (this.filterStrength > 0)
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
            for (int i = 0; i < 4 * this.Mbw; i++)
            {
                top[i] = (int)IntraPredictionMode.DcPrediction;
            }

            for (int i = 0; i < 4 * this.Mbh; i++)
            {
                left[i * this.PredsWidth] = (int)IntraPredictionMode.DcPrediction;
            }

            int predsW = (4 * this.Mbw) + 1;
            int predsH = (4 * this.Mbh) + 1;
            int predsSize = predsW * predsH;
            this.Preds.AsSpan(predsSize + this.PredsWidth - 4, 4).Clear();

            this.Nz[0] = 0;   // constant
        }

        // Simplified k-Means, to assign Nb segments based on alpha-histogram.
        private void AssignSegments(int[] alphas)
        {
            int nb = this.SegmentHeader.NumSegments < NumMbSegments ? this.SegmentHeader.NumSegments : NumMbSegments;
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
                mb.Alpha = centers[map[alpha]];
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
                dqm[n].Alpha = Numerics.Clamp(alpha, -127, 127);
                dqm[n].Beta = Numerics.Clamp(beta, 0, 255);
            }
        }

        private void SetSegmentParams(float quality)
        {
            int nb = this.SegmentHeader.NumSegments;
            Vp8SegmentInfo[] dqm = this.SegmentInfos;
            double amp = WebpConstants.SnsToDq * this.spatialNoiseShaping / 100.0d / 128.0d;
            double cBase = QualityToCompression(quality / 100.0d);
            for (int i = 0; i < nb; i++)
            {
                // We modulate the base coefficient to accommodate for the quantization
                // susceptibility and allow denser segments to be quantized more.
                double expn = 1.0d - (amp * dqm[i].Alpha);
                double c = Math.Pow(cBase, expn);
                int q = (int)(127.0d * (1.0d - c));
                dqm[i].Quant = Numerics.Clamp(q, 0, 127);
            }

            // Purely indicative in the bitstream (except for the 1-segment case).
            this.BaseQuant = dqm[0].Quant;

            // uvAlpha is normally spread around ~60. The useful range is
            // typically ~30 (quite bad) to ~100 (ok to decimate UV more).
            // We map it to the safe maximal range of MAX/MIN_DQ_UV for dq_uv.
            this.DqUvAc = (this.uvAlpha - WebpConstants.QuantEncMidAlpha) * (WebpConstants.QuantEncMaxDqUv - WebpConstants.QuantEncMinDqUv) / (WebpConstants.QuantEncMaxAlpha - WebpConstants.QuantEncMinAlpha);

            // We rescale by the user-defined strength of adaptation.
            this.DqUvAc = this.DqUvAc * this.spatialNoiseShaping / 100;

            // and make it safe.
            this.DqUvAc = Numerics.Clamp(this.DqUvAc, WebpConstants.QuantEncMinDqUv, WebpConstants.QuantEncMaxDqUv);

            // We also boost the dc-uv-quant a little, based on sns-strength, since
            // U/V channels are quite more reactive to high quants (flat DC-blocks tend to appear, and are unpleasant).
            this.DqUvDc = -4 * this.spatialNoiseShaping / 100;
            this.DqUvDc = Numerics.Clamp(this.DqUvDc, -15, 15);   // 4bit-signed max allowed.

            this.DqY1Dc = 0;
            this.DqY2Dc = 0;
            this.DqY2Ac = 0;

            // Initialize segments' filtering.
            this.SetupFilterStrength();

            this.SetupMatrices(dqm);
        }

        private void SetupFilterStrength()
        {
            int filterSharpness = 0; // TODO: filterSharpness is hardcoded
            int filterType = 1; // TODO: filterType is hardcoded

            // level0 is in [0..500]. Using '-f 50' as filter_strength is mid-filtering.
            int level0 = 5 * this.filterStrength;
            for (int i = 0; i < WebpConstants.NumMbSegments; i++)
            {
                Vp8SegmentInfo m = this.SegmentInfos[i];

                // We focus on the quantization of AC coeffs.
                int qstep = WebpLookupTables.AcTable[Numerics.Clamp(m.Quant, 0, 127)] >> 2;
                int baseStrength = this.FilterStrengthFromDelta(this.FilterHeader.Sharpness, qstep);

                // Segments with lower complexity ('beta') will be less filtered.
                int f = baseStrength * level0 / (256 + m.Beta);
                m.FStrength = f < WebpConstants.FilterStrengthCutoff ? 0 : f > 63 ? 63 : f;
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

                this.SegmentHeader.UpdateMap = probas[0] != 255 || probas[1] != 255 || probas[2] != 255;
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

        private unsafe void SetupMatrices(Vp8SegmentInfo[] dqm)
        {
            int tlambdaScale = this.method >= WebpEncodingMethod.Default ? this.spatialNoiseShaping : 0;
            for (int i = 0; i < dqm.Length; i++)
            {
                Vp8SegmentInfo m = dqm[i];
                int q = m.Quant;

                m.Y1.Q[0] = WebpLookupTables.DcTable[Numerics.Clamp(q + this.DqY1Dc, 0, 127)];
                m.Y1.Q[1] = WebpLookupTables.AcTable[Numerics.Clamp(q, 0, 127)];

                m.Y2.Q[0] = (ushort)(WebpLookupTables.DcTable[Numerics.Clamp(q + this.DqY2Dc, 0, 127)] * 2);
                m.Y2.Q[1] = WebpLookupTables.AcTable2[Numerics.Clamp(q + this.DqY2Ac, 0, 127)];

                m.Uv.Q[0] = WebpLookupTables.DcTable[Numerics.Clamp(q + this.DqUvDc, 0, 117)];
                m.Uv.Q[1] = WebpLookupTables.AcTable[Numerics.Clamp(q + this.DqUvAc, 0, 127)];

                int qi4 = m.Y1.Expand(0);
                int qi16 = m.Y2.Expand(1);
                int quv = m.Uv.Expand(2);

                m.LambdaI16 = 3 * qi16 * qi16;
                m.LambdaI4 = (3 * qi4 * qi4) >> 7;
                m.LambdaUv = (3 * quv * quv) >> 6;
                m.LambdaMode = (1 * qi4 * qi4) >> 7;
                m.TLambda = (tlambdaScale * qi4) >> 5;

                // none of these constants should be < 1.
                m.LambdaI16 = m.LambdaI16 < 1 ? 1 : m.LambdaI16;
                m.LambdaI4 = m.LambdaI4 < 1 ? 1 : m.LambdaI4;
                m.LambdaUv = m.LambdaUv < 1 ? 1 : m.LambdaUv;
                m.LambdaMode = m.LambdaMode < 1 ? 1 : m.LambdaMode;
                m.TLambda = m.TLambda < 1 ? 1 : m.TLambda;

                m.MinDisto = 20 * m.Y1.Q[0];
                m.MaxEdge = 0;

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

            int bestAlpha;
            if (this.method <= WebpEncodingMethod.Level1)
            {
                bestAlpha = it.FastMbAnalyze(this.quality);
            }
            else
            {
                bestAlpha = it.MbAnalyzeBestIntra16Mode();
                if (this.method >= WebpEncodingMethod.Level5)
                {
                    // We go and make a fast decision for intra4/intra16.
                    // It's usually not a good and definitive pick, but helps seeding the stats about level bit-cost.
                    bestAlpha = it.MbAnalyzeBestIntra4Mode(bestAlpha);
                }
            }

            bestUvAlpha = it.MbAnalyzeBestUvMode();

            // Final susceptibility mix.
            bestAlpha = ((3 * bestAlpha) + bestUvAlpha + 2) >> 2;
            bestAlpha = FinalAlphaValue(bestAlpha);
            alphas[bestAlpha]++;
            it.CurrentMacroBlockInfo.Alpha = bestAlpha;   // For later remapping.

            return bestAlpha; // Mixed susceptibility (not just luma).
        }

        private bool Decimate(Vp8EncIterator it, ref Vp8ModeScore rd, Vp8RdLevel rdOpt)
        {
            rd.InitScore();

            // We can perform predictions for Luma16x16 and Chroma8x8 already.
            // Luma4x4 predictions needs to be done as-we-go.
            it.MakeLuma16Preds();
            it.MakeChroma8Preds();

            if (rdOpt > Vp8RdLevel.RdOptNone)
            {
                QuantEnc.PickBestIntra16(it, ref rd, this.SegmentInfos, this.Proba);
                if (this.method >= WebpEncodingMethod.Level2)
                {
                    QuantEnc.PickBestIntra4(it, ref rd, this.SegmentInfos, this.Proba, this.maxI4HeaderBits);
                }

                QuantEnc.PickBestUv(it, ref rd, this.SegmentInfos, this.Proba);
            }
            else
            {
                // At this point we have heuristically decided intra16 / intra4.
                // For method >= 2, pick the best intra4/intra16 based on SSE (~tad slower).
                // For method <= 1, we don't re-examine the decision but just go ahead with
                // quantization/reconstruction.
                QuantEnc.RefineUsingDistortion(it, this.SegmentInfos, rd, this.method >= WebpEncodingMethod.Level2, this.method >= WebpEncodingMethod.Level1, this.MbHeaderLimit);
            }

            bool isSkipped = rd.Nz == 0;
            it.SetSkip(isSkipped);

            return isSkipped;
        }

        private void CodeResiduals(Vp8EncIterator it, Vp8ModeScore rd, Vp8Residual residual)
        {
            int x, y, ch;
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
            for (y = 0; y < 4; y++)
            {
                for (x = 0; x < 4; x++)
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
                for (y = 0; y < 2; y++)
                {
                    for (x = 0; x < 2; x++)
                    {
                        int ctx = it.TopNz[4 + ch + x] + it.LeftNz[4 + ch + y];
                        residual.SetCoeffs(rd.UvLevels.AsSpan(16 * ((ch * 2) + x + (y * 2)), 16));
                        int res = this.bitWriter.PutCoeffs(ctx, residual);
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
            for (y = 0; y < 4; y++)
            {
                for (x = 0; x < 4; x++)
                {
                    int ctx = it.TopNz[x] + it.LeftNz[y];
                    Span<short> coeffs = rd.YAcLevels.AsSpan(16 * (x + (y * 4)), 16);
                    residual.SetCoeffs(coeffs);
                    int res = residual.RecordCoeffs(ctx);
                    it.TopNz[x] = res;
                    it.LeftNz[y] = res;
                }
            }

            // U/V
            residual.Init(0, 2, this.Proba);
            for (ch = 0; ch <= 2; ch += 2)
            {
                for (y = 0; y < 2; y++)
                {
                    for (x = 0; x < 2; x++)
                    {
                        int ctx = it.TopNz[4 + ch + x] + it.LeftNz[4 + ch + y];
                        residual.SetCoeffs(rd.UvLevels.AsSpan(16 * ((ch * 2) + x + (y * 2)), 16));
                        int res = residual.RecordCoeffs(ctx);
                        it.TopNz[4 + ch + x] = res;
                        it.LeftNz[4 + ch + y] = res;
                    }
                }
            }

            it.BytesToNz();
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static int FinalAlphaValue(int alpha)
        {
            alpha = WebpConstants.MaxAlpha - alpha;
            return Numerics.Clamp(alpha, 0, WebpConstants.MaxAlpha);
        }

        /// <summary>
        /// We want to emulate jpeg-like behaviour where the expected "good" quality
        /// is around q=75. Internally, our "good" middle is around c=50. So we
        /// map accordingly using linear piece-wise function
        /// </summary>
        [MethodImpl(InliningOptions.ShortMethod)]
        private static double QualityToCompression(double c)
        {
            double linearC = c < 0.75 ? c * (2.0d / 3.0d) : (2.0d * c) - 1.0d;

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
            int pos = delta < WebpConstants.MaxDelzaSize ? delta : WebpConstants.MaxDelzaSize - 1;
            return WebpLookupTables.LevelsFromDelta[sharpness, pos];
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static double GetPsnr(long mse, long size) => mse > 0 && size > 0 ? 10.0f * Math.Log10(255.0f * 255.0f * size / mse) : 99;

        [MethodImpl(InliningOptions.ShortMethod)]
        private static int GetProba(int a, int b)
        {
            int total = a + b;
            return total == 0 ? 255 // that's the default probability.
                : ((255 * a) + (total / 2)) / total;  // rounded proba
        }
    }
}
