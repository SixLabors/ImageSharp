// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Cur;
using SixLabors.ImageSharp.Formats.Ico;
using SixLabors.ImageSharp.Formats.Icon;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Memory.Internals;
using SixLabors.ImageSharp.Metadata;

namespace SixLabors.ImageSharp.Formats.Ani;

internal class AniDecoderCore : ImageDecoderCore
{
    /// <summary>
    /// The general decoder options.
    /// </summary>
    private readonly Configuration configuration;

    /// <summary>
    /// The stream to decode from.
    /// </summary>
    private BufferedReadStream currentStream = null!;

    private AniHeader header;

    public AniDecoderCore(DecoderOptions options)
        : base(options) =>
        this.configuration = options.Configuration;

    protected override Image<TPixel> Decode<TPixel>(BufferedReadStream stream, CancellationToken cancellationToken)
    {
        this.currentStream = stream;

        Guard.IsTrue(this.currentStream.TryReadUnmanaged(out RiffOrListChunkHeader riffHeader), nameof(riffHeader), "Invalid RIFF header.");
        long dataSize = riffHeader.Size;
        long dataStartPosition = this.currentStream.Position;

        ImageMetadata metadata = new();
        AniMetadata aniMetadata = this.ReadHeader(dataStartPosition, dataSize, metadata);

        List<(AniFrameFormat Type, Image<TPixel> Image)> frames = [];
        this.HandleRiffChunk(out Span<int> sequence, out Span<uint> rate, dataStartPosition, dataSize, aniMetadata, frames, DecodeFrameChunk);

        List<ImageFrame<TPixel>> list = [];

        for (int i = 0; i < sequence.Length; i++)
        {
            int sequenceIndex = sequence[i];
            (AniFrameFormat type, Image<TPixel>? img) = frames[sequenceIndex];

            AniFrameMetadata aniFrameMetadata = new()
            {
                FrameDelay = rate.IsEmpty ? aniMetadata.DisplayRate : rate[sequenceIndex],
                SequenceNumber = i
            };

            list.AddRange(img.Frames.Select(source =>
            {
                ImageFrame<TPixel> target = new(this.Options.Configuration, this.Dimensions);
                for (int y = 0; y < source.Height; y++)
                {
                    source.PixelBuffer.DangerousGetRowSpan(y).CopyTo(target.PixelBuffer.DangerousGetRowSpan(y));
                }

                AniFrameMetadata clonedMetadata = aniFrameMetadata.DeepClone();
                source.Metadata.SetFormatMetadata(AniFormat.Instance, clonedMetadata);
                clonedMetadata.FrameFormat = type;
                switch (type)
                {
                    case AniFrameFormat.Ico:
                        IcoFrameMetadata icoFrameMetadata = source.Metadata.GetIcoMetadata();
                        // TODO source.Metadata.SetFormatMetadata(IcoFormat.Instance, null);
                        clonedMetadata.IcoFrameMetadata = icoFrameMetadata;
                        clonedMetadata.EncodingWidth = icoFrameMetadata.EncodingWidth;
                        clonedMetadata.EncodingHeight = icoFrameMetadata.EncodingHeight;
                        break;
                    case AniFrameFormat.Cur:
                        CurFrameMetadata curFrameMetadata = source.Metadata.GetCurMetadata();
                        // TODO source.Metadata.SetFormatMetadata(CurFormat.Instance, null);
                        clonedMetadata.CurFrameMetadata = curFrameMetadata;
                        clonedMetadata.EncodingWidth = curFrameMetadata.EncodingWidth;
                        clonedMetadata.EncodingHeight = curFrameMetadata.EncodingHeight;
                        break;
                    case AniFrameFormat.Bmp:
                        clonedMetadata.EncodingWidth = Narrow(source.Width);
                        clonedMetadata.EncodingHeight = Narrow(source.Height);
                        break;
                    default:
                        break;
                }

                return target;
            }));
        }

        foreach ((AniFrameFormat _, Image<TPixel> img) in frames)
        {
            img.Dispose();
        }

        Image<TPixel> image = new(this.Options.Configuration, metadata, list);

        return image;

        void DecodeFrameChunk()
        {
            while (this.TryReadChunk(dataStartPosition, dataSize, out RiffChunkHeader chunk))
            {
                if ((AniListFrameType)chunk.FourCc is not AniListFrameType.Icon)
                {
                    continue;
                }

                long endPosition = this.currentStream.Position + chunk.Size;
                Image<TPixel>? frame = null;
                AniFrameFormat type = default;
                if (aniMetadata.Flags.HasFlag(AniHeaderFlags.IsIcon))
                {
                    if (this.currentStream.TryReadUnmanaged(out IconDir dir))
                    {
                        this.currentStream.Position -= Unsafe.SizeOf<IconDir>();

                        switch (dir.Type)
                        {
                            case IconFileType.CUR:
                                frame = CurDecoder.Instance.Decode<TPixel>(this.Options, this.currentStream);
                                type = AniFrameFormat.Cur;
                                break;
                            case IconFileType.ICO:
                                frame = IcoDecoder.Instance.Decode<TPixel>(this.Options, this.currentStream);
                                type = AniFrameFormat.Ico;
                                break;
                        }
                    }
                }
                else
                {
                    frame = BmpDecoder.Instance.Decode<TPixel>(this.Options, this.currentStream);
                    type = AniFrameFormat.Bmp;
                }

                if (frame is not null)
                {
                    frames.Add((type, frame));
                    this.Dimensions = new(Math.Max(this.Dimensions.Width, frame.Size.Width), Math.Max(this.Dimensions.Height, frame.Size.Height));
                }

                this.currentStream.Position = endPosition;
            }
        }
    }

