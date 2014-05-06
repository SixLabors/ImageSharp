// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MatrixFilters.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   The filters available to the Filter <see cref="IGraphicsProcessor" />.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Filters
{
    using ImageProcessor.Processors;

    /// <summary>
    /// The filters available to the Filter <see cref="IGraphicsProcessor"/>.
    /// </summary>
    public static class MatrixFilters
    {
        /// <summary>
        /// Gets the black white filter.
        /// </summary>
        [MatrixFilterRegex("blackwhite")]
        public static IMatrixFilter BlackWhite
        {
            get
            {
                return new BlackWhiteMatrixFilter();
            }
        }

        /// <summary>
        /// Gets the comic filter.
        /// </summary>
        [MatrixFilterRegex("comic")]
        public static IMatrixFilter Comic
        {
            get
            {
                return new ComicMatrixFilter();
            }
        }

        /// <summary>
        /// Gets the gotham filter.
        /// </summary>
        [MatrixFilterRegex("gotham")]
        public static IMatrixFilter Gotham
        {
            get
            {
                return new GothamMatrixFilter();
            }
        }

        /// <summary>
        /// Gets the greyscale filter.
        /// </summary>
        [MatrixFilterRegex("greyscale")]
        public static IMatrixFilter GreyScale
        {
            get
            {
                return new GreyScaleMatrixFilter();
            }
        }

        /// <summary>
        /// Gets the high saturation filter.
        /// </summary>
        [MatrixFilterRegex("hisatch")]
        public static IMatrixFilter HiSatch
        {
            get
            {
                return new HiSatchMatrixFilter();
            }
        }

        /// <summary>
        /// Gets the invert filter.
        /// </summary>
        [MatrixFilterRegex("invert")]
        public static IMatrixFilter Invert
        {
            get
            {
                return new InvertMatrixFilter();
            }
        }

        /// <summary>
        /// Gets the lomograph filter.
        /// </summary>
        [MatrixFilterRegex("lomograph")]
        public static IMatrixFilter Lomograph
        {
            get
            {
                return new LomographMatrixFilter();
            }
        }

        /// <summary>
        /// Gets the low saturation filter.
        /// </summary>
        [MatrixFilterRegex("losatch")]
        public static IMatrixFilter LoSatch
        {
            get
            {
                return new LomographMatrixFilter();
            }
        }

        /// <summary>
        /// Gets the polaroid filter.
        /// </summary>
        [MatrixFilterRegex("polaroid")]
        public static IMatrixFilter Polaroid
        {
            get
            {
                return new PolaroidMatrixFilter();
            }
        }

        /// <summary>
        /// Gets the sepia filter.
        /// </summary>
        [MatrixFilterRegex("sepia")]
        public static IMatrixFilter Sepia
        {
            get
            {
                return new SepiaMatrixFilter();
            }
        }
    }
}
