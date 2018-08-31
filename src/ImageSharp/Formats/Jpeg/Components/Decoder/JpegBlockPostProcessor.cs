// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder
{
    /// <summary>
    /// Encapsulates the implementation of processing "raw" jpeg buffers into Jpeg image channels.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct JpegBlockPostProcessor
    {
        /// <summary>
        /// Source block
        /// </summary>
        public Block8x8F SourceBlock;

        /// <summary>
        /// Temporal block 1 to store intermediate and/or final computation results
        /// </summary>
        public Block8x8F WorkspaceBlock1;

        /// <summary>
        /// Temporal block 2 to store intermediate and/or final computation results
        /// </summary>
        public Block8x8F WorkspaceBlock2;

        /// <summary>
        /// The quantization table as <see cref="Block8x8F"/>
        /// </summary>
        public Block8x8F DequantiazationTable;

        /// <summary>
        /// Defines the horizontal and vertical scale we need to apply to the 8x8 sized block.
        /// </summary>
        private Size subSamplingDivisors;

        /// <summary>
        /// Initializes a new instance of the <see cref="JpegBlockPostProcessor"/> struct.
        /// </summary>
        /// <param name="decoder">The raw jpeg data.</param>
        /// <param name="component">The raw component.</param>
        public JpegBlockPostProcessor(IRawJpegData decoder, IJpegComponent component)
        {
            int qtIndex = component.QuantizationTableIndex;
            this.DequantiazationTable = ZigZag.CreateDequantizationTable(ref decoder.QuantizationTables[qtIndex]);
            this.subSamplingDivisors = component.SubSamplingDivisors;

            this.SourceBlock = default;
            this.WorkspaceBlock1 = default;
            this.WorkspaceBlock2 = default;
        }

        /// <summary>
        /// Processes 'sourceBlock' producing Jpeg color channel values from spectral values:
        /// - Dequantize
        /// - Applying IDCT
        /// - Level shift by +128, clip to [0, 255]
        /// - Copy the resultin color values into 'destArea' scaling up the block by amount defined in <see cref="subSamplingDivisors"/>
        /// </summary>
        /// <param name="sourceBlock">The source block.</param>
        /// <param name="destArea">The destination buffer area.</param>
        public void ProcessBlockColorsInto(
            ref Block8x8 sourceBlock,
            in BufferArea<float> destArea)
        {
            ref Block8x8F b = ref this.SourceBlock;
            b.LoadFrom(ref sourceBlock);

            // Dequantize:
            b.MultiplyInplace(ref this.DequantiazationTable);

            FastFloatingPointDCT.TransformIDCT(ref b, ref this.WorkspaceBlock1, ref this.WorkspaceBlock2);

            // To conform better to libjpeg we actually NEED TO loose precision here.
            // This is because they store blocks as Int16 between all the operations.
            // To be "more accurate", we need to emulate this by rounding!
            this.WorkspaceBlock1.NormalizeColorsAndRoundInplace();

            this.WorkspaceBlock1.CopyTo(destArea, this.subSamplingDivisors.Width, this.subSamplingDivisors.Height);
        }
    }
}