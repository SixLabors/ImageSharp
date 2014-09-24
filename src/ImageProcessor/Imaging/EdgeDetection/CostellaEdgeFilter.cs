// --------------------------------------------------------------------------------------------------------------------
// <copyright file="CostellaEdgeFilter.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   The Costella operator filter.
//   <see href="http://johncostella.com/edgedetect/" />
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.EdgeDetection
{
    /// <summary>
    /// The Costella operator filter.
    /// <see href="http://johncostella.com/edgedetect/"/>
    /// </summary>
    public class CostellaEdgeFilter : IEdgeFilter
    {
        /// <summary>
        /// Gets the horizontal gradient operator.
        /// </summary>
        public double[,] HorizontalGradientOperator
        {
            get
            {
                return new double[,] 
                { { -1, -1, -1, -1, -1, }, 
                  { -1, -1, -1, -1, -1, }, 
                  { -1, -1, 24, -1, -1, }, 
                  { -1, -1, -1, -1, -1, }, 
                  { -1, -1, -1, -1, -1  }, };
                return new double[,] 
                { 
                    { -1, -3, 0, 3, 1 }, 
                    { -1, -3, 0, 3, 1 }, 
                    { -1, -3, 0, 3, 1 }, 
                    { -1, -3, 0, 3, 1 }, 
                    { -1, -3, 0, 3, 1 } 
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
                { { -1, -1, -1, -1, -1, }, 
                  { -1, -1, -1, -1, -1, }, 
                  { -1, -1, 24, -1, -1, }, 
                  { -1, -1, -1, -1, -1, }, 
                  { -1, -1, -1, -1, -1  }, };
                return new double[,]
                { 
                    { 1, 1, 1, 1, 1 }, 
                    { 3, 3, 3, 3, 3 }, 
                    { 0, 0, 0, 0, 0 }, 
                    { -3, -3, -3, -3, -3 }, 
                    { -1, -1, -1, -1, -1 } 
                };
            }
        }
    }
}
