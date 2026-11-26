// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Cur;
using SixLabors.ImageSharp.Formats.Ico;
using SixLabors.ImageSharp.Formats.Icon;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Ani;

/// <summary>
/// Performs ANI encoding.
/// </summary>
internal sealed class AniEncoderCore
{
    private readonly AniEncoder encoder;

    // Each nested encoder is configured once and reused for every resource of that type in this ANI operation.
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
        bool hasBitmapFrames = false;
        bool writeSequence = imageMetadata.Flags.HasFlag(AniHeaderFlags.ContainsSequence);
        uint displayRate = firstMetadata.FrameDelay is 0 ? imageMetadata.DisplayRate : firstMetadata.FrameDelay;
        bool hasVariableRates = false;
        int groupCount = 0;
        int maxGroupSize = 1;

        // This validation pass derives the fixed ANI header and largest icon directory without allocating a grouping graph.
        // Encoding repeats the linear grouping scan below, trading a cheap pass for zero per-group collections.
        for (int frameIndex = 0; frameIndex < image.Frames.Count;)
        {
            AniFrameMetadata metadata = image.Frames[frameIndex].Metadata.GetAniMetadata();
            int groupSize = 1;

            if (metadata.FrameFormat is not (AniFrameFormat.Ico or AniFrameFormat.Cur or AniFrameFormat.Bmp))
            {
                // FrameFormat is public metadata and therefore must be validated before any container bytes are written.
                throw new ImageFormatException("ANI contains an unsupported embedded frame format.");
            }

            // Positive sequence numbers group adjacent resolution variants; non-positive values form independent steps.
            if (metadata.SequenceNumber > 0)
            {
                while (frameIndex + groupSize < image.Frames.Count && image.Frames[frameIndex + groupSize].Metadata.GetAniMetadata().SequenceNumber == metadata.SequenceNumber)
                {
                    groupSize++;
                }
            }

            hasBitmapFrames |= metadata.FrameFormat is AniFrameFormat.Bmp;

            // All variants share one animation step, which requires one child format and one rate value.
            for (int i = 1; i < groupSize; i++)
            {
                AniFrameMetadata current = image.Frames[frameIndex + i].Metadata.GetAniMetadata();
                if (GetEncodedFrameFormat(current.FrameFormat) != GetEncodedFrameFormat(metadata.FrameFormat))
                {
                    throw new ImageFormatException("ANI resolution variants must use the same embedded format.");
                }

                if (current.FrameDelay != metadata.FrameDelay)
                {
                    throw new ImageFormatException("ANI resolution variants must use the same frame delay.");
                }
            }

            uint frameDelay = metadata.FrameDelay is 0 ? displayRate : metadata.FrameDelay;
            hasVariableRates |= frameDelay != displayRate;
            maxGroupSize = Math.Max(maxGroupSize, groupSize);
            groupCount++;
            frameIndex += groupSize;
        }

        // Bitmap frames are written as CUR frames that hold a bitmap of the declared bit depth.
        if (hasBitmapFrames && imageMetadata.BitCount is not (0 or 1 or 2 or 4 or 8 or 16 or 24 or 32))
        {
            throw new ImageFormatException("ANI bitmap resources require a supported bit depth.");
        }

        BmpBitsPerPixel bitmapBitsPerPixel = GetBmpBitsPerPixel(imageMetadata.BitCount);

        // The ACON specification defines each frame as an embedded .CUR file, so the encoder always sets AF_ICON. It marks
        // cx, cy, cBitCount and cPlanes as reserved, must be 0, because each ICO or CUR frame holds those values. See the
        // Multimedia Data Standards Update, April 15, 1994, page 9:
        // https://www.mmsp.ece.mcgill.ca/Documents/AudioFormats/WAVE/Docs/RIFFNEW.pdf
        AniHeader header = new()
        {
            BytesInHeader = AniHeader.Size,
            FrameCount = (uint)groupCount,
            StepCount = (uint)groupCount,
            DisplayRate = displayRate,
            Flags = AniHeaderFlags.IsIcon | (writeSequence ? AniHeaderFlags.ContainsSequence : 0)
        };

