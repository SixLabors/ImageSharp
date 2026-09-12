// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.IO;
using SixLabors.ImageSharp.Formats.Jxl.IO.FrameHeader;
using SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

internal sealed class JxlFrameDecoder
{
    private JxlPassesDecoderState decoderState;
    private List<JxlTocEntry> toc = [];
    private ulong sectionSizesSum;
    private JxlFrameHeader frameHeader;
    private JxlFrameDimensions frameDimensions;
    private JxlImageBundle decoded;
    private JxlModularFrameDecoder modularFrameDecoder;
    private bool renderSpotcolors = true;
    private bool coalescing = true;
    private List<byte> processedSection = [];
    private List<byte> decodedPassesPerAcGroup = [];
    private List<byte> decodedDcGroups = [];
    private bool decodedDcGlobal;
    private bool decodedAcGlobal;
    private bool finalizedDc = true;
    private long numSectionsDone;
    private bool isFinalized = true;
    private bool allocated;
    private List<JxlGroupDecoderCache> groupDecoderCaches = [];
    private bool useTaskId;
    private bool useSlowRenderingPipeline;
    private JxlProgressiveDetail progressiveDetail = JxlProgressiveDetail.Frames;
    private List<int> passesToPause = [];

    /// <summary>
    /// Gets a value indicating whether there are any DC groups left to decode.
    /// </summary>
    private bool ContainsDcGroupToDecode => this.decodedDcGroups.Any(x => x == 0);

    private static int GetStride(int width, JxlPixelFormat format)
    {
        if (!JxlMath.SafeMultiply(BytesPerChannel(format.DataType), format.Channels, out int xStride))
        {
            throw new InvalidOperationException("Image too large");
        }

        if (!JxlMath.SafeMultiply(xStride, width, out int yStride))
        {
            throw new InvalidOperationException("Image too large");
        }

        if (!JxlMath.SafeRoundUpTo(yStride, format.Align, yStride))
        {
            throw new InvalidOperationException("Image too large");
        }

        return yStride;
    }

    public static void DecodeGlobalDcInfo(Configuration configuration, JxlBitReader reader, bool isJpeg, JxlPassesDecoderState state)
    {
        state.SharedStorage.Quantizer.Decode(reader);

        if (!JxlEntropyCoder.DecodeBlockContextMap(configuration, reader, ref state.SharedStorage.BlockContextMap))
        {
            throw new InvalidOperationException("Could not decode block context map");
        }

        if (!state.SharedStorage.ColorMap.DecodeDc(reader))
        {
            throw new InvalidOperationException("Could not decode DC color correlation map");
        }

        if (isJpeg)
        {
            state.SharedStorage.Quantizer.ClearDcMultipliers();
        }

        state.SharedStorage.AcStrategy.FillInvalid();
    }

    public static void DecodeFrame(JxlPassesDecoderState decoderState, Stream stream, ref JxlFrameHeader header, JxlImageBundle decoded, JxlCodecMetadata metadata, bool useSlowRenderingPipeline)
    {
        JxlFrameDecoder frameDecoder = new(decoderState, metadata, useSlowRenderingPipeline);
        JxlBitReader reader = new(stream);

        if (!frameDecoder.InitializeFrame(reader, decoded, isPreview: false))
        {
            throw new InvalidOperationException("Frame initialization failed");
        }

        if (!frameDecoder.InitializeFrameOutput())
        {
            throw new InvalidOperationException("Could not initialize frame output");
        }

        if (header is not null)
        {
            header = frameDecoder.frameHeader;
        }

        bool closeOk = true;
        List<JxlBitReader> sectionReaders = [];
        List<JxlBitReaderScopedCloser> sectionClosers = [];
        List<JxlSectionInfo> sectionInfos = [];
        List<JxlSectionStatus> sectionStatuses = [];

        int index = 0;

        foreach (JxlTocEntry toc in frameDecoder.toc)
        {
            JxlBitReader br = new(stream);
            sectionInfos.Add(new JxlSectionInfo(br, toc.Id, index++));
            sectionClosers.Add(new JxlBitReaderScopedCloser(reader, closeOk));
            sectionReaders.Add(br);
        }

        frameDecoder.ProcessSections(sectionInfos, sectionStatuses);
        for (int i = 0; i < sectionStatuses.Count; i++)
        {
            if (sectionStatuses[i] != JxlSectionStatus.Done)
            {
                throw new InvalidOperationException("Section incomplete");
            }
        }

        if (!closeOk)
        {
            throw new InvalidDataException("Stream cannot be closed");
        }

        frameDecoder.FinalizeFrame();
    }

    private static int BytesPerChannel(JxlDataType dataType) =>
        dataType == JxlDataType.Byte ? 1
           : dataType == JxlDataType.Single
              ? 4
              : 2;

    private int GetStorageLocation(int thread, int task) => this.useTaskId ? task : thread;

    private void PrepareStorage(int numThreads, int numTasks)
    {
        int storageSize = Math.Min(numThreads, numTasks);
        if (storageSize > this.groupDecoderCaches.Count)
        {
            this.groupDecoderCaches = [.. this.groupDecoderCaches.Take(storageSize)];
        }

        this.useTaskId = numThreads > numTasks;
        bool useNoise = (this.frameHeader.Flags & (int)JxlFrameHeaderFlags.Noise) != 0;
        bool useGroupIds = this.modularFrameDecoder.UsesFullImage && (this.frameHeader.Encoding == JxlFrameEncoding.VarDct || useNoise);

        this.decoderState.RenderPipeline?.PrepareForThreads(storageSize, useGroupIds);
        this.decoderState.Upsampler8x.PrepareForThreads(numThreads);
    }
}
