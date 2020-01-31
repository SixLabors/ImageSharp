// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.WebP
{
    /// <summary>
    /// Holds information for decoding a lossy webp image.
    /// </summary>
    internal class Vp8Decoder
    {
        public Vp8Decoder()
        {
            this.DeQuantMatrices = new Vp8QuantMatrix[WebPConstants.NumMbSegments];
            this.FilterStrength = new Vp8FilterInfo[WebPConstants.NumMbSegments, 2];
        }

        public void Init(Vp8Io io)
        {
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
            this.BotomRightMbX = (io.CropRight + 15 + extraPixels) >> 4;
            if (this.BotomRightMbX > this.MbWidth)
            {
                this.BotomRightMbX = this.MbWidth;
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
                        info.Limit = 0;  // no filtering
                    }

                    info.InnerLevel = (byte)i4x4;
                }
            }
        }

        public Vp8FrameHeader FrameHeader { get; set; }

        public Vp8PictureHeader PictureHeader { get; set; }

        public Vp8FilterHeader FilterHeader { get; set; }

        public Vp8SegmentHeader SegmentHeader { get; set; }

        public bool Dither { get; set; }

        /// <summary>
        /// Gets or sets dequantization matrices (one set of DC/AC dequant factor per segment).
        /// </summary>
        public Vp8QuantMatrix[] DeQuantMatrices { get; private set; }

        public bool UseSkipProba { get; set; }

        public byte SkipProbability { get; set; }

        public Vp8Proba Probabilities { get; set; }

        /// <summary>
        /// Gets or sets the width in macroblock units.
        /// </summary>
        public int MbWidth { get; set; }

        /// <summary>
        /// Gets or sets the height in macroblock units.
        /// </summary>
        public int MbHeight { get; set; }

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
        public int BotomRightMbX { get; set; }

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
        /// Gets or sets the parsed reconstruction data.
        /// </summary>
        public Vp8MacroBlockData[] MacroBlockData { get; set; }

        /// <summary>
        /// Gets or sets contextual macroblock infos.
        /// </summary>
        public Vp8MacroBlock[] MacroBlockInfo { get; set; }

        public int MacroBlockPos { get; set; }

        public LoopFilter Filter { get; set; }

        public Vp8FilterInfo[,] FilterStrength { get; }

        /// <summary>
        /// Gets or sets filter strength info.
        /// </summary>
        public Vp8FilterInfo FilterInfo { get; set; }
    }
}
