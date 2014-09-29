// --------------------------------------------------------------------------------------------------------------------
// <copyright file="KirschEdgeFilter.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   The Kirsch operator filter.
//   <see href="http://en.wikipedia.org/wiki/Kirsch_operator" />
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Filters.EdgeDetection
{
    /// <summary>
    /// The Kirsch operator filter.
    /// <see href="http://en.wikipedia.org/wiki/Kirsch_operator"/>
    /// </summary>
    public class KirschEdgeFilter : I2DEdgeFilter
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
                    { 5, 5, 5 }, 
                    { -3, 0, -3 }, 
                    { -3, -3, -3 }
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
                    { 5, -3, -3 }, 
                    { 5,  0, -3 }, 
                    { 5, -3, -3 } 
                };
            }
        }
    }
}
