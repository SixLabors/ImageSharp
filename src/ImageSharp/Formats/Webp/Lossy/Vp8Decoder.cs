// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
#nullable disable

using System.Buffers;
using SixLabors.ImageSharp.Formats.Webp.BitReader;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Webp.Lossy;

/// <summary>
/// Holds information for decoding a lossy webp image.
/// </summary>
internal class Vp8Decoder : IDisposable
{
    private Vp8MacroBlock leftMacroBlock;

    /// <summary>
    /// Initializes a new instance of the <see cref="Vp8Decoder"/> class.
    /// </summary>
    /// <param name="frameHeader">The frame header.</param>
    /// <param name="pictureHeader">The picture header.</param>
    /// <param name="segmentHeader">The segment header.</param>
    /// <param name="probabilities">The probabilities.</param>
    /// <param name="memoryAllocator">Used for allocating memory for the pixel data output and the temporary buffers.</param>
    public Vp8Decoder(Vp8FrameHeader frameHeader, Vp8PictureHeader pictureHeader, Vp8SegmentHeader segmentHeader, Vp8Proba probabilities, MemoryAllocator memoryAllocator)
    {
        this.FilterHeader = new Vp8FilterHeader();
        this.FrameHeader = frameHeader;
        this.PictureHeader = pictureHeader;
        this.SegmentHeader = segmentHeader;
        this.Probabilities = probabilities;
        this.IntraL = new byte[4];
        this.MbWidth = (int)((this.PictureHeader.Width + 15) >> 4);
        this.MbHeight = (int)((this.PictureHeader.Height + 15) >> 4);
        this.CacheYStride = 16 * this.MbWidth;
        this.CacheUvStride = 8 * this.MbWidth;
        this.MacroBlockInfo = new Vp8MacroBlock[this.MbWidth + 1];
        this.MacroBlockData = new Vp8MacroBlockData[this.MbWidth];
        this.YuvTopSamples = new Vp8TopSamples[this.MbWidth];
        this.FilterInfo = new Vp8FilterInfo[this.MbWidth];
        for (int i = 0; i < this.MbWidth; i++)
        {
            this.MacroBlockInfo[i] = new Vp8MacroBlock();
            this.MacroBlockData[i] = new Vp8MacroBlockData();
            this.YuvTopSamples[i] = new Vp8TopSamples();
            this.FilterInfo[i] = new Vp8FilterInfo();
        }

        this.MacroBlockInfo[this.MbWidth] = new Vp8MacroBlock();

        this.DeQuantMatrices = new Vp8QuantMatrix[WebpConstants.NumMbSegments];
        this.FilterStrength = new Vp8FilterInfo[WebpConstants.NumMbSegments, 2];
        for (int i = 0; i < WebpConstants.NumMbSegments; i++)
        {
            this.DeQuantMatrices[i] = new Vp8QuantMatrix();
            for (int j = 0; j < 2; j++)
            {
                this.FilterStrength[i, j] = new Vp8FilterInfo();
            }
        }

        uint width = pictureHeader.Width;
        uint height = pictureHeader.Height;

        int extraRows = WebpConstants.FilterExtraRows[(int)LoopFilter.Complex]; // assuming worst case: complex filter
        int extraY = extraRows * this.CacheYStride;
        int extraUv = extraRows / 2 * this.CacheUvStride;
        this.YuvBuffer = memoryAllocator.Allocate<byte>((WebpConstants.Bps * 17) + (WebpConstants.Bps * 9) + extraY);
        this.CacheY = memoryAllocator.Allocate<byte>((16 * this.CacheYStride) + extraY, AllocationOptions.Clean);
        int cacheUvSize = (16 * this.CacheUvStride) + extraUv;
        this.CacheU = memoryAllocator.Allocate<byte>(cacheUvSize, AllocationOptions.Clean);
        this.CacheV = memoryAllocator.Allocate<byte>(cacheUvSize, AllocationOptions.Clean);
        this.TmpYBuffer = memoryAllocator.Allocate<byte>((int)width, AllocationOptions.Clean);
        this.TmpUBuffer = memoryAllocator.Allocate<byte>((int)width, AllocationOptions.Clean);
        this.TmpVBuffer = memoryAllocator.Allocate<byte>((int)width, AllocationOptions.Clean);
        this.Pixels = memoryAllocator.Allocate<byte>((int)(width * height * 4), AllocationOptions.Clean);

#if DEBUG
        // Filling those buffers with 205, is only useful for debugging,
        // so the default values are the same as the reference libwebp implementation.
        this.YuvBuffer.Memory.Span.Fill(205);
        this.CacheY.Memory.Span.Fill(205);
        this.CacheU.Memory.Span.Fill(205);
        this.CacheV.Memory.Span.Fill(205);
#endif

        this.Vp8BitReaders = new Vp8BitReader[WebpConstants.MaxNumPartitions];
    }

