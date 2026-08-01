// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics;
using SixLabors.ImageSharp.Formats.Jxl.Cms;
using SixLabors.ImageSharp.Formats.Jxl.Fields;

namespace SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;

internal sealed class JxlImageMetadata : IJxlFields
{
    public bool AllDefault { get; set; }

    public JxlBitDepth? BitDepth { get; set; }

    public bool Modular16BitBufferSufficient { get; set; } // Otherwise, 32 is

    public bool XybEncoded { get; set; }

    public JxlColorEncoding? ColorEncoding { get; set; }

    public int Orientation { get; set; } = 1;

    public bool HavePreview { get; set; }

    public bool HaveAnimation { get; set; }

    public bool HaveIntrinsicSize { get; set; }

    public JxlSizeHeader IntrinsicSize { get; set; }

    public JxlToneMapping? ToneMapping { get; set; }

    public int ExtraChannelCount { get; set; }

    public List<JxlExtraChannelInfo> ExtraChannels { get; set; } = [];

    public JxlPreviewHeader PreviewSize { get; set; }

    public JxlAnimationHeader Animation { get; set; }

    public long Extensions { get; set; }

    public bool NonserializedOnlyParseBasicInfos { get; set; }

    public float IntensityTarget
    {
        get
        {
            float intensityTarget = this.ToneMapping?.IntensityTarget ?? 0f;

            Debug.Assert(intensityTarget != 0f, "Intensity target should be present");

            return intensityTarget;
        }

        set
        {
            if (this.ToneMapping != null)
            {
                this.ToneMapping.IntensityTarget = value;
            }
        }
    }

    public int AlphaBits
    {
        get
        {
            JxlExtraChannelInfo? ec = this.FindExtraChannel(JxlExtraChannel.Alpha);

            if (ec == null)
            {
                return 0;
            }

            return ec.BitDepth?.BitsPerSample ?? 0;
        }

        set
        {
        }
    }

    public bool HasAlpha => this.AlphaBits != 0;

    public JxlExtraChannelInfo? FindExtraChannel(JxlExtraChannel type)
        => this.ExtraChannels.FirstOrDefault(eci => eci.Type == type);

    public JxlExifOrientation GetExifOrientation() => (JxlExifOrientation)this.Orientation;

    public void SetFloat16Samples()
    {
        if (this.BitDepth != null)
        {
            this.BitDepth.BitsPerSample = 16;
            this.BitDepth.ExponentBitsPerSample = 5;
            this.BitDepth.FloatingPointSample = true;
        }

        this.Modular16BitBufferSufficient = false;
    }

    public void SetFloat32Samples()
    {
        if (this.BitDepth != null)
        {
            this.BitDepth.BitsPerSample = 32;
            this.BitDepth.ExponentBitsPerSample = 8;
            this.BitDepth.FloatingPointSample = true;
        }

        this.Modular16BitBufferSufficient = false;
    }

    public void SetUIntSamples(int bits)
    {
        if (this.BitDepth != null)
        {
            this.BitDepth.BitsPerSample = bits;
            this.BitDepth.ExponentBitsPerSample = 0;
            this.BitDepth.FloatingPointSample = false;
        }

        this.Modular16BitBufferSufficient = bits <= 12;
    }

    public void SetIntensityTarget()
    {
        JxlCustomTransferFunction? tf = this.ColorEncoding?.TransferFunction;

        if (tf is not null)
        {
            if (tf.Value.IsPq)
            {
                this.SetIntensityTarget(10000);
            }
            else if (tf.Value.IsHlg)
            {
                this.SetIntensityTarget(1000);
            }
            else
            {
                this.SetIntensityTarget(DefaultIntensityTarget);
            }
        }
    }

    public bool Visit(JxlVisitor visitor) => throw new NotImplementedException();
}