    protected override ImageInfo Identify(BufferedReadStream stream, CancellationToken cancellationToken)
    {
        this.currentStream = stream;

        Guard.IsTrue(this.currentStream.TryReadUnmanaged(out RiffOrListChunkHeader riffHeader), nameof(riffHeader), "Invalid RIFF header.");
        long dataSize = riffHeader.Size;
        long dataStartPosition = this.currentStream.Position;

        ImageMetadata metadata = new();
        AniMetadata aniMetadata = this.ReadHeader(dataStartPosition, dataSize, metadata);

        List<(AniFrameFormat Type, ImageInfo Info)> infoList = [];
        this.HandleRiffChunk(out Span<int> sequence, out Span<uint> rate, dataStartPosition, dataSize, aniMetadata, infoList, IdentifyFrameChunk);

        List<ImageFrameMetadata> frameMetadataCollection = new(sequence.Length);

        for (int i = 0; i < sequence.Length; i++)
        {
            int sequenceIndex = sequence[i];
            (AniFrameFormat type, ImageInfo info) = infoList[sequenceIndex];

            AniFrameMetadata aniFrameMetadata = new()
            {
                FrameDelay = rate.IsEmpty ? aniMetadata.DisplayRate : rate[sequenceIndex],
                SequenceNumber = i
            };

            if (info.FrameMetadataCollection.Count is not 0)
            {
                frameMetadataCollection.AddRange(
                    info.FrameMetadataCollection.Select(frameMetadata =>
                    {
                        AniFrameMetadata clonedMetadata = aniFrameMetadata.DeepClone();
                        frameMetadata.SetFormatMetadata(AniFormat.Instance, clonedMetadata);
                        clonedMetadata.FrameFormat = type;
                        switch (type)
                        {
                            case AniFrameFormat.Ico:
                                IcoFrameMetadata icoFrameMetadata = frameMetadata.GetIcoMetadata();
                                // TODO source.Metadata.SetFormatMetadata(IcoFormat.Instance, null);
                                clonedMetadata.IcoFrameMetadata = icoFrameMetadata;
                                clonedMetadata.EncodingWidth = icoFrameMetadata.EncodingWidth;
                                clonedMetadata.EncodingHeight = icoFrameMetadata.EncodingHeight;
                                break;
                            case AniFrameFormat.Cur:
                                CurFrameMetadata curFrameMetadata = frameMetadata.GetCurMetadata();
                                // TODO source.Metadata.SetFormatMetadata(CurFormat.Instance, null);
                                clonedMetadata.CurFrameMetadata = curFrameMetadata;
                                clonedMetadata.EncodingWidth = curFrameMetadata.EncodingWidth;
                                clonedMetadata.EncodingHeight = curFrameMetadata.EncodingHeight;
                                break;
                            default:
                                ThrowHelper.ThrowArgumentOutOfRangeException(nameof(type), "FrameMetadata must be ICO or CUR");
                                break;
                        }

                        return frameMetadata;
                    }));
            }
            else // BMP
            {
                aniFrameMetadata.EncodingWidth = Narrow(info.Width);
                aniFrameMetadata.EncodingHeight = Narrow(info.Height);
                aniFrameMetadata.FrameFormat = type;
                ImageFrameMetadata frameMetadata = new();
                frameMetadata.SetFormatMetadata(AniFormat.Instance, aniFrameMetadata);
                frameMetadataCollection.Add(frameMetadata);
            }
        }

        ImageInfo imageInfo = new(this.Dimensions, metadata, frameMetadataCollection);

        return imageInfo;

        void IdentifyFrameChunk()
        {
            while (this.TryReadChunk(dataStartPosition, dataSize, out RiffChunkHeader chunk))
            {
                if ((AniListFrameType)chunk.FourCc is not AniListFrameType.Icon)
                {
                    continue;
                }

                long endPosition = this.currentStream.Position + chunk.Size;
                ImageInfo? info = null;
                AniFrameFormat type = default;
                if (aniMetadata.Flags.HasFlag(AniHeaderFlags.IsIcon))
                {
                    if (this.currentStream.TryReadUnmanaged(out IconDir dir))
                    {
                        this.currentStream.Position -= Unsafe.SizeOf<IconDir>();

                        switch (dir.Type)
                        {
                            case IconFileType.CUR:
                                info = CurDecoder.Instance.Identify(this.Options, this.currentStream);
                                type = AniFrameFormat.Cur;
                                break;
                            case IconFileType.ICO:
                                info = IcoDecoder.Instance.Identify(this.Options, this.currentStream);
                                type = AniFrameFormat.Ico;
                                break;
                        }
                    }
                }
                else
                {
                    info = BmpDecoder.Instance.Identify(this.Options, this.currentStream);
                    type = AniFrameFormat.Bmp;
                }

                if (info is not null)
                {
                    infoList.Add((type, info));
                    this.Dimensions = new(Math.Max(this.Dimensions.Width, info.Size.Width), Math.Max(this.Dimensions.Height, info.Size.Height));
                }

                this.currentStream.Position = endPosition;
            }
        }
    }

