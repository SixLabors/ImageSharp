// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

// Disable IDE0032 for consistency with other fields.
// We have to avoid auto properties for most fields
// so we can use the ref keyword on them directly.
#pragma warning disable IDE0032 // Use auto property

using SixLabors.ImageSharp.Formats.Jxl.Fields;
using SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;
using SixLabors.ImageSharp.Formats.Jxl.Processing;

namespace SixLabors.ImageSharp.Formats.Jxl.IO.FrameHeader;

/// <summary>
/// Control information for a JPEG XL frame.
/// </summary>
internal sealed class JxlFrameHeader : IJxlFields
{
    // The following are backing fields for properties.
    private JxlFrameEncoding encoding = JxlFrameEncoding.Modular;
    private JxlFrameType frameType = JxlFrameType.RegularFrame;
    private ulong flags;
    private JxlColorTransform colorTransform = JxlColorTransform.Xyb;
    private JxlYCbCrChromaSubsampling? chromaSubsampling;
    private uint groupSizeShift;
    private uint xQmScale;
    private uint bQmScale;
    private string? name;
    private bool customSizeOrOrigin;
    private Size frameSize;
    private uint upsampling;
    private List<uint> extraChannelUpsampling = [];
    private Point frameOrigin;
    private JxlBlendingInfo? blendingInfo;
    private List<JxlBlendingInfo> extraChannelBlendingInfo = [];
    private readonly JxlAnimationFrame? animationFrame;
    private bool isLast;
    private uint saveAsReference;
    private bool saveBeforeColorTransform;
    private uint dcLevel;
    private JxlCodecMetadata? metadata;
    private JxlLoopFilter? loopFilter;
    private ulong extensions;

    private bool isPreviewFrame;    // Non-serialized

    /// <summary>
    /// Gets or sets the frame encoding method (e.g., Modular or VarDCT).
    /// </summary>
    public JxlFrameEncoding Encoding
    {
        get => this.encoding;
        set => this.encoding = value;
    }

    /// <summary>
    /// Gets or sets the type of frame (e.g., RegularFrame).
    /// </summary>
    public JxlFrameType FrameType
    {
        get => this.frameType;
        set => this.frameType = value;
    }

    /// <summary>
    /// Gets or sets the frame flags.
    /// </summary>
    public ulong Flags
    {
        get => this.flags;
        set => this.flags = value;
    }

    /// <summary>
    /// Gets or sets the color transform used (e.g., XYB).
    /// </summary>
    public JxlColorTransform ColorTransform
    {
        get => this.colorTransform;
        set => this.colorTransform = value;
    }

    /// <summary>
    /// Gets or sets the chroma subsampling information.
    /// </summary>
    public JxlYCbCrChromaSubsampling? ChromaSubsampling
    {
        get => this.chromaSubsampling;
        set => this.chromaSubsampling = value;
    }

    /// <summary>
    /// Gets or sets the group size shift value.
    /// </summary>
    public uint GroupSizeShift
    {
        get => this.groupSizeShift;
        set => this.groupSizeShift = value;
    }

    /// <summary>
    /// Gets or sets the X quantization matrix scale.
    /// </summary>
    public uint XQmScale
    {
        get => this.xQmScale;
        set => this.xQmScale = value;
    }

    /// <summary>
    /// Gets or sets the B quantization matrix scale.
    /// </summary>
    public uint BQmScale
    {
        get => this.bQmScale;
        set => this.bQmScale = value;
    }

