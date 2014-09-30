// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Laplacian3X3EdgeFilter.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   The Laplacian 3 x 3 operator filter.
//   <see href="http://en.wikipedia.org/wiki/Discrete_Laplace_operator" />
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Filters.EdgeDetection
{
    /// <summary>
    /// The Laplacian 3 x 3 operator filter.
    /// <see href="http://en.wikipedia.org/wiki/Discrete_Laplace_operator"/>
    /// </summary>
    public class Laplacian3X3EdgeFilter : IEdgeFilter
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
                    { -1, -1, -1 }, 
                    { -1,  8, -1 }, 
                    { -1, -1, -1 } 
                };
            }
        }
    }
}
