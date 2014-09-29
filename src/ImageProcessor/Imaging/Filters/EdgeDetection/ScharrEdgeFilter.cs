// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ScharrEdgeFilter.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   The Scharr operator filter.
//   <see href="http://en.wikipedia.org/wiki/Sobel_operator#Alternative_operators" />
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Filters.EdgeDetection
{
    /// <summary>
    /// The Scharr operator filter.
    /// <see href="http://en.wikipedia.org/wiki/Sobel_operator#Alternative_operators"/>
    /// </summary>
    public class ScharrEdgeFilter : I2DEdgeFilter
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
                    { -3, 0, 3 }, 
                    { -10, 0, 10 }, 
                    { -3, 0, 3 }
                };
            }
        }

        /// <summary>
        /// Gets the vertical gradient operator.
        /// </summary>
        public double[,] VerticalGradientOperator
        {
            get
            {
                return new double[,]
                {
                    { 3, 10, 3 }, 
                    { 0, 0, 0 }, 
                    { -3, -10, -3 }
                };
            }
        }
    }
}
