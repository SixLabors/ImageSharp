// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jxl.Cms;
using SixLabors.ImageSharp.Formats.Jxl.Cms.ToneMapping;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.RenderPipeline;

internal class ToneMappingStage : RenderPipelineStageBase
{
    private readonly JxlRec2408ToneMapper? toneMapper;
    private readonly JxlHlgOotfToneMapper? hlgOotf;
    private readonly JxlOutputEncodingInfo outputEncodingInfo;
    private readonly float toIntensityTarget = 1.0f;
    private readonly float fromDesiredIntensityTarget = 1.0f;

    public ToneMappingStage(Configuration configuration, JxlOutputEncodingInfo outputEncodingInfo)
        : base(configuration)
    {
        this.Settings = default;
        this.outputEncodingInfo = outputEncodingInfo;

        if (outputEncodingInfo.DesiredIntensityTarget == outputEncodingInfo.OriginalIntensityTarget)
        {
            // No tone mapping requested
            return;
        }

        JxlCustomTransferFunction originalTf = outputEncodingInfo.OriginalColorEncoding!.TransferFunction;
        JxlCustomTransferFunction destinationTf = outputEncodingInfo.ColorEncoding!.TransferFunction;

        if (originalTf.IsPq && outputEncodingInfo.DesiredIntensityTarget < outputEncodingInfo.OriginalIntensityTarget)
        {
            this.toneMapper = new(
                sourceRange: CreateRange(0.0f, outputEncodingInfo.OriginalIntensityTarget),
                targetRange: CreateRange(0.0f, outputEncodingInfo.DesiredIntensityTarget),
                outputEncodingInfo.Luminances);
        }
        else if (originalTf.IsHlg && !destinationTf.IsHlg)
        {
            this.hlgOotf = new(
                sourceLuminance: outputEncodingInfo.OriginalIntensityTarget,
                targetLuminance: outputEncodingInfo.DesiredIntensityTarget,
                outputEncodingInfo.Luminances);
        }

        if (destinationTf.IsPq && (this.toneMapper.HasValue || this.hlgOotf.HasValue))
        {
            this.toIntensityTarget = 10000f / outputEncodingInfo.OriginalIntensityTarget;
            this.fromDesiredIntensityTarget = outputEncodingInfo.DesiredIntensityTarget / 10000f;
        }
    }

    public override string Name => "ToneMapping";

    /// <summary>
    /// Gets a value indicating whether there's a Rec2408 or HLG OOTF tone mapper.
    /// </summary>
    public bool IsNeeded => this.toneMapper.HasValue || this.hlgOotf.HasValue;

    public override void ProcessRow(Buffer2D<Memory<float>> inputRows, Buffer2D<Memory<float>> outputRows, int xExtraLeft, int xExtraRight, int width, int xPos, int yPos)
    {
        if (!this.IsNeeded)
        {
            // Tone mapping is unnecessary, so skip this stage.
            return;
        }

        Span<float> row0 = this.GetInputRow(inputRows, 0, 0);
        Span<float> row1 = this.GetInputRow(inputRows, 1, 0);
        Span<float> row2 = this.GetInputRow(inputRows, 2, 0);

        ref float row0Ref = ref MemoryMarshal.GetReference(row0);
        ref float row1Ref = ref MemoryMarshal.GetReference(row1);
        ref float row2Ref = ref MemoryMarshal.GetReference(row2);

        Vector<float> vToIntensityTarget = Vector.Create(this.toIntensityTarget);
        Vector<float> vFromDesiredIntensityTarget = Vector.Create(this.fromDesiredIntensityTarget);

        for (int x = 0; x < width; x += Vector<float>.Count)
        {
            ref float currRow0 = ref Unsafe.Add(ref row0Ref, x);
            ref float currRow1 = ref Unsafe.Add(ref row1Ref, x);
            ref float currRow2 = ref Unsafe.Add(ref row2Ref, x);

            Vector<float> r = Vector.LoadUnsafe(ref currRow0) * vToIntensityTarget;
            Vector<float> g = Vector.LoadUnsafe(ref currRow1) * vToIntensityTarget;
            Vector<float> b = Vector.LoadUnsafe(ref currRow2) * vToIntensityTarget;

            if (this.toneMapper.HasValue)
            {
                this.toneMapper.Value.ToneMap(ref r, ref g, ref b);
            }
            else
            {
                // We can safely assume that an HLG OOTF tone mapper
                // is present, because we already checked for both to
                // exist above (through this.IsNeeded).
                this.hlgOotf!.Value.Apply(ref r, ref g, ref b);
            }

            if (this.toneMapper.HasValue || this.hlgOotf!.Value.WarrantsGamutMapping)
            {
                JxlToneMappingUtils.GamutMap(ref r, ref g, ref b, this.outputEncodingInfo.Luminances);
            }

            r *= vFromDesiredIntensityTarget;
            g *= vFromDesiredIntensityTarget;
            b *= vFromDesiredIntensityTarget;

            r.StoreUnsafe(ref currRow0);
            g.StoreUnsafe(ref currRow1);
            b.StoreUnsafe(ref currRow2);
        }
    }

    public override RenderPipelineChannelMode GetChannelMode(int channel) =>
        channel < 3 ? RenderPipelineChannelMode.InPlace : RenderPipelineChannelMode.Ignored;

    /// <summary>
    /// Simple utility method that constructs a 2-element inline array
    /// for use in the <see cref="toneMapper"/> constructor.
    /// </summary>
    /// <param name="x">First item.</param>
    /// <param name="y">Second item.</param>
    /// <returns>The inline array.</returns>
    private static InlineArray2<float> CreateRange(float x, float y)
    {
        InlineArray2<float> range = default;
        range[0] = x;
        range[1] = y;
        return range;
    }
}
