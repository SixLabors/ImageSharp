// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IEdgeFilter.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Describes properties for creating edge detection filters.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.EdgeDetection
{
    /// <summary>
    /// Describes properties for creating edge detection filters.
    /// </summary>
    public interface IEdgeFilter
    {
        /// <summary>
        /// Gets the horizontal gradient operator.
        /// </summary>
        double[,] HorizontalGradientOperator { get; }

        /// <summary>
        /// Gets the vertical gradient operator.
        /// </summary>
        double[,] VerticalGradientOperator { get; }
    }
}
