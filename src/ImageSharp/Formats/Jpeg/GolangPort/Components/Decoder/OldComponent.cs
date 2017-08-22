// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Jpeg.GolangPort.Components.Decoder
{
    using System;

    using SixLabors.ImageSharp.Formats.Jpeg.Common;
    using SixLabors.ImageSharp.Memory;

    /// <summary>
    /// Represents a single color component
    /// </summary>
    internal class OldComponent : IDisposable
    {
        public OldComponent(byte identifier, int index)
        {
            this.Identifier = identifier;
            this.Index = index;
        }

        /// <summary>
        /// Gets the identifier
        /// </summary>
        public byte Identifier { get; }

        /// <summary>
        /// Gets the component's position in <see cref="OldJpegDecoderCore.Components"/>
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// Gets the horizontal sampling factor.
        /// </summary>
        public int HorizontalFactor { get; private set; }

        /// <summary>
        /// Gets the vertical sampling factor.
        /// </summary>
        public int VerticalFactor { get; private set; }

        /// <summary>
        /// Gets the quantization table destination selector.
        /// </summary>
        public byte Selector { get; private set; }

        /// <summary>
        /// Gets the <see cref="Buffer{T}"/> storing the "raw" frequency-domain decoded blocks.
        /// We need to apply IDCT, dequantiazition and unzigging to transform them into color-space blocks.
        /// This is done by <see cref="OldJpegDecoderCore.ProcessBlocksIntoJpegImageChannels{TPixel}"/>.
        /// When <see cref="OldJpegDecoderCore.IsProgressive"/> us true, we are touching these blocks multiple times - each time we process a Scan.
        /// </summary>
        public Buffer2D<Block8x8F> SpectralBlocks { get; private set; }

        /// <summary>
        /// Gets the number of blocks for this component along the X axis
        /// </summary>
        public int BlockCountX { get; private set; }

        /// <summary>
        /// Gets the number of blocks for this component along the Y axis
        /// </summary>
        public int BlockCountY { get; private set; }

        public ref Block8x8F GetBlockReference(int bx, int by)
        {
            return ref this.SpectralBlocks[bx, by];
        }

        /// <summary>
        /// Initializes <see cref="SpectralBlocks"/>
        /// </summary>
        /// <param name="decoder">The <see cref="OldJpegDecoderCore"/> instance</param>
        public void InitializeBlocks(OldJpegDecoderCore decoder)
        {
            this.BlockCountX = decoder.MCUCountX * this.HorizontalFactor;
            this.BlockCountY = decoder.MCUCountY * this.VerticalFactor;
            this.SpectralBlocks = Buffer2D<Block8x8F>.CreateClean(this.BlockCountX, this.BlockCountY);
        }

        /// <summary>
        /// Initializes all component data except <see cref="SpectralBlocks"/>.
        /// </summary>
        /// <param name="decoder">The <see cref="OldJpegDecoderCore"/> instance</param>
        public void InitializeData(OldJpegDecoderCore decoder)
        {
            // Section B.2.2 states that "the value of C_i shall be different from
            // the values of C_1 through C_(i-1)".
            int i = this.Index;

            for (int j = 0; j < this.Index; j++)
            {
                if (this.Identifier == decoder.Components[j].Identifier)
                {
                    throw new ImageFormatException("Repeated component identifier");
                }
            }

            this.Selector = decoder.Temp[8 + (3 * i)];
            if (this.Selector > OldJpegDecoderCore.MaxTq)
            {
                throw new ImageFormatException("Bad Tq value");
            }

            byte hv = decoder.Temp[7 + (3 * i)];
            int h = hv >> 4;
            int v = hv & 0x0f;
            if (h < 1 || h > 4 || v < 1 || v > 4)
            {
                throw new ImageFormatException("Unsupported Luma/chroma subsampling ratio");
            }

            if (h == 3 || v == 3)
            {
                throw new ImageFormatException("Lnsupported subsampling ratio");
            }

            switch (decoder.ComponentCount)
            {
                case 1:

                    // If a JPEG image has only one component, section A.2 says "this data
                    // is non-interleaved by definition" and section A.2.2 says "[in this
                    // case...] the order of data units within a scan shall be left-to-right
                    // and top-to-bottom... regardless of the values of H_1 and V_1". Section
                    // 4.8.2 also says "[for non-interleaved data], the MCU is defined to be
                    // one data unit". Similarly, section A.1.1 explains that it is the ratio
                    // of H_i to max_j(H_j) that matters, and similarly for V. For grayscale
                    // images, H_1 is the maximum H_j for all components j, so that ratio is
                    // always 1. The component's (h, v) is effectively always (1, 1): even if
                    // the nominal (h, v) is (2, 1), a 20x5 image is encoded in three 8x8
                    // MCUs, not two 16x8 MCUs.
                    h = 1;
                    v = 1;
                    break;

                case 3:

                    // For YCbCr images, we only support 4:4:4, 4:4:0, 4:2:2, 4:2:0,
                    // 4:1:1 or 4:1:0 chroma subsampling ratios. This implies that the
                    // (h, v) values for the Y component are either (1, 1), (1, 2),
                    // (2, 1), (2, 2), (4, 1) or (4, 2), and the Y component's values
                    // must be a multiple of the Cb and Cr component's values. We also
                    // assume that the two chroma components have the same subsampling
                    // ratio.
                    switch (i)
                    {
                        case 0:
                            {
                                // Y.
                                // We have already verified, above, that h and v are both
                                // either 1, 2 or 4, so invalid (h, v) combinations are those
                                // with v == 4.
                                if (v == 4)
                                {
                                    throw new ImageFormatException("Unsupported subsampling ratio");
                                }

                                break;
                            }

                        case 1:
                            {
                                // Cb.
                                if (decoder.Components[0].HorizontalFactor % h != 0
                                    || decoder.Components[0].VerticalFactor % v != 0)
                                {
                                    throw new ImageFormatException("Unsupported subsampling ratio");
                                }

                                break;
                            }

                        case 2:
                            {
                                // Cr.
                                if (decoder.Components[1].HorizontalFactor != h
                                    || decoder.Components[1].VerticalFactor != v)
                                {
                                    throw new ImageFormatException("Unsupported subsampling ratio");
                                }

                                break;
                            }
                    }

                    break;

                case 4:

                    // For 4-component images (either CMYK or YCbCrK), we only support two
                    // hv vectors: [0x11 0x11 0x11 0x11] and [0x22 0x11 0x11 0x22].
                    // Theoretically, 4-component JPEG images could mix and match hv values
                    // but in practice, those two combinations are the only ones in use,
                    // and it simplifies the applyBlack code below if we can assume that:
                    // - for CMYK, the C and K channels have full samples, and if the M
                    // and Y channels subsample, they subsample both horizontally and
                    // vertically.
                    // - for YCbCrK, the Y and K channels have full samples.
                    switch (i)
                    {
                        case 0:
                            if (hv != 0x11 && hv != 0x22)
                            {
                                throw new ImageFormatException("Unsupported subsampling ratio");
                            }

                            break;
                        case 1:
                        case 2:
                            if (hv != 0x11)
                            {
                                throw new ImageFormatException("Unsupported subsampling ratio");
                            }

                            break;
                        case 3:
                            if (decoder.Components[0].HorizontalFactor != h
                                || decoder.Components[0].VerticalFactor != v)
                            {
                                throw new ImageFormatException("Unsupported subsampling ratio");
                            }

                            break;
                    }

                    break;
            }

            this.HorizontalFactor = h;
            this.VerticalFactor = v;
        }

        public void Dispose()
        {
            this.SpectralBlocks.Dispose();
        }
    }
}
