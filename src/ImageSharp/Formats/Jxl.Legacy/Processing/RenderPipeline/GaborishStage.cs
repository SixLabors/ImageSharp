// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.RenderPipeline;

/// <summary>
/// Applies the normalized Gaborish three-by-three filter to the three color channels.
/// </summary>
internal sealed class GaborishStage : RenderPipelineStageBase
{
    // Each channel stores [center, axial neighbor, diagonal neighbor]. The inline storage contains no sample allocations.
    private InlineArray3<InlineArray3<float>> weights;

    /// <summary>
    /// Prepares the channel weights and declares the one-sample input border.
    /// </summary>
    /// <param name="configuration">The configuration for this stage.</param>
    /// <param name="loopFilter">The decoded per-channel Gaborish weights.</param>
    public GaborishStage(Configuration configuration, JxlLoopFilter loopFilter)
        : base(configuration)
    {
        this.Settings = RenderPipelineStageConfiguration.CreateSymmetricBorderOnly(1);
        this.weights[0][0] = this.weights[1][0] = this.weights[2][0] = 1F;
        this.weights[0][1] = loopFilter.GaborishXWeight1;
        this.weights[0][2] = loopFilter.GaborishXWeight2;
        this.weights[1][1] = loopFilter.GaborishYWeight1;
        this.weights[1][2] = loopFilter.GaborishYWeight2;
        this.weights[2][1] = loopFilter.GaborishBWeight1;
        this.weights[2][2] = loopFilter.GaborishBWeight2;

        for (int c = 0; c < 3; c++)
        {
            // Four axial and four diagonal samples surround the center. Normalize their total weight to one.
            Span<float> channelWeights = this.weights[c];
            float multiplier = 1F / (channelWeights[0] + (4F * (channelWeights[1] + channelWeights[2])));
            channelWeights[0] *= multiplier;
            channelWeights[1] *= multiplier;
            channelWeights[2] *= multiplier;
        }
    }

    /// <inheritdoc />
    public override string Name => "Gab";

    /// <summary>
    /// Filters exactly the requested center range and extra samples, using the pipeline's prepared neighboring rows and halo.
    /// </summary>
    /// <param name="inputRows">The borrowed channel row table, with rows above, at and below the current row.</param>
    /// <param name="outputRows">The distinct output row table.</param>
    /// <param name="xExtraLeft">The requested output samples before the center range.</param>
    /// <param name="xExtraRight">The requested output samples after the center range.</param>
    /// <param name="width">The center range width.</param>
    /// <param name="xPos">The center range's image column. Prepared borders already account for this position.</param>
    /// <param name="yPos">The center range's image row. Prepared borders already account for this position.</param>
    /// <remarks>
    /// The row table places center sample zero at RenderPipelineXOffset. Inputs extend one sample beyond each requested output edge.
    /// The pipeline reserves the left prefix for xExtraLeft plus this input halo and supplies the image-edge extension.
    /// </remarks>
    public override void ProcessRow(
        Buffer2D<Memory<float>> inputRows,
        Buffer2D<Memory<float>> outputRows,
        int xExtraLeft,
        int xExtraRight,
        int width,
        int xPos,
        int yPos)
    {
        int outputLength = xExtraLeft + width + xExtraRight;
        int inputLength = outputLength + 2;

        for (int c = 0; c < 3; c++)
        {
            // Borrow the full requested range plus one input neighbor at each end. The kernel uses only nonnegative span indices.
            // Keep the exact extra widths: vector alignment must not expand the pipeline's logical request or overwrite adjacent outputs.
            ReadOnlySpan<float> top = this.GetInputRow(inputRows, c, -1, xExtraLeft + 1)[..inputLength];
            ReadOnlySpan<float> middle = this.GetInputRow(inputRows, c, 0, xExtraLeft + 1)[..inputLength];
            ReadOnlySpan<float> bottom = this.GetInputRow(inputRows, c, 1, xExtraLeft + 1)[..inputLength];
            Span<float> output = outputRows[c, 0].Span.Slice(RenderPipelineXOffset - xExtraLeft, outputLength);
            ReadOnlySpan<float> channelWeights = this.weights[c];
            JxlConvolve.Symmetric3Row(top, middle, bottom, channelWeights[0], channelWeights[1], channelWeights[2], output);
        }
    }

    /// <inheritdoc />
    public override RenderPipelineChannelMode GetChannelMode(int channel)
        // Horizontal neighbors must retain their input values until the complete output row is written.
        => channel < 3 ? RenderPipelineChannelMode.InOut : RenderPipelineChannelMode.Ignored;
}
