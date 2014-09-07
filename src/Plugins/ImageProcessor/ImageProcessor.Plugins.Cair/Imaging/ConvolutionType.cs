// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ConvolutionType.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Provides enumeration of the content aware resize convolution types.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Plugins.Cair.Imaging
{
    /// <summary>
    /// Provides enumeration of the content aware resize convolution types.
    /// </summary>
    public enum ConvolutionType
    {
        /// <summary>
        /// The Prewitt kernel convolution type.
        /// </summary>
        Prewitt = 0,

        /// <summary>
        /// The V1 kernel convolution type.
        /// </summary>
        V1 = 1,

        /// <summary>
        /// The VSquare kernel convolution type.
        /// </summary>
        VSquare = 2,

        /// <summary>
        /// The Sobel kernel convolution type.
        /// </summary>
        Sobel = 3,

        /// <summary>
        /// The Laplacian kernel convolution type.
        /// </summary>
        Laplacian = 4
    }
}