    /// <summary>
    /// Gets the frame header.
    /// </summary>
    public Vp8FrameHeader FrameHeader { get; }

    /// <summary>
    /// Gets the picture header.
    /// </summary>
    public Vp8PictureHeader PictureHeader { get; }

    /// <summary>
    /// Gets the filter header.
    /// </summary>
    public Vp8FilterHeader FilterHeader { get; }

    /// <summary>
    /// Gets the segment header.
    /// </summary>
    public Vp8SegmentHeader SegmentHeader { get; }

    /// <summary>
    /// Gets or sets the number of partitions minus one.
    /// </summary>
    public int NumPartsMinusOne { get; set; }

    /// <summary>
    /// Gets the per-partition boolean decoders.
    /// </summary>
    public Vp8BitReader[] Vp8BitReaders { get; }

    /// <summary>
    /// Gets the dequantization matrices (one set of DC/AC dequant factor per segment).
    /// </summary>
    public Vp8QuantMatrix[] DeQuantMatrices { get; }

    /// <summary>
    /// Gets or sets a value indicating whether to use the skip probabilities.
    /// </summary>
    public bool UseSkipProbability { get; set; }

    /// <summary>
    /// Gets or sets the skip probability.
    /// </summary>
    public byte SkipProbability { get; set; }

    /// <summary>
    /// Gets or sets the Probabilities.
    /// </summary>
    public Vp8Proba Probabilities { get; set; }

    /// <summary>
    /// Gets or sets the top intra modes values: 4 * MbWidth.
    /// </summary>
    public byte[] IntraT { get; set; }

    /// <summary>
    /// Gets the left intra modes values.
    /// </summary>
    public byte[] IntraL { get; }

    /// <summary>
    /// Gets the width in macroblock units.
    /// </summary>
    public int MbWidth { get; }

    /// <summary>
    /// Gets the height in macroblock units.
    /// </summary>
    public int MbHeight { get; }

    /// <summary>
    /// Gets or sets the top-left x index of the macroblock that must be in-loop filtered.
    /// </summary>
    public int TopLeftMbX { get; set; }

    /// <summary>
    /// Gets or sets the top-left y index of the macroblock that must be in-loop filtered.
    /// </summary>
    public int TopLeftMbY { get; set; }

    /// <summary>
    /// Gets or sets the last bottom-right x index of the macroblock that must be decoded.
    /// </summary>
    public int BottomRightMbX { get; set; }

    /// <summary>
    /// Gets or sets the last bottom-right y index of the macroblock that must be decoded.
    /// </summary>
    public int BottomRightMbY { get; set; }

    /// <summary>
    /// Gets or sets the current x position in macroblock units.
    /// </summary>
    public int MbX { get; set; }

    /// <summary>
    /// Gets or sets the current y position in macroblock units.
    /// </summary>
    public int MbY { get; set; }

    /// <summary>
    /// Gets the parsed reconstruction data.
    /// </summary>
    public Vp8MacroBlockData[] MacroBlockData { get; }

    /// <summary>
    /// Gets the contextual macroblock info.
    /// </summary>
    public Vp8MacroBlock[] MacroBlockInfo { get; }

