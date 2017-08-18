// <copyright file="JpegBlockProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats.Jpg
{
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    using ImageSharp.Memory;

    /// <summary>
    /// Encapsulates the implementation of processing "raw" <see cref="Buffer{T}"/>-s into Jpeg image channels.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct JpegBlockProcessor
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
        /// The component index.
        /// </summary>
        private int componentIndex;

        /// <summary>
        /// Initialize the <see cref="JpegBlockProcessor"/> instance on the stack.
        /// </summary>
        /// <param name="processor">The <see cref="JpegBlockProcessor"/> instance</param>
        /// <param name="componentIndex">The current component index</param>
        public static void Init(JpegBlockProcessor* processor, int componentIndex)
        {
            processor->componentIndex = componentIndex;
            processor->data = ComputationData.Create();
            processor->pointers = new DataPointers(&processor->data);
        }

        /// <summary>
        /// Dequantize, perform the inverse DCT and store the blocks to the into the corresponding <see cref="JpegPixelArea"/> instances.
        /// </summary>
        /// <param name="decoder">The <see cref="JpegDecoderCore"/> instance</param>
        public void ProcessAllBlocks(JpegDecoderCore decoder)
        {
            Buffer<DecodedBlock> blockArray = decoder.DecodedBlocks[this.componentIndex];
            for (int i = 0; i < blockArray.Length; i++)
            {
                this.ProcessBlockColors(decoder, ref blockArray[i]);
            }
        }

        /// <summary>
        /// Dequantize, perform the inverse DCT and store decodedBlock.Block to the into the corresponding <see cref="JpegPixelArea"/> instance.
        /// </summary>
        /// <param name="decoder">The <see cref="JpegDecoderCore"/></param>
        /// <param name="decodedBlock">The <see cref="DecodedBlock"/></param>
        private void ProcessBlockColors(JpegDecoderCore decoder, ref DecodedBlock decodedBlock)
        {
            this.data.Block = decodedBlock.Block;
            int qtIndex = decoder.ComponentArray[this.componentIndex].Selector;
            this.data.QuantiazationTable = decoder.QuantizationTables[qtIndex];

            Block8x8F* b = this.pointers.Block;

            Block8x8F.UnZig(b, this.pointers.QuantiazationTable, this.pointers.Unzig);

            DCT.TransformIDCT(ref *b, ref *this.pointers.Temp1, ref *this.pointers.Temp2);

            JpegPixelArea destChannel = decoder.GetDestinationChannel(this.componentIndex);
            JpegPixelArea destArea = destChannel.GetOffsetedSubAreaForBlock(decodedBlock.Bx, decodedBlock.By);
            destArea.LoadColorsFrom(this.pointers.Temp1, this.pointers.Temp2);
        }

        /// <summary>
        /// Holds the "large" data blocks needed for computations.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct ComputationData
        {
            /// <summary>
            /// Temporal block 1 to store intermediate and/or final computation results
            /// </summary>
            public Block8x8F Block;

            /// <summary>
            /// Temporal block 1 to store intermediate and/or final computation results
            /// </summary>
            public Block8x8F Temp1;

            /// <summary>
            /// Temporal block 2 to store intermediate and/or final computation results
            /// </summary>
            public Block8x8F Temp2;

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
                ComputationData data = default(ComputationData);
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
            /// Pointer to <see cref="DecodedBlock.Block"/>
            /// </summary>
            public Block8x8F* Block;

            /// <summary>
            /// Pointer to <see cref="ComputationData.Temp1"/>
            /// </summary>
            public Block8x8F* Temp1;

            /// <summary>
            /// Pointer to <see cref="ComputationData.Temp2"/>
            /// </summary>
            public Block8x8F* Temp2;

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
                this.Block = &dataPtr->Block;
                this.Temp1 = &dataPtr->Temp1;
                this.Temp2 = &dataPtr->Temp2;
                this.QuantiazationTable = &dataPtr->QuantiazationTable;
                this.Unzig = dataPtr->Unzig.Data;
            }
        }
    }
}