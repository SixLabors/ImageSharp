// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jpeg.Common;
using SixLabors.ImageSharp.Memory;
using Block8x8F = SixLabors.ImageSharp.Formats.Jpeg.Common.Block8x8F;

namespace SixLabors.ImageSharp.Formats.Jpeg.GolangPort.Components.Decoder
{
    using System.Runtime.CompilerServices;

    using SixLabors.Primitives;

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

        /// <summary>
        /// Dequantize, perform the inverse DCT and store the blocks to the into the corresponding <see cref="OrigJpegPixelArea"/> instances.
        /// </summary>
        /// <param name="decoder">The <see cref="OrigJpegDecoderCore"/> instance</param>
        /// <param name="component">The component</param>
        public void ProcessAllBlocks(OrigJpegDecoderCore decoder, IJpegComponent component)
        {
            for (int by = 0; by < component.SizeInBlocks.Height; by++)
            {
                for (int bx = 0; bx < component.SizeInBlocks.Width; bx++)
                {
                    this.ProcessBlockColors(decoder, component, bx, by);
                }
            }
        }

        public void QuantizeAndTransform(IRawJpegData decoder, IJpegComponent component, ref Block8x8 sourceBlock)
        {
            this.data.SourceBlock = sourceBlock.AsFloatBlock();
            int qtIndex = component.QuantizationTableIndex;
            this.data.QuantiazationTable = decoder.QuantizationTables[qtIndex];

            Block8x8F* b = this.pointers.SourceBlock;

            Block8x8F.QuantizeBlock(b, this.pointers.QuantiazationTable, this.pointers.Unzig);

            FastFloatingPointDCT.TransformIDCT(ref *b, ref this.data.ResultBlock, ref this.data.TempBlock);
        }

        public void ProcessBlockColorsInto(
            IRawJpegData decoder,
            IJpegComponent component,
            ref Block8x8 sourceBlock,
            BufferArea<float> destArea)
        {
            this.QuantizeAndTransform(decoder, component, ref sourceBlock);

            this.data.ResultBlock.NormalizeColorsInplace();
            Size divs = component.SubSamplingDivisors;

            // To conform better to libjpeg we actually NEED TO loose precision here.
            // This is because they store blocks as Int16 between all the operations.
            // Unfortunately, we need to emulate this to be "more accurate" :(
            this.data.ResultBlock.RoundInplace();

            this.data.ResultBlock.CopyTo(destArea, divs.Width, divs.Height);
        }

        /// <summary>
        /// Dequantize, perform the inverse DCT and store decodedBlock.Block to the into the corresponding <see cref="OrigJpegPixelArea"/> instance.
        /// </summary>
        /// <param name="decoder">The <see cref="OrigJpegDecoderCore"/></param>
        /// <param name="component">The <see cref="OrigComponent"/></param>
        /// <param name="bx">The x index of the block in <see cref="OrigComponent.SpectralBlocks"/></param>
        /// <param name="by">The y index of the block in <see cref="OrigComponent.SpectralBlocks"/></param>
        private void ProcessBlockColors(OrigJpegDecoderCore decoder, IJpegComponent component, int bx, int by)
        {
            ref Block8x8 sourceBlock = ref component.GetBlockReference(bx, @by);

            this.QuantizeAndTransform(decoder, component, ref sourceBlock);

            OrigJpegPixelArea destChannel = decoder.GetDestinationChannel(component.Index);
            OrigJpegPixelArea destArea = destChannel.GetOffsetedSubAreaForBlock(bx, by);
            destArea.LoadColorsFrom(this.pointers.ResultBlock, this.pointers.TempBlock);
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
            public Block8x8F SourceBlock;

            /// <summary>
            /// Temporal block 1 to store intermediate and/or final computation results
            /// </summary>
            public Block8x8F ResultBlock;

            /// <summary>
            /// Temporal block 2 to store intermediate and/or final computation results
            /// </summary>
            public Block8x8F TempBlock;

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
            /// Pointer to <see cref="ComputationData.SourceBlock"/>
            /// </summary>
            public Block8x8F* SourceBlock;

            /// <summary>
            /// Pointer to <see cref="ComputationData.ResultBlock"/>
            /// </summary>
            public Block8x8F* ResultBlock;

            /// <summary>
            /// Pointer to <see cref="ComputationData.TempBlock"/>
            /// </summary>
            public Block8x8F* TempBlock;

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
                this.ResultBlock = &dataPtr->ResultBlock;
                this.TempBlock = &dataPtr->TempBlock;
                this.QuantiazationTable = &dataPtr->QuantiazationTable;
                this.Unzig = dataPtr->Unzig.Data;
            }
        }
    }
}