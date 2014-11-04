// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ColorData.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace nQuant
{
    /// <summary>
    /// The color data.
    /// </summary>
    public class ColorData
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ColorData"/> class.
        /// </summary>
        /// <param name="dataGranularity">
        /// The data granularity.
        /// </param>
        public ColorData(int dataGranularity)
        {
            dataGranularity++;

            this.Moments = new ColorMoment[dataGranularity, dataGranularity, dataGranularity, dataGranularity];
        }

        /// <summary>
        /// Gets the moments.
        /// </summary>
        public ColorMoment[, , ,] Moments { get; private set; }
    }
}