// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ColorMatrixes.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   A list of available color matrices to apply to an image.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Filters.Photo
{
    using System.Drawing.Imaging;

    /// <summary>
    /// A list of available color matrices to apply to an image.
    /// </summary>
    internal static class ColorMatrixes
    {
        /// <summary>
        /// The <see cref="T:System.Drawing.Imaging.ColorMatrix"/> for generating the black and white filter.
        /// </summary>
        private static ColorMatrix blackWhite;

        /// <summary>
        /// Gets the <see cref="T:System.Drawing.Imaging.ColorMatrix"/> for generating the high pass
        /// on the comic book filter.
        /// </summary>
        private static ColorMatrix comicHigh;

        /// <summary>
        /// Gets <see cref="T:System.Drawing.Imaging.ColorMatrix"/> for generating the low pass
        /// on the comic book filter.
        /// </summary>
        private static ColorMatrix comicLow;

        /// <summary>
        /// The <see cref="T:System.Drawing.Imaging.ColorMatrix"/> for generating the greyscale filter.
        /// </summary>
        private static ColorMatrix greyScale;

        /// <summary>
        /// The <see cref="T:System.Drawing.Imaging.ColorMatrix"/> for generating the high saturation filter.
        /// </summary>
        private static ColorMatrix hiSatch;

        /// <summary>
        /// The <see cref="T:System.Drawing.Imaging.ColorMatrix"/> for generating the invert filter.
        /// </summary>
        private static ColorMatrix invert;

        /// <summary>
        /// The <see cref="T:System.Drawing.Imaging.ColorMatrix"/> for generating the lomograph filter.
        /// </summary>
        private static ColorMatrix lomograph;

        /// <summary>
        /// The <see cref="T:System.Drawing.Imaging.ColorMatrix"/> for generating the low saturation filter.
        /// </summary>
        private static ColorMatrix loSatch;

        /// <summary>
        /// The <see cref="T:System.Drawing.Imaging.ColorMatrix"/> for generating the polaroid filter.
        /// </summary>
        private static ColorMatrix polaroid;

        /// <summary>
        /// The <see cref="T:System.Drawing.Imaging.ColorMatrix"/> for generating the sepia filter.
        /// </summary>
        private static ColorMatrix sepia;

        /// <summary>
        /// Gets the <see cref="T:System.Drawing.Imaging.ColorMatrix"/> for generating the black and white filter.
        /// </summary>
        internal static ColorMatrix BlackWhite
        {
            get
            {
                return blackWhite ?? (blackWhite = new ColorMatrix(
                    new[]
                            {
                                new[] { 1.5f, 1.5f, 1.5f, 0, 0 }, 
                                new[] { 1.5f, 1.5f, 1.5f, 0, 0 },
                                new[] { 1.5f, 1.5f, 1.5f, 0, 0 },
                                new float[] { 0, 0, 0, 1, 0 },
                                new float[] { -1, -1, -1, 0, 1 }
                          }));
            }
        }

        /// <summary>
        /// Gets the <see cref="T:System.Drawing.Imaging.ColorMatrix"/> for generating the high pass
        /// on the comic book filter.
        /// </summary>
        internal static ColorMatrix ComicHigh
        {
            get
            {
                return comicHigh ?? (comicHigh = new ColorMatrix(
                    new[]
                            {
                                new[] { 2, -0.5f, -0.5f, 0, 0 }, 
                                new[] { -0.5f, 2, -0.5f, 0, 0 },  
                                new[] { -0.5f, -0.5f, 2, 0, 0 }, 
                                new float[] { 0, 0, 0, 1, 0 },
                                new float[] { 0, 0, 0, 0, 1 }
                            }));
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
                return comicLow ?? (comicLow = new ColorMatrix(
                       new[]
                            {
                                new float[] { 1, 0, 0, 0, 0 }, 
                                new float[] { 0, 1, 0, 0, 0 },  
                                new float[] { 0, 0, 1, 0, 0 }, 
                                new float[] { 0, 0, 0, 1, 0 },
                                new[] { .075f, .075f, .075f, 0, 1 }
                            }));
            }
        }

        /// <summary>
        /// Gets the <see cref="T:System.Drawing.Imaging.ColorMatrix"/> for generating the greyscale filter.
        /// </summary>
        internal static ColorMatrix GreyScale
        {
            get
            {
                return greyScale ?? (greyScale = new ColorMatrix(
                    new[]
                            {
                                new[] { .33f, .33f, .33f, 0, 0 }, 
                                new[] { .59f, .59f, .59f, 0, 0 },
                                new[] { .11f, .11f, .11f, 0, 0 }, 
                                new float[] { 0, 0, 0, 1, 0 },
                                new float[] { 0, 0, 0, 0, 1 }
                            }));
            }
        }

        /// <summary>
        /// Gets the <see cref="T:System.Drawing.Imaging.ColorMatrix"/> for generating the high saturation filter.
        /// </summary>
        internal static ColorMatrix HiSatch
        {
            get
            {
                return hiSatch ?? (hiSatch = new ColorMatrix(
                    new[]
                            {
                                new float[] { 3, -1, -1, 0, 0 }, 
                                new float[] { -1, 3, -1, 0, 0 },  
                                new float[] { -1, -1, 3, 0, 0 }, 
                                new float[] { 0, 0, 0, 1, 0 },
                                new float[] { 0, 0, 0, 0, 1 }
                            }));
            }
        }

        /// <summary>
        /// Gets the <see cref="T:System.Drawing.Imaging.ColorMatrix"/> for generating the invert filter.
        /// </summary>
        internal static ColorMatrix Invert
        {
            get
            {
                return invert ?? (invert = new ColorMatrix(
                    new[]
                            {
                                new float[] { -1, 0, 0, 0, 0 }, 
                                new float[] { 0, -1, 0, 0, 0 },  
                                new float[] { 0, 0, -1, 0, 0 }, 
                                new float[] { 0, 0, 0, 1, 0 },
                                new float[] { 1, 1, 1, 0, 1 }
                            }));
            }
        }

        /// <summary>
        /// Gets the <see cref="T:System.Drawing.Imaging.ColorMatrix"/> for generating the lomograph filter.
        /// </summary>
        internal static ColorMatrix Lomograph
        {
            get
            {
                return lomograph
                       ?? (lomograph = new ColorMatrix(
                               new[]
                                   {
                                       new[] { 1.50f, 0, 0, 0, 0 }, 
                                       new[] { 0, 1.45f, 0, 0, 0 },
                                       new[] { 0, 0, 1.09f, 0, 0 }, 
                                       new float[] { 0, 0, 0, 1, 0 },
                                       new[] { -0.10f, 0.05f, -0.08f, 0, 1 }
                                   }));
            }
        }

        /// <summary>
        /// Gets the <see cref="T:System.Drawing.Imaging.ColorMatrix"/> for generating the low saturation filter.
        /// </summary>
        internal static ColorMatrix LoSatch
        {
            get
            {
                return loSatch ?? (loSatch = new ColorMatrix(
                       new[]
                            {
                                new float[] { 1, 0, 0, 0, 0 }, 
                                new float[] { 0, 1, 0, 0, 0 },  
                                new float[] { 0, 0, 1, 0, 0 }, 
                                new float[] { 0, 0, 0, 1, 0 },
                                new[] { .10f, .10f, .10f, 0, 1 }
                            }));
            }
        }

        /// <summary>
        /// Gets the <see cref="T:System.Drawing.Imaging.ColorMatrix"/> for generating the polaroid filter.
        /// </summary>
        internal static ColorMatrix Polaroid
        {
            get
            {
                return polaroid ?? (polaroid = new ColorMatrix(
                    new[]
                            {
                                new[] { 1.638f, -0.062f, -0.262f, 0, 0 },
                                new[] { -0.122f, 1.378f, -0.122f, 0, 0 },
                                new[] { 1.016f, -0.016f, 1.383f, 0, 0 },
                                new float[] { 0, 0, 0, 1, 0 },
                                new[] { 0.06f, -0.05f, -0.05f, 0, 1 }
                          }));
            }
        }

        /// <summary>
        /// Gets the <see cref="T:System.Drawing.Imaging.ColorMatrix"/> for generating the sepia filter.
        /// </summary>
        internal static ColorMatrix Sepia
        {
            get
            {
                return sepia ?? (sepia = new ColorMatrix(
                        new[]
                            {
                                new[] { .393f, .349f, .272f, 0, 0 }, 
                                new[] { .769f, .686f, .534f, 0, 0 },
                                new[] { .189f, .168f, .131f, 0, 0 }, 
                                new float[] { 0, 0, 0, 1, 0 },
                                new float[] { 0, 0, 0, 0, 1 }
                            }));
            }
        }
    }
}