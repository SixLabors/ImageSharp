// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MatrixFilters.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   The filters available to the Filter <see cref="IGraphicsProcessor" />.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Filters.Photo
{
    using ImageProcessor.Processors;

    /// <summary>
    /// The filters available to the Filter <see cref="IGraphicsProcessor"/>.
    /// </summary>
    public static class MatrixFilters
    {
        /// <summary>
        /// Gets the <see cref="IMatrixFilter"/> for generating the black and white filter.
        /// </summary>
        public static IMatrixFilter BlackWhite
        {
            get
            {
                return new BlackWhiteMatrixFilter();
            }
        }

        /// <summary>
        /// Gets the <see cref="IMatrixFilter"/> for generating the comic filter.
        /// </summary>
        public static IMatrixFilter Comic
        {
            get
            {
                return new ComicMatrixFilter();
            }
        }

        /// <summary>
        /// Gets the <see cref="IMatrixFilter"/> for generating the gotham filter.
        /// </summary>
        public static IMatrixFilter Gotham
        {
            get
            {
                return new GothamMatrixFilter();
            }
        }

        /// <summary>
        /// Gets the <see cref="IMatrixFilter"/> for generating the greyscale filter.
        /// </summary>
        public static IMatrixFilter GreyScale
        {
            get
            {
                return new GreyScaleMatrixFilter();
            }
        }

        /// <summary>
        /// Gets the <see cref="IMatrixFilter"/> for generating the high saturation filter.
        /// </summary>
        public static IMatrixFilter HiSatch
        {
            get
            {
                return new HiSatchMatrixFilter();
            }
        }

        /// <summary>
        /// Gets the <see cref="IMatrixFilter"/> for generating the invert filter.
        /// </summary>
        public static IMatrixFilter Invert
        {
            get
            {
                return new InvertMatrixFilter();
            }
        }

        /// <summary>
        /// Gets the <see cref="IMatrixFilter"/> for generating the lomograph filter.
        /// </summary>
        public static IMatrixFilter Lomograph
        {
            get
            {
                return new LomographMatrixFilter();
            }
        }

        /// <summary>
        /// Gets the <see cref="IMatrixFilter"/> for generating the low saturation filter.
        /// </summary>
        public static IMatrixFilter LoSatch
        {
            get
            {
                return new LoSatchMatrixFilter();
            }
        }

        /// <summary>
        /// Gets the <see cref="IMatrixFilter"/> for generating the polaroid filter.
        /// </summary>
        public static IMatrixFilter Polaroid
        {
            get
            {
                return new PolaroidMatrixFilter();
            }
        }

        /// <summary>
        /// Gets the <see cref="IMatrixFilter"/> for generating the sepia filter.
        /// </summary>
        public static IMatrixFilter Sepia
        {
            get
            {
                return new SepiaMatrixFilter();
            }
        }
    }
}