// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Cur;
using SixLabors.ImageSharp.Formats.Ico;
using SixLabors.ImageSharp.Formats.Icon;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp.Formats.Ani;

/// <summary>
/// Performs ANI encoding.
/// </summary>
internal sealed class AniEncoderCore
{
    private readonly AniEncoder encoder;

    // The ANI encoder configures each nested icon encoder once and uses it for every resource of that type.
    private IcoEncoderCore? icoEncoder;
    private CurEncoderCore? curEncoder;

    /// <summary>
    /// Reusable storage for the fixed ANI header and smaller RIFF values.
    /// </summary>
    private InlineArray36<byte> buffer;

    /// <summary>
    /// Initializes a new instance of the <see cref="AniEncoderCore"/> class.
    /// </summary>
    /// <param name="encoder">The encoder options.</param>
    public AniEncoderCore(AniEncoder encoder)
        => this.encoder = encoder;

    /// <summary>
    /// Encodes an image as ANI data.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel type.</typeparam>
    /// <param name="image">The source image.</param>
    /// <param name="stream">The destination stream.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public void Encode<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Guard.NotNull(image, nameof(image));
        Guard.NotNull(stream, nameof(stream));

        AniMetadata imageMetadata = image.Metadata.GetAniMetadata();
        AniFrameMetadata firstMetadata = image.Frames.RootFrame.Metadata.GetAniMetadata();
        uint displayRate = firstMetadata.FrameDelay is 0 ? imageMetadata.DisplayRate : firstMetadata.FrameDelay;
        bool hasVariableRates = false;
        bool reusesFrames = false;
        int stepCount = 0;
        int resourceCount = 0;
        int maxGroupSize = 1;

        // Each step has one frame or more, so the frame count bounds the step count.
        using IMemoryOwner<AniStep> stepsOwner = image.Configuration.MemoryAllocator.Allocate<AniStep>(image.Frames.Count);
        Span<AniStep> steps = stepsOwner.GetSpan();

        for (int frameIndex = 0; frameIndex < image.Frames.Count;)
        {
            AniFrameMetadata metadata = image.Frames[frameIndex].Metadata.GetAniMetadata();

            if (metadata.FrameFormat is not (AniFrameFormat.Ico or AniFrameFormat.Cur))
            {
                // FrameFormat is public metadata and therefore must be validated before any container bytes are written.
                throw new ImageFormatException("ANI contains an unsupported embedded frame format.");
            }

            int groupSize = GetStepLength(image, frameIndex);

            // All variants share one animation step, which requires one child format and one rate value.
            for (int i = 1; i < groupSize; i++)
            {
                AniFrameMetadata current = image.Frames[frameIndex + i].Metadata.GetAniMetadata();
                if (current.FrameFormat != metadata.FrameFormat)
                {
                    throw new ImageFormatException("ANI resolution variants must use the same embedded format.");
                }

                if (current.FrameDelay != metadata.FrameDelay)
                {
                    throw new ImageFormatException("ANI resolution variants must use the same frame delay.");
                }
            }

            // The icon encoder owns the rules of each icon entry. The ANI encoder applies them before it writes the RIFF header. Thus, if a frame has invalid
            // metadata, the encoder writes no bytes.
            AniIconFrameMetadataProvider provider = new(metadata.FrameFormat);
            for (int i = 0; i < groupSize; i++)
            {
                ImageFrame frame = image.Frames[frameIndex + i];
                IconEncoderCore.ValidateEntry(provider.GetEncodingFrameMetadata(frame), frame.Size);
            }

            // Equal positive sequence numbers mark one stored frame. The first step with a number stores the frame. Each
            // later step with that number plays the stored frame again, so the encoder does not write its frames. A
            // linear search over the earlier steps finds that stored frame without an allocation.
            int resourceIndex = resourceCount;
            if (metadata.SequenceNumber > 0)
            {
                for (int i = 0; i < stepCount; i++)
                {
                    if (steps[i].SequenceNumber == metadata.SequenceNumber)
                    {
                        resourceIndex = steps[i].ResourceIndex;
                        reusesFrames = true;
                        break;
                    }
                }
            }

            if (resourceIndex == resourceCount)
            {
                resourceCount++;
                maxGroupSize = Math.Max(maxGroupSize, groupSize);
            }

            steps[stepCount++] = new AniStep(frameIndex, groupSize, resourceIndex, metadata.SequenceNumber);

            uint frameDelay = metadata.FrameDelay is 0 ? displayRate : metadata.FrameDelay;
            hasVariableRates |= frameDelay != displayRate;
            frameIndex += groupSize;
        }