    /// <summary>
    /// Gets or sets the loop filter used. The purpose of the loop filter is to eliminate (or at least reduce)
    /// visually objectionable artifacts.
    /// </summary>
    public LoopFilter Filter { get; set; }

    /// <summary>
    /// Gets the pre-calculated per-segment filter strengths.
    /// </summary>
    public Vp8FilterInfo[,] FilterStrength { get; }

    public IMemoryOwner<byte> YuvBuffer { get; }

    public Vp8TopSamples[] YuvTopSamples { get; }

    public IMemoryOwner<byte> CacheY { get; }

    public IMemoryOwner<byte> CacheU { get; }

    public IMemoryOwner<byte> CacheV { get; }

    public int CacheYOffset { get; set; }

    public int CacheUvOffset { get; set; }

    public int CacheYStride { get; }

    public int CacheUvStride { get; }

    public IMemoryOwner<byte> TmpYBuffer { get; }

    public IMemoryOwner<byte> TmpUBuffer { get; }

    public IMemoryOwner<byte> TmpVBuffer { get; }

    /// <summary>
    /// Gets the pixel buffer where the decoded pixel data will be stored.
    /// </summary>
    public IMemoryOwner<byte> Pixels { get; }

    /// <summary>
    /// Gets or sets filter info.
    /// </summary>
    public Vp8FilterInfo[] FilterInfo { get; set; }

    public Vp8MacroBlock CurrentMacroBlock => this.MacroBlockInfo[this.MbX];

    public Vp8MacroBlock LeftMacroBlock => this.leftMacroBlock ??= new Vp8MacroBlock();

    public Vp8MacroBlockData CurrentBlockData => this.MacroBlockData[this.MbX];

    public void PrecomputeFilterStrengths()
    {
        if (this.Filter == LoopFilter.None)
        {
            return;
        }

        Vp8FilterHeader hdr = this.FilterHeader;
        for (int s = 0; s < WebpConstants.NumMbSegments; ++s)
        {
            int baseLevel;

            // First, compute the initial level.
            if (this.SegmentHeader.UseSegment)
            {
                baseLevel = this.SegmentHeader.FilterStrength[s];
                if (!this.SegmentHeader.Delta)
                {
                    baseLevel += hdr.FilterLevel;
                }
            }
            else
            {
                baseLevel = hdr.FilterLevel;
            }

            for (int i4x4 = 0; i4x4 <= 1; i4x4++)
            {
                Vp8FilterInfo info = this.FilterStrength[s, i4x4];
                int level = baseLevel;
                if (hdr.UseLfDelta)
                {
                    level += hdr.RefLfDelta[0];
                    if (i4x4 > 0)
                    {
                        level += hdr.ModeLfDelta[0];
                    }
                }

                level = level < 0 ? 0 : level > 63 ? 63 : level;
                if (level > 0)
                {
                    int iLevel = level;
                    if (hdr.Sharpness > 0)
                    {
                        if (hdr.Sharpness > 4)
                        {
                            iLevel >>= 2;
                        }
                        else
                        {
                            iLevel >>= 1;
                        }

                        int iLevelCap = 9 - hdr.Sharpness;
                        if (iLevel > iLevelCap)
                        {
                            iLevel = iLevelCap;
                        }
                    }

                    if (iLevel < 1)
                    {
                        iLevel = 1;
                    }

                    info.InnerLevel = (byte)iLevel;
                    info.Limit = (byte)((2 * level) + iLevel);
                    info.HighEdgeVarianceThreshold = (byte)(level >= 40 ? 2 : level >= 15 ? 1 : 0);
                }
                else
                {
                    info.Limit = 0;  // no filtering.
                }

                info.UseInnerFiltering = i4x4 == 1;
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.YuvBuffer.Dispose();
        this.CacheY.Dispose();
        this.CacheU.Dispose();
        this.CacheV.Dispose();
        this.TmpYBuffer.Dispose();
        this.TmpUBuffer.Dispose();
        this.TmpVBuffer.Dispose();
        this.Pixels.Dispose();
    }
}
