// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.VarDct;

internal sealed partial class JxlVarDctDecoder
{
    /// <summary>
    /// The negative sigma floor from native epf.cc ComputeSigma.
    /// The reciprocal uses min(-1e-4, scaledSigma), so its denominator cannot approach zero from below.
    /// </summary>
    private const float MinimumSigmaMagnitude = -1e-4F;

    /// <summary>
    /// Expands chroma and applies shared restoration to this frame's reconstructed float planes.
    /// </summary>
    /// <param name="frame">The coding dimensions and parsed restoration parameters.</param>
    /// <param name="allocator">The tracked allocator for the reusable row window and sigma metadata.</param>
    /// <param name="cancellationToken">The token checked between rows and filter passes.</param>
    public void ApplyRestoration(JxlFrameHeader frame, MemoryAllocator allocator, CancellationToken cancellationToken)
    {
        Size size = frame.CodingSize;
        for (int component = 0; component < 3; component++)
        {
            JxlUpsampler.ExpandChroma(
                this.reconstructedChannels[component],
                this.reconstructedSizes[component],
                size,
                this.horizontalShifts[component],
                this.verticalShifts[component],
                cancellationToken);

            this.reconstructedSizes[component] = size;
        }

        VarDctSigmaSource sigma = new(this, frame.RestorationFilter);
        JxlRestoration.Apply(this.reconstructedChannels, size, frame.RestorationFilter, sigma, allocator, cancellationToken);
    }

    /// <summary>
    /// Borrows the prepared restoration workspace for sequential group-row output.
    /// </summary>
    /// <param name="frame">The coding dimensions and parsed restoration parameters.</param>
    /// <returns>The row processor. Its borrowed storage remains owned by this decoder.</returns>
    public JxlRestoration.RowProcessor<VarDctSigmaSource> CreateRestorationRows(JxlFrameHeader frame)
        => new(frame.CodingSize, frame.RestorationFilter, new VarDctSigmaSource(this, frame.RestorationFilter), this.restorationMemory.Span);

    /// <summary>
    /// Prepares one row of the native inverse-sigma field without allocating a full block-grid image.
    /// </summary>
    /// <param name="filter">The quantization multiplier and sharpness table.</param>
    /// <param name="row">The block row.</param>
    /// <param name="quantizationSigma">The transform-origin sigma term retained for each covered column.</param>
    /// <param name="sigma">The output inverse-sigma row.</param>
    private void PrepareSigmaRow(JxlRestorationFilter filter, int row, Span<float> quantizationSigma, Span<float> sigma)
    {
        ReadOnlySpan<byte> strategies = this.Strategies.DangerousGetRowSpan(row);
        ReadOnlySpan<int> origins = this.Quantization.DangerousGetRowSpan(row);
        ReadOnlySpan<byte> sharpness = this.Sharpness.DangerousGetRowSpan(row);
        ReadOnlySpan<float> factors = filter.Sharpness;
        float quantizationScale = this.GlobalScale * (1F / GlobalScaleDenominator);
        for (int x = 0; x < this.dcSize.Width; x++)
        {
            byte strategy = strategies[x];
            if ((strategy & FirstBlockFlag) != 0)
            {
                float term = filter.QuantizationMultiplier / (quantizationScale * origins[x] * JxlRestoration.InverseSigmaNumerator);
                quantizationSigma.Slice(x, JxlCoefficientOrder.StrategyWidths[strategy >> 1]).Fill(term);
            }

            // A continuation keeps the quantizer of its transform origin, while sharpness belongs to each block.
            // The top block row starts at transform origins, so every column is initialized before it is read.
            float value = quantizationSigma[x] * factors[sharpness[x]];

            // The native minimum magnitude keeps this reciprocal finite. Zero sharpness produces a skip-level inverse sigma.
            sigma[x] = 1F / MathF.Min(MinimumSigmaMagnitude, value);
        }
    }

    /// <summary>
    /// Borrows this decoder's validated block metadata for sigma preparation without copying its tables.
    /// </summary>
    public readonly struct VarDctSigmaSource : JxlRestoration.ISigmaSource
    {
        private readonly JxlVarDctDecoder decoder;
        private readonly JxlRestorationFilter filter;

        /// <summary>
        /// Initializes a new instance of the <see cref="VarDctSigmaSource"/> struct for the synchronous restoration call.
        /// </summary>
        /// <param name="decoder">The live owner of the completed block metadata.</param>
        /// <param name="filter">The current frame's parsed restoration parameters.</param>
        public VarDctSigmaSource(JxlVarDctDecoder decoder, JxlRestorationFilter filter)
        {
            this.decoder = decoder;
            this.filter = filter;
        }

        /// <inheritdoc />
        public static bool IsConstant => false;

        /// <inheritdoc />
        public int BlockColumns => this.decoder.dcSize.Width;

        /// <inheritdoc />
        public int RetainedLength => this.decoder.dcSize.Width;

        /// <inheritdoc />
        public void PrepareRow(int row, Span<float> retained, Span<float> sigma) => this.decoder.PrepareSigmaRow(this.filter, row, retained, sigma);
    }
}
