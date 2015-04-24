// --------------------------------------------------------------------------------------------------------------------
// <copyright file="EnergyFunction.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Enumerates the energy functions available to the resize method.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Plugins.Cair.Imaging
{
    /// <summary>
    /// Enumerates the energy functions available to the resize method.
    /// </summary>
    public enum EnergyFunction
    {
        /// <summary>
        /// The standard energy function.
        /// </summary>
        Backward = 0,

        /// <summary>
        /// The forward energy function. A look-ahead is performed to reduce jagged edges
        /// caused by removing areas of low energy.
        /// </summary>
        Forward = 1
    }
}