        steps = steps[..stepCount];

        // The ACON specification defines each frame as an embedded .CUR file (page 9), so the encoder always sets
        // AF_ICON. It marks cx, cy, cBitCount and cPlanes as "reserved, must be 0" (page 10). Each ICO or CUR frame holds
        // those values instead. The 'seq ' chunk gives the stored frame of each step, so it is necessary only when a
        // step plays a stored frame again. See the Multimedia Data Standards Update, April 15, 1994, pages 9-10:
        // https://www.mmsp.ece.mcgill.ca/Documents/AudioFormats/WAVE/Docs/RIFFNEW.pdf
        AniHeader header = new()
        {
            BytesInHeader = AniHeader.Size,
            FrameCount = (uint)resourceCount,
            StepCount = (uint)stepCount,
            DisplayRate = displayRate,
            Flags = AniHeaderFlags.IsIcon | (reusesFrames ? AniHeaderFlags.ContainsSequence : 0)
        };

        // One allocator-owned directory buffer is sliced and reused for every icon resource; its capacity is the largest group.
        using IMemoryOwner<IconEncoderCore.EncodingFrameMetadata> iconEntriesOwner =
            image.Configuration.MemoryAllocator.Allocate<IconEncoderCore.EncodingFrameMetadata>(maxGroupSize);

        Span<IconEncoderCore.EncodingFrameMetadata> iconEntries = iconEntriesOwner.GetSpan();

        // ImageEncoder guarantees a seekable destination, allowing direct nested encoding and RIFF size backpatching.
        long riffSizePosition = BeginContainer(stream, AniConstants.RiffFourCc, AniConstants.AniFormTypeFourCc);
        this.WriteHeader(stream, header);

        if (reusesFrames)
        {
            this.WriteSequence(stream, steps);
        }

        if (hasVariableRates)
        {
            this.WriteRates(stream, image, steps, displayRate);
        }

        if (!this.encoder.SkipMetadata && (imageMetadata.Name is not null || imageMetadata.Artist is not null))
        {
            WriteInfoList(stream, imageMetadata, image.Configuration.MemoryAllocator);
        }

        long frameListSizePosition = BeginContainer(stream, "LIST"u8, "fram"u8);

        // The encoder writes each stored frame once, from its first step. Stored frames get their indices in this order.
        int written = 0;
        foreach (AniStep step in steps)
        {
            if (step.ResourceIndex != written)
            {
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();

            AniFrameFormat format = image.Frames[step.FrameIndex].Metadata.GetAniMetadata().FrameFormat;
            long frameSizePosition = RiffHelper.BeginWriteChunk(stream, "icon"u8);
            this.WriteFrameResource(image, stream, step.FrameIndex, step.FrameCount, format, iconEntries, cancellationToken);
            RiffHelper.EndWriteChunk(stream, frameSizePosition);
            written++;
        }

        RiffHelper.EndWriteChunk(stream, frameListSizePosition);
        RiffHelper.EndWriteChunk(stream, riffSizePosition);
    }

    /// <summary>
    /// Gets the frame count of the animation step that starts at a frame.
    /// </summary>
    /// <remarks>
    /// A step holds the resolution variants of one stored frame: adjacent frames with the same positive sequence number.
    /// A stored frame can play in two adjacent steps, so equal numbers alone do not end a step. The images of one icon
    /// each have "a different size and/or color depth". Thus an image whose size and depth are already in the step
    /// starts the next step: https://learn.microsoft.com/en-us/previous-versions/ms997538(v=msdn.10)
    /// </remarks>
    /// <param name="image">The source image.</param>
    /// <param name="frameIndex">The first frame of the step.</param>
    /// <returns>The frame count of the step.</returns>
    private static int GetStepLength(Image image, int frameIndex)
    {
        int sequenceNumber = image.Frames[frameIndex].Metadata.GetAniMetadata().SequenceNumber;
        if (sequenceNumber <= 0)
        {
            // A non-positive value encodes the frame as its own animation step.
            return 1;
        }

        int end = frameIndex + 1;
        while (end < image.Frames.Count)
        {
            ImageFrame next = image.Frames[end];
            if (next.Metadata.GetAniMetadata().SequenceNumber != sequenceNumber)
            {
                break;
            }

            for (int i = frameIndex; i < end; i++)
            {
                if (IsSameIconImage(image.Frames[i], next))
                {
                    return end - frameIndex;
                }
            }

            end++;
        }

        return end - frameIndex;
    }

