// --------------------------------------------------------------------------------------------------------------------
// <copyright file="OutputType.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Enumerates the output type to produce.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Plugins.Cair.Imaging
{
    /// <summary>
    /// Enumerates the output type to produce.
    /// </summary>
    public enum OutputType
    {
        /// <summary>
        /// Output the result as a carved image. The default action.
        /// </summary>
        Cair = 0,

        /// <summary>
        /// Output the result as a greyscale image.
        /// </summary>
        Grayscale = 1,

        /// <summary>
        /// Output the result highlighting the detected edges.
        /// </summary>
        Edge = 2,

        /// <summary>
        /// Output the result highlighting the vertical energy patterns.
        /// </summary>
        VerticalEnergy = 3,

        /// <summary>
        /// Output the result highlighting the vertical energy patterns.
        /// </summary>
        HorizontalEnergy = 4,

        /// <summary>
        /// Appears to do nothing.
        /// </summary>
        Removal = 5,

        /// <summary>
        /// Output the result as a carved image with the focus on high quality output over speed.
        /// </summary>
        CairHighDefinition = 6
    }
}
