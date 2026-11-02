// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jxl.IO.FrameHeader;
using SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;
using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Blending;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Patch;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.RenderPipeline;

/// <summary>
/// This render pipeline stage performs alpha blending.
/// </summary>
internal class BlendingStage : RenderPipelineStageBase
{
    private readonly Configuration configuration;
    private readonly JxlFrameHeader frameHeader;
    private readonly JxlPassesCommonContext context;
    private readonly JxlBlendingInfo info;
    private readonly JxlImageBundle background;
    private readonly bool initialized = true;
    private readonly int imageWidth;
    private readonly int imageHeight;
    private readonly List<JxlPatchBlending> blendingInfo;
    private readonly List<JxlExtraChannelInfo> extraChannelInfos;
    private readonly IMemoryOwner<float> zeroes;

    public BlendingStage(Configuration configuration, JxlFrameHeader frameHeader, JxlPassesDecoderContext decoderContext)
        : base(configuration)
    {
        // Initialize private fields.
        this.configuration = configuration;
        this.frameHeader = frameHeader;
        this.context = decoderContext.Shared;
        this.imageWidth = frameHeader.Metadata!.XSize;
        this.imageHeight = frameHeader.Metadata!.YSize;
        this.extraChannelInfos = frameHeader.Metadata.ImageMetadata!.ExtraChannels;
        this.info = frameHeader.BlendingInfo!;

        List<JxlBlendingInfo> extraChannelInfo = frameHeader.ExtraChannelBlendingInfo;
        JxlImageBundle background = this.context.ReferenceFrames[this.info.Source].Frame;
        this.background = background;

        if (background.XSize == 0 || background.YSize == 0)
        {
            this.zeroes = configuration.MemoryAllocator.Allocate<float>(this.imageWidth);
        }
        else if (this.context.ReferenceFrames[this.info.Source].ImageBundleIsInXyb)
        {
            // The reference frame uses the XYB color transform, but the current frame
            // doesn't use it. This isn't intended.
            throw new InvalidOperationException("Cannot blend a reference frame that uses XYB with a frame that doesn't use XYB");
        }
        else if (extraChannelInfo.Any(info => this.context.ReferenceFrames[this.info.Source].Frame.XSizeOrYSizeIsZero))
        {
            // Reference frame to which the blending info belongs to is empty.
            this.zeroes = configuration.MemoryAllocator.Allocate<float>(this.imageWidth);
        }
        else
        {
            this.zeroes = JxlMemoryHelpers.GetEmptyMemoryOwner<float>();
        }

        bool ok = VerifyBackgroundSize(this.background);
        foreach (JxlExtraChannelInfo extraChannel in this.extraChannelInfos)
        {
            JxlImageBundle extraChannelBackground = this.context.ReferenceFrames[this.info.Source].Frame;
            if (ok)
            {
                ok = VerifyBackgroundSize(extraChannelBackground);
            }
        }

        if (!ok)
        {
            this.initialized = false;
            this.blendingInfo = [];
            return;
        }

        if (this.context.CodecMetadata.ImageMetadata!.XybEncoded)
        {
            // Check if we can blend this color space.
            if (!decoderContext.OutputEncodingInfo.ColorEncodingIsOriginal)
            {
                throw new InvalidOperationException("Blending in an unsupported color space");
            }
        }

        this.blendingInfo = new List<JxlPatchBlending>(this.extraChannelInfos.Count + 1);

        bool VerifyBackgroundSize(JxlImageBundle background)
        {
            if (background.XSize != 0 && background.YSize != 0 &&
                (background.XSize < this.imageWidth || background.YSize > this.imageHeight ||
                background.Origin.X != 0 || background.Origin.Y != 0))
            {
                return false;
            }

            return true;
        }

        static void MakeBlending(JxlBlendingInfo info, ref JxlPatchBlending pb)
        {
            pb.AlphaChannel = (int)info.AlphaChannel;
            pb.Clamp = info.Clamp;

            // Translate the blending mode to a patch blending mode.
            pb.Mode = info.BlendMode switch
            {
                JxlBlendMode.Replace => JxlPatchBlendMode.Replace,
                JxlBlendMode.Add => JxlPatchBlendMode.Add,
                JxlBlendMode.Multiply => JxlPatchBlendMode.Multiply,
                JxlBlendMode.Blend => JxlPatchBlendMode.BlendAbove,
                JxlBlendMode.AlphaWeightedBlend => JxlPatchBlendMode.AlphaWeightedAddAbove,
                _ => throw new InvalidOperationException("The blend mode cannot be translated to a patch blend mode")
            };
        }

        Span<JxlPatchBlending> pb = CollectionsMarshal.AsSpan(this.blendingInfo);

        MakeBlending(this.info, ref pb[0]);
        for (int i = 0; i < extraChannelInfo.Count; i++)
        {
            MakeBlending(extraChannelInfo[i], ref pb[i + 1]);
        }
    }

    /// <inheritdoc />
    public override string Name => "Blending";

    /// <inheritdoc />
    public override bool IsInitialized => this.initialized;

