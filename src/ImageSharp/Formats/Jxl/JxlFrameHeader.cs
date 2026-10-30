// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl;

/// <summary>
/// Reads frame geometry, coding controls, blending and display timing shared by identification and reconstruction.
/// </summary>
internal sealed class JxlFrameHeader
{
    /// <summary>
    /// The flag selecting a saved low-frequency frame instead of independently coded low-frequency data.
    /// </summary>
    public const ulong UseLowFrequencyFrame = 0x20;

    private InlineArray4<byte> jpegSampling;
    private InlineArray16<byte> passShifts;
    private InlineArray4<byte> downsampling;
    private InlineArray4<byte> lastDownsamplingPass;
    private byte downsamplingCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlFrameHeader"/> class with image-dependent defaults.
    /// </summary>
    /// <param name="image">The image-wide header.</param>
    /// <param name="preview">Whether this frame contains the optional preview.</param>
    private JxlFrameHeader(JxlHeader image, bool preview)
    {
        this.Size = preview ? image.PreviewSize.GetValueOrDefault() : image.Size;

        // Each frame owns its channel arrays. Defaults include the image-wide resolution shift even when
        // an all-default frame omits every per-channel field from the codestream.
        this.ExtraChannelUpsampling = new byte[image.Metadata.ExtraChannels.Count];
        this.ExtraChannelBlending = new BlendingInfo[image.Metadata.ExtraChannels.Count];

        for (int i = 0; i < this.ExtraChannelUpsampling.Length; i++)
        {
            this.ExtraChannelUpsampling[i] = (byte)(1 << image.Metadata.ExtraChannels[i].DimensionShift);
        }

        if (!image.UsesXybTransform)
        {
            this.XQuantizationScale = this.BQuantizationScale = 2;
        }
    }

    /// <summary>
    /// Identifies how a frame participates in display or reference reconstruction.
    /// </summary>
    public enum FrameType : byte
    {
        /// <summary>
        /// A regular displayed or intermediate frame.
        /// </summary>
        Regular,

        /// <summary>
        /// A low-frequency reference frame.
        /// </summary>
        LowFrequency,

        /// <summary>
        /// A frame saved only for reference or patches.
        /// </summary>
        ReferenceOnly,

        /// <summary>
        /// A frame that can be omitted during progressive rendering.
        /// </summary>
        SkipProgressive
    }