        // One allocator-owned directory buffer is sliced and reused for every icon resource; its capacity is the largest group.
        using IMemoryOwner<IconEncoderCore.EncodingFrameMetadata> iconEntriesOwner = image.Configuration.MemoryAllocator.Allocate<IconEncoderCore.EncodingFrameMetadata>(maxGroupSize);
        Span<IconEncoderCore.EncodingFrameMetadata> iconEntries = iconEntriesOwner.GetSpan();

        // ImageEncoder guarantees a seekable destination, allowing direct nested encoding and RIFF size backpatching.
        long riffSizePosition = this.BeginContainer(stream, AniConstants.RiffFourCc, AniConstants.AniFormTypeFourCc);
        this.WriteHeader(stream, header);

        if (writeSequence)
        {
            this.WriteSequence(stream, groupCount);
        }

        if (hasVariableRates)
        {
            this.WriteRates(stream, image, displayRate);
        }

        if (!this.encoder.SkipMetadata && (imageMetadata.Name is not null || imageMetadata.Artist is not null))
        {
            this.WriteInfoList(stream, imageMetadata, image.Configuration.MemoryAllocator);
        }

        long frameListSizePosition = this.BeginContainer(stream, "LIST"u8, "fram"u8);

        // Repeat the allocation-free adjacent grouping scan used by the validation pass.
        for (int frameIndex = 0; frameIndex < image.Frames.Count;)
        {
            cancellationToken.ThrowIfCancellationRequested();

            AniFrameMetadata metadata = image.Frames[frameIndex].Metadata.GetAniMetadata();
            int groupSize = 1;
            if (metadata.SequenceNumber > 0)
            {
                while (frameIndex + groupSize < image.Frames.Count && image.Frames[frameIndex + groupSize].Metadata.GetAniMetadata().SequenceNumber == metadata.SequenceNumber)
                {
                    groupSize++;
                }
            }

            long frameSizePosition = this.BeginChunk(stream, "icon"u8);
            this.WriteFrameResource(image, stream, frameIndex, groupSize, GetEncodedFrameFormat(metadata.FrameFormat), bitmapBitsPerPixel, iconEntries, cancellationToken);
            this.EndChunk(stream, frameSizePosition);
            frameIndex += groupSize;
        }

