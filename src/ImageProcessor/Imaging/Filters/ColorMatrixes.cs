// -----------------------------------------------------------------------
// <copyright file="ColorMatrixes.cs" company="James South">
//     Copyright (c) James South.
//     Dual licensed under the MIT or GPL Version 2 licenses.
// </copyright>
// -----------------------------------------------------------------------

namespace ImageProcessor.Imaging.Filters
{
    #region Using
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Drawing.Imaging; 
    #endregion

    /// <summary>
    /// A list of available color matrices to apply to an image.
    /// </summary>
    internal static class ColorMatrixes
    {
        /// <summary>
        /// Gets Sepia.
        /// </summary>
        internal static ColorMatrix Sepia
        {
            get
            {
                return new ColorMatrix(
                    new float[][]
                            {
                                new float[] { .393f, .349f, .272f, 0, 0 }, 
                                new float[] { .769f, .686f, .534f, 0, 0 },
                                new float[] { .189f, .168f, .131f, 0, 0 },
                                new float[] { 0, 0, 0, 1, 0 },
                                new float[] { 0, 0, 0, 0, 1 }
                          });
            }
        }

        /// <summary>
        /// Gets BlackWhite.
        /// </summary>
        internal static ColorMatrix BlackWhite
        {
            get
            {
                return new ColorMatrix(
                    new float[][]
                            {
                                new float[] { 1.5f, 1.5f, 1.5f, 0, 0 }, 
                                new float[] { 1.5f, 1.5f, 1.5f, 0, 0 },
                                new float[] { 1.5f, 1.5f, 1.5f, 0, 0 },
                                new float[] { 0, 0, 0, 1, 0 },
                                new float[] { -1, -1, -1, 0, 1 }
                          });
            }
        }

        /// <summary>
        /// Gets Polaroid.
        /// </summary>
        internal static ColorMatrix Polaroid
        {
            get
            {
                return new ColorMatrix(
                    new float[][]
                            {
                                new float[] { 1.638f, -0.062f, -0.262f, 0, 0 },
                                new float[] { -0.122f, 1.378f, -0.122f, 0, 0 },
                                new float[] { 1.016f, -0.016f, 1.383f, 0, 0 },
                                new float[] { 0, 0, 0, 1, 0 },
                                new float[] { 0.06f, -0.05f, -0.05f, 0, 1 }
                          });
            }
        }

        /// <summary>
        /// Gets Lomograph.
        /// </summary>
        internal static ColorMatrix Lomograph
        {
            get
            {
                return new ColorMatrix(
                    new float[][]
                            {
                                new float[] { 1.50f, 0, 0, 0, 0 }, 
                                new float[] { 0, 1.45f, 0, 0, 0 },
                                new float[] { 0, 0, 1.09f, 0, 0 }, 
                                new float[] { 0, 0, 0, 1, 0 },
                                new float[] { -0.10f, 0.05f, -0.08f, 0, 1 }
                            });
            }
        }

        /// <summary>
        /// Gets GreyScale.
        /// </summary>
        internal static ColorMatrix GreyScale
        {
            get
            {
                return new ColorMatrix(
                    new float[][]
                            {
                                new float[] { .33f, .33f, .33f, 0, 0 }, 
                                new float[] { .59f, .59f, .59f, 0, 0 },
                                new float[] { .11f, .11f, .11f, 0, 0 }, 
                                new float[] { 0, 0, 0, 1, 0 },
                                new float[] { 0, 0, 0, 0, 1 }
                            });
            }
        }

        /// <summary>
        /// Gets Gotham.
        /// </summary>
        internal static ColorMatrix Gotham
        {
            get
            {
                return new ColorMatrix(
                    new float[][]
                            {
                                new float[] { .9f, .9f, .9f, 0, 0 }, 
                                new float[] { .9f, .9f, .9f, 0, 0 }, 
                                new float[] { .9f, .9f, .9f, 0, 0 }, 
                                new float[] { 0, 0, 0, 1, 0 },
                                new float[] { -.5f, -.5f, -.45f, 0, 1 }
                            });
            }
        }

        /// <summary>
        /// Gets Invert.
        /// </summary>
        internal static ColorMatrix Invert
        {
            get
            {
                return new ColorMatrix(
                    new float[][]
                            {
                                new float[] { -1, 0, 0, 0, 0 }, 
                                new float[] { 0, -1, 0, 0, 0 },  
                                new float[] { 0, 0, -1, 0, 0 }, 
                                new float[] { 0, 0, 0, 1, 0 },
                                new float[] { 1, 1, 1, 0, 1 }
                            });
            }
        }

        /// <summary>
        /// Gets HiSatch.
        /// </summary>
        internal static ColorMatrix HiSatch
        {
            get
            {
                return new ColorMatrix(
                    new float[][]
                            {
                                new float[] { 3, -1, -1, 0, 0 }, 
                                new float[] { -1, 3, -1, 0, 0 },  
                                new float[] { -1, -1, 3, 0, 0 }, 
                                new float[] { 0, 0, 0, 1, 0 },
                                new float[] { 0, 0, 0, 0, 1 }
                            });
            }
        }

        /// <summary>
        /// Gets LoSatch.
        /// </summary>
        internal static ColorMatrix LoSatch
        {
            get
            {
                return new ColorMatrix(
                       new float[][]
                            {
                                new float[] { 1, 0, 0, 0, 0 }, 
                                new float[] { 0, 1, 0, 0, 0 },  
                                new float[] { 0, 0, 1, 0, 0 }, 
                                new float[] { 0, 0, 0, 1, 0 },
                                new float[] { .25f, .25f, .25f, 0, 1 }
                            });
            }
        }
    }
}
