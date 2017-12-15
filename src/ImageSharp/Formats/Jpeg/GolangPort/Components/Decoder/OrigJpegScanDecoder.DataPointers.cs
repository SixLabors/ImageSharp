// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats.Jpeg.Common;

namespace SixLabors.ImageSharp.Formats.Jpeg.GolangPort.Components.Decoder
{
    /// <content>
    /// Conains the definition of <see cref="DataPointers"/>
    /// </content>
    internal unsafe partial struct OrigJpegScanDecoder
    {
        /// <summary>
        /// Contains pointers to the memory regions of <see cref="ComputationData"/> so they can be easily passed around to pointer based utility methods of <see cref="Block8x8F"/>
        /// </summary>
        public struct DataPointers
        {
            /// <summary>
            /// Pointer to <see cref="ComputationData.Block"/>
            /// </summary>
            public Block8x8* Block;

            /// <summary>
            /// Pointer to <see cref="ComputationData.Unzig"/> as int*
            /// </summary>
            public int* Unzig;

            /// <summary>
            /// Pointer to <see cref="ComputationData.ScanData"/> as Scan*
            /// </summary>
            public OrigComponentScan* ComponentScan;

            /// <summary>
            /// Pointer to <see cref="ComputationData.Dc"/>
            /// </summary>
            public int* Dc;

            /// <summary>
            /// Initializes a new instance of the <see cref="DataPointers" /> struct.
            /// </summary>
            /// <param name="basePtr">The pointer pointing to <see cref="ComputationData"/></param>
            public DataPointers(ComputationData* basePtr)
            {
                this.Block = &basePtr->Block;
                this.Unzig = basePtr->Unzig.Data;
                this.ComponentScan = (OrigComponentScan*)basePtr->ScanData;
                this.Dc = basePtr->Dc;
            }
        }
    }
}