    /// <summary>
    /// Gets the frame's role in the codestream.
    /// </summary>
    public FrameType Type { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the frame uses modular coding instead of VarDCT.
    /// </summary>
    public bool Modular { get; private set; }

    /// <summary>
    /// Gets the feature flags retained for reconstruction.
    /// </summary>
    public ulong Flags { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the frame applies the JPEG YCbCr transform to its three color components.
    /// </summary>
    public bool YCbCr { get; private set; }

    /// <summary>
    /// Gets the three JPEG sampling codes in codestream component order.
    /// </summary>
    public ReadOnlySpan<byte> JpegSampling => this.jpegSampling[..3];

    /// <summary>
    /// Gets the color upsampling factor, from one to eight in powers of two.
    /// </summary>
    public byte Upsampling { get; private set; } = 1;

    /// <summary>
    /// Gets each extra channel's effective upsampling factor, including its image-wide dimension shift.
    /// </summary>
    public byte[] ExtraChannelUpsampling { get; }

    /// <summary>
    /// Gets the base-two scale for the 128-pixel coding group dimension.
    /// </summary>
    public byte GroupSizeShift { get; private set; } = 1;

    /// <summary>
    /// Gets the XYB X-component quantization-matrix scale.
    /// </summary>
    public byte XQuantizationScale { get; private set; } = 3;

    /// <summary>
    /// Gets the XYB B-component quantization-matrix scale.
    /// </summary>
    public byte BQuantizationScale { get; private set; } = 2;

    /// <summary>
    /// Gets the progressive pass count, from one to eleven.
    /// </summary>
    public byte PassCount { get; private set; } = 1;

    /// <summary>
    /// Gets the coefficient shifts for all passes. The last pass always has shift zero.
    /// </summary>
    public ReadOnlySpan<byte> PassShifts => this.passShifts[..this.PassCount];

    /// <summary>
    /// Gets the decreasing downsampling factors for progressive display stages.
    /// </summary>
    public ReadOnlySpan<byte> Downsampling => this.downsampling[..this.downsamplingCount];

    /// <summary>
    /// Gets the last pass for each progressive downsampling factor.
    /// </summary>
    public ReadOnlySpan<byte> LastDownsamplingPass => this.lastDownsamplingPass[..this.downsamplingCount];

    /// <summary>
    /// Gets the low-frequency pyramid level, or zero for other frame types.
    /// </summary>
    public byte LowFrequencyLevel { get; private set; }

    /// <summary>
    /// Gets the frame origin in image coordinates. Cropped frames can begin outside the image.
    /// </summary>
    public Point Origin { get; private set; }

    /// <summary>
    /// Gets the frame dimensions before low-frequency reduction and color upsampling.
    /// </summary>
    public Size Size { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the frame has explicit dimensions or an origin.
    /// </summary>
    public bool HasCrop { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the frame covers the complete image canvas.
    /// </summary>
    public bool CoversImage { get; private set; } = true;

    /// <summary>
    /// Gets the color components' native frame-blending controls.
    /// </summary>
    public BlendingInfo Blending { get; private set; }

    /// <summary>
    /// Gets the extra channels' native frame-blending controls, in encoded order.
    /// </summary>
    public BlendingInfo[] ExtraChannelBlending { get; }

    /// <summary>
    /// Gets the display duration in the image header's animation ticks.
    /// </summary>
    public uint Duration { get; private set; }

    /// <summary>
    /// Gets the optional encoded animation time code.
    /// </summary>
    public uint TimeCode { get; private set; }

    /// <summary>
    /// Gets a value indicating whether this is the final frame in the image.
    /// </summary>
    public bool IsLast { get; private set; } = true;

    /// <summary>
    /// Gets the saved reference slot, from zero to three. Zero-duration frames can also use slot zero.
    /// </summary>
    public byte ReferenceSlot { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the saved reference contains native samples before the frame color transform.
    /// </summary>
    public bool SaveBeforeColorTransform { get; private set; }

    /// <summary>
    /// Gets the optional frame label.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the parsed reconstruction filter parameters.
    /// </summary>
    public JxlRestorationFilter RestorationFilter { get; private set; } = JxlRestorationFilter.Default;

    /// <summary>
    /// Gets a value indicating whether this frame contributes one displayed image rather than only reconstruction state.
    /// </summary>
    public bool IsDisplayed => this.Type is FrameType.Regular or FrameType.SkipProgressive && (this.IsLast || this.Duration != 0);

    /// <summary>
    /// Gets the coded dimensions after low-frequency reduction and before color upsampling.
    /// </summary>
    public Size CodingSize
    {
        get
        {
            // LF level n reduces each dimension by 8^n; upsampling adds another power-of-two divisor.
            // Combining the divisors preserves the same ceiling as applying these reductions successively.
            uint divisor = (uint)this.Upsampling << (3 * this.LowFrequencyLevel);

            return new Size((int)(((uint)this.Size.Width + divisor - 1) / divisor), (int)(((uint)this.Size.Height + divisor - 1) / divisor));
        }
    }

    /// <summary>
    /// Gets the section-table entry count without overflowing large dimension products.
    /// </summary>
    public int SectionCount
    {
        get
        {
            // A coding group spans 128 << shift pixels. An LF group spans eight coding groups per dimension.
            // Chroma padding cannot cross these power-of-two group boundaries, so unpadded coding sizes suffice here.
            Size size = this.CodingSize;
            uint groupSize = 128U << this.GroupSizeShift;
            ulong groups = (((uint)size.Width + groupSize - 1) / groupSize) * (ulong)(((uint)size.Height + groupSize - 1) / groupSize);
            uint dcGroupSize = groupSize * 8;
            ulong dcGroups = (((uint)size.Width + dcGroupSize - 1) / dcGroupSize) * (ulong)(((uint)size.Height + dcGroupSize - 1) / dcGroupSize);

            // A single group with one pass combines all sections. Otherwise the table has LF-global,
            // LF-group, HF-global and one section per group and pass. Widen before multiplying untrusted dimensions.
            ulong count = groups == 1 && this.PassCount == 1 ? 1 : 2 + dcGroups + (groups * this.PassCount);
            if (count > int.MaxValue)
            {
                throw new InvalidImageContentException("The JPEG XL frame has too many coding sections.");
            }

            return (int)count;
        }
    }

    /// <summary>
    /// Reads a frame header from its byte-aligned start using libjxl's field order and defaults.
    /// </summary>
    /// <param name="reader">The bit reader at the frame boundary.</param>
    /// <param name="image">The image-wide header.</param>
    /// <param name="preview">Whether this is the optional preview frame.</param>
    /// <param name="handling">The policy for an invalid optional frame label.</param>
    /// <returns>The frame's retained parsing and reconstruction state.</returns>
    public static JxlFrameHeader Read(ref JxlBitReader reader, JxlHeader image, bool preview, SegmentIntegrityHandling handling)
    {
        JxlFrameHeader frame = new(image, preview);
        if (reader.ReadBoolean())
        {
            // One flag replaces the complete serialized header with its defaults. The TOC permutation flag
            // follows immediately; the section-table parser consumes padding after that flag, not here.
            return frame;
        }

        frame.Type = (FrameType)reader.ReadBits(2);
        if (preview && frame.Type != FrameType.Regular)
        {
            throw new InvalidImageContentException("The JPEG XL preview must use a regular frame.");
        }

        frame.Modular = reader.ReadBoolean();
        frame.Flags = reader.ReadU64();

        // XYB is an image-wide choice. Only a non-XYB image carries this per-frame RGB/YCbCr selector.
        // A saved LF frame also supplies its JPEG sampling layout, so those sampling fields are then absent.
        frame.YCbCr = !image.UsesXybTransform && reader.ReadBoolean();
        if (frame.YCbCr && (frame.Flags & UseLowFrequencyFrame) == 0)
        {
            for (int i = 0; i < 3; i++)
            {
                frame.jpegSampling[i] = (byte)reader.ReadBits(2);
            }
        }

        if ((frame.Flags & UseLowFrequencyFrame) == 0)
        {
            frame.Upsampling = (byte)(1 << (int)reader.ReadBits(2));

            for (int i = 0; i < frame.ExtraChannelUpsampling.Length; i++)
            {
                // The frame factor multiplies the image-wide channel shift. Compare effective resolutions,
                // not just the encoded factors: extra channels cannot be finer than the color channels here.
                int scale = (1 << (int)reader.ReadBits(2)) << image.Metadata.ExtraChannels[i].DimensionShift;
                if (scale < frame.Upsampling || scale > 8)
                {
                    throw new InvalidImageContentException("The JPEG XL extra-channel upsampling factor is invalid.");
                }

                frame.ExtraChannelUpsampling[i] = (byte)scale;
            }
        }

        if (frame.Modular)
        {
            frame.GroupSizeShift = (byte)reader.ReadBits(2);

            // Modular coding does not use VarDCT quantization matrices, even when the image is XYB-encoded.
            // Both scale codes must therefore retain the native no-op value rather than the XYB VarDCT X default.
            frame.XQuantizationScale = frame.BQuantizationScale = 2;
        }
        else if (image.UsesXybTransform)
        {
            frame.XQuantizationScale = (byte)reader.ReadBits(3);
            frame.BQuantizationScale = (byte)reader.ReadBits(3);
        }

        if (frame.Type != FrameType.ReferenceOnly)
        {
            // Reference-only frames omit the progressive pass header and retain its one-pass defaults.
            frame.ReadPasses(ref reader);
        }

        if (frame.Type == FrameType.LowFrequency)
        {
            // LF frames describe a reduced-resolution pyramid level. They do not carry crop fields.
            frame.LowFrequencyLevel = (byte)(reader.ReadBits(2) + 1);
        }
        else
        {
            frame.HasCrop = reader.ReadBoolean();
            if (frame.HasCrop)
            {
                if (frame.Type != FrameType.ReferenceOnly)
                {
                    // Zigzag coding maps unsigned values to 0, -1, 1, -2, ... . Reference-only origins remain zero.
                    uint x = ReadCropField(ref reader);
                    uint y = ReadCropField(ref reader);
                    frame.Origin = new Point((int)(x >> 1) ^ -(int)(x & 1), (int)(y >> 1) ^ -(int)(y & 1));
                }

                uint width = ReadCropField(ref reader);
                uint height = ReadCropField(ref reader);
                if (width == 0 || height == 0)
                {
                    throw new InvalidImageContentException("The JPEG XL crop dimensions cannot be zero.");
                }

                frame.Size = new Size((int)width, (int)height);
                Size canvas = preview ? image.PreviewSize.GetValueOrDefault() : image.Size;

                // Origins are signed, while dimensions are positive. Widen their sums before testing canvas coverage.
                frame.CoversImage = frame.Origin.X <= 0 && frame.Origin.Y <= 0
                    && (long)frame.Origin.X + width >= canvas.Width && (long)frame.Origin.Y + height >= canvas.Height;
            }
        }

        bool regular = frame.Type is FrameType.Regular or FrameType.SkipProgressive;
        frame.IsLast = false;
        if (regular)
        {
            // Only display-capable frames carry blending, duration and the final-frame flag.
            // Every extra channel has its own blending instruction, independent of the color instruction.
            int extraCount = image.Metadata.ExtraChannels.Count;
            frame.Blending = BlendingInfo.Read(ref reader, extraCount, frame.CoversImage);
            bool replaceAll = frame.Blending.Mode == 0;

            for (int i = 0; i < frame.ExtraChannelBlending.Length; i++)
            {
                frame.ExtraChannelBlending[i] = BlendingInfo.Read(ref reader, extraCount, frame.CoversImage);
                replaceAll &= frame.ExtraChannelBlending[i].Mode == 0;
            }

            if (preview && (!replaceAll || frame.HasCrop))
            {
                throw new InvalidImageContentException("The JPEG XL preview cannot crop or blend with a reference.");
            }

            if (image.TicksPerSecond.HasValue)
            {
                // Retain integer ticks here. The image timebase determines seconds only at the metadata boundary.
                frame.Duration = reader.ReadU32((0, 0), (0, 1), (8, 0), (32, 0));
                if (image.HasTimeCodes)
                {
                    frame.TimeCode = reader.ReadBits(32);
                }
            }

            frame.IsLast = reader.ReadBoolean();
        }

        if (frame.Type != FrameType.LowFrequency && !frame.IsLast)
        {
            // Slot zero is reusable for a zero-duration intermediate frame. A displayed nonzero-duration
            // frame needs a nonzero slot to remain available as a later reference.
            frame.ReferenceSlot = (byte)reader.ReadBits(2);
        }

        bool referenced = !frame.IsLast && frame.Type != FrameType.LowFrequency && (frame.Duration == 0 || frame.ReferenceSlot != 0);
        if (frame.Type == FrameType.LowFrequency)
        {
            // LF reconstruction consumes samples before the color transform; there is no selector bit for this case.
            frame.SaveBeforeColorTransform = true;
        }
        else if (frame.Type == FrameType.ReferenceOnly || (referenced && regular && frame.Blending.Mode == 0 && frame.CoversImage))
        {
            // A complete replacement can be saved before or after color conversion. A partial or blended
            // display frame already contains post-transform background samples and cannot select the earlier stage.
            frame.SaveBeforeColorTransform = reader.ReadBoolean();
            if (frame.Type == FrameType.ReferenceOnly && !frame.SaveBeforeColorTransform
                && (frame.Size.Width < image.Size.Width || frame.Size.Height < image.Size.Height))
            {
                throw new InvalidImageContentException("The JPEG XL post-transform reference does not cover the image.");
            }
        }

        frame.Name = reader.ReadName(handling);
        frame.RestorationFilter = JxlRestorationFilter.Read(ref reader, frame.Modular);

        // Extensions belong to the frame header. The section table begins at the next bit;
        // its permutation flag must be read before the section-table parser consumes byte padding.
        reader.SkipExtensions();

        return frame;
    }

    /// <summary>
    /// Reads a crop dimension or packed signed origin using the frame's shared U32 coding.
    /// </summary>
    /// <param name="reader">The frame bit reader.</param>
    /// <returns>The encoded unsigned value.</returns>
    private static uint ReadCropField(ref JxlBitReader reader) => reader.ReadU32((8, 0), (11, 256), (14, 2304), (30, 18688));

    /// <summary>
    /// Reads progressive pass controls and validates their ordering before later section indexing uses them.
    /// </summary>
    /// <param name="reader">The frame bit reader.</param>
    private void ReadPasses(ref JxlBitReader reader)
    {
        this.PassCount = (byte)reader.ReadU32((0, 1), (0, 2), (0, 3), (3, 4));
        if (this.PassCount == 1)
        {
            return;
        }

        this.downsamplingCount = (byte)reader.ReadU32((0, 0), (0, 1), (0, 2), (1, 3));
        if (this.downsamplingCount > this.PassCount)
        {
            throw new InvalidImageContentException("The JPEG XL downsampling stage count exceeds its pass count.");
        }

        // The last pass has no shift field: the zero-initialized final entry gives full coefficient precision.
        for (int i = 0; i < this.PassCount - 1; i++)
        {
            this.passShifts[i] = (byte)reader.ReadBits(2);
        }

        // Display stages approach full resolution. Their power-of-two downsampling factors must strictly decrease.
        for (int i = 0; i < this.downsamplingCount; i++)
        {
            byte factor = (byte)(1 << (int)reader.ReadBits(2));
            if (i != 0 && factor >= this.downsampling[i - 1])
            {
                throw new InvalidImageContentException("The JPEG XL progressive downsampling factors are not decreasing.");
            }

            this.downsampling[i] = factor;
        }

        // Each display stage ends at a later pass, and every boundary must name an existing pass.
        for (int i = 0; i < this.downsamplingCount; i++)
        {
            byte pass = (byte)reader.ReadU32((0, 0), (0, 1), (0, 2), (3, 0));
            if (pass >= this.PassCount || (i != 0 && pass <= this.lastDownsamplingPass[i - 1]))
            {
                throw new InvalidImageContentException("The JPEG XL progressive pass boundaries are invalid.");
            }

            this.lastDownsamplingPass[i] = pass;
        }
    }

    /// <summary>
    /// Retains one color or extra-channel blending instruction. Its selected weight need not be displayed alpha.
    /// </summary>
    public readonly struct BlendingInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BlendingInfo"/> struct from validated fields.
        /// </summary>
        /// <param name="mode">The native mode: replace, add, blend, alpha-weighted add or multiply.</param>
        /// <param name="alphaChannel">The selected extra-channel index.</param>
        /// <param name="clamp">Whether reconstruction bounds the selected weight.</param>
        /// <param name="source">The reference frame slot.</param>
        private BlendingInfo(byte mode, byte alphaChannel, bool clamp, byte source)
        {
            this.Mode = mode;
            this.AlphaChannel = alphaChannel;
            this.Clamp = clamp;
            this.Source = source;
        }

        /// <summary>
        /// Gets the encoded blending mode, from zero through four.
        /// </summary>
        public byte Mode { get; }

        /// <summary>
        /// Gets the extra channel supplying the selected weight.
        /// </summary>
        public byte AlphaChannel { get; }

        /// <summary>
        /// Gets a value indicating whether reconstruction bounds the selected weight or multiply operand to [0,1].
        /// </summary>
        public bool Clamp { get; }

        /// <summary>
        /// Gets the reference frame slot, from zero through three.
        /// </summary>
        public byte Source { get; }

        /// <summary>
        /// Reads one blending instruction and validates references to extra channels.
        /// </summary>
        /// <param name="reader">The frame bit reader.</param>
        /// <param name="extraChannels">The image's extra-channel count.</param>
        /// <param name="fullFrame">Whether this frame covers the image.</param>
        /// <returns>The parsed instruction.</returns>
        public static BlendingInfo Read(ref JxlBitReader reader, int extraChannels, bool fullFrame)
        {
            uint mode = reader.ReadU32((0, 0), (0, 1), (0, 2), (2, 3));
            if (mode > 4)
            {
                throw new InvalidImageContentException("The JPEG XL blending mode is invalid.");
            }

            uint alpha = 0;

            // This index selects a codec extra channel. Its values act as weights; they do not change the
            // shared pixel API's alpha contract. With no extra channel, the format supplies an opaque weight.
            bool weighted = extraChannels > 0 && mode is 2 or 3;
            if (weighted)
            {
                alpha = reader.ReadU32((0, 0), (0, 1), (0, 2), (3, 3));
                if (alpha >= extraChannels)
                {
                    throw new InvalidImageContentException("The JPEG XL blending weight refers to a missing extra channel.");
                }
            }

            // Multiply applies the clamp to its channel operand. Weighted modes apply it to the selected weight.
            // A full-frame replacement needs no background source, so its reference-slot field is absent.
            bool clamp = (weighted || mode == 4) && reader.ReadBoolean();
            byte source = !fullFrame || mode != 0 ? (byte)reader.ReadBits(2) : (byte)0;
            return new BlendingInfo((byte)mode, (byte)alpha, clamp, source);
        }
    }
}