    /// <summary>
    /// Gets a value indicating whether two frames give the same icon image: the same encoded size and the same depth.
    /// One icon cannot hold two such images.
    /// </summary>
    /// <param name="a">The first frame.</param>
    /// <param name="b">The second frame.</param>
    /// <returns><see langword="true"/> if the frames give the same icon image. Otherwise, <see langword="false"/>.</returns>
    private static bool IsSameIconImage(ImageFrame a, ImageFrame b)
    {
        AniFrameMetadata first = a.Metadata.GetAniMetadata();
        AniFrameMetadata second = b.Metadata.GetAniMetadata();

        // A PNG image is always 32 bits per pixel in an icon.
        int firstDepth = first.Compression is IconFrameCompression.Png ? 32 : (int)first.BmpBitsPerPixel;
        int secondDepth = second.Compression is IconFrameCompression.Png ? 32 : (int)second.BmpBitsPerPixel;

        return IconDirEntry.ClampEncodingDimension(first.EncodingWidth ?? a.Width) == IconDirEntry.ClampEncodingDimension(second.EncodingWidth ?? b.Width)
            && IconDirEntry.ClampEncodingDimension(first.EncodingHeight ?? a.Height) == IconDirEntry.ClampEncodingDimension(second.EncodingHeight ?? b.Height)
            && firstDepth == secondDepth;
    }

    /// <summary>
    /// Writes the fixed-size ANI animation header chunk.
    /// </summary>
    /// <param name="stream">The destination stream.</param>
    /// <param name="header">The animation header.</param>
    private void WriteHeader(Stream stream, AniHeader header)
    {
        long sizePosition = RiffHelper.BeginWriteChunk(stream, "anih"u8);
        Span<byte> data = this.buffer;
        header.WriteTo(data);
        stream.Write(data);
        RiffHelper.EndWriteChunk(stream, sizePosition);
    }

    /// <summary>
    /// Writes the stored frame index of each animation step.
    /// </summary>
    /// <param name="stream">The destination stream.</param>
    /// <param name="steps">The animation steps.</param>
    private void WriteSequence(Stream stream, ReadOnlySpan<AniStep> steps)
    {
        long sizePosition = RiffHelper.BeginWriteChunk(stream, "seq "u8);
        Span<byte> value = this.buffer[..sizeof(uint)];

        foreach (AniStep step in steps)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(value, (uint)step.ResourceIndex);
            stream.Write(value);
        }

