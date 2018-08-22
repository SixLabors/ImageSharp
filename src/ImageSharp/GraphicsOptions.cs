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

        private PixelColorBlendingMode colorBlendingMode;

        private PixelAlphaCompositionMode alphaCompositionMode;

        /// <summary>
        /// Initializes a new instance of the <see cref="GraphicsOptions"/> struct.
        /// </summary>
        /// <param name="enableAntialiasing">If set to <c>true</c> [enable antialiasing].</param>
        public GraphicsOptions(bool enableAntialiasing)
        {
            this.colorBlendingMode = PixelColorBlendingMode.Normal;
            this.alphaCompositionMode = PixelAlphaCompositionMode.SrcOver;
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

            this.colorBlendingMode = PixelColorBlendingMode.Normal;
            this.alphaCompositionMode = PixelAlphaCompositionMode.SrcOver;
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

            this.colorBlendingMode = blending;
            this.alphaCompositionMode = PixelAlphaCompositionMode.SrcOver;
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

            this.colorBlendingMode = blending;
            this.alphaCompositionMode = composition;
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
        public PixelColorBlendingMode ColorBlendingMode
        {
            get => this.colorBlendingMode;
            set => this.colorBlendingMode = value;
        }

        /// <summary>
        /// Gets or sets a value indicating the alpha composition mode to apply to the drawing operation
        /// </summary>
        public PixelAlphaCompositionMode AlphaCompositionMode
        {
            get => this.alphaCompositionMode;
            set => this.alphaCompositionMode = value;
        }

        /// <summary>
        /// Evaluates if a given SOURCE color can completely replace a BACKDROP color given the current blending and composition settings.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format</typeparam>
        /// <param name="color">the color</param>
        /// <returns>true if the color can be considered opaque</returns>
        /// <remarks>
        /// Blending and composition is an expensive operation, in some cases, like
        /// filling with a solid color, the blending can be avoided by a plain color replacement.
        /// This method can be useful for such processors to select the fast path.
        /// </remarks>
        internal bool IsOpaqueColorWithoutBlending<TPixel>(TPixel color)
            where TPixel : struct, IPixel<TPixel>
        {
            if (this.ColorBlendingMode != PixelColorBlendingMode.Normal)
            {
                return false;
            }

            if (this.AlphaCompositionMode != PixelAlphaCompositionMode.SrcOver &&
                this.AlphaCompositionMode != PixelAlphaCompositionMode.Src)
            {
                return false;
            }

            if (this.BlendPercentage != 1f)
            {
                return false;
            }

            if (color.ToVector4().W != 1f)
            {
                return false;
            }

            return true;
        }
    }
}