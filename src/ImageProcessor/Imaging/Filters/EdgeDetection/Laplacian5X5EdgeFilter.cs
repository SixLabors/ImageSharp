// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Laplacian5X5EdgeFilter.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   The Laplacian 5 x 5 operator filter.
//   <see href="http://en.wikipedia.org/wiki/Discrete_Laplace_operator" />
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Filters.EdgeDetection
{
    /// <summary>
    /// The Laplacian 5 x 5 operator filter.
    /// <see href="http://en.wikipedia.org/wiki/Discrete_Laplace_operator"/>
    /// </summary>
    public class Laplacian5X5EdgeFilter : IEdgeFilter
    {
        /// <summary>
        /// Gets the horizontal gradient operator.
        /// </summary>
        public double[,] HorizontalGradientOperator
        {
            get
            {
                return new double[,] 
                { 
                    { -1, -1, -1, -1, -1 }, 
                    { -1, -1, -1, -1, -1 }, 
                    { -1, -1, 24, -1, -1 }, 
                    { -1, -1, -1, -1, -1 }, 
                    { -1, -1, -1, -1, -1 }
                };
            }
        }
    }
}