        RiffHelper.EndWriteChunk(stream, sizePosition);
    }

    /// <summary>
    /// Writes per-step rates when they cannot be represented by one header value.
    /// </summary>
    /// <param name="stream">The destination stream.</param>
    /// <param name="image">The source image.</param>
    /// <param name="steps">The animation steps.</param>
    /// <param name="displayRate">The default header display rate.</param>
    private void WriteRates(Stream stream, Image image, ReadOnlySpan<AniStep> steps, uint displayRate)
    {
        long sizePosition = RiffHelper.BeginWriteChunk(stream, "rate"u8);
        Span<byte> value = this.buffer[..sizeof(uint)];

        // The rate table contains one DWORD per animation step, not one value per resolution variant.
        foreach (AniStep step in steps)
        {
            uint frameDelay = image.Frames[step.FrameIndex].Metadata.GetAniMetadata().FrameDelay;
            BinaryPrimitives.WriteUInt32LittleEndian(value, frameDelay is 0 ? displayRate : frameDelay);
            stream.Write(value);
        }

        RiffHelper.EndWriteChunk(stream, sizePosition);
    }

    /// <summary>
    /// Writes the optional ANI name and artist list.
    /// </summary>
    /// <param name="stream">The destination stream.</param>
    /// <param name="metadata">The ANI image metadata.</param>
    /// <param name="memoryAllocator">The allocator used for the text buffer.</param>
    private static void WriteInfoList(Stream stream, AniMetadata metadata, MemoryAllocator memoryAllocator)
    {
        long sizePosition = BeginContainer(stream, "LIST"u8, "INFO"u8);
        int nameLength = metadata.Name is null ? 0 : Encoding.ASCII.GetByteCount(metadata.Name);
        int artistLength = metadata.Artist is null ? 0 : Encoding.ASCII.GetByteCount(metadata.Artist);

        // Name and artist are emitted sequentially, so a single buffer sized for the larger value avoids a second allocation.
        using IMemoryOwner<byte> owner = memoryAllocator.Allocate<byte>(Math.Max(nameLength, artistLength));
        Span<byte> buffer = owner.GetSpan();

        if (metadata.Name is not null)
        {
            WriteTextChunk(stream, "INAM"u8, metadata.Name, buffer);
        }

        if (metadata.Artist is not null)
        {
            WriteTextChunk(stream, "IART"u8, metadata.Artist, buffer);
        }

        RiffHelper.EndWriteChunk(stream, sizePosition);
    }

    /// <summary>
    /// Writes a null-terminated ASCII RIFF information chunk.
    /// </summary>
    /// <param name="stream">The destination stream.</param>
    /// <param name="fourCc">The chunk identifier.</param>
    /// <param name="value">The text value.</param>
    /// <param name="buffer">The reusable text buffer.</param>
    private static void WriteTextChunk(Stream stream, ReadOnlySpan<byte> fourCc, string value, Span<byte> buffer)
    {
        long sizePosition = RiffHelper.BeginWriteChunk(stream, fourCc);
        int written = Encoding.ASCII.GetBytes(value, buffer);
        stream.Write(buffer[..written]);

        // The terminating zero belongs to the RIFF text payload and is therefore included in the backpatched chunk size.
        stream.WriteByte(0);

        RiffHelper.EndWriteChunk(stream, sizePosition);
    }

    /// <summary>
    /// Encodes one ANI frame resource using the existing ICO or CUR encoder.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel type.</typeparam>
    /// <param name="image">The source image.</param>
    /// <param name="stream">The destination stream.</param>
    /// <param name="frameIndex">The first source-frame index.</param>
    /// <param name="frameCount">The number of source frames in this resource.</param>
    /// <param name="format">The embedded resource format: <see cref="AniFrameFormat.Ico"/> or <see cref="AniFrameFormat.Cur"/>.</param>
    /// <param name="iconEntries">The reusable icon directory metadata buffer.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    private void WriteFrameResource<TPixel>(
        Image<TPixel> image,
        Stream stream,
        int frameIndex,
        int frameCount,
        AniFrameFormat format,
        Span<IconEncoderCore.EncodingFrameMetadata> iconEntries,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // The child encoder gets only the active prefix. Later resources use the same allocation.
        Span<IconEncoderCore.EncodingFrameMetadata> entries = iconEntries[..frameCount];
        AniIconFrameMetadataProvider provider = new(format);

        if (format is AniFrameFormat.Ico)
        {
            this.icoEncoder ??= new IcoEncoderCore(new IcoEncoder
            {
                PixelSamplingStrategy = this.encoder.PixelSamplingStrategy,
                Quantizer = this.encoder.Quantizer,
                SkipMetadata = this.encoder.SkipMetadata,
                TransparentColorMode = this.encoder.TransparentColorMode
            });

            this.icoEncoder.Encode(image, stream, frameIndex, entries, provider, cancellationToken);
        }
        else
        {
            this.curEncoder ??= new CurEncoderCore(new CurEncoder
            {
                PixelSamplingStrategy = this.encoder.PixelSamplingStrategy,
                Quantizer = this.encoder.Quantizer,
                SkipMetadata = this.encoder.SkipMetadata,
                TransparentColorMode = this.encoder.TransparentColorMode
            });

            this.curEncoder.Encode(image, stream, frameIndex, entries, provider, cancellationToken);
        }
    }

    /// <summary>
    /// Creates an icon directory entry from ANI-owned frame metadata.
    /// </summary>
    /// <param name="metadata">The ANI frame metadata.</param>
    /// <param name="format">The embedded icon format.</param>
    /// <param name="size">The source frame size.</param>
    /// <returns>The icon directory entry.</returns>
    private static IconDirEntry CreateIconDirEntry(AniFrameMetadata metadata, AniFrameFormat format, Size size)
    {
        // PNG and direct-color bitmap entries do not declare a palette; indexed bitmap entries advertise their color count.
        byte colorCount = metadata.Compression is IconFrameCompression.Png || metadata.BmpBitsPerPixel > BmpBitsPerPixel.Bit8
            ? (byte)0
            : (byte)ColorNumerics.GetColorCountForBitDepth((int)metadata.BmpBitsPerPixel);

        // ICO stores planes/BPP in these fields, while CUR reuses the same two words for the hotspot coordinates.
        return new IconDirEntry
        {
            Width = IconDirEntry.ClampEncodingDimension(metadata.EncodingWidth ?? size.Width),
            Height = IconDirEntry.ClampEncodingDimension(metadata.EncodingHeight ?? size.Height),
            ColorCount = colorCount,
            Planes = format is AniFrameFormat.Ico ? (ushort)1 : metadata.HotspotX,
            BitCount = format is AniFrameFormat.Ico
                ? metadata.Compression is IconFrameCompression.Bmp ? (ushort)metadata.BmpBitsPerPixel : (ushort)32
                : metadata.HotspotY
        };
    }

    /// <summary>
    /// Begins a RIFF container chunk and writes its form or list type.
    /// </summary>
    /// <param name="stream">The destination stream.</param>
    /// <param name="fourCc">The container identifier.</param>
    /// <param name="type">The container form or list type.</param>
    /// <returns>The stream position of the container-size field.</returns>
    private static long BeginContainer(Stream stream, ReadOnlySpan<byte> fourCc, ReadOnlySpan<byte> type)
    {
        long sizePosition = RiffHelper.BeginWriteChunk(stream, fourCc);
        stream.Write(type);
        return sizePosition;
    }

    /// <summary>
    /// One animation step: the frames of the step and the stored frame that it plays.
    /// </summary>
    private readonly struct AniStep
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AniStep"/> struct.
        /// </summary>
        /// <param name="frameIndex">The first frame of the step.</param>
        /// <param name="frameCount">The frame count of the step.</param>
        /// <param name="resourceIndex">The index of the stored frame that the step plays.</param>
        /// <param name="sequenceNumber">The sequence number of the frames of the step.</param>
        public AniStep(int frameIndex, int frameCount, int resourceIndex, int sequenceNumber)
        {
            this.FrameIndex = frameIndex;
            this.FrameCount = frameCount;
            this.ResourceIndex = resourceIndex;
            this.SequenceNumber = sequenceNumber;
        }

        /// <summary>
        /// Gets the first frame of the step.
        /// </summary>
        public int FrameIndex { get; }

        /// <summary>
        /// Gets the frame count of the step.
        /// </summary>
        public int FrameCount { get; }

        /// <summary>
        /// Gets the index of the stored frame that the step plays.
        /// </summary>
        public int ResourceIndex { get; }

        /// <summary>
        /// Gets the sequence number of the frames of the step. The reuse search compares this value, so it does not read
        /// the frame metadata again.
        /// </summary>
        public int SequenceNumber { get; }
    }

    /// <summary>
    /// Projects ANI-owned metadata into the icon encoder without allocating intermediary metadata objects.
    /// </summary>
    private readonly struct AniIconFrameMetadataProvider : IconEncoderCore.IEncodingFrameMetadataProvider
    {
        private readonly AniFrameFormat format;

        /// <summary>
        /// Initializes a new instance of the <see cref="AniIconFrameMetadataProvider"/> struct.
        /// </summary>
        /// <param name="format">The embedded icon format.</param>
        public AniIconFrameMetadataProvider(AniFrameFormat format)
            => this.format = format;

        /// <inheritdoc/>
        public IconEncoderCore.EncodingFrameMetadata GetEncodingFrameMetadata(ImageFrame frame)
        {
            AniFrameMetadata metadata = frame.Metadata.GetAniMetadata();

            return new IconEncoderCore.EncodingFrameMetadata(
                metadata.Compression,
                metadata.BmpBitsPerPixel,
                CreateIconDirEntry(metadata, this.format, frame.Size));
        }

        /// <inheritdoc/>
        public ReadOnlyMemory<Color>? GetColorTable(ImageFrame frame) => frame.Metadata.GetAniMetadata().ColorTable;
    }
}
