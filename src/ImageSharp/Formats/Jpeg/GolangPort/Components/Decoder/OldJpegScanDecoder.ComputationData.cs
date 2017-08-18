// <copyright file="JpegScanDecoder.ComputationData.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats.Jpeg.GolangPort.Components.Decoder
{
    using System.Runtime.InteropServices;
    using Block8x8F = ImageSharp.Formats.Jpeg.Common.Block8x8F;

    /// <content>
    /// Conains the definition of <see cref="ComputationData"/>
    /// </content>
    internal unsafe partial struct OldJpegScanDecoder
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
            public Block8x8F Block;

            /// <summary>
            /// The jpeg unzig data
            /// </summary>
            public UnzigData Unzig;

            /// <summary>
            /// The buffer storing the <see cref="OldComponentScan"/>-s for each component
            /// </summary>
            public fixed byte ScanData[3 * OldJpegDecoderCore.MaxComponents];

            /// <summary>
            /// The DC values for each component
            /// </summary>
            public fixed int Dc[OldJpegDecoderCore.MaxComponents];

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
    }
}