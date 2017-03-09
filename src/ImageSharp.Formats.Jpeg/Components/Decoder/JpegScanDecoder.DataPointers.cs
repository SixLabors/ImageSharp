// <copyright file="JpegScanDecoder.DataPointers.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats.Jpg
{
    /// <content>
    /// Conains the definition of <see cref="DataPointers"/>
    /// </content>
    internal unsafe partial struct JpegScanDecoder
    {
        /// <summary>
        /// Contains pointers to the memory regions of <see cref="ComputationData"/> so they can be easily passed around to pointer based utility methods of <see cref="Block8x8F"/>
        /// </summary>
        public struct DataPointers
        {
            /// <summary>
            /// Pointer to <see cref="ComputationData.Block"/>
            /// </summary>
            public Block8x8F* Block;

            /// <summary>
            /// Pointer to <see cref="ComputationData.Unzig"/> as int*
            /// </summary>
            public int* Unzig;

            /// <summary>
            /// Pointer to <see cref="ComputationData.ScanData"/> as Scan*
            /// </summary>
            public ComponentScan* ComponentScan;

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
                this.ComponentScan = (ComponentScan*)basePtr->ScanData;
                this.Dc = basePtr->Dc;
            }
        }
    }
}