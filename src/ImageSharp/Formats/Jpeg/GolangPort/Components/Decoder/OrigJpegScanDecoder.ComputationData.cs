// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jpeg.Common;

namespace SixLabors.ImageSharp.Formats.Jpeg.GolangPort.Components.Decoder
{
    /// <content>
    /// Conains the definition of <see cref="ComputationData"/>
    /// </content>
    internal unsafe partial struct OrigJpegScanDecoder
    {
        /// <summary>
        /// Holds the "large" data blocks needed for computations.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct ComputationData
        {
            /// <summary>
            /// The main input/working block
            /// </summary>
            public Block8x8 Block;

            /// <summary>
            /// The jpeg unzig data
            /// </summary>
            public ZigZag Unzig;

            /// <summary>
            /// The buffer storing the <see cref="OrigComponentScan"/>-s for each component
            /// </summary>
            public fixed byte ScanData[3 * OrigJpegDecoderCore.MaxComponents];

            /// <summary>
            /// The DC values for each component
            /// </summary>
            public fixed int Dc[OrigJpegDecoderCore.MaxComponents];

            /// <summary>
            /// Creates and initializes a new <see cref="ComputationData"/> instance
            /// </summary>
            /// <returns>The <see cref="ComputationData"/></returns>
            public static ComputationData Create()
            {
                var data = default(ComputationData);
                data.Unzig = ZigZag.CreateUnzigTable();
                return data;
            }
        }
    }
}