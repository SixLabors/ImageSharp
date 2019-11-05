// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Options for influencing the drawing functions.
    /// </summary>
    public struct GraphicsOptions
    {
        /// <summary>
        /// Represents the default <see cref="GraphicsOptions"/>.
        /// </summary>
        public static readonly GraphicsOptions Default = new GraphicsOptions(true);

        private float? blendPercentage;

        private int? antialiasSubpixelDepth;

        private bool? antialias;

        /// <summary>
        /// Initializes a new instance of the <see cref="GraphicsOptions"/> struct.
        /// </summary>
        /// <param name="enableAntialiasing">If set to <c>true</c> [enable antialiasing].</param>
        public GraphicsOptions(bool enableAntialiasing)
        {
            this.ColorBlendingMode = PixelColorBlendingMode.Normal;
            this.AlphaCompositionMode = PixelAlphaCompositionMode.SrcOver;
            this.blendPercentage = 1;
            this.antialiasSubpixelDepth = 16;
            this.antialias = enableAntialiasing;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GraphicsOptions"/> struct.
        /// </summary>
        /// <param name="enableAntialiasing">If set to <c>true</c> [enable antialiasing].</param>
        /// <param name="opacity">blending percentage to apply to the drawing operation</param>
        public GraphicsOptions(bool enableAntialiasing, float opacity)
        {
            Guard.MustBeBetweenOrEqualTo(opacity, 0, 1, nameof(opacity));

            this.ColorBlendingMode = PixelColorBlendingMode.Normal;
            this.AlphaCompositionMode = PixelAlphaCompositionMode.SrcOver;
            this.blendPercentage = opacity;
            this.antialiasSubpixelDepth = 16;
            this.antialias = enableAntialiasing;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GraphicsOptions"/> struct.
        /// </summary>
        /// <param name="enableAntialiasing">If set to <c>true</c> [enable antialiasing].</param>
        /// <param name="opacity">blending percentage to apply to the drawing operation</param>
        /// <param name="blending">color blending mode to apply to the drawing operation</param>
        public GraphicsOptions(bool enableAntialiasing, PixelColorBlendingMode blending, float opacity)
        {
            Guard.MustBeBetweenOrEqualTo(opacity, 0, 1, nameof(opacity));

            this.ColorBlendingMode = blending;
            this.AlphaCompositionMode = PixelAlphaCompositionMode.SrcOver;
            this.blendPercentage = opacity;
            this.antialiasSubpixelDepth = 16;
            this.antialias = enableAntialiasing;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GraphicsOptions"/> struct.
        /// </summary>
        /// <param name="enableAntialiasing">If set to <c>true</c> [enable antialiasing].</param>
        /// <param name="opacity">blending percentage to apply to the drawing operation</param>
        /// <param name="blending">color blending mode to apply to the drawing operation</param>
        /// <param name="composition">alpha composition mode to apply to the drawing operation</param>
        public GraphicsOptions(bool enableAntialiasing, PixelColorBlendingMode blending, PixelAlphaCompositionMode composition, float opacity)
        {
            Guard.MustBeBetweenOrEqualTo(opacity, 0, 1, nameof(opacity));

            this.ColorBlendingMode = blending;
            this.AlphaCompositionMode = composition;
            this.blendPercentage = opacity;
            this.antialiasSubpixelDepth = 16;
            this.antialias = enableAntialiasing;
        }

        /// <summary>
        /// Gets or sets a value indicating whether antialiasing should be applied.
        /// </summary>
        public bool Antialias
        {
            get => this.antialias ?? true;
            set => this.antialias = value;
        }

        /// <summary>
        /// Gets or sets a value indicating the number of subpixels to use while rendering with antialiasing enabled.
        /// </summary>
        public int AntialiasSubpixelDepth
        {
            get => this.antialiasSubpixelDepth ?? 16;
            set => this.antialiasSubpixelDepth = value;
        }

        /// <summary>
        /// Gets or sets a value indicating the blending percentage to apply to the drawing operation
        /// </summary>
        public float BlendPercentage
        {
            get => (this.blendPercentage ?? 1).Clamp(0, 1);
            set => this.blendPercentage = value;
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
