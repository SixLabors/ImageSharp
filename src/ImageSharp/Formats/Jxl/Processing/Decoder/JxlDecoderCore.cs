// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats.Jxl.Fields;
using SixLabors.ImageSharp.Formats.Jxl.IO;
using SixLabors.ImageSharp.Formats.Jxl.IO.Container;
using SixLabors.ImageSharp.Formats.Jxl.IO.FrameHeader;
using SixLabors.ImageSharp.Formats.Jxl.IO.Jpeg.Data;
using SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

/// <summary>
/// Internal decoder for JPEG XL.
/// </summary>
internal sealed class JxlDecoderCore : ImageDecoderCore, IDisposable
{
    private const long NumBuffersLimit = 1 << 20;

    /// <summary>
    /// Current stage of the decoding pipeline.
    /// </summary>
    private JxlDecoderStage decoderStage;

    /// <summary>
    /// Status of whether or not has the signature been parsed.
    /// </summary>
    private bool gotSignature;

    /// <summary>
    /// Did we parse the final code stream?
    /// </summary>
    private bool lastCodestreamSeen;

    /// <summary>
    /// Did we parse the signature of the code stream?
    /// </summary>
    private bool gotCodestreamSignature;

    /// <summary>
    /// Did we parse basic JXL information?
    /// </summary>
    private bool gotBasicInfo;

    /// <summary>
    /// Did we parse the transform data?
    /// </summary>
    private bool gotTransformData;

    /// <summary>
    /// Did we parse all the codestream metadata headers?
    /// </summary>
    private bool gotAllHeaders;

    /// <summary>
    /// Are we decoding pixels now?
    /// </summary>
    private bool postHeaders;

    /// <summary>
    /// ICC profile for JPEG XL metadata, if present.
    /// </summary>
    private IccProfile? iccProfile;

    /// <summary>
    /// The frame index box, if present.
    /// </summary>
    private JxlDecoderFrameIndexBox? frameIndexBox;

    /// <summary>
    /// Did we get the preview image, or determined we cannot get it or there isn't any?
    /// </summary>
    private bool gotPreviewImage;

    /// <summary>
    /// Is this a preview frame?
    /// </summary>
    private bool previewFrame;

    private long filePosition;

    /// <summary>
    /// Offset where box contents start.
    /// </summary>
    private long boxContentsBegin;

    /// <summary>
    /// Offset where box contents end.
    /// </summary>
    private long boxContentsEnd;

    /// <summary>
    /// boxContentsEnd - boxContentsBegin
    /// </summary>
    private long boxContentsSize;

    /// <summary>
    /// Total size of the box in bytes.
    /// </summary>
    private long boxSize;

    /// <summary>
    /// Size of the headers in bytes.
    /// </summary>
    private long headerSize;

    /// <summary>
    /// Are box contents unbounded?
    /// </summary>
    private bool boxContentsUnbounded;

    /// <summary>
    /// Type of the box currently being decoded.
    /// </summary>
    private JxlBoxType boxType;

    /// <summary>
    /// Underlying type for brob boxes.
    /// </summary>
    private JxlBoxType boxDecodedType;

    private bool boxEvent;

    /// <summary>
    /// Should box contents be decompressed (using Brotli)?
    /// </summary>
    private bool decompressBoxes;

    /// <summary>
    /// Should the output buffer for the box be set?
    /// </summary>
    private bool boxOutBufferSet;

    /// <summary>
    /// Should the output buffer for the current box be set?
    /// </summary>
    private bool boxOutBufferSetCurrentBox;

    /// <summary>
    /// Output buffer for the current box.
    /// </summary>
    private IMemoryOwner<byte>? boxOutputBuffer;

    /// <summary>
    /// Size of the output buffer.
    /// </summary>
    private long boxOutBufferSize;

    /// <summary>
    /// Offset of start of box output buffer.
    /// </summary>
    private long boxOutBufferBegin;

    /// <summary>
    /// Current offset of start of box output buffer.
    /// </summary>
    private long boxOutBufferPos;

    /// <summary>
    /// Should orientation be preserved?
    /// </summary>
    private bool keepOrientation;

    /// <summary>
    /// Should alpha channel be unpremultiplied?
    /// </summary>
    private bool unpremultiplyAlpha;

    private bool renderSpotcolors;

    private bool coalescing;

    /// <summary>
    /// Custom intensity target.
    /// </summary>
    private float desiredIntensityTarget;

    private int eventsWanted;

    private int originalEventsWanted;

    private long basicInfoSizeHint;

    /// <summary>
    /// Is container format present?
    /// </summary>
    private bool haveContainer;

    /// <summary>
    /// Total number of boxes.
    /// </summary>
    private long boxCount;

    /// <summary>
    /// The level of progressive detail in frame coding.
    /// </summary>
    private JxlProgressiveDetail progressiveDetail = JxlProgressiveDetail.Dc;

    /// <summary>
    /// Progressive detail of current frame.
    /// </summary>
    private JxlProgressiveDetail frameProgressiveDetail;

    /// <summary>
    /// The intended downsampling ratio for the current progression step.
    /// </summary>
    private long downsamplingTarget;

    /// <summary>
    /// True if the image output buffer or callback was set.
    /// </summary>
    private bool imageOutBufferSet;

    /// <summary>
    /// Size of the image output buffer.
    /// </summary>
    private long imageOutputSize;

    /// <summary>
    /// Output data for extra channels.
    /// </summary>
    private readonly List<JxlExtraChannelOutput> extraChannelOutputs = [];

    /// <summary>
    /// Codec metadata if present.
    /// </summary>
    private JxlCodecMetadata? metadata;

    /// <summary>
    /// Image metadata if present.
    /// </summary>
    private JxlImageMetadata? imageMetadata;

    /// <summary>
    /// The image bundle.
    /// </summary>
    private JxlImageBundle? imageBundle;

    /// <summary>
    /// State for passes decoder.
    /// </summary>
    private JxlPassesDecoderState? passesState;

    /// <summary>
    /// State for frame decoder.
    /// </summary>
    private JxlFrameDecoder? frameDecoder;

    /// <summary>
    /// The next section.
    /// </summary>
    private long nextSection;

    private readonly List<byte> sectionProcessed = [];

    /// <summary>
    /// The frame header, if present.
    /// </summary>
    private JxlFrameHeader? frameHeader;

    /// <summary>
    /// Remaining frame size.
    /// </summary>
    private long remainingFrameSize;

    /// <summary>
    /// Stage of the decoding pipeline.
    /// </summary>
    private JxlFrameStage frameStage;

    /// <summary>
    /// Has progression for DC frames been completed?
    /// </summary>
    private bool dcFrameProgressionDone;

    private bool isLastOfStill;

    /// <summary>
    /// Is the currently processed frame the last of the codestream?
    /// </summary>
    private bool isLastTotal;

    /// <summary>
    /// How many frames should be skipped?
    /// </summary>
    private int skipFrames;

    /// <summary>
    /// Is active frame being skipped?
    /// </summary>
    private bool skippingFrame;

    private int internalFrames;

    private int externalFrames;

    /// <summary>
    /// All frame reference.s
    /// </summary>
    private readonly List<JxlFrameReference> frameReferences = [];

    private readonly List<int> frameExternalToInternal = [];

    private readonly List<byte> frameRequired = [];

    /// <summary>
    /// Codestream input data is temporarily copied here.
    /// </summary>
    private JxlMemoryWriter? codestreamCopy;

    private long codestreamUnconsumed;

    /// <summary>
    /// Position in the codestreamCopy vector.
    /// </summary>
    private long codestreamPos;

    /// <summary>
    /// Number of remaining bits in the codestream copy.
    /// </summary>
    private long codestreamBitsAhead;

    /// <summary>
    /// Stage of the box parsing pipeline.
    /// </summary>
    private JxlBoxStage boxStage;

    /// <summary>
    /// FTYP minor-version.
    /// <list type="bullet">
    ///   <item>0 - jxlp must be in order</item>
    ///   <item>1 - OOO jxlp allowed</item>
    /// </list>
    /// </summary>
    private int jxlFileFormatVersion;

    /// <summary>
    /// Counter of next expected jxlp box.
    /// </summary>
    private int nextJxlpIndex;

    /// <summary>
    /// OOO jxlp payloads keyed by counter. Keys are: codestream bytes without
    /// 4byte header, and is_last.
    /// </summary>
    private readonly Dictionary<int, JxlOooEntry> jxlpOooBuffer = [];

    private long jxlpOooBufferTotal;

    private int bufferingJxlpIndex;

    private bool bufferingJxlpIsLast;

    /// <summary>
    /// Decompresses box contents.
    /// </summary>
    private readonly JxlBoxContentDecoder? boxContentDecoder;

    private readonly JxlBoxContentDecoder? metadataDecoder;

    /// <summary>
    /// Raw bytes for EXIF metadata.
    /// </summary>
    private IMemoryOwner<byte>? exifMetadata;

    /// <summary>
    /// Raw bytes for XMP metadata.
    /// </summary>
    private IMemoryOwner<byte>? xmpMetadata;

    /// <summary>
    /// State of EXIF storage. 0 - not stored,
    /// 1 - currently stored, 2 - finished.
    /// </summary>
    private int storeExif;

    /// <summary>
    /// State of XMP storage. 0 - not stored,
    /// 1 - currently stored, 2 - finished.
    /// </summary>
    private int storeXmp;

    /// <summary>
    /// Position in the output buffer for JPEG
    /// reconstruction.
    /// </summary>
    private long reconstructionOutputBufferPos;

    /// <summary>
    /// EXIF size for JPEG reconstruction.
    /// </summary>
    private long reconstructionExifSize;

    /// <summary>
    /// XMP size for JPEG reconstruction.
    /// </summary>
    private long reconstructionXmpSize;