    /// <summary>
    /// Gets or sets the frame name.
    /// </summary>
    public string? Name
    {
        get => this.name;
        set => this.name = value;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the frame has a custom size or origin.
    /// </summary>
    public bool CustomSizeOrOrigin
    {
        get => this.customSizeOrOrigin;
        set => this.customSizeOrOrigin = value;
    }

    /// <summary>
    /// Gets or sets the frame size.
    /// </summary>
    public Size FrameSize
    {
        get => this.frameSize;
        set => this.frameSize = value;
    }

    /// <summary>
    /// Gets or sets the upsampling factor.
    /// </summary>
    public uint Upsampling
    {
        get => this.upsampling;
        set => this.upsampling = value;
    }

    /// <summary>
    /// Gets or sets the upsampling factors for extra channels.
    /// </summary>
    public List<uint> ExtraChannelUpsampling
    {
        get => this.extraChannelUpsampling;
        set => this.extraChannelUpsampling = value;
    }

    /// <summary>
    /// Gets or sets the frame origin point.
    /// </summary>
    public Point FrameOrigin
    {
        get => this.frameOrigin;
        set => this.frameOrigin = value;
    }

    /// <summary>
    /// Gets or sets the blending information for the frame.
    /// </summary>
    public JxlBlendingInfo? BlendingInfo
    {
        get => this.blendingInfo;
        set => this.blendingInfo = value;
    }

    /// <summary>
    /// Gets or sets the blending information for extra channels.
    /// </summary>
    public List<JxlBlendingInfo> ExtraChannelBlendingInfo
    {
        get => this.extraChannelBlendingInfo;
        set => this.extraChannelBlendingInfo = value;
    }

    /// <summary>
    /// Gets the associated animation frame, if any.
    /// </summary>
    public JxlAnimationFrame? AnimationFrame => this.animationFrame;

    /// <summary>
    /// Gets or sets a value indicating whether this is the last frame.
    /// </summary>
    public bool IsLast
    {
        get => this.isLast;
        set => this.isLast = value;
    }

    /// <summary>
    /// Gets or sets the reference frame index to save.
    /// </summary>
    public uint SaveAsReference
    {
        get => this.saveAsReference;
        set => this.saveAsReference = value;
    }

    /// <summary>
    /// Gets or sets a value indicating whether to save before color transform.
    /// </summary>
    public bool SaveBeforeColorTransform
    {
        get => this.saveBeforeColorTransform;
        set => this.saveBeforeColorTransform = value;
    }

    /// <summary>
    /// Gets or sets the DC level of the frame.
    /// </summary>
    public uint DcLevel
    {
        get => this.dcLevel;
        set => this.dcLevel = value;
    }

    /// <summary>
    /// Gets or sets the codec metadata.
    /// </summary>
    public JxlCodecMetadata? Metadata
    {
        get => this.metadata;
        set => this.metadata = value;
    }

    /// <summary>
    /// Gets or sets the loop filter applied to the frame.
    /// </summary>
    public JxlLoopFilter? LoopFilter
    {
        get => this.loopFilter;
        set => this.loopFilter = value;
    }

    /// <summary>
    /// Gets or sets a value indicating whether this is a preview frame. Non-serialized.
    /// </summary>
    public bool IsPreviewFrame
    {
        get => this.isPreviewFrame;
        set => this.isPreviewFrame = value;
    }

    /// <summary>
    /// Gets or sets the number of extensions.
    /// </summary>
    public ulong Extensions
    {
        get => this.extensions;
        set => this.extensions = value;
    }

    public int DefaultXSize
    {
        get
        {
            if (this.metadata == null)
            {
                return 0;
            }

            if (this.isPreviewFrame)
            {
                return this.metadata.ImageMetadata?.PreviewSize?.XSize ?? 0;
            }

            return this.metadata.XSize;
        }
    }

    public int DefaultYSize
    {
        get
        {
            if (this.metadata == null)
            {
                return 0;
            }

            if (this.isPreviewFrame)
            {
                return this.metadata.ImageMetadata?.PreviewSize?.YSize ?? 0;
            }

            return this.metadata.YSize;
        }
    }

    public JxlFrameDimensions FrameDimensions
    {
        get
        {
            int xsize = this.DefaultXSize;
            int ysize = this.DefaultYSize;

            xsize = this.frameSize.Width != 0 ? this.frameSize.Width : xsize;
            ysize = this.frameSize.Height != 0 ? this.frameSize.Height : ysize;

            if (this.dcLevel != 0)
            {
                xsize = JxlMath.DivCeil(xsize, 1 << (3 * (int)this.dcLevel));
                ysize = JxlMath.DivCeil(ysize, 1 << (3 * (int)this.dcLevel));
            }

            JxlFrameDimensions frameDim = new(
                xsize,
                ysize,
                (int)this.groupSizeShift,
                this.chromaSubsampling?.MaxHShift ?? 0,
                this.chromaSubsampling?.MaxVShift ?? 0,
                this.encoding == JxlFrameEncoding.Modular,
                (int)this.upsampling);

            return frameDim;
        }
    }

    public bool NeedsColorTransform => !this.saveBeforeColorTransform ||
                this.frameType == JxlFrameType.RegularFrame ||
                this.frameType == JxlFrameType.SkipProgressive;

    /// <summary>
    /// Gets a value indicating whether this frame is supposed to be saved for future usage by other frames.
    /// </summary>
    public bool CanBeReferenced => // DC frames cannot be referenced. The last frame cannot be referenced.
                                   // A duration 0 frame makes little sense if it is not referenced.
                                   // A non-duration 0 frame may or may not be referenced.
                                   !this.isLast &&
                                       this.frameType != JxlFrameType.DcFrame &&
                                       (this.animationFrame?.Duration == 0 || this.saveAsReference != 0);

    private void UpdateFlag(bool condition, ulong flag)
    {
        if (condition)
        {
            this.flags |= flag;
        }
        else
        {
            this.flags &= ~flag;
        }
    }

    public bool Visit(JxlVisitor visitor)
    {
        bool allDefault = false;
        if (visitor.AllDefault(this, ref allDefault))
        {
            visitor.SetDefault(this);
            return true;
        }

        if (!VisitFrameType(visitor, JxlFrameType.RegularFrame, ref this.frameType))
        {
            return false;
        }

        if (visitor.IsReading && this.isPreviewFrame && this.frameType != JxlFrameType.RegularFrame)
        {
            throw new InvalidOperationException("Only regular frame could be a preview");
        }

        // FrameEncoding
        bool isModular = this.encoding == JxlFrameEncoding.Modular;
        if (!visitor.Boolean(false, ref isModular))
        {
            return false;
        }

        this.encoding = isModular
            ? JxlFrameEncoding.Modular
            : JxlFrameEncoding.VarDct;

        // Flags
        if (!visitor.U64(0, ref this.flags))
        {
            return false;
        }

        // Color transform
        bool xybEncoded = this.metadata?.ImageMetadata?.XybEncoded == true;
        if (xybEncoded)
        {
            this.colorTransform = JxlColorTransform.Xyb;
        }
        else
        {
            bool alternate = this.colorTransform == JxlColorTransform.YCbCr;
            if (!visitor.Boolean(false, ref alternate))
            {
                return false;
            }

            this.colorTransform = alternate
                ? JxlColorTransform.YCbCr
                : JxlColorTransform.None;
        }

        // Chroma subsampling
        if (visitor.Conditional(this.colorTransform == JxlColorTransform.YCbCr &&
                                ((this.flags & (ulong)JxlFrameHeaderFlags.Dc) == 0)))
        {
            if (!visitor.VisitNested(this.chromaSubsampling!))
            {
                return false;
            }
        }

        int numExtraChannels = this.metadata?.ImageMetadata?.ExtraChannelCount ?? 0;

        // Upsampling
        if (visitor.Conditional((this.flags & (ulong)JxlFrameHeaderFlags.Dc) == 0))
        {
            if (!visitor.U32(
                JxlFieldExpressions.Value(1),
                JxlFieldExpressions.Value(2),
                JxlFieldExpressions.Value(4),
                JxlFieldExpressions.Value(8),
                1,
                ref this.upsampling))
            {
                return false;
            }

            if (this.metadata != null && visitor.Conditional(numExtraChannels != 0))
            {
                List<JxlExtraChannelInfo> extraChannels = this.metadata!.ImageMetadata?.ExtraChannels ?? [];
                this.extraChannelUpsampling = new List<uint>(extraChannels.Count);

                for (int i = 0; i < extraChannels.Count; i++)
                {
                    uint dimShift = (uint)extraChannels[i].DimensionShift;
                    uint ecUpsampling = 1;
                    ecUpsampling >>= (int)dimShift;

                    if (!visitor.U32(
                        JxlFieldExpressions.Value(1),
                        JxlFieldExpressions.Value(2),
                        JxlFieldExpressions.Value(4),
                        JxlFieldExpressions.Value(8),
                        1,
                        ref ecUpsampling))
                    {
                        return false;
                    }

                    ecUpsampling <<= (int)dimShift;

                    if (ecUpsampling < this.upsampling)
                    {
                        throw new InvalidOperationException("EC upsampling < color upsampling, invalid");
                    }

                    if (ecUpsampling > 8)
                    {
                        throw new InvalidOperationException("EC upsampling too large");
                    }

                    this.extraChannelUpsampling.Add(ecUpsampling);
                }
            }
            else
            {
                this.extraChannelUpsampling.Clear();
            }
        }

        // Modular / VarDCT specifics
        if (visitor.Conditional(this.encoding == JxlFrameEncoding.Modular))
        {
            if (!visitor.Bits(2, 1, ref this.groupSizeShift))
            {
                return false;
            }
        }

        if (visitor.Conditional(this.encoding == JxlFrameEncoding.VarDct &&
                                this.colorTransform == JxlColorTransform.Xyb))
        {
            if (!visitor.Bits(3, 3, ref this.xQmScale))
            {
                return false;
            }

            if (!visitor.Bits(3, 2, ref this.bQmScale))
            {
                return false;
            }
        }
        else
        {
            this.xQmScale = this.bQmScale = 2;
        }

        // Passes
        if (visitor.Conditional(this.frameType != JxlFrameType.ReferenceOnly))
        {
            if (!visitor.VisitNested(this.passes))
            {
                return false;
            }
        }

        // DC frame
        if (visitor.Conditional(this.frameType == JxlFrameType.DcFrame))
        {
            if (!visitor.U32(
                JxlFieldExpressions.Value(1),
                JxlFieldExpressions.Value(2),
                JxlFieldExpressions.Value(3),
                JxlFieldExpressions.Value(4),
                1,
                ref this.dcLevel))
            {
                return false;
            }
        }
        else
        {
            this.dcLevel = 0;
        }

        // Custom size/origin
        bool isPartialFrame = false;

        if (visitor.Conditional(this.frameType != JxlFrameType.DcFrame))
        {
            if (!visitor.Boolean(false, ref this.customSizeOrOrigin))
            {
                return false;
            }

            if (visitor.Conditional(this.customSizeOrOrigin))
            {
                JxlU32Enc enc = new(
                    JxlFieldExpressions.Bits(8),
                    JxlFieldExpressions.BitsOffset(11, 256),
                    JxlFieldExpressions.BitsOffset(14, 2304),
                    JxlFieldExpressions.BitsOffset(30, 18688));

                if (visitor.Conditional(this.frameType is JxlFrameType.RegularFrame or JxlFrameType.SkipProgressive))
                {
                    uint ux0 = JxlPackSigned.PackUnsigned(this.frameOrigin.X);
                    uint uy0 = JxlPackSigned.PackUnsigned(this.frameOrigin.Y);

                    if (!visitor.U32(enc, 0, ref ux0))
                    {
                        return false;
                    }

                    if (!visitor.U32(enc, 0, ref uy0))
                    {
                        return false;
                    }

                    this.frameOrigin = new Point(JxlPackSigned.UnpackSigned(ux0), JxlPackSigned.UnpackSigned(uy0));
                }

                uint frameSizeWidth = (uint)this.frameSize.Width;
                uint frameSizeHeight = (uint)this.frameSize.Height;

                if (!visitor.U32(enc, 0, ref frameSizeWidth))
                {
                    return false;
                }

                if (!visitor.U32(enc, 0, ref frameSizeHeight))
                {
                    return false;
                }

                if (this.customSizeOrOrigin && (this.frameSize.Width == 0 || this.frameSize.Height == 0))
                {
                    throw new InvalidOperationException("Invalid crop dimensions for frame");
                }

                int imageXSize = this.DefaultXSize;
                int imageYSize = this.DefaultYSize;

                if (this.frameType is JxlFrameType.RegularFrame or JxlFrameType.SkipProgressive)
                {
                    isPartialFrame |= this.frameOrigin.X > 0;
                    isPartialFrame |= this.frameOrigin.Y > 0;
                    isPartialFrame |= (this.frameSize.Width + this.frameOrigin.X) < imageXSize;
                    isPartialFrame |= (this.frameSize.Height + this.frameOrigin.Y) < imageYSize;
                }
            }
        }

        // Blending, animation, last frame
        if (visitor.Conditional(this.frameType is JxlFrameType.RegularFrame or JxlFrameType.SkipProgressive))
        {
            this.blendingInfo!.ExtraChannelCount = numExtraChannels;
            this.blendingInfo.IsPartialFrame = isPartialFrame;

            if (!visitor.VisitNested(this.blendingInfo))
            {
                return false;
            }

            bool replaceAll = this.blendingInfo.BlendMode == JxlBlendMode.Replace;

            this.extraChannelBlendingInfo = new List<JxlBlendingInfo>(numExtraChannels);
            for (int i = 0; i < numExtraChannels; i++)
            {
                JxlBlendingInfo ecBlendingInfo = new()
                {
                    IsPartialFrame = isPartialFrame,
                    ExtraChannelCount = numExtraChannels
                };

                if (!visitor.VisitNested(ecBlendingInfo))
                {
                    return false;
                }

                this.extraChannelBlendingInfo.Add(ecBlendingInfo);
                replaceAll &= ecBlendingInfo.BlendMode == JxlBlendMode.Replace;
            }

            if (visitor.IsReading && this.isPreviewFrame)
            {
                if (!replaceAll || this.customSizeOrOrigin)
                {
                    throw new InvalidOperationException("Preview is not compatible with blending");
                }
            }

            if (visitor.Conditional(this.metadata?.ImageMetadata?.HaveAnimation == true))
            {
                this.animationFrame!.CodecMetadata = this.metadata;

                if (!visitor.VisitNested(this.animationFrame!))
                {
                    return false;
                }
            }

            if (!visitor.Boolean(true, ref this.isLast))
            {
                return false;
            }
        }
        else
        {
            this.isLast = false;
        }

        // SaveAsReference
        if (visitor.Conditional(this.frameType != JxlFrameType.DcFrame && !this.isLast))
        {
            if (!visitor.U32(
                JxlFieldExpressions.Value(0),
                JxlFieldExpressions.Value(1),
                JxlFieldExpressions.Value(2),
                JxlFieldExpressions.Value(3),
                0,
                ref this.saveAsReference))
            {
                return false;
            }
        }

        // SaveBeforeColorTransform logic
        if (this.frameType != JxlFrameType.DcFrame)
        {
            if (visitor.Conditional(
                this.CanBeReferenced &&
                this.blendingInfo?.BlendMode == JxlBlendMode.Replace &&
                !isPartialFrame &&
                (this.frameType == JxlFrameType.RegularFrame ||
                this.frameType == JxlFrameType.SkipProgressive)))
            {
                if (!visitor.Boolean(false, ref this.saveBeforeColorTransform))
                {
                    return false;
                }
            }
            else if (visitor.Conditional(this.frameType == JxlFrameType.ReferenceOnly))
            {
                if (!visitor.Boolean(true, ref this.saveBeforeColorTransform))
                {
                    return false;
                }

                int xsize = this.customSizeOrOrigin
                    ? this.frameSize.Width
                    : this.metadata!.XSize;

                int ysize = this.customSizeOrOrigin
                    ? this.frameSize.Height
                    : this.metadata!.YSize;

                if (!this.saveBeforeColorTransform &&
                    (xsize < this.metadata!.XSize ||
                    ysize < this.metadata!.YSize ||
                    this.frameOrigin.X != 0 ||
                    this.frameOrigin.Y != 0))
                {
                    throw new InvalidOperationException("Non-patch reference frame with invalid crop");
                }
            }
        }
        else
        {
            this.saveBeforeColorTransform = true;
        }

        if (!VisitNameString(visitor, ref this.name))
        {
            return false;
        }

        this.loopFilter!.IsModular = isModular;
        if (!visitor.VisitNested(this.loopFilter!))
        {
            return false;
        }

        if (!visitor.BeginExtensions(ref this.extensions))
        {
            return false;
        }

        return visitor.EndExtensions();
    }
}