        this.EndChunk(stream, frameListSizePosition);
        this.EndChunk(stream, riffSizePosition);
    }

    /// <summary>
    /// Writes the fixed-size ANI animation header chunk.
    /// </summary>
    /// <param name="stream">The destination stream.</param>
    /// <param name="header">The animation header.</param>
    private void WriteHeader(Stream stream, AniHeader header)
    {
        long sizePosition = this.BeginChunk(stream, "anih"u8);
        Span<byte> data = this.buffer;
        header.WriteTo(data);
        stream.Write(data);
        this.EndChunk(stream, sizePosition);
    }

    /// <summary>
    /// Writes an identity sequence table when the source metadata declares an explicit sequence.
    /// </summary>
    /// <param name="stream">The destination stream.</param>
    /// <param name="stepCount">The number of animation steps.</param>
    private void WriteSequence(Stream stream, int stepCount)
    {
        long sizePosition = this.BeginChunk(stream, "seq "u8);
        Span<byte> value = this.buffer[..sizeof(uint)];

        // Decoding expands source resource references into presentation order. Encoding writes those expanded steps as
        // distinct resources, so an identity table preserves the explicit-sequence flag without changing playback.
        for (uint i = 0; i < stepCount; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(value, i);
            stream.Write(value);
        }

        this.EndChunk(stream, sizePosition);
    }

    /// <summary>
    /// Writes per-step rates when they cannot be represented by one header value.
    /// </summary>
    /// <param name="stream">The destination stream.</param>
    /// <param name="image">The source image.</param>
    /// <param name="displayRate">The default header display rate.</param>
    private void WriteRates(Stream stream, Image image, uint displayRate)
    {
        long sizePosition = this.BeginChunk(stream, "rate"u8);
        Span<byte> value = this.buffer[..sizeof(uint)];

        // The rate table contains one DWORD per animation step, not one value per resolution variant.
        for (int frameIndex = 0; frameIndex < image.Frames.Count;)
        {
            AniFrameMetadata metadata = image.Frames[frameIndex].Metadata.GetAniMetadata();
            uint frameDelay = metadata.FrameDelay;
            BinaryPrimitives.WriteUInt32LittleEndian(value, frameDelay is 0 ? displayRate : frameDelay);
            stream.Write(value);

            frameIndex++;
            if (metadata.SequenceNumber > 0)
            {
                while (frameIndex < image.Frames.Count && image.Frames[frameIndex].Metadata.GetAniMetadata().SequenceNumber == metadata.SequenceNumber)
                {
                    frameIndex++;
                }
            }
        }

        this.EndChunk(stream, sizePosition);
    }

    /// <summary>
    /// Writes the optional ANI name and artist list.
    /// </summary>
    /// <param name="stream">The destination stream.</param>
    /// <param name="metadata">The ANI image metadata.</param>
    /// <param name="memoryAllocator">The allocator used for the text buffer.</param>
    private void WriteInfoList(Stream stream, AniMetadata metadata, MemoryAllocator memoryAllocator)
    {
        long sizePosition = this.BeginContainer(stream, "LIST"u8, "INFO"u8);
        int nameLength = metadata.Name is null ? 0 : Encoding.ASCII.GetByteCount(metadata.Name);
        int artistLength = metadata.Artist is null ? 0 : Encoding.ASCII.GetByteCount(metadata.Artist);

        // Name and artist are emitted sequentially, so a single buffer sized for the larger value avoids a second allocation.
        using IMemoryOwner<byte> owner = memoryAllocator.Allocate<byte>(Math.Max(nameLength, artistLength));
        Span<byte> buffer = owner.GetSpan();

        if (metadata.Name is not null)
        {
            this.WriteTextChunk(stream, "INAM"u8, metadata.Name, buffer);
        }

        if (metadata.Artist is not null)
        {
            this.WriteTextChunk(stream, "IART"u8, metadata.Artist, buffer);
        }

        this.EndChunk(stream, sizePosition);
    }

    /// <summary>
    /// Writes a null-terminated ASCII RIFF information chunk.
    /// </summary>
    /// <param name="stream">The destination stream.</param>
    /// <param name="fourCc">The chunk identifier.</param>
    /// <param name="value">The text value.</param>
    /// <param name="buffer">The reusable text buffer.</param>
    private void WriteTextChunk(Stream stream, ReadOnlySpan<byte> fourCc, string value, Span<byte> buffer)
    {
        long sizePosition = this.BeginChunk(stream, fourCc);
        int written = Encoding.ASCII.GetBytes(value, buffer);
        stream.Write(buffer[..written]);

        // The terminating zero belongs to the RIFF text payload and is therefore included in the backpatched chunk size.
        stream.WriteByte(0);

        this.EndChunk(stream, sizePosition);
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
    /// <param name="bitmapBitsPerPixel">The bit depth of the bitmap frames, from the ANI header bit count.</param>
    /// <param name="iconEntries">The reusable icon directory metadata buffer.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    private void WriteFrameResource<TPixel>(Image<TPixel> image, Stream stream, int frameIndex, int frameCount, AniFrameFormat format, BmpBitsPerPixel bitmapBitsPerPixel, Span<IconEncoderCore.EncodingFrameMetadata> iconEntries, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Only the active prefix is exposed to the child encoder; the same backing allocation serves later resources.
        Span<IconEncoderCore.EncodingFrameMetadata> entries = iconEntries[..frameCount];
        AniIconFrameMetadataProvider provider = new(format, bitmapBitsPerPixel);

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
    /// Gets the format that the encoder writes for a frame.
    /// </summary>
    /// <remarks>
    /// The ACON specification defines each 'icon' chunk as an embedded .CUR file and does not define raw bitmap frames.
    /// See the Multimedia Data Standards Update, April 15, 1994, page 9:
    /// https://www.mmsp.ece.mcgill.ca/Documents/AudioFormats/WAVE/Docs/RIFFNEW.pdf
    /// Wine, SDL_image, gdk-pixbuf and KDE do not read raw bitmap frames, so the encoder writes a bitmap frame as a CUR
    /// frame that holds the bitmap.
    /// </remarks>
    /// <param name="format">The frame format of the metadata.</param>
    /// <returns><see cref="AniFrameFormat.Ico"/> or <see cref="AniFrameFormat.Cur"/>.</returns>
    private static AniFrameFormat GetEncodedFrameFormat(AniFrameFormat format)
        => format is AniFrameFormat.Bmp ? AniFrameFormat.Cur : format;

    /// <summary>
    /// Creates an icon directory entry from ANI-owned frame metadata.
    /// </summary>
    /// <param name="metadata">The ANI frame metadata.</param>
    /// <param name="compression">The compression of the icon frame.</param>
    /// <param name="bitsPerPixel">The bit depth of a bitmap icon frame.</param>
    /// <param name="format">The embedded icon format.</param>
    /// <param name="size">The source frame size.</param>
    /// <returns>The icon directory entry.</returns>
    private static IconDirEntry CreateIconDirEntry(AniFrameMetadata metadata, IconFrameCompression compression, BmpBitsPerPixel bitsPerPixel, AniFrameFormat format, Size size)
    {
        // PNG and direct-color bitmap entries do not declare a palette; indexed bitmap entries advertise their color count.
        byte colorCount = compression is IconFrameCompression.Png || bitsPerPixel > BmpBitsPerPixel.Bit8
            ? (byte)0
            : (byte)ColorNumerics.GetColorCountForBitDepth((int)bitsPerPixel);

        // ICO stores planes/BPP in these fields, while CUR reuses the same two words for the hotspot coordinates.
        return new IconDirEntry
        {
            Width = metadata.EncodingWidth ?? NarrowDimension(size.Width),
            Height = metadata.EncodingHeight ?? NarrowDimension(size.Height),
            ColorCount = colorCount,
            Planes = format is AniFrameFormat.Ico ? (ushort)1 : metadata.HotspotX,
            BitCount = format is AniFrameFormat.Ico
                ? compression is IconFrameCompression.Bmp ? (ushort)bitsPerPixel : (ushort)32
                : metadata.HotspotY
        };
    }

    /// <summary>
    /// Converts an ANI bitmap bit depth to a supported BMP encoder value.
    /// </summary>
    /// <param name="bitCount">The ANI bit depth.</param>
    /// <returns>The BMP encoder bit depth.</returns>
    private static BmpBitsPerPixel GetBmpBitsPerPixel(uint bitCount)
        => bitCount switch
        {
            1 => BmpBitsPerPixel.Bit1,
            2 => BmpBitsPerPixel.Bit2,
            4 => BmpBitsPerPixel.Bit4,
            8 => BmpBitsPerPixel.Bit8,
            16 => BmpBitsPerPixel.Bit16,
            24 => BmpBitsPerPixel.Bit24,
            _ => BmpBitsPerPixel.Bit32
        };

    /// <summary>
    /// Converts a pixel dimension to the one-byte ICO/CUR representation.
    /// </summary>
    /// <param name="value">The pixel dimension.</param>
    /// <returns>The encoded dimension, where zero represents 256 pixels or greater.</returns>
    private static byte NarrowDimension(int value) => value > byte.MaxValue ? (byte)0 : (byte)value;

    /// <summary>
    /// Begins a RIFF chunk whose size will be backpatched after its payload is written.
    /// </summary>
    /// <param name="stream">The destination stream.</param>
    /// <param name="fourCc">The chunk identifier.</param>
    /// <returns>The stream position of the chunk-size field.</returns>
    private long BeginChunk(Stream stream, ReadOnlySpan<byte> fourCc)
    {
        stream.Write(fourCc);
        long sizePosition = stream.Position;

        // Payload length is unknown until nested encoding completes, so reserve the DWORD and remember its absolute position.
        Span<byte> size = this.buffer[..sizeof(uint)];
        size.Clear();
        stream.Write(size);

        return sizePosition;
    }

    /// <summary>
    /// Begins a RIFF container chunk and writes its form or list type.
    /// </summary>
    /// <param name="stream">The destination stream.</param>
    /// <param name="fourCc">The container identifier.</param>
    /// <param name="type">The container form or list type.</param>
    /// <returns>The stream position of the container-size field.</returns>
    private long BeginContainer(Stream stream, ReadOnlySpan<byte> fourCc, ReadOnlySpan<byte> type)
    {
        long sizePosition = this.BeginChunk(stream, fourCc);
        stream.Write(type);
        return sizePosition;
    }

    /// <summary>
    /// Word-aligns a RIFF chunk and writes its payload size into the reserved field.
    /// </summary>
    /// <param name="stream">The destination stream.</param>
    /// <param name="sizePosition">The stream position of the reserved size field.</param>
    private void EndChunk(Stream stream, long sizePosition)
    {
        long endPosition = stream.Position;

        // sizePosition addresses the size DWORD itself; subtracting its four bytes yields payload length.
        // The RIFF chunk size field is 32 bits. Thus the writer cannot write a larger chunk with a correct size.
        long dataLength = endPosition - sizePosition - sizeof(uint);
        if (dataLength > uint.MaxValue)
        {
            throw new ImageFormatException($"The RIFF chunk size {dataLength} is larger than the 32-bit size field allows.");
        }

        uint dataSize = (uint)dataLength;

        // RIFF chunk sizes exclude the optional padding byte used to align the next chunk to a WORD boundary.
        if ((dataSize & 1) is 1)
        {
            stream.WriteByte(0);
            endPosition++;
        }

        Span<byte> size = this.buffer[..sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(size, dataSize);

        // Backpatch only the reserved DWORD, then restore the append position after any alignment byte.
        stream.Position = sizePosition;
        stream.Write(size);
        stream.Position = endPosition;
    }

    /// <summary>
    /// Projects ANI-owned metadata into the icon encoder without allocating intermediary metadata objects.
    /// </summary>
    private readonly struct AniIconFrameMetadataProvider : IconEncoderCore.IEncodingFrameMetadataProvider
    {
        private readonly AniFrameFormat format;

        private readonly BmpBitsPerPixel bitmapBitsPerPixel;

        /// <summary>
        /// Initializes a new instance of the <see cref="AniIconFrameMetadataProvider"/> struct.
        /// </summary>
        /// <param name="format">The embedded icon format.</param>
        /// <param name="bitmapBitsPerPixel">The bit depth of the bitmap frames, from the ANI header bit count.</param>
        public AniIconFrameMetadataProvider(AniFrameFormat format, BmpBitsPerPixel bitmapBitsPerPixel)
        {
            this.format = format;
            this.bitmapBitsPerPixel = bitmapBitsPerPixel;
        }

        /// <inheritdoc/>
        public IconEncoderCore.EncodingFrameMetadata GetEncodingFrameMetadata(ImageFrame frame, out ReadOnlyMemory<Color>? colorTable)
        {
            AniFrameMetadata metadata = frame.Metadata.GetAniMetadata();
            colorTable = metadata.ColorTable;

            // A bitmap frame is written as a CUR frame that holds a bitmap with the bit depth of the ANI header.
            bool bitmap = metadata.FrameFormat is AniFrameFormat.Bmp;
            IconFrameCompression compression = bitmap ? IconFrameCompression.Bmp : metadata.Compression;
            BmpBitsPerPixel bitsPerPixel = bitmap ? this.bitmapBitsPerPixel : metadata.BmpBitsPerPixel;
            return new IconEncoderCore.EncodingFrameMetadata(compression, bitsPerPixel, CreateIconDirEntry(metadata, compression, bitsPerPixel, this.format, frame.Size));
        }
    }
}
