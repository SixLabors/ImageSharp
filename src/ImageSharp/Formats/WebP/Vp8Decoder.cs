// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.WebP
{
    /// <summary>
    /// Holds information for decoding a lossy webp image.
    /// </summary>
    internal class Vp8Decoder
    {
        public Vp8Decoder(Vp8FrameHeader frameHeader, Vp8PictureHeader pictureHeader, Vp8SegmentHeader segmentHeader, Vp8Proba probabilities, Vp8Io io)
        {
            this.FilterHeader = new Vp8FilterHeader();
            this.FrameHeader = frameHeader;
            this.PictureHeader = pictureHeader;
            this.SegmentHeader = segmentHeader;
            this.Probabilities = probabilities;
            this.IntraL = new byte[4];
            this.YuvBuffer = new byte[(WebPConstants.Bps * 17) + (WebPConstants.Bps * 9)];
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

            this.DeQuantMatrices = new Vp8QuantMatrix[WebPConstants.NumMbSegments];
            this.FilterStrength = new Vp8FilterInfo[WebPConstants.NumMbSegments, 2];
            for (int i = 0; i < WebPConstants.NumMbSegments; i++)
            {
                this.DeQuantMatrices[i] = new Vp8QuantMatrix();
                for (int j = 0; j < 2; j++)
                {
                    this.FilterStrength[i, j] = new Vp8FilterInfo();
                }
            }

            uint width = pictureHeader.Width;
            uint height = pictureHeader.Height;

            // TODO: use memory allocator
            this.CacheY = new byte[width * height]; // TODO: this is way too much mem, figure out what the min req is.
            this.CacheU = new byte[width * height];
            this.CacheV = new byte[width * height];
            this.TmpYBuffer = new byte[width * height]; // TODO: figure out min buffer length
            this.TmpUBuffer = new byte[width * height]; // TODO: figure out min buffer length
            this.TmpVBuffer = new byte[width * height]; // TODO: figure out min buffer length
            this.Bgr = new byte[width * height * 4];

            this.Vp8BitReaders = new Vp8BitReader[WebPConstants.MaxNumPartitions];
            this.Init(io);
        }

        public Vp8FrameHeader FrameHeader { get; }

        public Vp8PictureHeader PictureHeader { get; }

        public Vp8FilterHeader FilterHeader { get; }

        public Vp8SegmentHeader SegmentHeader { get; }

        // number of partitions minus one.
        public uint NumPartsMinusOne { get; }

        // per-partition boolean decoders.
        public Vp8BitReader[] Vp8BitReaders { get; }

        public bool Dither { get; set; }

        /// <summary>
        /// Gets or sets dequantization matrices (one set of DC/AC dequant factor per segment).
        /// </summary>
        public Vp8QuantMatrix[] DeQuantMatrices { get; }

        public bool UseSkipProbability { get; set; }

        public byte SkipProbability { get; set; }

        public Vp8Proba Probabilities { get; set; }

        // top intra modes values: 4 * MbWidth
        public byte[] IntraT { get; set; }

        // left intra modes values
        public byte[] IntraL { get; }

        /// <summary>
        /// Gets or sets the width in macroblock units.
        /// </summary>
        public int MbWidth { get; }

        /// <summary>
        /// Gets or sets the height in macroblock units.
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
        /// Gets contextual contextual macroblock info (mbw + 1).
        /// </summary>
        public Vp8MacroBlock[] MacroBlockInfo { get;  }

        public LoopFilter Filter { get; set; }

        public Vp8FilterInfo[,] FilterStrength { get; }

        public byte[] YuvBuffer { get; }

        public Vp8TopSamples[] YuvTopSamples { get; }

        public byte[] CacheY { get; }

        public byte[] CacheU { get; }

        public byte[] CacheV { get; }

        public int CacheYStride { get; }

        public int CacheUvStride { get; }

        public byte[] TmpYBuffer { get; }

        public byte[] TmpUBuffer { get; }

        public byte[] TmpVBuffer { get; }

        public byte[] Bgr { get; }

        /// <summary>
        /// Gets or sets filter strength info.
        /// </summary>
        public Vp8FilterInfo[] FilterInfo { get; set; }

        public Vp8MacroBlock CurrentMacroBlock
        {
            get
            {
                return this.MacroBlockInfo[this.MbX + 1];
            }
        }

        public Vp8MacroBlock LeftMacroBlock
        {
            get
            {
                return this.MacroBlockInfo[this.MbX];
            }
        }

        public Vp8MacroBlockData CurrentBlockData
        {
            get
            {
                return this.MacroBlockData[this.MbX];
            }
        }

        public void Init(Vp8Io io)
        {
            int intraPredModeSize = 4 * this.MbWidth;
            this.IntraT = new byte[intraPredModeSize];

            int extraPixels = WebPConstants.FilterExtraRows[(int)this.Filter];
            if (this.Filter is LoopFilter.Complex)
            {
                // For complex filter, we need to preserve the dependency chain.
                this.TopLeftMbX = 0;
                this.TopLeftMbY = 0;
            }
            else
            {
                // For simple filter, we can filter only the cropped region. We include 'extraPixels' on
                // the other side of the boundary, since vertical or horizontal filtering of the previous
                // macroblock can modify some abutting pixels.
                this.TopLeftMbX = (io.CropLeft - extraPixels) >> 4;
                this.TopLeftMbY = (io.CropTop - extraPixels) >> 4;
                if (this.TopLeftMbX < 0)
                {
                    this.TopLeftMbX = 0;
                }

                if (this.TopLeftMbY < 0)
                {
                    this.TopLeftMbY = 0;
                }
            }

            // We need some 'extra' pixels on the right/bottom.
            this.BottomRightMbY = (io.CropBottom + 15 + extraPixels) >> 4;
            this.BottomRightMbX = (io.CropRight + 15 + extraPixels) >> 4;
            if (this.BottomRightMbX > this.MbWidth)
            {
                this.BottomRightMbX = this.MbWidth;
            }

            if (this.BottomRightMbY > this.MbHeight)
            {
                this.BottomRightMbY = this.MbHeight;
            }

            this.PrecomputeFilterStrengths();
        }

        private void PrecomputeFilterStrengths()
        {
            if (this.Filter is LoopFilter.None)
            {
                return;
            }

            Vp8FilterHeader hdr = this.FilterHeader;
            for (int s = 0; s < WebPConstants.NumMbSegments; ++s)
            {
                int baseLevel;

                // First, compute the initial level
                if (this.SegmentHeader.UseSegment)
                {
                    baseLevel = this.SegmentHeader.FilterStrength[s];
                    if (!this.SegmentHeader.Delta)
                    {
                        baseLevel += hdr.Level;
                    }
                }
                else
                {
                    baseLevel = hdr.Level;
                }

                for (int i4x4 = 0; i4x4 <= 1; ++i4x4)
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

                    level = (level < 0) ? 0 : (level > 63) ? 63 : level;
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

                            if (iLevel > 9 - hdr.Sharpness)
                            {
                                iLevel = 9 - hdr.Sharpness;
                            }
                        }

                        if (iLevel < 1)
                        {
                            iLevel = 1;
                        }

                        info.InnerLevel = (byte)iLevel;
                        info.Limit = (byte)((2 * level) + iLevel);
                        info.HighEdgeVarianceThreshold = (byte)((level >= 40) ? 2 : (level >= 15) ? 1 : 0);
                    }
                    else
                    {
                        info.Limit = 0;  // no filtering.
                    }

                    info.InnerLevel = (byte)i4x4;
                }
            }
        }
    }
}
