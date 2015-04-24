// --------------------------------------------------------------------------------------------------------------------
// <copyright file="KayyaliEdgeFilter.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   The Kayyali operator filter.
//   <see href="http://edgedetection.webs.com/" />
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Filters.EdgeDetection
{
    /// <summary>
    /// The Kayyali operator filter.
    /// <see href="http://edgedetection.webs.com/"/>
    /// </summary>
    public class KayyaliEdgeFilter : I2DEdgeFilter
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
                    { 6, 0, -6 }, 
                    { 0, 0, 0 }, 
                    { -6, 0, 6 }
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
                    { -6, 0, 6 }, 
                    { 0, 0, 0 }, 
                    { 6, 0, -6 }
                };
            }
        }
    }
}
