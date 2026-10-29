// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jxl.IO.FrameHeader;
using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Processing.AcStrategy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Dct;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Ans;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;
using SixLabors.ImageSharp.Formats.Jxl.Processing.RenderPipeline;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

/// <summary>
/// Owns decoder workspace and borrows the shared state used for one codestream or encoder roundtrip.
/// </summary>
internal sealed class JxlPassesDecoderState : IDisposable
{
    /// <summary>
    /// Initializes empty decoder workspace without allocating frame-sized sample buffers.
    /// </summary>
    /// <param name="shared">The shared state, which its encoder or decoder caller owns and keeps alive.</param>
    public JxlPassesDecoderState(JxlPassesSharedState shared)
    {
        this.Shared = shared;
        this.Coefficients = new(shared.Configuration, 0, 0);
        this.FrameStorageForReferencing = new(shared.CodecMetadata.ImageMetadata);
        this.OutputEncodingInfo.SetFromMetadata(shared.CodecMetadata);

        // Custom upsampling weights belong to the codestream header. Build the DC stage only after Shared is bound to those weights.
        this.Upsampler8x = new UpsamplingStage(shared.Configuration, shared.CodecMetadata.CustomTransformData, 0, 3);
    }

    /// <summary>
    /// Gets the borrowed shared state. Its owner, rather than this workspace, releases its frame and reference storage.
    /// </summary>
    public JxlPassesSharedState Shared { get; }

    /// <summary>
    /// Gets the owned DC upsampling stage built from the codestream's custom filter weights.
    /// </summary>
    public RenderPipelineStageBase Upsampler8x { get; }

    /// <summary>
    /// Gets the entropy code for each pass of the current frame.
    /// </summary>
    public List<JxlAnsCode> Code { get; } = [];

    /// <summary>
    /// Gets the entropy context maps for the current frame's passes.
    /// </summary>
    public List<List<byte>> ContextMap { get; } = [];

    /// <summary>
    /// Gets the X-channel dequantization-matrix multiplier for the current frame.
    /// </summary>
    public float XDmMultiplier { get; private set; }

    /// <summary>
    /// Gets the B-channel dequantization-matrix multiplier for the current frame.
    /// </summary>
    public float BDmMultiplier { get; private set; }

    /// <summary>
    /// Gets the owned edge-preserving filter sigma plane, or null when that filter is disabled.
    /// </summary>
    public JxlPlane<float>? Sigma { get; private set; }

    /// <summary>
    /// Gets or sets the visible-frame index used to seed decoded noise.
    /// </summary>
    public int VisibleFrameIndex { get; set; }

    /// <summary>
    /// Gets or sets the nonvisible-frame index used to seed decoded noise.
    /// </summary>
    public int NonvisibleFrameIndex { get; set; }

    /// <summary>
    /// Gets or sets the transform-strategy bit mask used by this frame.
    /// </summary>
    public int UsedAcs { get; set; }

    /// <summary>
    /// Gets or sets the owned accumulation buffer. Empty storage selects direct coefficient decoding.
    /// </summary>
    public JxlDctAcImage<int> Coefficients { get; set; }

    /// <summary>
    /// Gets or sets the frame's render pipeline, whose implementation is part of renderer integration.
    /// </summary>
    public JxlRenderPipeline RenderPipeline { get; set; }

    /// <summary>
    /// Gets the owned frame storage that can be transferred or copied into a shared reference slot after rendering.
    /// </summary>
    public JxlImageBundle FrameStorageForReferencing { get; }

    /// <summary>
    /// Gets color-conversion information initialized from the codestream's metadata.
    /// </summary>
    public JxlOutputEncodingInfo OutputEncodingInfo { get; } = new();

    /// <summary>
    /// Initializes decoder-only frame workspace after the shared frame controls are available.
    /// </summary>
    /// <param name="header">The parsed header for the current frame.</param>
    /// <remarks>Encoder roundtrips call this after preparing their own shared controls. This operation does not modify those controls.</remarks>
    public void InitializeFrame(JxlFrameHeader header)
    {
        this.XDmMultiplier = MathF.Pow(1F / 1.25F, header.XQmScale - 2F);
        this.BDmMultiplier = MathF.Pow(1F / 1.25F, header.BQmScale - 2F);
        this.UsedAcs = 0;
        this.Code.Clear();
        this.ContextMap.Clear();
        this.FrameStorageForReferencing.RemoveColor();
        this.FrameStorageForReferencing.ClearExtraChannels();

        if (header.LoopFilter?.EpfIterations > 0)
        {
            // Sigma has one sample per frame block, with two padding columns or rows on EACH edge.
            // Add both borders to the block extent; multiplying the extent by the padding would change the sampling grid.
            int width = this.Shared.FrameDimensions.XSizeBlocks + (2 * JxlDecoderCache.SigmaPadding);
            int height = this.Shared.FrameDimensions.YSizeBlocks + (2 * JxlDecoderCache.SigmaPadding);
            if (this.Sigma is null || !this.Sigma.ShrinkTo(width, height))
            {
                JxlPlane<float> sigma = new(this.Shared.Configuration, width, height);
                this.Sigma?.Dispose();
                this.Sigma = sigma;
            }
        }
        else
        {
            this.Sigma?.Dispose();
            this.Sigma = null;
        }
    }

    /// <summary>
    /// Reserves only the decoded coefficient orders needed by this frame's selected transform strategies.
    /// </summary>
    /// <param name="numPasses">The number of progressive passes in this frame.</param>
    /// <remarks>This is a bitstream-decoder operation. Encoder roundtrips already supply their coefficients in memory.</remarks>
    public void InitializeForAc(int numPasses)
    {
        this.Shared.CoeffOrderSize = 0;
        for (int strategy = 0; strategy < JxlAcStrategy.NumberOfValidStrategies; strategy++)
        {
            if (((1 << strategy) & this.UsedAcs) == 0)
            {
                continue;
            }

            int order = JxlCoefficientOrder.StrategyOrder[strategy];
            this.Shared.CoeffOrderSize = Math.Max(
                JxlCoefficientOrder.CoefficientOrderOffsets[3 * (order + 1)] * JxlFrameDimensions.DctBlockSize,
                this.Shared.CoeffOrderSize);
        }

        int length = numPasses * this.Shared.CoeffOrderSize;
        if (length > this.Shared.CoeffOrders.Count)
        {
            int previousLength = this.Shared.CoeffOrders.Count;
            CollectionsMarshal.SetCount(this.Shared.CoeffOrders, length);
            CollectionsMarshal.AsSpan(this.Shared.CoeffOrders)[previousLength..].Clear();
        }
    }

    /// <summary>
    /// Releases decoder-owned workspace without disposing the borrowed shared state or its reference images.
    /// </summary>
    public void Dispose()
    {
        this.Sigma?.Dispose();
        this.Sigma = null;
        this.Upsampler8x.Dispose();
        this.Coefficients.Dispose();
        this.FrameStorageForReferencing.Dispose();
    }
}
