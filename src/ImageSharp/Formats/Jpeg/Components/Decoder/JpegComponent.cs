// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using SixLabors.ImageSharp.Memory;
using SixLabors.Memory;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder
{
    /// <summary>
    /// Represents a single frame component
    /// </summary>
    internal class JpegComponent : IDisposable, IJpegComponent
    {
        private readonly MemoryAllocator memoryAllocator;

        public JpegComponent(MemoryAllocator memoryAllocator, JpegFrame frame, byte id, int horizontalFactor, int verticalFactor, byte quantizationTableIndex, int index)
        {
            this.memoryAllocator = memoryAllocator;
            this.Frame = frame;
            this.Id = id;
            this.HorizontalSamplingFactor = horizontalFactor;
            this.VerticalSamplingFactor = verticalFactor;
            this.SamplingFactors = new Size(this.HorizontalSamplingFactor, this.VerticalSamplingFactor);
            this.QuantizationTableIndex = quantizationTableIndex;
            this.Index = index;
        }

        /// <summary>
        /// Gets the component Id
        /// </summary>
        public byte Id { get; }

        /// <summary>
        /// Gets or sets DC coefficient predictor
        /// </summary>
        public int DcPredictor { get; set; }

        /// <summary>
        /// Gets the horizontal sampling factor.
        /// </summary>
        public int HorizontalSamplingFactor { get; }

        /// <summary>
        /// Gets the vertical sampling factor.
        /// </summary>
        public int VerticalSamplingFactor { get; }

        /// <inheritdoc />
        public Buffer2D<Block8x8> SpectralBlocks { get; private set; }

        /// <inheritdoc />
        public Size SubSamplingDivisors { get; private set; }

        /// <inheritdoc />
        public int QuantizationTableIndex { get; }

        /// <inheritdoc />
        public int Index { get; }

        /// <inheritdoc />
        public Size SizeInBlocks { get; private set; }

        /// <inheritdoc />
        public Size SamplingFactors { get; set; }

        /// <summary>
        /// Gets the number of blocks per line
        /// </summary>
        public int WidthInBlocks { get; private set; }

        /// <summary>
        /// Gets the number of blocks per column
        /// </summary>
        public int HeightInBlocks { get; private set; }

        /// <summary>
        /// Gets or sets the index for the DC Huffman table
        /// </summary>
        public int DCHuffmanTableId { get; set; }

        /// <summary>
        /// Gets or sets the index for the AC Huffman table
        /// </summary>
        public int ACHuffmanTableId { get; set; }

        public JpegFrame Frame { get; }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.SpectralBlocks?.Dispose();
            this.SpectralBlocks = null;
        }

        public void Init()
        {
            this.WidthInBlocks = (int)MathF.Ceiling(
                MathF.Ceiling(this.Frame.SamplesPerLine / 8F) * this.HorizontalSamplingFactor / this.Frame.MaxHorizontalFactor);

            this.HeightInBlocks = (int)MathF.Ceiling(
                MathF.Ceiling(this.Frame.Scanlines / 8F) * this.VerticalSamplingFactor / this.Frame.MaxVerticalFactor);

            int blocksPerLineForMcu = this.Frame.McusPerLine * this.HorizontalSamplingFactor;
            int blocksPerColumnForMcu = this.Frame.McusPerColumn * this.VerticalSamplingFactor;
            this.SizeInBlocks = new Size(blocksPerLineForMcu, blocksPerColumnForMcu);

            // For 4-component images (either CMYK or YCbCrK), we only support two
            // hv vectors: [0x11 0x11 0x11 0x11] and [0x22 0x11 0x11 0x22].
            // Theoretically, 4-component JPEG images could mix and match hv values
            // but in practice, those two combinations are the only ones in use,
            // and it simplifies the applyBlack code below if we can assume that:
            // - for CMYK, the C and K channels have full samples, and if the M
            // and Y channels subsample, they subsample both horizontally and
            // vertically.
            // - for YCbCrK, the Y and K channels have full samples.
            if (this.Index == 0 || this.Index == 3)
            {
                this.SubSamplingDivisors = new Size(1, 1);
            }
            else
            {
                JpegComponent c0 = this.Frame.Components[0];
                this.SubSamplingDivisors = c0.SamplingFactors.DivideBy(this.SamplingFactors);
            }

            this.SpectralBlocks = this.memoryAllocator.Allocate2D<Block8x8>(blocksPerColumnForMcu, blocksPerLineForMcu + 1, AllocationOptions.Clean);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref Block8x8 GetBlockReference(int column, int row)
        {
            int offset = ((this.WidthInBlocks + 1) * row) + column;
            return ref Unsafe.Add(ref MemoryMarshal.GetReference(this.SpectralBlocks.GetSpan()), offset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref short GetBlockDataReference(int column, int row)
        {
            ref Block8x8 blockRef = ref this.GetBlockReference(column, row);
            return ref Unsafe.As<Block8x8, short>(ref blockRef);
        }
    }
}