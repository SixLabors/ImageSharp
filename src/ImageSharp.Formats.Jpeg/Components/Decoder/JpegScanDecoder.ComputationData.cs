// <copyright file="JpegScanDecoder.ComputationData.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats.Jpg
{
    using System.Runtime.InteropServices;

    /// <content>
    /// Conains the definition of <see cref="ComputationData"/>
    /// </content>
    internal unsafe partial struct JpegScanDecoder
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
            /// The buffer storing the <see cref="ComponentScan"/>-s for each component
            /// </summary>
            public fixed byte ScanData[3 * JpegDecoderCore.MaxComponents];

            /// <summary>
            /// The DC values for each component
            /// </summary>
            public fixed int Dc[JpegDecoderCore.MaxComponents];

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