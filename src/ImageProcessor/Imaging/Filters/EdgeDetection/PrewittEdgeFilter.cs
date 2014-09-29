// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PrewittEdgeFilter.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   The Prewitt operator filter.
//   <see href="http://en.wikipedia.org/wiki/Prewitt_operator" />
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Filters.EdgeDetection
{
    /// <summary>
    /// The Prewitt operator filter.
    /// <see href="http://en.wikipedia.org/wiki/Prewitt_operator"/>
    /// </summary>
    public class PrewittEdgeFilter : I2DEdgeFilter
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
                    { -1, 0, 1 }, 
                    { -1, 0, 1 }, 
                    { -1, 0, 1 }
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
                    { 1, 1, 1 }, 
                    { 0, 0, 0 }, 
                    { -1, -1, -1 }
                };
            }
        }
    }
}