    private AniMetadata ReadHeader(long dataStartPosition, long dataSize, ImageMetadata metadata)
    {
        if (!this.TryReadChunk(dataStartPosition, dataSize, out RiffChunkHeader riffChunkHeader) ||
            (AniChunkType)riffChunkHeader.FourCc is not AniChunkType.AniH)
        {
            Guard.IsTrue(false, nameof(riffChunkHeader), "Missing ANIH chunk.");
        }

        AniMetadata aniMetadata = metadata.GetAniMetadata();

        if (this.currentStream.TryReadUnmanaged(out AniHeader result))
        {
            this.header = result;
            aniMetadata.Width = result.Width;
            aniMetadata.Height = result.Height;
            aniMetadata.BitCount = result.BitCount;
            aniMetadata.Planes = result.Planes;
            aniMetadata.DisplayRate = result.DisplayRate;
            aniMetadata.Flags = result.Flags;
        }

        return aniMetadata;
    }

    /// <summary>
    /// Call <see cref="HandleRiffChunk"/> <br/>
    /// -> Call <see cref="HandleListChunk"/> <br/>
    /// -> Call <paramref name="handleFrameChunk"/>
    /// </summary>
    private void HandleRiffChunk(out Span<int> sequence, out Span<uint> rate, long dataStartPosition, long dataSize, AniMetadata aniMetadata, ICollection totalFrameCount, Action handleFrameChunk)
    {
        sequence = default;
        rate = default;

        while (this.TryReadChunk(dataStartPosition, dataSize, out RiffChunkHeader chunk))
        {
            switch ((AniChunkType)chunk.FourCc)
            {
                case AniChunkType.Seq:
                {
                    using IMemoryOwner<byte> data = this.ReadChunkData(chunk.Size);
                    sequence = MemoryMarshal.Cast<byte, int>(data.Memory.Span);
                    break;
                }

                case AniChunkType.Rate:
                {
                    using IMemoryOwner<byte> data = this.ReadChunkData(chunk.Size);
                    rate = MemoryMarshal.Cast<byte, uint>(data.Memory.Span);
                    break;
                }

                case AniChunkType.List:
                    this.HandleListChunk(dataStartPosition, dataSize, aniMetadata, handleFrameChunk);
                    break;
                default:
                    break;
            }
        }

        if (sequence.IsEmpty)
        {
            sequence = Enumerable.Range(0, totalFrameCount.Count).ToArray();
        }
    }

