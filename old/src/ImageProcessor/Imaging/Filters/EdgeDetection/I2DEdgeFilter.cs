// --------------------------------------------------------------------------------------------------------------------
// <copyright file="I2DEdgeFilter.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Describes properties for creating 2 dimension edge detection filters.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Filters.EdgeDetection
{
    /// <summary>
    /// Describes properties for creating 2 dimension edge detection filters.
    /// </summary>
    public interface I2DEdgeFilter : IEdgeFilter
    {
        /// <summary>
        /// Gets the vertical gradient operator.
        /// </summary>
        double[,] VerticalGradientOperator { get; }
    }
}
