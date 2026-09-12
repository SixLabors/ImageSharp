// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Jxl.Cms;
using SixLabors.ImageSharp.Formats.Jxl.Fields;

namespace SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;

internal sealed class JxlImageMetadata : IJxlFields
{
    private bool allDefault;
    private JxlBitDepthMetadata? bitDepth;
    private bool modular16BitBufferSufficient;
    private bool xybEncoded;
    private JxlColorEncoding? colorEncoding;
    private int orientation = 1;
    private bool havePreview;
    private bool haveAnimation;
    private bool haveIntrinsicSize;
    private JxlSizeHeader intrinsicSize = new();
    private JxlToneMapping? toneMapping;
    private int extraChannelCount;
    private List<JxlExtraChannelInfo> extraChannels = [];
    private JxlPreviewHeader previewSize = new();
    private JxlAnimationHeader animation = new();
    private long extensions;

    public JxlImageMetadata() => JxlBundle.Init(this);

    public bool AllDefault
    {
        get => this.allDefault;
        set => this.allDefault = value;
    }

    public JxlBitDepthMetadata? BitDepth
    {
        get => this.bitDepth;
        set => this.bitDepth = value;
    }

    public bool Modular16BitBufferSufficient
    {
        get => this.modular16BitBufferSufficient;
        set => this.modular16BitBufferSufficient = value;
    }

    public bool XybEncoded
    {
        get => this.xybEncoded;
        set => this.xybEncoded = value;
    }

    public JxlColorEncoding? ColorEncoding
    {
        get => this.colorEncoding;
        set => this.colorEncoding = value;
    }

    public int Orientation
    {
        get => this.orientation;
        set => this.orientation = value;
    }

    public bool HavePreview
    {
        get => this.havePreview;
        set => this.havePreview = value;
    }

    public bool HaveAnimation
    {
        get => this.haveAnimation;
        set => this.haveAnimation = value;
    }

    public bool HaveIntrinsicSize
    {
        get => this.haveIntrinsicSize;
        set => this.haveIntrinsicSize = value;
    }

    public JxlSizeHeader IntrinsicSize
    {
        get => this.intrinsicSize;
        set => this.intrinsicSize = value;
    }

    public JxlToneMapping? ToneMapping
    {
        get => this.toneMapping;
        set => this.toneMapping = value;
    }

    public int ExtraChannelCount
    {
        get => this.extraChannelCount;
        set => this.extraChannelCount = value;
    }

    public List<JxlExtraChannelInfo> ExtraChannels
    {
        get => this.extraChannels;
        set => this.extraChannels = value;
    }

    public JxlPreviewHeader PreviewSize
    {
        get => this.previewSize;
        set => this.previewSize = value;
    }

    public JxlAnimationHeader Animation
    {
        get => this.animation;
        set => this.animation = value;
    }

    public long Extensions
    {
        get => this.extensions;
        set => this.extensions = value;
    }

    public bool NonserializedOnlyParseBasicInfos { get; set; }

    public float IntensityTarget
    {
        get
        {
            float intensityTarget = this.ToneMapping?.IntensityTarget ?? 0f;

            if (intensityTarget == 0f)
            {
                throw new InvalidOperationException("Intensity target should be present");
            }

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

            return (int?)ec.BitDepth?.BitsPerSample ?? 0;
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
            this.BitDepth.BitsPerSample = (uint)bits;
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

    public bool Visit(JxlVisitor visitor)
    {
        if (visitor.AllDefault(this, ref this.allDefault))
        {
            // Overwrite all serialized fields, but not any nonserialized_*.
            visitor.SetDefault(this);
            return true;
        }

        // Bundle.AllDefault does not allow usage when reading because it may abort
        // when a codestream has invalid values. When reading, extraFields is
        // overwritten below, so AllDefault is not needed.
        bool toneMappingDefault = !visitor.IsReading && JxlBundle.AllDefault(this.toneMapping!);

        bool extraFields = this.orientation != 1 ||
                           this.havePreview ||
                           this.haveAnimation ||
                           this.haveIntrinsicSize ||
                           !toneMappingDefault;

        if (!visitor.Boolean(false, ref extraFields))
        {
            return false;
        }

        if (visitor.Conditional(extraFields))
        {
            this.orientation--;

            if (!visitor.Bits(3, 0, ref Unsafe.As<int, uint>(ref this.orientation)))
            {
                return false;
            }

            this.orientation++;

            // No bounds checking is necessary because exactly 3 bits are read.
            if (!visitor.Boolean(false, ref this.haveIntrinsicSize))
            {
                return false;
            }

            if (visitor.Conditional(this.haveIntrinsicSize))
            {
                if (!visitor.VisitNested(this.intrinsicSize))
                {
                    return false;
                }
            }

            if (!visitor.Boolean(false, ref this.havePreview))
            {
                return false;
            }

            if (visitor.Conditional(this.havePreview))
            {
                if (!visitor.VisitNested(this.previewSize))
                {
                    return false;
                }
            }

            if (!visitor.Boolean(false, ref this.haveAnimation))
            {
                return false;
            }

            if (visitor.Conditional(this.haveAnimation))
            {
                if (!visitor.VisitNested(this.animation))
                {
                    return false;
                }
            }
        }
        else
        {
            this.orientation = 1;
            this.haveIntrinsicSize = false;
            this.havePreview = false;
            this.haveAnimation = false;
        }

        if (!visitor.VisitNested(this.bitDepth!))
        {
            return false;
        }

        if (!visitor.Boolean(true, ref this.modular16BitBufferSufficient))
        {
            return false;
        }

        this.extraChannelCount = this.extraChannels.Count;

        if (!visitor.U32(
            JxlFieldExpressions.Value(0u),
            JxlFieldExpressions.Value(1u),
            JxlFieldExpressions.BitsOffset(4u, 2u),
            JxlFieldExpressions.BitsOffset(12u, 1u),
            0u,
            ref Unsafe.As<int, uint>(ref this.extraChannelCount)))
        {
            return false;
        }

        if (visitor.Conditional(this.extraChannelCount != 0))
        {
            if (visitor.IsReading)
            {
                this.extraChannels.Clear();
                this.extraChannels.Capacity = this.extraChannelCount;

                for (int i = 0; i < this.extraChannelCount; i++)
                {
                    this.extraChannels.Add(new JxlExtraChannelInfo());
                }
            }

            for (int i = 0; i < this.extraChannels.Count; i++)
            {
                if (!visitor.VisitNested(this.extraChannels[i]))
                {
                    return false;
                }
            }
        }

        if (!visitor.Boolean(true, ref this.xybEncoded))
        {
            return false;
        }

        if (!visitor.VisitNested(this.colorEncoding!))
        {
            return false;
        }

        if (visitor.Conditional(extraFields))
        {
            if (!visitor.VisitNested(this.toneMapping!))
            {
                return false;
            }
        }

        // Treat as if only the fields up to extra channels exist.
        if (visitor.IsReading && this.NonserializedOnlyParseBasicInfos)
        {
            return true;
        }

        if (!visitor.BeginExtensions(ref Unsafe.As<long, ulong>(ref this.extensions)))
        {
            return false;
        }

        // Extensions: in chronological order of being added to the format.
        return visitor.EndExtensions();
    }
}