    /// <inheritdoc />
    public override bool SwitchToImageDimensions => true;

    /// <inheritdoc />
    public override RenderPipelineChannelMode GetChannelMode(int channel) => RenderPipelineChannelMode.InPlace;

    /// <inheritdoc />
    public override void GetImageDimensions(out int width, out int height, out Point frameOrigin)
    {
        width = this.imageWidth;
        height = this.imageHeight;
        frameOrigin = this.frameHeader.FrameOrigin;
    }

    /// <inheritdoc />
    public override void ProcessPaddingRow(Buffer2D<Memory<float>> outputRows, int width, int xPos, int yPos)
    {
        if (this.background.XSize == 0 || this.background.YSize == 0)
        {
            // When we don't have a background, clear output rows.
            this.GetInputRow(outputRows, 0, 0).Clear();
            this.GetInputRow(outputRows, 1, 0).Clear();
            this.GetInputRow(outputRows, 2, 0).Clear();
        }
        else
        {
            // Copy a background.
            for (int c = 0; c < 3; c++)
            {
                this.background.Color!.PlaneRow(c, yPos).Slice(xPos, width)
                    .CopyTo(this.GetInputRow(outputRows, c, 0));
            }
        }

        // Copy the bundles
        for (int ec = 0; ec < this.extraChannelInfos.Count; ec++)
        {
            // Get the background of an extra channel.
            JxlImageBundle extraBackground = this.context.ReferenceFrames[this.frameHeader.ExtraChannelBlendingInfo[ec].Source].Frame;
            if (extraBackground.XSize == 0 || extraBackground.YSize == 0)
            {
                // If the background of the extra channel is empty,
                // clear the extra channel output.
                this.GetInputRow(outputRows, 3 + ec, 0).Clear();
            }
            else
            {
                // Otherwise, copy the extra channel's background.
                this.GetInputRow(outputRows, 3 + ec, 0)
                    .CopyTo(extraBackground.GetExtraChannel(ec).GetRow(yPos)[xPos..]);
            }
        }
    }

    /// <inheritdoc />
    public override void ProcessRow(Buffer2D<Memory<float>> inputRows, Buffer2D<Memory<float>> outputRows, int xExtraLeft, int xExtraRight, int width, int xPos, int yPos)
    {
        if (!this.initialized)
        {
            // The blending stage must be correctly initialized.
            throw new InvalidOperationException("The blending stage failed to initialize.");
        }

        Point frameOrigin = this.frameHeader.FrameOrigin;
        int backgroundXPos = frameOrigin.X + xPos;
        int backgroundYPos = frameOrigin.Y + yPos;
        int offset = 0;

        if (backgroundXPos + width <= 0 ||
            frameOrigin.X >= this.imageWidth ||
            backgroundYPos < 0 || backgroundYPos >= this.imageHeight)
        {
            throw new InvalidOperationException("The background is out of range.");
        }

        if (backgroundXPos < 0)
        {
            // When the background X position is negative,
            // set it to 0 and update the offset and width.
            offset -= backgroundXPos;
            width += backgroundXPos;
            backgroundXPos = 0;
        }

        if (backgroundXPos + width > this.imageWidth)
        {
            // The background offset should be within bounds.
            width = Math.Max(0, this.imageWidth - backgroundXPos);
        }

        // We can't stack-alloc a Memory<T>, so we have to pool it.
        Memory<float>[] backgroundRowsPool = ArrayPool<Memory<float>>.Shared.Rent(inputRows.Width);
        Span<Memory<float>> backgroundRows = backgroundRowsPool;
        Memory<float>[] foregroundRowsPool = ArrayPool<Memory<float>>.Shared.Rent(inputRows.Width);
        Span<Memory<float>> foregroundRows = foregroundRowsPool;

        // Total number of channels (including extra channels).
        int channels = Math.Min(inputRows.Width, this.extraChannelInfos.Count + 3);

        for (int c = 0; c < channels; c++)
        {
            foregroundRows[c] = this.GetInputRowMemory(inputRows, c, 0)[offset..];
            if (c < 3)
            {
                backgroundRows[c] = this.background.XSize != 0 && this.background.YSize != 0
                    ? this.background.Color!.PlaneRowMemory(c, backgroundYPos)[backgroundXPos..]
                    : this.zeroes.Memory;
            }
            else
            {
                JxlImageBundle ecBackground = this.context.ReferenceFrames[this.frameHeader.ExtraChannelBlendingInfo[c - 3].Source].Frame;
                backgroundRows[c] = ecBackground.XSize != 0 && ecBackground.YSize != 0
                    ? ecBackground.GetExtraChannel(c - 3).GetRowMemory(backgroundYPos)[backgroundXPos..]
                    : this.zeroes.Memory;
            }
        }

        JxlBlending.PerformBlending(
            this.configuration,
            backgroundRows,
            foregroundRows,
            foregroundRows,
            0,
            width,
            this.blendingInfo[0],
            CollectionsMarshal.AsSpan(this.blendingInfo)[1..],
            this.extraChannelInfos);
    }
}
