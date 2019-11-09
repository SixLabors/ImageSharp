// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Options for influencing the drawing functions.
    /// </summary>
    public class GraphicsOptions
    {
        private static readonly Lazy<GraphicsOptions> Lazy = new Lazy<GraphicsOptions>();
        private int antialiasSubpixelDepth;
        private float blendPercentage;

        /// <summary>
        /// Initializes a new instance of the <see cref="GraphicsOptions"/> class.
        /// </summary>
        public GraphicsOptions()
            : this(true)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GraphicsOptions"/> class.
        /// </summary>
        /// <param name="enableAntialiasing">If set to <c>true</c> [enable antialiasing].</param>
        public GraphicsOptions(bool enableAntialiasing)
            : this(enableAntialiasing, 1F)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GraphicsOptions"/> class.
        /// </summary>
        /// <param name="enableAntialiasing">If set to <c>true</c> [enable antialiasing].</param>
        /// <param name="blendPercentage">The blending percentage to apply to the drawing operation</param>
        public GraphicsOptions(bool enableAntialiasing, float blendPercentage)
            : this(enableAntialiasing, PixelColorBlendingMode.Normal, blendPercentage)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GraphicsOptions"/> class.
        /// </summary>
        /// <param name="enableAntialiasing">If set to <c>true</c> [enable antialiasing].</param>
        /// <param name="blending">The color blending mode to apply to the drawing operation</param>
        /// <param name="blendPercentage">The blending percentage to apply to the drawing operation</param>
        public GraphicsOptions(
            bool enableAntialiasing,
            PixelColorBlendingMode blending,
            float blendPercentage)
            : this(enableAntialiasing, blending, PixelAlphaCompositionMode.SrcOver, blendPercentage)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GraphicsOptions"/> class.
        /// </summary>
        /// <param name="enableAntialiasing">If set to <c>true</c> [enable antialiasing].</param>
        /// <param name="blending">The color blending mode to apply to the drawing operation</param>
        /// <param name="composition">The alpha composition mode to apply to the drawing operation</param>
        /// <param name="blendPercentage">The blending percentage to apply to the drawing operation</param>
        public GraphicsOptions(
            bool enableAntialiasing,
            PixelColorBlendingMode blending,
            PixelAlphaCompositionMode composition,
            float blendPercentage)
        {
            this.ColorBlendingMode = blending;
            this.AlphaCompositionMode = composition;
            this.BlendPercentage = blendPercentage;
            this.AntialiasSubpixelDepth = 16;
            this.Antialias = enableAntialiasing;
        }

        /// <summary>
        /// Gets the default <see cref="GraphicsOptions"/> instance.
        /// </summary>
        public static GraphicsOptions Default { get; } = Lazy.Value;

        /// <summary>
        /// Gets or sets a value indicating whether antialiasing should be applied.
        /// </summary>
        public bool Antialias { get; set; }

        /// <summary>
        /// Gets or sets a value indicating the number of subpixels to use while rendering with antialiasing enabled.
        /// </summary>
        public int AntialiasSubpixelDepth
        {
            get
            {
                return this.antialiasSubpixelDepth;
            }

            set
            {
                Guard.MustBeGreaterThanOrEqualTo(value, 0, nameof(this.AntialiasSubpixelDepth));
                this.antialiasSubpixelDepth = value;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating the blending percentage to apply to the drawing operation.
        /// </summary>
        public float BlendPercentage
        {
            get
            {
                return this.blendPercentage;
            }

            set
            {
                Guard.MustBeBetweenOrEqualTo(value, 0, 1F, nameof(this.BlendPercentage));
                this.blendPercentage = value;
            }
        }

        // In the future we could expose a PixelBlender<TPixel> directly on here
        // or some forms of PixelBlender factory for each pixel type. Will need
        // some API thought post V1.

        /// <summary>
        /// Gets or sets a value indicating the color blending mode to apply to the drawing operation
        /// </summary>
        public PixelColorBlendingMode ColorBlendingMode { get; set; }

        /// <summary>
        /// Gets or sets a value indicating the alpha composition mode to apply to the drawing operation
        /// </summary>
        public PixelAlphaCompositionMode AlphaCompositionMode { get; set; }
    }
}
