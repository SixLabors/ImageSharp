// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder
{
    /// <summary>
    /// Encapsulates spectral data to rgba32 processing for one component.
    /// </summary>
    internal class JpegComponentPostProcessor8 : IDisposable
    {
        /// <summary>
        /// The size of the area in <see cref="ColorBuffer"/> corresponding to one 8x8 Jpeg block
        /// </summary>
        private readonly Size blockAreaSize;

        /// <summary>
        /// Jpeg frame instance containing required decoding metadata.
        /// </summary>
        private readonly JpegFrame frame;

        /// <summary>
        /// Gets the <see cref="IJpegComponent"/> component containing decoding meta information.
        /// </summary>
        private readonly IJpegComponent component;

        /// <summary>
        /// Gets the <see cref="IRawJpegData"/> instance containing decoding meta information.
        /// </summary>
        private readonly IRawJpegData rawJpeg;

        private readonly float dcDequantizer;

        /// <summary>
        /// Initializes a new instance of the <see cref="JpegComponentPostProcessor8"/> class.
        /// </summary>
        public JpegComponentPostProcessor8(MemoryAllocator memoryAllocator, JpegFrame frame, IRawJpegData rawJpeg, Size postProcessorBufferSize, IJpegComponent component)
        {
            // TODO: this must be a variable depending on dct scale factor
            const int blockSize = 1;

            this.frame = frame;

            this.component = component;

            this.rawJpeg = rawJpeg;

            this.dcDequantizer = rawJpeg.QuantizationTables[this.component.QuantizationTableIndex][0];

            this.blockAreaSize = this.component.SubSamplingDivisors * blockSize;
            this.ColorBuffer = memoryAllocator.Allocate2DOveraligned<float>(
                postProcessorBufferSize.Width,
                postProcessorBufferSize.Height,
                this.blockAreaSize.Height);
        }

        /// <summary>
        /// Gets the temporary working buffer of color values.
        /// </summary>
        public Buffer2D<float> ColorBuffer { get; }

        /// <inheritdoc />
        public void Dispose() => this.ColorBuffer.Dispose();

        /// <summary>
        /// Convert raw spectral DCT data to color data and copy it to the color buffer <see cref="ColorBuffer"/>.
        /// </summary>
        public void CopyBlocksToColorBuffer(int spectralStep)
        {
            Buffer2D<Block8x8> spectralBuffer = this.component.SpectralBlocks;

            float maximumValue = this.frame.MaxColorChannelValue;

            int blocksRowsPerStep = this.component.SamplingFactors.Height;

            int yBlockStart = spectralStep * blocksRowsPerStep;

            for (int y = 0; y < blocksRowsPerStep; y++)
            {
                int yBuffer = y * this.blockAreaSize.Height;

                Span<float> colorBufferRow = this.ColorBuffer.DangerousGetRowSpan(yBuffer);
                Span<Block8x8> blockRow = spectralBuffer.DangerousGetRowSpan(yBlockStart + y);

                for (int xBlock = 0; xBlock < spectralBuffer.Width; xBlock++)
                {
                    // get DC - averaged 8x8 pixel value
                    float DC = blockRow[xBlock][0];

                    // dequantization
                    DC *= this.dcDequantizer;

                    // Normalize & round
                    DC = (float)Math.Round(Numerics.Clamp(DC + MathF.Ceiling(maximumValue / 2), 0, maximumValue));

                    // Save to the intermediate buffer
                    int xColorBufferStart = xBlock * this.blockAreaSize.Width;
                    colorBufferRow[xColorBufferStart] = DC;

                    //// Integer to float
                    //workspaceBlock.LoadFrom(ref blockRow[xBlock]);

                    //// Dequantize
                    //workspaceBlock.MultiplyInPlace(ref dequantTable);

                    //// Convert from spectral to color
                    //FastFloatingPointDCT.TransformIDCT(ref workspaceBlock);

                    //// To conform better to libjpeg we actually NEED TO loose precision here.
                    //// This is because they store blocks as Int16 between all the operations.
                    //// To be "more accurate", we need to emulate this by rounding!
                    //workspaceBlock.NormalizeColorsAndRoundInPlace(maximumValue);

                    //// Write to color buffer acording to sampling factors
                    //int xColorBufferStart = xBlock * this.blockAreaSize.Width;
                    //workspaceBlock.ScaledCopyTo(
                    //    ref colorBufferRow[xColorBufferStart],
                    //    destAreaStride,
                    //    subSamplingDivisors.Width,
                    //    subSamplingDivisors.Height);
                }
            }
        }

        public void ClearSpectralBuffers()
        {
            Buffer2D<Block8x8> spectralBlocks = this.component.SpectralBlocks;
            for (int i = 0; i < spectralBlocks.Height; i++)
            {
                spectralBlocks.DangerousGetRowSpan(i).Clear();
            }
        }

        public Span<float> GetColorBufferRowSpan(int row) =>
            this.ColorBuffer.DangerousGetRowSpan(row);
    }
}