    private void HandleListChunk(long dataStartPosition, long dataSize, AniMetadata aniMetadata, Action handleFrameChunk)
    {
        if (!this.currentStream.TryReadUnmanaged(out uint listType))
        {
            return;
        }

        switch ((AniListType)listType)
        {
            case AniListType.Fram:
            {
                handleFrameChunk();
                break;
            }

            case AniListType.Info:
            {
                while (this.TryReadChunk(dataStartPosition, dataSize, out RiffChunkHeader chunk))
                {
                    switch ((AniListInfoType)chunk.FourCc)
                    {
                        case AniListInfoType.INam:
                        {
                            using IMemoryOwner<byte> data = this.ReadChunkData(chunk.Size);
                            aniMetadata.Name = Encoding.ASCII.GetString(data.Memory.Span).TrimEnd('\0');
                            break;
                        }

                        case AniListInfoType.IArt:
                        {
                            using IMemoryOwner<byte> data = this.ReadChunkData(chunk.Size);
                            aniMetadata.Artist = Encoding.ASCII.GetString(data.Memory.Span).TrimEnd('\0');
                            break;
                        }

                        default:
                            break;
                    }
                }

                break;
            }
        }
    }

    private bool TryReadChunk(long startPosition, long size, out RiffChunkHeader chunk)
    {
        if (this.currentStream.Position - startPosition >= size)
        {
            chunk = default;
            return false;
        }

        return this.currentStream.TryReadUnmanaged(out chunk);
    }

    /// <summary>
    /// Reads the chunk data from the stream.
    /// </summary>
    /// <param name="length">The length of the chunk data to read.</param>
    [MethodImpl(InliningOptions.ShortMethod)]
    private IMemoryOwner<byte> ReadChunkData(uint length)
    {
        if (length is 0)
        {
            return new BasicArrayBuffer<byte>([]);
        }

        // We rent the buffer here to return it afterwards in Decode()
        // We don't want to throw a degenerated memory exception here as we want to allow partial decoding
        // so limit the length.
        int len = (int)Math.Min(length, this.currentStream.Length - this.currentStream.Position);
        IMemoryOwner<byte> buffer = this.configuration.MemoryAllocator.Allocate<byte>(len, AllocationOptions.Clean);

        this.currentStream.Read(buffer.GetSpan(), 0, len);

        return buffer;
    }

    private static byte Narrow(int value) => value > byte.MaxValue ? (byte)0 : (byte)value;
}
