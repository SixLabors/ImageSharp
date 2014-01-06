// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ColorMatrixes.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   A list of available color matrices to apply to an image.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Filters
{
    #region Using
    using System.Diagnostics.CodeAnalysis;
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
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "Reviewed. Suppression is OK here.")]
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
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "Reviewed. Suppression is OK here.")]
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
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "Reviewed. Suppression is OK here.")]
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

        /// <summary>
        /// Gets <see cref="T:System.Drawing.Imaging.ColorMatrix"/> for generating the high pass
        /// on the comic book filter.
        /// </summary>
        internal static ColorMatrix ComicHigh
        {
            get
            {
                return new ColorMatrix(
                    new float[][]
                            {
                                new float[] { 2, -0.5f, -0.5f, 0, 0 }, 
                                new float[] { -0.5f, 2, -0.5f, 0, 0 },  
                                new float[] { -0.5f, -0.5f, 2, 0, 0 }, 
                                new float[] { 0, 0, 0, 1, 0 },
                                new float[] { 0, 0, 0, 0, 1 }
                            });
            }
        }

        /// <summary>
        /// Gets <see cref="T:System.Drawing.Imaging.ColorMatrix"/> for generating the low pass
        /// on the comic book filter.
        /// </summary>
        internal static ColorMatrix ComicLow
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
                                new float[] { .075f, .075f, .075f, 0, 1 }
                            });
            }
        }

    }
}
