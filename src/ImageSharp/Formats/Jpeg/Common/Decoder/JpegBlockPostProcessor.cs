// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Formats.Jpeg.Common.Decoder
{
    /// <summary>
    /// Encapsulates the implementation of processing "raw" <see cref="Buffer{T}"/>-s into Jpeg image channels.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct JpegBlockPostProcessor
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

        private Size subSamplingDivisors;

        /// <summary>
        /// Initialize the <see cref="JpegBlockPostProcessor"/> instance on the stack.
        /// </summary>
        public static void Init(JpegBlockPostProcessor* postProcessor, IRawJpegData decoder, IJpegComponent component)
        {
            int qtIndex = component.QuantizationTableIndex;
            postProcessor->DequantiazationTable = ZigZag.CreateDequantizationTable(ref decoder.QuantizationTables[qtIndex]);
            postProcessor->subSamplingDivisors = component.SubSamplingDivisors;
        }

        public void ProcessBlockColorsInto(
            ref Block8x8 sourceBlock,
            BufferArea<float> destArea)
        {
            ref Block8x8F b = ref this.SourceBlock;
            sourceBlock.CopyToFloatBlock(ref b);

            // Dequantize:
            b.MultiplyInplace(ref this.DequantiazationTable);

            FastFloatingPointDCT.TransformIDCT(ref b, ref this.WorkspaceBlock1, ref this.WorkspaceBlock2);

            // To conform better to libjpeg we actually NEED TO loose precision here.
            // This is because they store blocks as Int16 between all the operations.
            // To be "more accurate", we need to emulate this by rounding!
            if (SimdUtils.IsAvx2CompatibleArchitecture)
            {
                this.WorkspaceBlock1.NormalizeColorsAndRoundInplaceAvx2();
            }
            else
            {
                this.WorkspaceBlock1.NormalizeColorsInplace();
                this.WorkspaceBlock1.RoundInplace();
            }

            this.WorkspaceBlock1.CopyTo(destArea, this.subSamplingDivisors.Width, this.subSamplingDivisors.Height);
        }
    }
}