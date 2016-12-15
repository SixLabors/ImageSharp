using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ImageSharp.Drawing
{
    /// <summary>
    /// Options for influancing the drawing functions.
    /// </summary>
    public struct GraphicsOptions
    {
        /// <summary>
        /// Represents the default <see cref="GraphicsOptions"/>.
        /// </summary>
        public static readonly GraphicsOptions Default = new GraphicsOptions(true);

        /// <summary>
        /// Initializes a new instance of the <see cref="GraphicsOptions"/> struct.
        /// </summary>
        /// <param name="enableAntialiasing">if set to <c>true</c> [enable antialiasing].</param>
        public GraphicsOptions(bool enableAntialiasing)
        {
            Antialias = enableAntialiasing;
        }

        /// <summary>
        /// Should antialias be applied.
        /// </summary>
        public bool Antialias;
    }
}