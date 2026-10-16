// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.IO.FrameHeader;
using SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;
using SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;
using SixLabors.ImageSharp.Formats.Jxl.Processing.AcStrategy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Dct;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Ans;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;
using SixLabors.ImageSharp.Formats.Jxl.Processing.RenderPipeline;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

internal sealed class JxlPassesDecoderState
{
    public JxlPassesDecoderState(JxlFrameHeader header, Configuration configuration)
    {
        this.XDmMultiplier = MathF.Pow(1 / 1.25f, header.XQmScale - 2f);
        this.BDmMultiplier = MathF.Pow(1 / 1.25f, header.BQmScale - 2f);

        this.MainOutput.Callback = PixelCallback;
        this.MainOutput.Buffer = null;

        this.UndoOrientation = JxlExifOrientation.Identity;
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

    public RenderPipelineStageBase Upsampler8x { get; set; } = new();

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

    public JxlExifOrientation UndoOrientation { get; set; }

    public int VisibleFrameIndex { get; set; }

    public int NonvisibleFrameIndex { get; set; }

    public int UsedAcs { get; set; }

    public JxlDctAcImage<int> Coefficients { get; set; } = [];

    public JxlRenderPipeline RenderPipeline { get; set; }

    public JxlImageBundle FrameStorageForReferencing { get; set; }

    public JxlOutputEncodingInfo OutputEncodingInfo { get; set; } = new();

    public void InitializeForAc(int numPasses)
    {
        this.SharedStorage.CoeffOrderSize = 0;

        for (int o = 0; o < JxlAcStrategy.NumberOfValidStrategies; o++)
        {
            if (((1 << o) & this.UsedAcs) == 0)
            {
                continue;
            }

            int order = JxlCoefficientOrder.StrategyOrder[o];
            this.SharedStorage.CoeffOrderSize = Math.Max(
                JxlCoefficientOrder.CoefficientOrderOffsets[3 * (order + 1)] * JxlFrameDimensions.DctBlockSize,
                this.SharedStorage.CoeffOrderSize);
        }

        int sz = numPasses * this.SharedStorage.CoeffOrderSize;
        if (sz > this.SharedStorage.CoeffOrders.Count)
        {
            this.SharedStorage.CoeffOrders.Resize(sz);
        }
    }
}
