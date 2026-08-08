// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.IO.FrameHeader;
using SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

internal sealed class JxlPassesDecoderState
{
    public JxlPassesDecoderState(JxlFrameHeader header, Configuration configuration)
    {
        this.XDmMultiplier = MathF.Pow(1 / 1.25f, header.XQmScale - 2f);
        this.BDmMultiplier = MathF.Pow(1 / 1.25f, header.BQmScale - 2f);

        this.MainOutput.Callback = PixelCallback;
        this.MainOutput.Buffer = null;

        this.UndoOrientation = JxlOrientation.Identity;
        this.Upsampler8x = GetUpsamplingImage(configuration, this.Shared.CodecMetadata.CustomTransformData, 0, 3);

        if (header.LoopFilter?.EpfIterations > 0)
        {
            this.Sigma = new JxlImageF(
                configuration,
                (this.Shared.FrameDimensions.XSizeBlocks + 2) * SigmaPadding,
                (this.Shared.FrameDimensions.YSizeBlocks + 2) * SigmaPadding);
        }

        this.SharedStorage = new(configuration, header, false);
        this.Shared = this.SharedStorage;
    }

    public JxlPassesSharedState SharedStorage { get; set; }

    public JxlPassesSharedState Shared { get; set; }

    public JxlRenderPipelineStage[] Upsampler8x { get; set; } = [];

    public List<JxlAnsCode> Code { get; set; } = [];

    public List<List<byte>> ContextMap { get; set; } = [];

    public float XDmMultiplier { get; set; }

    public float BDmMultiplier { get; set; }

    public JxlImageF? Sigma { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public JxlImageOutput MainOutput { get; set; }

    public List<JxlImageOutput> ExtraOutput { get; set; } = [];

    public bool FastXybSRgb8Conversion { get; set; }

    public bool UnpremultiplyAlpha { get; set; }

    public JxlOrientation UndoOrientation { get; set; }

    public int VisibleFrameIndex { get; set; }

    public int NonvisibleFrameIndex { get; set; }

    public int UsedAcs { get; set; }

    public JxlDctAcImage<int> Coefficients { get; set; } = [];

    public JxlRenderPipeline RenderPipeline { get; set; }

    public JxlImageBundle FrameStorageForReferencing { get; set; }

    public JxlOutputEncodingInfo OutputEncodingInfo { get; set; } = new();
}
