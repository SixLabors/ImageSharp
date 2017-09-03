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
        /// The <see cref="ComputationData"/>
        /// </summary>
        private ComputationData data;

        /// <summary>
        /// Pointers to elements of <see cref="data"/>
        /// </summary>
        private DataPointers pointers;

        /// <summary>
        /// Initialize the <see cref="JpegBlockPostProcessor"/> instance on the stack.
        /// </summary>
        /// <param name="postProcessor">The <see cref="JpegBlockPostProcessor"/> instance</param>
        public static void Init(JpegBlockPostProcessor* postProcessor)
        {
            postProcessor->data = ComputationData.Create();
            postProcessor->pointers = new DataPointers(&postProcessor->data);
        }

        public void QuantizeAndTransform(IRawJpegData decoder, IJpegComponent component, ref Block8x8 sourceBlock)
        {
            this.data.SourceBlock = sourceBlock.AsFloatBlock();
            int qtIndex = component.QuantizationTableIndex;
            this.data.QuantiazationTable = decoder.QuantizationTables[qtIndex];

            Block8x8F* b = this.pointers.SourceBlock;

            Block8x8F.DequantizeBlock(b, this.pointers.QuantiazationTable, this.pointers.Unzig);

            FastFloatingPointDCT.TransformIDCT(ref *b, ref this.data.WorkspaceBlock1, ref this.data.WorkspaceBlock2);
        }

        public void ProcessBlockColorsInto(
            IRawJpegData decoder,
            IJpegComponent component,
            ref Block8x8 sourceBlock,
            BufferArea<float> destArea)
        {
            this.QuantizeAndTransform(decoder, component, ref sourceBlock);

            this.data.WorkspaceBlock1.NormalizeColorsInplace();

            // To conform better to libjpeg we actually NEED TO loose precision here.
            // This is because they store blocks as Int16 between all the operations.
            // Unfortunately, we need to emulate this to be "more accurate" :(
            this.data.WorkspaceBlock1.RoundInplace();

            Size divs = component.SubSamplingDivisors;
            this.data.WorkspaceBlock1.CopyTo(destArea, divs.Width, divs.Height);
        }

        /// <summary>
        /// Holds the "large" data blocks needed for computations.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct ComputationData
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
            public Block8x8F QuantiazationTable;

            /// <summary>
            /// The jpeg unzig data
            /// </summary>
            public UnzigData Unzig;

            /// <summary>
            /// Creates and initializes a new <see cref="ComputationData"/> instance
            /// </summary>
            /// <returns>The <see cref="ComputationData"/></returns>
            public static ComputationData Create()
            {
                var data = default(ComputationData);
                data.Unzig = UnzigData.Create();
                return data;
            }
        }

        /// <summary>
        /// Contains pointers to the memory regions of <see cref="ComputationData"/> so they can be easily passed around to pointer based utility methods of <see cref="Block8x8F"/>
        /// </summary>
        public struct DataPointers
        {
            /// <summary>
            /// Pointer to <see cref="ComputationData.SourceBlock"/>
            /// </summary>
            public Block8x8F* SourceBlock;

            /// <summary>
            /// Pointer to <see cref="ComputationData.WorkspaceBlock1"/>
            /// </summary>
            public Block8x8F* WorkspaceBlock1;

            /// <summary>
            /// Pointer to <see cref="ComputationData.WorkspaceBlock2"/>
            /// </summary>
            public Block8x8F* WorkspaceBlock2;

            /// <summary>
            /// Pointer to <see cref="ComputationData.QuantiazationTable"/>
            /// </summary>
            public Block8x8F* QuantiazationTable;

            /// <summary>
            /// Pointer to <see cref="ComputationData.Unzig"/> as int*
            /// </summary>
            public int* Unzig;

            /// <summary>
            /// Initializes a new instance of the <see cref="DataPointers" /> struct.
            /// </summary>
            /// <param name="dataPtr">Pointer to <see cref="ComputationData"/></param>
            internal DataPointers(ComputationData* dataPtr)
            {
                this.SourceBlock = &dataPtr->SourceBlock;
                this.WorkspaceBlock1 = &dataPtr->WorkspaceBlock1;
                this.WorkspaceBlock2 = &dataPtr->WorkspaceBlock2;
                this.QuantiazationTable = &dataPtr->QuantiazationTable;
                this.Unzig = dataPtr->Unzig.Data;
            }
        }
    }
}