    /// <summary>
    /// Stage of reconstruction pipeline.
    /// </summary>
    private JpegReconstructionStage reconstructionOutputJpeg;

    /// <summary>
    /// Next input data.
    /// </summary>
    private IMemoryOwner<byte>? nextInput;

    private long availableInput;

    private bool inputClosed;

    /// <summary>
    /// Output image buffer.
    /// </summary>
    private Stream? imageOutBuffer;

    /// <summary>
    /// Callback to initialize image output.
    /// </summary>
    private JxlImageOutputInitializerCallback? imageOutputInitCallback;

    /// <summary>
    /// Callback to run image output.
    /// </summary>
    private JxlImageOutputRunCallback? imageOutputRunCallback;

    /// <summary>
    /// Callback to dispose image output.
    /// </summary>
    private JxlImageOutputDestroyCallback? imageOutputDestroyCallback;

    /// <summary>
    /// Bit depth for image output.
    /// </summary>
    private JxlBitDepthMetadata imageOutputBitDepth = new();

    public JxlDecoderCore(DecoderOptions options)
        : base(options)
        => this.Reset();

    public long SizeHintBasicInfo => this.gotBasicInfo ? 0 : this.basicInfoSizeHint;

    /// <summary>
    /// Gets or sets a value indicating whether orientation should be kept.
    /// </summary>
    public bool KeepOrientation
    {
        get => this.keepOrientation;
        set
        {
            this.BeforeUpdateState(nameof(this.KeepOrientation));
            this.keepOrientation = value;
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether to unpremultiply RGB values
    /// by the alpha channel.
    /// </summary>
    public bool UnpremultiplyAlpha
    {
        get => this.unpremultiplyAlpha;
        set
        {
            this.BeforeUpdateState(nameof(this.UnpremultiplyAlpha));
            this.unpremultiplyAlpha = value;
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether spotcolors (special inks used in printing)
    /// are rendered in the output.
    /// </summary>
    public bool RenderSpotcolors
    {
        get => this.renderSpotcolors;
        set
        {
            this.BeforeUpdateState(nameof(this.RenderSpotcolors));
            this.renderSpotcolors = value;
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether multiple frames (especially zero-duration frames)
    /// have to be merged into a single image.
    /// </summary>
    public bool Coalescing
    {
        get => this.coalescing;
        set
        {
            this.BeforeUpdateState(nameof(this.Coalescing));
            this.coalescing = value;
        }
    }

    /// <summary>
    /// Gets the dimensions of the current image buffer.
    /// </summary>
    public Size CurrentDimensions
    {
        get
        {
            int width;
            int height;

            if (this.frameHeader?.IsPreviewFrame == true)
            {
                width = this.metadata!.GetOrientedPreviewXSize(this.keepOrientation);
                height = this.metadata!.GetOrientedPreviewYSize(this.keepOrientation);
            }
            else
            {
                width = this.metadata!.GetOrientedXSize(this.keepOrientation);
                height = this.metadata!.GetOrientedYSize(this.keepOrientation);

                if (!this.coalescing)
                {
                    JxlFrameDimensions dim = this.frameHeader!.FrameDimensions;

                    width = dim.XSizeUpsampled;
                    height = dim.YSizeUpsampled;

                    if (!this.keepOrientation && this.metadata.ImageMetadata!.Orientation > 4)
                    {
                        RuntimeUtility.Swap(ref width, ref height);
                    }
                }
            }

            return new Size(width, height);
        }
    }

    /// <summary>
    /// Stage of the decoder pipeline.
    /// </summary>
    private enum JxlDecoderStage : byte
    {
        /// <summary>
        /// Initialized but hasn't decoded yet.
        /// </summary>
        Initialized,

        /// <summary>
        /// Decoding right now.
        /// </summary>
        Started,

        /// <summary>
        /// Code stream done, but other boxes could still occur.
        /// </summary>
        CodeStreamFinished,

        /// <summary>
        /// Decoding failed and the decoder is no longer usable.
        /// </summary>
        Error
    }

    /// <summary>
    /// Identifies the signature of the JPEG XL file.
    /// </summary>
    private enum JxlSignature : byte
    {
        /// <summary>
        /// Error status indicating not enough bytes to detect the signature.
        /// </summary>
        NotEnoughBytes,

        /// <summary>
        /// A JPEG XL code stream.
        /// </summary>
        CodeStream,

        /// <summary>
        /// The signature is invalid.
        /// </summary>
        Invalid,

        /// <summary>
        /// Container format.
        /// </summary>
        Container
    }

    /// <summary>
    /// Frame stage for this decoder.
    /// </summary>
    private enum JxlFrameStage : byte
    {
        /// <summary>
        /// Frame header should be parsed.
        /// </summary>
        Header,

        /// <summary>
        /// TOC should be parsed.
        /// </summary>
        Toc,

        /// <summary>
        /// Full pixels should be parsed.
        /// </summary>
        Full
    }

    /// <summary>
    /// Stage of the box parsing pipeline.
    /// </summary>
    private enum JxlBoxStage : byte
    {
        /// <summary>
        /// Box header of the next box.
        /// </summary>
        Header,

        /// <summary>
        /// File type box.
        /// </summary>
        Ftyp,

        /// <summary>
        /// Box with skipped contents.
        /// </summary>
        Skip,

        /// <summary>
        /// Code stream boxes.
        /// </summary>
        CodeStream,

        /// <summary>
        /// Extra header of partial code stream box.
        /// </summary>
        PartialCodeStream,

        /// <summary>
        /// Out-of-order jxlp box payload.
        /// </summary>
        BufferingJxlp,

        /// <summary>
        /// Jpeg reconstruction box.
        /// </summary>
        JpegReconstruction
    }

    /// <summary>
    /// Reconstruction stage for JPEG images.
    /// </summary>
    private enum JpegReconstructionStage : byte
    {
        /// <summary>
        /// Don't output anything.
        /// </summary>
        None,

        /// <summary>
        /// Set metadata to the JPEG data.
        /// </summary>
        SetMetadata,

        /// <summary>
        /// Outputting the JPEG bytes.
        /// </summary>
        Output
    }

    /// <summary>
    /// A single frame index box entry. See <see cref="JxlDecoderFrameIndexBox"/>.
    /// </summary>
    private struct JxlDecoderFrameIndexBoxEntry
    {
        /// <summary>
        /// Offset of start byte of this frame compared to start
        /// byte of previous frame.
        /// </summary>
        public long Offset;

        /// <summary>
        /// Duration in ticks between the start of this frame and the start of the next frame.
        /// </summary>
        public int DurationInTicks;

        /// <summary>
        /// Amount of frames.
        /// </summary>
        public int AmountOfFrames;
    }

    // This is a class not a struct. This is so we can
    // assign its values from an array access. Like this:
    //  this.frameReferences[(int)internalIndex].Reference = ...
    // where this.frameReferences = JxlFrameReference[].
    private sealed class JxlFrameReference(int reference, int savedAs)
    {
        public int Reference { get; set; } = reference;

        public int SavedAs { get; set; } = savedAs;
    }

    /// <summary>
    /// A frame index box.
    /// </summary>
    private sealed class JxlDecoderFrameIndexBox
    {
        /// <summary>
        /// Gets or sets all entries within this frame index box.
        /// </summary>
        public List<JxlDecoderFrameIndexBoxEntry> Entries { get; set; } = [];

        /// <summary>
        /// Gets the number of entries.
        /// </summary>
        public int Count => this.Entries.Count;

        /// <summary>
        /// Gets or sets the numerator. (Default: 1)
        /// </summary>
        public int Numerator { get; set; } = 1;

        /// <summary>
        /// Gets or sets the denominator. (Default: 1000)
        /// </summary>
        public int Denominator { get; set; } = 1000;

        /// <summary>
        /// Adds a new frame.
        /// </summary>
        /// <param name="offset">Offset to first byte.</param>
        /// <param name="ticks">Duration in ticks.</param>
        /// <param name="frames">Amount of frames.</param>
        public void AddFrame(long offset, int ticks, int frames) => this.Entries.Add(new JxlDecoderFrameIndexBoxEntry()
        {
            Offset = offset,
            AmountOfFrames = frames,
            DurationInTicks = ticks
        });
    }

    private sealed record JxlExtraChannelOutput(JxlPixelFormat Format, object? Buffer, long BufferSize);

    private sealed record JxlOooEntry(byte[] CodestreamBytes, bool IsLast);

    public void Dispose()
    {
        // RewindDecodingState resets everything,
        // including disposal of streams.
        this.RewindDecodingState();

        // Streams like input and output streams may
        // be unmanaged.
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Ensures that the coordinates are not out of bounds.
    /// </summary>
    /// <param name="a">First coordinate</param>
    /// <param name="b">Second coordinate</param>
    /// <param name="size">Image width</param>
    /// <returns>Boolean indicating whether the coordinates are out of bounds</returns>
    private static bool IsOutOfBounds(int a, int b, int size)
    {
        long position = a + b;

        return position > size || position < a;
    }

    private static int InitialBasicInfoSizeHint()
    {
        const int containerHeaderSize = 48;
        const int maxCodestreamBasicInfoSize = 50;
        return containerHeaderSize + maxCodestreamBasicInfoSize;
    }

    private static JxlSignature DetectSignature(Stream buffer)
    {
        int firstByte = buffer.ReadByte();
        if (firstByte == -1)
        {
            throw new EndOfStreamException();
        }

        if (firstByte == 0xFF)
        {
            int secondByte = buffer.ReadByte();
            if (secondByte == -1)
            {
                throw new EndOfStreamException();
            }

            if (secondByte == JxlShared.CodestreamMarker)
            {
                return JxlSignature.CodeStream;
            }
            else
            {
                return JxlSignature.Invalid;
            }
        }

        // Container?
        if (firstByte == 0)
        {
            Span<byte> signatureBox = stackalloc byte[JxlShared.SignatureBox.Length];
            buffer.ReadExactly(signatureBox);

            if (signatureBox.SequenceEqual(JxlShared.SignatureBox))
            {
                return JxlSignature.Container;
            }
            else
            {
                return JxlSignature.Invalid;
            }
        }

        // Signature is invalid
        return JxlSignature.Invalid;
    }

    private static uint BitsPerChannel(JxlDataType dataType)
        => dataType switch
        {
            JxlDataType.Byte => 8,
            JxlDataType.UInt16 or JxlDataType.Half => 16,
            JxlDataType.Single => 32,
            _ => 0
        };

    private static uint GetBitDepth(JxlBitDepth bitDepth, JxlImageMetadata metadata, JxlPixelFormat pixelFormat)
    {
        if (bitDepth.Type == JxlBitDepthType.FromPixelFormat)
        {
            return BitsPerChannel(pixelFormat.DataType);
        }
        else if (bitDepth.Type == JxlBitDepthType.FromCodeStream)
        {
            return metadata.BitDepth!.BitsPerSample;
        }
        else if (bitDepth.Type == JxlBitDepthType.Custom)
        {
            return bitDepth.BitsPerSample;
        }

        return 0;
    }

    private List<int> GetFrameDependencies(int index, Span<JxlFrameReference> references)
    {
        DebugGuard.MustBeLessThan(index, references.Length, nameof(index));

        const int storageNum = 8;

        List<int> result = [];
        int invalid = references.Length;
        List<int>[] storage = new List<int>[storageNum];

        for (int s = 0; s < storageNum; s++)
        {
            storage[s] = new List<int>(references.Length);
            int mask = 1 << s;
            int id = invalid;

            for (int i = 0; i < references.Length; i++)
            {
                if ((references[i].SavedAs & mask) != 0)
                {
                    id = i;
                }

                storage[s][i] = id;
            }
        }

        Span<byte> seen = stackalloc byte[index + 1];
        seen.Clear();   // All values are explicitly cleared in reference source

        Stack<int> stack = [];
        stack.Push(index);
        seen[index] = 1;

        for (int s = 0; s < storageNum; s++)
        {
            int frameRef = storage[s][index];

            if (frameRef == invalid)
            {
                continue;
            }

            if (seen[frameRef] != 0)
            {
                continue;
            }

            stack.Push(frameRef);
            seen[frameRef] = 1;
            result.Add(frameRef);
        }

        while (stack.Count > 0)
        {
            int frameIndex = stack.Pop();
            if (frameIndex == 0)
            {
                continue;
            }

            for (int s = 0; s < storageNum; s++)
            {
                int mask = 1 << s;

                if ((references[frameIndex].Reference & mask) == 0)
                {
                    continue;
                }

                int frameRef = storage[s][frameIndex - 1];
                if (frameRef == invalid)
                {
                    continue;
                }

                if (seen[frameRef] != 0)
                {
                    continue;
                }

                stack.Push(frameRef);
                seen[frameRef] = 1;
                result.Add(frameRef);
            }
        }

        return result;
    }

    /// <summary>
    /// Checks if the buffer of specified length can be added.
    /// </summary>
    /// <param name="length">Length of the desired buffer.</param>
    /// <returns>
    /// True if buffers with such lengths can be added; false if they
    /// exceed the size limit.
    /// </returns>
    public bool CanAddBuffer(long length)
    {
        const long bufferLimit = 1 << 48;
        return length < bufferLimit &&
            (length + this.jxlpOooBufferTotal + (this.codestreamCopy?.AsMemory().Length ?? 0)) < bufferLimit;
    }

    public bool TryInjectNextBufferedJxlpBox()
    {
        if (!this.jxlpOooBuffer.TryGetValue(this.nextJxlpIndex, out JxlOooEntry? value))
        {
            return false;
        }

        if (value == this.jxlpOooBuffer.Last().Value)
        {
            return true;
        }

        value.Deconstruct(out byte[] data, out bool isLast);
        int length = data.Length;

        this.codestreamCopy!.Write(data);

        if (isLast)
        {
            this.lastCodestreamSeen = true;
        }

        _ = this.jxlpOooBuffer.Remove(this.nextJxlpIndex++);

        this.jxlpOooBufferTotal -= length;

        return true;
    }

    /// <summary>
    /// Returns true if the jbrd box needs exif or xmp.
    /// </summary>
    /// <returns>JBRD needs more boxes - true, otherwise false.</returns>
    public bool JbrdNeedsMoreBoxes() =>
        (this.storeExif < 2 && this.reconstructionExifSize > 0)
        || (this.storeXmp < 2 && this.reconstructionXmpSize > 0);

    /// <summary>
    /// Moves the input data forward by size bytes.
    /// </summary>
    /// <param name="size">Number of bytes to advance.</param>
    /// <exception cref="InvalidOperationException">Thrown if advancing out of bounds.</exception>
    public void AdvanceInput(long size)
    {
        if (this.availableInput < size)
        {
            throw new InvalidOperationException("Attempting to advance out of bounds");
        }

        this.nextInput += size;
        this.filePosition += size;
        this.availableInput -= size;
    }

    /// <summary>
    /// Returns number of available bytes in the code stream.
    /// </summary>
    /// <returns>
    /// Number of available code stream bytes.
    /// </returns>
    public long AvailableCodeStream()
    {
        long avail = this.availableInput;

        if (!this.boxContentsUnbounded)
        {
            avail = Math.Min(avail, this.boxContentsEnd - this.filePosition);
        }

        return avail;
    }

    /// <summary>
    /// Ensures that the copy of the code stream is present.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the copy is missing or null.
    /// </exception>
    private void EnsureCodeStreamCopy()
    {
        if (this.codestreamCopy is null)
        {
            throw new InvalidOperationException("Copy of the code stream is missing");
        }
    }

    /// <summary>
    /// Moves forward by 'size' bytes in the code stream.
    /// </summary>
    /// <param name="size">Number of bytes to advance.</param>
    public void AdvanceCodeStream(long size)
    {
        this.EnsureCodeStreamCopy();
        long avail = this.AvailableCodeStream();

        if (this.codestreamCopy!.Length == 0)
        {
            if (size <= avail)
            {
                // We have >= size bytes available, so
                // advancing won't be out of bounds.
                this.AdvanceInput(size);
            }
            else
            {
                // We have a limited amount of bytes for the
                // code stream, and advancing by size would be
                // out of bounds. So limit the value.
                this.codestreamPos = size - avail;
                this.AdvanceInput(avail);
            }
        }
        else
        {
            this.codestreamPos += size;
            if (this.codestreamPos + this.codestreamUnconsumed >= this.codestreamCopy.Length)
            {
                long advance = Math.Min(
                    this.codestreamUnconsumed,
                    this.codestreamUnconsumed + this.codestreamPos - this.codestreamCopy.Length);

                this.AdvanceInput(advance);

                this.codestreamPos -= Math.Min(this.codestreamPos, this.codestreamCopy.Length);
                this.codestreamUnconsumed = 0;

                // Now we want to clear the code stream copy...
                this.codestreamCopy.Dispose();
                this.codestreamCopy = new(this.Options.Configuration.MemoryAllocator);
            }
        }
    }

    /// <summary>
    /// Attempts to expand the buffer.
    /// </summary>
    /// <returns>Status of requesting more input.</returns>
    public bool TryRequestMoreInput()
    {
        this.EnsureCodeStreamCopy();

        if (this.codestreamCopy!.Length > 0)
        {
            long avail = this.AvailableCodeStream();

            if (!this.CanAddBuffer(avail))
            {
                return false;
            }

            using IMemoryOwner<byte> codestreamPending = this.Options.Configuration.MemoryAllocator.Allocate<byte>((int)avail);
            this.codestreamCopy.Write(codestreamPending.Memory.Span);
        }
        else
        {
            this.AdvanceInput(this.codestreamUnconsumed);
            this.codestreamUnconsumed = 0;
        }

        return true;
    }

    public Memory<byte>? TryGetCodestreamInput()
    {
        if (this.codestreamCopy is null)
        {
            return null;
        }

        if (this.codestreamCopy.Length == 0 && this.codestreamPos > 0)
        {
            long avail = this.AvailableCodeStream();
            long skip = Math.Min(this.codestreamPos, avail);
            this.Skip(skip);
            this.codestreamPos -= skip;

            if (this.codestreamPos > 0)
            {
                _ = this.TryRequestMoreInput();
                return null;
            }
        }

        if (this.codestreamPos > this.codestreamCopy.Length)
        {
            throw new InvalidOperationException("Codestream position > length of codestream copy");
        }

        if (this.codestreamUnconsumed > this.codestreamCopy.Length)
        {
            throw new InvalidOperationException("Codestream unconsumed > length of codestream copy");
        }

        long availCodestream = this.AvailableCodeStream();

        if (this.codestreamCopy.Length == 0)
        {
            if (availCodestream == 0)
            {
                _ = this.TryRequestMoreInput();
                return null;
            }

            return this.nextInput!.Memory[..(int)availCodestream];
        }
        else
        {
            if (!this.CanAddBuffer(availCodestream))
            {
                return null;
            }

            this.codestreamCopy.Write(this.nextInput!.Memory.Span.Slice((int)this.codestreamUnconsumed, (int)(availCodestream - this.codestreamUnconsumed)));

            this.codestreamUnconsumed = availCodestream;

            return this.codestreamCopy.AsMemory();
        }
    }

    /// <summary>
    /// Returns true if the decoder can continue using code stream input.
    /// </summary>
    /// <returns>True if the decoder can use code stream input. Otherwise false.</returns>
    public bool CanUseMoreCodestreamInput() => this.decoderStage != JxlDecoderStage.CodeStreamFinished;

    /// <summary>
    /// Checks if width * height can be represented safely as a
    /// positive integer after rounding the width up to the next
    /// multiple of 32.
    /// </summary>
    /// <param name="width">Input width.</param>
    /// <param name="height">Input height.</param>
    /// <returns>
    /// Boolean indicating whether the padded image dimensions fit
    /// within a signed 32-bit integer when calculating the total
    /// pixel count.
    /// </returns>
    /// <remarks>
    /// Negative values aren't rejected, but will produce incorrect
    /// results. This method is meant to be used with positive values only.
    /// </remarks>
    public static bool CheckSizeLimit(int width, int height)
    {
        if (width == 0 || height == 0)
        {
            return true;
        }

        int paddedWidth = JxlMath.DivCeil(width, 32) * 32;

        if (paddedWidth < width)
        {
            // Overflow
            return false;
        }

        int pixelCount = paddedWidth * height;

        if (pixelCount / paddedWidth != height)
        {
            // Overflow
            return false;
        }

        return true;
    }

    /// <summary>
    /// Resets the decoder state to its default values, and,
    /// additionally, releases memory used by buffers and replaces
    /// them with new fresh copies.
    /// </summary>
    public void RewindDecodingState()
    {
        this.decoderStage = JxlDecoderStage.Initialized;

        this.gotSignature = false;
        this.lastCodestreamSeen = false;
        this.gotCodestreamSignature = false;
        this.gotBasicInfo = false;
        this.gotTransformData = false;
        this.gotAllHeaders = false;
        this.postHeaders = false;

        this.iccProfile = null;

        this.gotPreviewImage = false;
        this.previewFrame = false;
        this.filePosition = 0;

        this.boxContentsBegin = 0;
        this.boxContentsEnd = 0;
        this.boxContentsSize = 0;
        this.boxSize = 0;
        this.headerSize = 0;
        this.boxContentsUnbounded = false;

        this.boxType = null;
        this.boxDecodedType = null;

        this.boxEvent = false;
        this.boxStage = JxlBoxStage.Header;

        this.jxlFileFormatVersion = 0;
        this.nextJxlpIndex = 0;
        this.jxlpOooBuffer.Clear();
        this.jxlpOooBufferTotal = 0;
        this.bufferingJxlpIndex = 0;
        this.bufferingJxlpIsLast = false;

        this.boxOutBufferSet = false;
        this.boxOutBufferSetCurrentBox = false;
        this.boxOutputBuffer?.Dispose();
        this.boxOutputBuffer = null;
        this.boxOutBufferSize = 0;
        this.boxOutBufferBegin = 0;
        this.boxOutBufferPos = 0;

        this.exifMetadata?.Dispose();
        this.exifMetadata = null;
        this.xmpMetadata?.Dispose();
        this.xmpMetadata = null;
        this.storeExif = 0;
        this.storeXmp = 0;

        this.reconstructionOutputBufferPos = 0;
        this.reconstructionExifSize = 0;
        this.reconstructionXmpSize = 0;
        this.reconstructionOutputJpeg = JpegReconstructionStage.None;

        this.eventsWanted = this.originalEventsWanted;
        this.basicInfoSizeHint = InitialBasicInfoSizeHint();
        this.haveContainer = false;
        this.boxCount = 0;
        this.downsamplingTarget = 8;

        this.imageOutBufferSet = false;
        this.imageOutBuffer?.Dispose();
        this.imageOutBuffer = null;
        this.imageOutputInitCallback = null;
        this.imageOutputRunCallback = null;
        this.imageOutputDestroyCallback = null;
        this.imageOutputSize = 0;

        this.imageOutputBitDepth = new()
        {
            Type = JxlBitDepthType.FromPixelFormat
        };

        this.extraChannelOutputs.Clear();

        this.nextInput?.Dispose();
        this.nextInput = null;

        this.availableInput = 0;
        this.inputClosed = false;

        this.passesState?.Reset();
        this.frameDecoder?.Reset();
        this.nextSection = 0;
        this.sectionProcessed.Clear();

        this.imageBundle.Reset();
        this.metadata = new JxlCodecMetadata();
        this.imageMetadata = this.metadata.ImageMetadata;

        this.frameHeader = new()
        {
            Metadata = this.metadata
        };

        this.codestreamCopy?.Dispose();
        this.codestreamCopy = new(this.Options.Configuration.MemoryAllocator);
        this.codestreamUnconsumed = 0;
        this.codestreamPos = 0;
        this.codestreamBitsAhead = 0;

        this.frameStage = JxlFrameStage.Header;
        this.remainingFrameSize = 0;
        this.isLastOfStill = false;
        this.isLastTotal = false;
        this.skipFrames = 0;
        this.skippingFrame = false;
        this.internalFrames = 0;
        this.externalFrames = 0;
    }

    /// <summary>
    /// Resets the decoder to its default values.
    /// </summary>
    public void Reset()
    {
        this.RewindDecodingState();

        this.keepOrientation = false;
        this.unpremultiplyAlpha = false;
        this.renderSpotcolors = true;
        this.coalescing = true;
        this.desiredIntensityTarget = 0f;
        this.originalEventsWanted = 0;
        this.eventsWanted = 0;

        this.frameReferences.Clear();
        this.frameExternalToInternal.Clear();
        this.frameRequired.Clear();

        this.decompressBoxes = false;
    }

    /// <summary>
    /// Returns the code stream as a Span.
    /// </summary>
    /// <returns>A Span representing the code stream.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the code stream cannot be retrieved.</exception>
    private Span<byte> GetCodeStreamSpan()
    {
        Memory<byte> codestreamInput = this.TryGetCodestreamInput()
            ?? throw new InvalidOperationException("Cannot retrieve codestream input");

        Span<byte> span = codestreamInput.Span;

        return span;
    }

    /// <summary>
    /// Skips frames without decoding them.
    /// </summary>
    /// <param name="amount">Number of frames to skip.</param>
    public void SkipFrames(int amount)
    {
        this.skipFrames += amount;
        this.frameRequired.Clear();

        int nextFrame = this.externalFrames + this.skipFrames;

        if (nextFrame < this.frameExternalToInternal.Count)
        {
            int internalIndex = this.frameExternalToInternal[nextFrame];
            if (internalIndex < this.frameReferences.Count)
            {
                List<int> deps = this.GetFrameDependencies(internalIndex, CollectionsMarshal.AsSpan(this.frameReferences));
                this.ResizeFrameRequired(internalIndex + 1);

                foreach (int index in deps)
                {
                    if (index < this.frameRequired.Count)
                    {
                        this.frameRequired[index] = 1;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Ensures that frameRequired's count reaches <paramref name="upperBound"/>.
    /// </summary>
    /// <param name="upperBound">Max. number of items that frameRequired must have.</param>
    private void ResizeFrameRequired(int upperBound)
    {
        while (this.frameRequired.Count < upperBound)
        {
            this.frameRequired.Add(0);
        }
    }

    /// <summary>
    /// Skips the current frame without having to decode it.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the frame cannot be skipped.
    /// </exception>
    public void SkipCurrentFrame()
    {
        if (this.frameStage == JxlFrameStage.Full)
        {
            throw new InvalidOperationException("The decoder is ready to parse the frame, so the frame cannot be skipped");
        }

        this.frameStage = JxlFrameStage.Header;
        this.AdvanceCodeStream(this.remainingFrameSize);

        if (this.isLastOfStill)
        {
            this.imageOutBufferSet = false;
        }
    }

    /// <summary>
    /// Ensures that the decoder state doesn't change while
    /// it's busy decoding an image.
    /// </summary>
    /// <param name="propertyName">Name of the parameter that was changed/</param>
    /// <exception cref="InvalidOperationException">Thrown if the state doesn't match initialization.</exception>
    private void BeforeUpdateState(string propertyName)
    {
        if (this.decoderStage != JxlDecoderStage.Initialized)
        {
            throw new InvalidOperationException("The decoder is already processing the image, so " + propertyName + " cannot be changed");
        }
    }

    /// <summary>
    /// Reads a single bundle into <paramref name="bundle"/>.
    /// </summary>
    /// <typeparam name="T">Type of the bundle to read.</typeparam>
    /// <param name="stream">Stream to read data from.</param>
    /// <param name="br">Bit reader to continue from.</param>
    /// <param name="bundle">The bundle to parse.</param>
    /// <returns>Status of parsing the bundle.</returns>
    private bool ReadBundle<T>(Stream stream, JxlBitReader br, T bundle)
        where T : IJxlFields
    {
        JxlBitReader reader = new(stream);
        reader.SkipBits64((ulong)br.TotalBitsConsumed);

        bool canRead = JxlBundle.CanRead(reader, bundle);

        if (!canRead)
        {
            return this.TryRequestMoreInput();
        }

        if (!JxlBundle.Read(reader, bundle))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Reads all basic metadata and headers.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the data is incorrect.</exception>
    /// <exception cref="InvalidDataException">Thrown if the data is malformed.</exception>
    public void ReadBasicInfo(Stream stream)
    {
        if (!this.gotCodestreamSignature)
        {
            Span<byte> fileSignature = [0, 0];
            stream.ReadExactly(fileSignature);

            if (fileSignature[0] != 0xFF || fileSignature[1] != JxlShared.CodestreamMarker)
            {
                throw new InvalidOperationException("The file signature is invalid");
            }

            this.gotCodestreamSignature = true;
        }

        JxlBitReader bitReader = new(stream);

        if (!this.ReadBundle(stream, bitReader, this.metadata!.Size!))
        {
            throw new InvalidDataException("Could not parse the size header");
        }

        if (!this.ReadBundle(stream, bitReader, this.metadata!.ImageMetadata!))
        {
            throw new InvalidDataException("Could not parse the image metadata");
        }

        long totalBits = bitReader.TotalBitsConsumed;

        (long div, long rem) = Math.DivRem(totalBits, JxlMath.BitsPerByte);
        this.AdvanceCodeStream(div);

        this.codestreamBitsAhead = rem;
        this.gotBasicInfo = true;
        this.basicInfoSizeHint = 0;
        this.imageMetadata = this.metadata.ImageMetadata;

        if (!CheckSizeLimit(this.metadata.Size!.XSize, this.metadata.Size.YSize))
        {
            throw new InvalidOperationException("The image is too large");
        }
    }

    /// <summary>
    /// Parses all necessary headers.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if data is incorrect.</exception>
    public void ReadAllHeaders()
    {
        if (!this.gotTransformData)
        {
            Span<byte> span = this.GetCodeStreamSpan();

            JxlBitReader reader = new(this.stream);
            reader.SkipBits64((ulong)this.codestreamBitsAhead);

            this.metadata!.CustomTransformData!.NonserializedXybEncoded = this.metadata.ImageMetadata!.XybEncoded;

            if (!this.ReadBundle(span, reader, this.metadata.CustomTransformData))
            {
                throw new InvalidOperationException("Cannot read custom transform data bundle");
            }

            long totalBits = reader.TotalBitsConsumed;
            (long div, long rem) = Math.DivRem(totalBits, JxlMath.BitsPerByte);

            this.AdvanceCodeStream(div);
            this.codestreamBitsAhead = rem;
            this.gotTransformData = true;
        }

        Span<byte> sp = this.GetCodeStreamSpan();

        JxlBitReader bitReader = new(sp);
        bitReader.SkipBits64((ulong)this.codestreamBitsAhead);

        if (this.metadata!.ImageMetadata!.ColorEncoding!.NeedsIcc)
        {
            // TODO: optimize this? ImageSharp ICC doesn't support spans
            // so we need to allocate an array.
            IccDataReader reader = new(sp.ToArray());
            IccProfileHeader header = IccReader.ReadHeader(reader);
            IccTagDataEntry[] tagData = IccReader.ReadTagData(reader);

            IccProfile icc = new(header, tagData);
            this.iccProfile = icc;
            sp = sp[reader.Index..];

            byte[] iccRawData = icc.ToByteArray();
            this.metadata.ImageMetadata.ColorEncoding.SetIccRaw(iccRawData);
        }

        this.gotAllHeaders = true;
        bitReader.JumpToByteBoundary();

        this.AdvanceCodeStream(bitReader.TotalBitsConsumed / JxlMath.BitsPerByte);
        this.codestreamBitsAhead = 0;

        this.passesState ??= new(this.frameHeader!, this.Options.Configuration);
        this.passesState.OutputEncodingInfo.SetFromMetadata(this.metadata);

        if (this.desiredIntensityTarget > 0f)
        {
            this.passesState.OutputEncodingInfo.DesiredIntensityTarget = this.desiredIntensityTarget;
        }

        this.imageMetadata = this.metadata.ImageMetadata;
    }

    /// <summary>
    /// Processes all sections in this JPEG XL images and invokes
    /// the frame decoder.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the data is invalid or malformed.</exception>
    public void ProcessSections()
    {
        Span<byte> span = this.GetCodeStreamSpan();

        var toc = this.frameDecoder!.Toc;

        long pos = 0;
        List<JxlSectionInfo> sectionInfo = [];
        List<JxlSectionStatus> sectionStatus = [];

        for (long i = this.nextSection; i < toc.Size; i++)
        {
            if (this.sectionProcessed[(int)i] != 0)
            {
                pos += toc[i].Size;
                continue;
            }

            long id = toc[i].Id;
            long size = toc[i].Size;

            if (IsOutOfBounds((int)pos, (int)size, span.Length))
            {
                break;
            }

            JxlBitReader br = new(span.Slice((int)pos, (int)size));
            sectionInfo.Add(new(br, (int)id, (int)i));
            sectionStatus.Add(default);
            pos += size;
        }

        this.frameDecoder.ProcessSections(sectionInfo, sectionStatus);

        for (int i = 0; i < sectionStatus.Count; i++)
        {
            JxlSectionStatus ss = sectionStatus[i];

            if (ss == JxlSectionStatus.Done)
            {
                this.sectionProcessed[sectionInfo[i].Index] = 1;
            }
            else if (ss != JxlSectionStatus.Skipped)
            {
                throw new InvalidOperationException("Unexpected section status");
            }
        }

        long completedPrefixBytes = 0;

        while (this.nextSection < this.sectionProcessed.Count && this.sectionProcessed[(int)this.nextSection] == 1)
        {
            completedPrefixBytes += toc[(int)this.nextSection].Size;
            this.nextSection++;
        }

        this.remainingFrameSize -= completedPrefixBytes;
        this.AdvanceCodeStream(completedPrefixBytes);
    }

    /// <summary>
    /// Processes all codestream contents.
    /// </summary>
    /// <returns>Status of codestream processing.</returns>
    /// <exception cref="InvalidOperationException">Thrown when data is corrupt, malformed, or incorrect.</exception>
    public int ProcessCodestream()
    {
        if (!this.gotBasicInfo)
        {
            this.ReadBasicInfo();
        }

        if ((this.eventsWanted & JxlDecoderStatus.BasicInfo) != 0)
        {
            this.eventsWanted &= ~JxlDecoderStatus.BasicInfo;
            return JxlDecoderStatus.BasicInfo;
        }

        if (this.eventsWanted == 0)
        {
            this.decoderStage = JxlDecoderStage.CodeStreamFinished;
            return JxlDecoderStatus.Success;
        }

        if (!this.gotAllHeaders)
        {
            this.ReadAllHeaders();
        }

        if ((this.eventsWanted & JxlDecoderStatus.ColorEncoding) != 0)
        {
            this.eventsWanted &= ~JxlDecoderStatus.ColorEncoding;
            return JxlDecoderStatus.ColorEncoding;
        }

        if (this.eventsWanted == 0)
        {
            this.decoderStage = JxlDecoderStage.CodeStreamFinished;
            return JxlDecoderStatus.Success;
        }

        this.postHeaders = true;

        if (!this.gotPreviewImage && this.metadata!.ImageMetadata!.HavePreview)
        {
            this.previewFrame = true;
        }

        while (true)
        {
            bool parseFrames = (this.eventsWanted & (JxlDecoderStatus.PreviewImage | JxlDecoderStatus.Frame | JxlDecoderStatus.FullImage)) != 0;
            if (!parseFrames || (this.frameStage == JxlFrameStage.Header && this.isLastTotal))
            {
                break;
            }

            if (this.frameStage == JxlFrameStage.Header)
            {
                if (this.reconstructionOutputJpeg is JpegReconstructionStage.SetMetadata or JpegReconstructionStage.Output)
                {
                    throw new InvalidOperationException("Cannot decode frames following a JPEG reconstruction frame");
                }

                this.imageBundle ??= new(this.imageMetadata!);

                if (!this.jpegDecoder.SetImageBundleJpegData(this.imageBundle!))
                {
                    throw new InvalidOperationException("Cannot set JXL->JPEG decoder image bundle");
                }

                this.frameDecoder = new(this.passesState!, this.metadata!, useSlowRenderingPipeline: false);
                this.frameHeader = new()
                {
                    Metadata = this.metadata
                };

                Span<byte> span = this.GetCodeStreamSpan();
                JxlBitReader reader = new(span);

                this.frameDecoder.InitializeFrame(reader, this.imageBundle!, this.previewFrame);

                this.AdvanceCodeStream(reader.TotalBitsConsumed / JxlMath.BitsPerByte);
                this.frameHeader = this.frameDecoder.GetFrameHeader();

                JxlFrameDimensions dim = this.frameHeader.FrameDimensions;

                if (!CheckSizeLimit(dim.XSizeUpsampledPadded, dim.YSizeUpsampledPadded))
                {
                    throw new InvalidOperationException("Frame is too large");
                }

                int outputType = this.previewFrame ? JxlDecoderStatus.PreviewImage : JxlDecoderStatus.FullImage;
                bool outputNeeded = (this.eventsWanted & outputType) != 0;

                if (outputNeeded)
                {
                    this.frameDecoder.InitializeFrameOutput();
                }

                this.remainingFrameSize = this.frameDecoder.SumSectionSizes();

                this.frameStage = JxlFrameStage.Toc;
                if (this.previewFrame)
                {
                    if ((this.eventsWanted & JxlDecoderStatus.PreviewImage) == 0)
                    {
                        this.frameStage = JxlFrameStage.Header;
                        this.AdvanceCodeStream(this.remainingFrameSize);
                        this.gotPreviewImage = true;
                        this.previewFrame = false;
                    }

                    continue;
                }

                int savedAs = JxlFrameDecoder.SavedAs(this.frameHeader);
                this.isLastTotal = this.frameHeader.IsLast;
                this.isLastOfStill = this.isLastTotal || this.frameHeader.AnimationFrame!.Duration > 0;
                this.isLastOfStill |= !this.coalescing && this.frameHeader.FrameType == JxlFrameType.RegularFrame;

                int internalFrameIndex = this.internalFrames;
                int externalFrameIndex = this.externalFrames;

                if (this.isLastOfStill)
                {
                    this.externalFrames++;
                }

                this.internalFrames++;

                if (this.skipFrames > 0)
                {
                    this.skippingFrame = true;

                    if (this.isLastOfStill)
                    {
                        this.skipFrames--;
                    }
                }
                else
                {
                    this.skippingFrame = false;
                }

                if (externalFrameIndex >= this.frameExternalToInternal.Count)
                {
                    this.frameExternalToInternal.Add(internalFrameIndex);

                    if (this.frameExternalToInternal.Count != externalFrameIndex + 1)
                    {
                        throw new InvalidOperationException("Internal error");
                    }
                }

                if (internalFrameIndex >= this.frameReferences.Count)
                {
                    this.frameReferences.Add(new JxlFrameReference(0xFF, savedAs));

                    if (this.frameReferences.Count != internalFrameIndex + 1)
                    {
                        throw new InvalidOperationException("Internal error");
                    }
                }

                if (this.skippingFrame)
                {
                    bool referenceable = this.frameHeader.CanBeReferenced || this.frameHeader.FrameType == JxlFrameType.DcFrame;

                    if (internalFrameIndex < this.frameRequired.Count && this.frameRequired[internalFrameIndex] == 0)
                    {
                        referenceable = false;
                    }

                    if (!referenceable)
                    {
                        this.frameStage = JxlFrameStage.Header;
                        this.AdvanceCodeStream(this.remainingFrameSize);
                        continue;
                    }
                }

                if ((this.eventsWanted & JxlDecoderStatus.Frame) != 0 && this.isLastOfStill)
                {
                    if (!this.skippingFrame)
                    {
                        return JxlDecoderStatus.Frame;
                    }
                }

                if (this.frameStage == JxlFrameStage.Toc)
                {
                    this.frameDecoder.SetRenderSpotcolors(this.renderSpotcolors);
                    this.frameDecoder.SetCoalescing(this.coalescing);

                    if (!this.previewFrame && (this.eventsWanted & JxlDecoderStatus.Progression) != 0)
                    {
                        this.frameProgressiveDetail = this.frameDecoder.SetPauseAtProgressive(this.progressiveDetail);
                    }
                    else
                    {
                        this.frameProgressiveDetail = JxlProgressiveDetail.Frames;
                    }

                    this.dcFrameProgressionDone = false;
                    this.nextSection = 0;
                    this.sectionProcessed.Clear();
                    ResizeSectionProcessed(this.frameDecoder.Toc.Size);

                    if (this.previewFrame || (this.eventsWanted & JxlDecoderStatus.FullImage) != 0)
                    {
                        this.frameStage = JxlFrameStage.Full;
                    }
                    else if (!this.isLastTotal)
                    {
                        this.frameStage = JxlFrameStage.Header;
                        this.AdvanceCodeStream(this.remainingFrameSize);
                        continue;
                    }
                    else
                    {
                        break;
                    }
                }

                if (this.frameStage == JxlFrameStage.Full)
                {
                    if (!this.imageOutBufferSet)
                    {
                        if (this.previewFrame)
                        {
                            return JxlDecoderStatus.NeedPreviewOutBuffer;
                        }

                        if ((!this.jpegDecoder.IsOutputSet || this.imageBundle!.JpegData is null)
                            && this.isLastOfStill
                            && !this.skippingFrame)
                        {
                            throw new InvalidOperationException("Image output buffer is too small");
                        }
                    }

                    if (this.imageOutBufferSet)
                    {
                        Size dimensions = this.CurrentDimensions;
                        int bitsPerSample = GetBitDepth(this.imageOutputBitDepth, this.metadata!.ImageMetadata!, this.imageOutputFormat);

                        this.frameDecoder.SetImageOutput(
                            new PixelCallback(
                                this.imageOutputInitCallback,
                                this.imageOutputRunCallback,
                                this.imageOutputDestroyCallback),
                            this.imageOutBuffer,
                            this.imageOutputSize,
                            dimensions.Width,
                            dimensions.Height,
                            this.imageOutputFormat,
                            bitsPerSample,
                            this.unpremultiplyAlpha,
                            !this.keepOrientation);

                        for (int i = 0; i < this.extraChannelOutputs.Count; i++)
                        {
                            JxlExtraChannelOutput extra = this.extraChannelOutputs[i];
                            int ecBitsPerSample = GetBitDepth(this.imageOutputBitDepth, this.metadata.ImageMetadata!.ExtraChannels[i], extra.Format);

                            this.frameDecoder.AddExtraChannelOutput(
                                extra.Buffer,
                                extra.BufferSize,
                                dimensions.Width,
                                extra.Format,
                                ecBitsPerSample);
                        }
                    }

                    long nextNumPassesToPause = this.frameDecoder.NextNumPassesToPause;

                    this.ProcessSections();

                    bool allSectionsDone = this.frameDecoder.DecodedAll;
                    bool gotDcOnly = !allSectionsDone && this.frameDecoder.HasDecodedDc;

                    if (this.frameProgressiveDetail >= JxlProgressiveDetail.Dc &&
                        !this.dcFrameProgressionDone &&
                        gotDcOnly)
                    {
                        this.dcFrameProgressionDone = true;
                        this.downsamplingTarget = 8;
                        return JxlDecoderStatus.Progression;
                    }

                    bool newProgressionStepDone = this.frameDecoder.NumCompletePasses >= nextNumPassesToPause;

                    if (!allSectionsDone &&
                        this.frameProgressiveDetail >= JxlProgressiveDetail.LastPasses &&
                        newProgressionStepDone)
                    {
                        this.downsamplingTarget = this.frameHeader.Passes.GetDownsamplingTargetForCompletedPasses(this.frameDecoder.NumCompletePasses);
                        return JxlDecoderStatus.Progression;
                    }

                    if (!allSectionsDone)
                    {
                        return this.TryRequestMoreInput() ? 1 : 0;
                    }

                    if (!this.previewFrame)
                    {
                        long internalIndex = this.internalFrames - 1;
                        if (this.frameReferences.Count <= internalIndex)
                        {
                            throw new InvalidOperationException("Internal error");
                        }

                        this.frameReferences[(int)internalIndex].Reference = this.frameDecoder.References;
                    }

                    this.frameDecoder.FinalizeFrame();

                    if (this.jpegDecoder.IsOutputSet && this.imageBundle!.JpegData is not null)
                    {
                        this.frameStage = JxlFrameStage.Header;
                        this.reconstructionOutputJpeg = JpegReconstructionStage.SetMetadata;

                        return JxlDecoderStatus.FullImage;
                    }

                    if (this.previewFrame || this.isLastOfStill)
                    {
                        this.imageOutBufferSet = false;
                        this.extraChannelOutputs.Clear();
                    }
                }

                this.frameStage = JxlFrameStage.Header;
                this.imageBundle.Reset();

                if (this.previewFrame)
                {
                    this.gotPreviewImage = true;
                    this.previewFrame = false;
                    this.eventsWanted &= ~JxlDecoderStatus.PreviewImage;
                    return JxlDecoderStatus.PreviewImage;
                }
                else if (this.isLastOfStill && (this.eventsWanted & JxlDecoderStatus.FullImage) != 0 && !this.skippingFrame)
                {
                    return JxlDecoderStatus.FullImage;
                }
            }
        }

        this.decoderStage = JxlDecoderStage.CodeStreamFinished;
        return 1;
    }

    /// <summary>
    /// Sets the input JPEG XL data to <paramref name="data"/>.
    /// </summary>
    /// <param name="data">The input data to parse JPEG XL.</param>
    /// <exception cref="InvalidOperationException">Thrown if the input data cannot be changed at this moment.</exception>
    public void SetInput(IMemoryOwner<byte> data)
    {
        if (this.nextInput is not null)
        {
            throw new InvalidOperationException("Input is already present. Use DisposeInput first");
        }

        if (this.inputClosed)
        {
            throw new InvalidOperationException("Input is closed");
        }

        this.nextInput = data;
        this.availableInput = data.Memory.Length;
    }

    /// <summary>
    /// Disposes the input data.
    /// </summary>
    /// <returns>
    /// Number of available bytes left in the input data before disposal.
    /// </returns>
    public long DisposeInput()
    {
        long previousAvailableBytes = this.availableInput;

        this.nextInput?.Dispose();
        this.nextInput = null;
        this.availableInput = 0;

        return previousAvailableBytes;
    }

    /// <summary>
    /// Closes the input stream so it can't be read anymore.
    /// </summary>
    public void CloseInput() => this.inputClosed = true;

    /// <summary>
    /// Sets the output buffer for JPEG reconstruction.
    /// </summary>
    /// <param name="data">Buffer for JPEG reconstruction.</param>
    /// <exception cref="InvalidOperationException">Thrown when the buffer can't be set.</exception>
    public void SetJpegBuffer(Memory<byte> data)
    {
        if (this.internalFrames > 1)
        {
            throw new InvalidOperationException("JPEG reconstruction only works for first frames");
        }

        if (this.jpegDecoder.IsOutputSet)
        {
            throw new InvalidOperationException("Already set JPEG buffer");
        }

        this.jpegDecoder.SetOutputBuffer(data);
    }

    /// <summary>
    /// Parses the start of a box.
    /// </summary>
    /// <param name="input">Input bytes to parse from.</param>
    /// <param name="type">Type of the box</param>
    /// <param name="boxSize">Output box size.</param>
    /// <param name="headerSize">Output header size.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when data is invalid.
    /// </exception>
    private static void ParseBoxHeader(Stream input, out int type, out long boxSize, out long headerSize)
    {
        JxlBoxHeader header = JxlBoxHeader.ReadHeader(input);
        boxSize = (long)header.Size;
        headerSize = (header.ContainsLargeSize ? 12 : 4) + 4;
        type = (int)header.Type;
    }

    /// <summary>
    /// Processes all boxes and their contents if this is a container format.
    /// </summary>
    /// <param name="stream">Stream to decode from.</param>
    /// <returns>Status of processing.</returns>
    /// <exception cref="InvalidOperationException">Thrown when data is invalid.</exception>
    public int ProcessBoxes(Stream stream)
    {
        // We have a box handling loop here.
        while (true)
        {
            if (this.boxStage != JxlBoxStage.Header)
            {
                // this.AdvanceInput(this.headerSize);
                this.headerSize = 0;

                if ((this.eventsWanted & JxlDecoderStatus.Box) != 0 && this.boxEvent && !this.boxOutBufferSetCurrentBox)
                {
                    this.boxEvent = false;
                }

                if ((this.eventsWanted & JxlDecoderStatus.Box) != 0 && this.boxOutBufferSetCurrentBox)
                {
                    Memory<byte> nextOut = this.boxOutputBuffer!.Memory[(int)this.boxOutBufferPos..];
                    long availOut = this.boxOutBufferSize - this.boxOutBufferPos;

                    Span<byte> bufferSpan = this.boxOutputBuffer.Memory.Span;
                    Span<byte> startSlice = bufferSpan[(int)this.boxOutBufferPos..];

                    int status = this.boxContentDecoder!.Process(
                        stream,
                        this.boxOutputBuffer);

                    long produced = startSlice.Length - availOut;
                    this.boxOutBufferPos += produced;

                    if (status == JxlDecoderStatus.Complete && (this.eventsWanted & JxlDecoderStatus.Complete) == 0)
                    {
                        status = JxlDecoderStatus.Success;
                    }

                    if (status is not (JxlDecoderStatus.Success or JxlDecoderStatus.NeedMoreInput))
                    {
                        return status;
                    }
                }

                if (this.storeExif == 1 || this.storeXmp == 1)
                {
                    IMemoryOwner<byte> metadata = (this.storeExif == 1 ? this.exifMetadata : this.xmpMetadata)
                        ?? throw new InvalidOperationException("Metadata is missing, but should be present");

                    // Boxes should not contain more than 64MiB data.
                    const long blockSizeLimit = 64L << 20;

                    // Use array version of metadata so we
                    // can resize the array.
                    byte[] md = metadata.Memory.ToArray();

                    while (true)
                    {
                        if (md.Length == 0)
                        {
                            Array.Resize(ref md, 64);
                        }

                        Span<byte> originalNextOutput = md.AsSpan()[(int)this.reconstructionOutputBufferPos..];
                        Span<byte> nextOutput = originalNextOutput;
                        long availableOutput = md.Length - this.reconstructionOutputBufferPos;

                        int boxResult = this.metadataDecoder.Decode(
                            this.nextInput,
                            this.availableInput,
                            this.filePosition - this.boxContentsBegin,
                            ref nextOutput,
                            ref availableOutput);

                        long produced = originalNextOutput.Length - nextOutput.Length;
                        this.reconstructionOutputBufferPos += produced;

                        if (boxResult == JxlDecoderStatus.NeedMoreOutput)
                        {
                            if (md.Length >= blockSizeLimit)
                            {
                                throw new InvalidOperationException("Box with EXIF or XMP metadata is too large");
                            }

                            Array.Resize(ref md, md.Length * 2);
                        }
                        else if (boxResult == JxlDecoderStatus.NeedMoreInput)
                        {
                            break;
                        }
                        else if (boxResult == JxlDecoderStatus.Complete)
                        {
                            long neededSize = this.storeExif == 1 ? this.reconstructionExifSize : this.reconstructionXmpSize;

                            if (this.boxContentsUnbounded && this.reconstructionOutputBufferPos < neededSize)
                            {
                                break;
                            }
                            else
                            {
                                Array.Resize(ref md, (int)this.reconstructionOutputBufferPos);

                                if (this.storeExif == 1)
                                {
                                    this.storeExif = 2;
                                }

                                if (this.storeXmp == 1)
                                {
                                    this.storeXmp = 2;
                                }

                                break;
                            }
                        }
                        else
                        {
                            // Error
                            return boxResult;
                        }
                    }
                }
            }

            if (this.reconstructionOutputJpeg == JpegReconstructionStage.SetMetadata && this.JbrdNeedsMoreBoxes())
            {
                JpegData jpegData = this.imageBundle!.JpegData.GetData();

                if (this.reconstructionExifSize > 0)
                {
                    JxlToJpegDecoder.TrySetExif(this.exifMetadata!.Memory.Span, jpegData);
                }

                if (this.reconstructionXmpSize > 0)
                {
                    JxlToJpegDecoder.TrySetXmp(this.xmpMetadata!.Memory.Span, jpegData);
                }

                this.reconstructionOutputJpeg = JpegReconstructionStage.Output;
            }

            if (this.reconstructionOutputJpeg == JpegReconstructionStage.Output && !this.JbrdNeedsMoreBoxes())
            {
                this.jpegDecoder!.WriteOutput(this.imageBundle!.JpegData);

                this.reconstructionOutputJpeg = JpegReconstructionStage.None;
                this.imageBundle.Reset();

                if ((this.eventsWanted & JxlDecoderStatus.FullImage) != 0)
                {
                    return JxlDecoderStatus.FullImage;
                }
            }

            if (this.boxStage == JxlBoxStage.Header)
            {
                if (!this.haveContainer)
                {
                    if (this.decoderStage == JxlDecoderStage.CodeStreamFinished)
                    {
                        return JxlDecoderStatus.Success;
                    }

                    this.boxStage = JxlBoxStage.CodeStream;
                    this.boxContentsUnbounded = true;

                    continue;
                }

                if (this.availableInput == 0)
                {
                    if (this.decoderStage != JxlDecoderStage.CodeStreamFinished || this.JbrdNeedsMoreBoxes())
                    {
                        ThrowNotEnoughData();
                    }

                    if (this.inputClosed || (this.eventsWanted & JxlDecoderStatus.Box) != 0)
                    {
                        return JxlDecoderStatus.Success;
                    }

                    ThrowNotEnoughData();
                }

                bool boxedCodestreamDone = ((this.eventsWanted & JxlDecoderStatus.Box) != 0)
                    && this.decoderStage == JxlDecoderStage.CodeStreamFinished
                    && !this.JbrdNeedsMoreBoxes()
                    && this.lastCodestreamSeen;

                if (boxedCodestreamDone &&
                    this.availableInput >= 2 &&
                    this.nextInput!.Memory.Span[0] == 0xFF &&
                    this.nextInput.Memory.Span[1] == JxlShared.CodestreamMarker)
                {
                    return JxlDecoderStatus.Success;
                }

                int status = ParseBoxHeader(this.nextInput, this.availableInput, 0, this.filePosition, this.boxType, out long boxSize, out long headerSize);

                if (this.boxType == JxlBoxTypes.Brob)
                {
                    this.boxDecodedType = BitConverter.ToInt32(this.nextInput!.Memory.Span[(int)headerSize..]);
                }
                else
                {
                    this.boxDecodedType = this.boxType;
                }

                this.boxCount++;

                if (boxedCodestreamDone && this.boxType == JxlBoxTypes.Jxl)
                {
                    return JxlDecoderStatus.Success;
                }

                if (this.boxCount == 2 && this.boxType != JxlBoxTypes.FileType)
                {
                    throw new InvalidOperationException("The second box must be a ftyp (File Type) box");
                }

                if (this.boxType == JxlBoxTypes.FileType && this.boxCount != 2)
                {
                    throw new InvalidOperationException("The ftyp (File Type) box must be a second box");
                }

                this.boxContentsUnbounded = boxSize == 0;
                this.boxContentsBegin = this.filePosition + headerSize;
                this.boxContentsEnd = this.boxContentsUnbounded ? 0 : (this.filePosition + boxSize);
                this.boxContentsSize = this.boxContentsUnbounded ? 0 : (boxSize - headerSize);
                this.boxSize = boxSize;
                this.headerSize = headerSize;

                if ((this.originalEventsWanted & JxlDecoderStatus.JpegReconstruction) != 0)
                {
                    if (this.storeExif == 0 && this.boxDecodedType == JxlBoxTypes.Exif)
                    {
                        this.storeExif = 1;
                        this.reconstructionOutputBufferPos = 0;
                    }

                    if (this.storeXmp == 0 && this.boxDecodedType == JxlBoxTypes.Xml)
                    {
                        this.storeXmp = 1;
                        this.reconstructionOutputBufferPos = 0;
                    }
                }

                if ((this.eventsWanted & JxlDecoderStatus.Box) != 0)
                {
                    bool decompress = this.decompressBoxes && this.boxType == JxlBoxTypes.Brob;
                    this.boxContentDecoder!.Initialize(decompress, this.boxContentsUnbounded, (ulong)this.boxContentsSize);
                }

                if (this.storeExif == 1 || this.storeXmp == 1)
                {
                    bool brob = this.boxType == JxlBoxTypes.Brob;
                    this.metadataDecoder!.Initialize(brob, this.boxContentsUnbounded, (ulong)this.boxContentsSize);
                }

                if (this.boxType == JxlBoxTypes.FileType)
                {
                    this.boxStage = JxlBoxStage.Ftyp;
                }
                else if (this.boxType == JxlBoxTypes.JxlCodeStream)
                {
                    if (this.lastCodestreamSeen)
                    {
                        throw new InvalidOperationException("Only one jxlc (JPEG XL codestream) box can be present");
                    }

                    this.lastCodestreamSeen = true;
                    this.boxStage = JxlBoxStage.CodeStream;
                }
                else if (this.boxType == JxlBoxTypes.JxlPartialCodeStream)
                {
                    this.boxStage = JxlBoxStage.PartialCodeStream;
                }
                else if ((this.originalEventsWanted & JxlDecoderStatus.JpegReconstruction) != 0 && this.boxType == JxlBoxTypes.JpegReconstructionData)
                {
                    if ((this.eventsWanted & JxlDecoderStatus.JpegReconstruction) == 0)
                    {
                        throw new InvalidOperationException("Multiple JPEG reconstruction boxes detected");
                    }

                    this.boxStage = JxlBoxStage.JpegReconstruction;
                }
                else
                {
                    this.boxStage = JxlBoxStage.Skip;
                }

                if ((this.eventsWanted & JxlDecoderStatus.Box) != 0)
                {
                    this.boxEvent = true;
                    this.boxOutBufferSetCurrentBox = false;
                    return JxlDecoderStatus.Box;
                }
            }
            else if (this.boxStage == JxlBoxStage.Ftyp)
            {
                if (this.boxContentsSize < 12)
                {
                    throw new InvalidOperationException("The file type box is too small");
                }

                if (BinaryUtils.ReadInt32BigEndian(stream) != 0x6A786C20) // Bytes "jxl " in Big Endian
                {
                    throw new InvalidOperationException("File type box major brand must be \"jxl \"");
                }

                uint version = BinaryUtils.ReadUInt32BigEndian(stream);
                if (version > 1)
                {
                    throw new InvalidOperationException("Unknown JXL file format version " + version + ", known versions are 0 and 1");
                }

                this.jxlFileFormatVersion = (int)version;
                this.boxStage = JxlBoxStage.Skip;
            }
            else if (this.boxStage == JxlBoxStage.PartialCodeStream)
            {
                if (this.lastCodestreamSeen)
                {
                    throw new InvalidOperationException("Cannot have jxlp box after last jxlp box");
                }

                if (!this.boxContentsUnbounded && this.boxContentsSize < 4)
                {
                    throw new InvalidOperationException("jxlp box is too small to contain an index");
                }

                uint jxlpIndex = BinaryUtils.ReadUInt32BigEndian(stream);
                uint counter = jxlpIndex & 0x7FFFFFFFu;
                bool isLast = (jxlpIndex & 0x80000000u) != 0;

                if (counter < this.nextJxlpIndex)
                {
                    throw new InvalidOperationException("jxlp box index " + counter + " is a duplicate (already processed)");
                }

                if (counter == this.nextJxlpIndex)
                {
                    this.nextJxlpIndex++;

                    if (isLast)
                    {
                        this.lastCodestreamSeen = true;
                    }

                    this.boxStage = JxlBoxStage.CodeStream;
                }
                else if (this.jxlFileFormatVersion >= 1)
                {
                    if (this.jxlpOooBuffer.Count >= NumBuffersLimit)
                    {
                        return JxlDecoderStatus.Error;
                    }

                    // When creating a new OOO (Out-of-order) entry,
                    // the data is initially empty.
                    byte[] buffer = [];
                    JxlOooEntry entry = new(buffer, isLast);
                    this.jxlpOooBuffer.Add((int)counter, entry);

                    this.bufferingJxlpIndex = (int)counter;
                    this.bufferingJxlpIsLast = isLast;
                    this.boxStage = JxlBoxStage.BufferingJxlp;
                }
                else
                {
                    throw new InvalidOperationException("JXLP box with index " + counter + " is out of order (index " + this.nextJxlpIndex + " was expected). Out-of-order jxlp boxes require file format version 1 in the file type (ftyp) box.");
                }
            }
            else if (this.boxStage == JxlBoxStage.CodeStream)
            {
                int status = this.ProcessCodestream();

                if (status == JxlDecoderStatus.FullImage)
                {
                    if (this.reconstructionOutputJpeg != JpegReconstructionStage.None)
                    {
                        continue;
                    }
                }

                if (status == JxlDecoderStatus.NeedMoreInput)
                {
                    if (this.filePosition == this.boxContentsEnd && !this.boxContentsUnbounded)
                    {
                        bool hasMoreData = this.TryInjectNextBufferedJxlpBox();

                        if (!hasMoreData)
                        {
                            this.boxStage = JxlBoxStage.Header;
                        }

                        continue;
                    }
                }

                if (status == JxlDecoderStatus.Success)
                {
                    if (this.JbrdNeedsMoreBoxes())
                    {
                        this.boxStage = JxlBoxStage.Skip;
                        continue;
                    }

                    if (this.boxContentsUnbounded)
                    {
                        break;
                    }

                    if ((this.eventsWanted & JxlDecoderStatus.Box) != 0)
                    {
                        this.boxStage = JxlBoxStage.Skip;
                        continue;
                    }
                }

                return status;
            }
            else if (this.boxStage == JxlBoxStage.BufferingJxlp)
            {
                long remaining = this.boxContentsUnbounded
                    ? this.availableInput
                    : Math.Min(this.availableInput, this.boxContentsEnd - this.filePosition);

                if (!this.CanAddBuffer(remaining) || !this.jxlpOooBuffer.TryGetValue(this.bufferingJxlpIndex, out JxlOooEntry? entry))
                {
                    return JxlDecoderStatus.Error;
                }

                // Now we want to write the 'remaining' number of bytes
                // from input into the codestream.
                using IMemoryOwner<byte> buffer = this.Options.Configuration.MemoryAllocator.Allocate<byte>((int)remaining);
                entry!.CodestreamBytes.Write(buffer.Memory.Span);
                this.jxlpOooBufferTotal += remaining;

                bool boxDone = !this.boxContentsUnbounded && this.filePosition >= this.boxContentsEnd;

                if (!boxDone)
                {
                    ThrowNotEnoughData();
                }

                this.boxStage = JxlBoxStage.Header;
            }
            else if (this.boxStage == JxlBoxStage.JpegReconstruction)
            {
                if (!this.jpegDecoder.IsParsingBox)
                {
                    this.jpegDecoder.StartBox(this.boxContentsUnbounded, this.boxContentsSize);
                }

                Span<byte> nextInput = this.nextInput!.Memory.Span;
                long availableInput = this.availableInput;

                int reconstructionResult = this.jpegDecoder.Process(ref nextInput, ref availableInput);

                long consumed = this.nextInput.Memory.Length - nextInput.Length;
                this.AdvanceInput(consumed);

                if (reconstructionResult == JxlDecoderStatus.JpegReconstruction)
                {
                    JpegData jpegData = this.jpegDecoder!.GetJpegData();
                    long numExif = JxlToJpegDecoder.NumExifMarkers(jpegData);
                    long numXmp = JxlToJpegDecoder.NumXmpMarkers(jpegData);

                    if (numExif > 0)
                    {
                        if (numExif > 1)
                        {
                            throw new InvalidOperationException("Only one EXIF marker for JPEG reconstruction can be present");
                        }

                        if (!JxlToJpegDecoder.ExifBoxContentSize(jpegData, ref this.reconstructionExifSize))
                        {
                            throw new InvalidOperationException("Invalid jbrd EXIF size");
                        }
                    }

                    if (numXmp > 0)
                    {
                        if (numXmp > 1)
                        {
                            throw new InvalidOperationException("Only one XMP marker for JPEG reconstruction can be present");
                        }

                        if (!JxlToJpegDecoder.XmpBoxContentSize(jpegData, ref this.reconstructionXmpSize))
                        {
                            throw new InvalidOperationException("Invalid jbrd XMP size");
                        }
                    }

                    this.boxStage = JxlBoxStage.Header;

                    if ((this.eventsWanted & JxlDecoderStatus.JpegReconstruction) != 0)
                    {
                        this.eventsWanted &= ~JxlDecoderStatus.JpegReconstruction;
                        return JxlDecoderStatus.JpegReconstruction;
                    }
                }
                else
                {
                    return reconstructionResult;
                }
            }
            else if (this.boxStage == JxlBoxStage.Skip)
            {
                if (this.boxContentsUnbounded)
                {
                    if (this.inputClosed || !this.boxOutBufferSet)
                    {
                        return JxlDecoderStatus.Success;
                    }

                    this.AdvanceInput(this.availableInput);
                    ThrowNotEnoughData();
                }

                long remaining = this.boxContentsEnd - this.filePosition;
                if (this.availableInput < remaining)
                {
                    this.basicInfoSizeHint = InitialBasicInfoSizeHint() + this.boxContentsEnd - this.filePosition;
                    this.AdvanceInput(this.availableInput);
                    ThrowNotEnoughData();
                }
                else
                {
                    this.AdvanceInput(remaining);
                    this.boxStage = JxlBoxStage.Header;
                }
            }
            else
            {
                throw new InvalidOperationException("Unreachable");
            }
        }

        return JxlDecoderStatus.Success;
    }

    /// <summary>
    /// Releases memory used by the JPEG output buffer.
    /// </summary>
    public void DisposeJpegBuffer() => this.jpegDecoder.DisposeOutputBuffer();

    /// <summary>
    /// Main core decoding routine.
    /// </summary>
    /// <param name="stream">Stream to decode from.</param>
    /// <exception cref="InvalidOperationException">Thrown when data or input parameters are invalid.</exception>
    public void DecodeInput(Stream stream)
    {
        this.Reset();
        this.decoderStage = JxlDecoderStage.Started;

        if (!this.gotSignature)
        {
            JxlSignature signature = DetectSignature(stream);
            if (signature == JxlSignature.Invalid)
            {
                throw new InvalidOperationException("The signature is invalid.");
            }

            if (signature == JxlSignature.NotEnoughBytes)
            {
                if (this.inputClosed)
                {
                    throw new InvalidOperationException("The input is closed");
                }

                ThrowNotEnoughData();
            }

            this.gotSignature = true;

            if (signature == JxlSignature.Container)
            {
                this.haveContainer = true;
            }
            else
            {
                this.lastCodestreamSeen = true;
            }
        }

        int status = this.ProcessBoxes(stream);

        if (status == JxlDecoderStatus.Success)
        {
            if (this.CanUseMoreCodestreamInput())
            {
                throw new InvalidOperationException("The code stream did not finish");
            }

            if (this.JbrdNeedsMoreBoxes())
            {
                throw new InvalidOperationException("Missing metadata boxes for JPEG reconstruction");
            }
        }
    }

    private static void ThrowNotEnoughData() => throw new InvalidOperationException("Not enough data");

    protected override Image<TPixel> Decode<TPixel>(BufferedReadStream stream, CancellationToken cancellationToken) => throw new NotImplementedException();

    protected override ImageInfo Identify(BufferedReadStream stream, CancellationToken cancellationToken) => throw new NotImplementedException();
}
