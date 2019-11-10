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
        private int antialiasSubpixelDepth = 16;
        private float blendPercentage = 1F;

        /// <summary>
        /// Gets the default <see cref="GraphicsOptions"/> instance.
        /// </summary>
        public static GraphicsOptions Default { get; } = new GraphicsOptions();

        /// <summary>
        /// Gets or sets a value indicating whether antialiasing should be applied.
        /// Defaults to true.
        /// </summary>
        public bool Antialias { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating the number of subpixels to use while rendering with antialiasing enabled.
        /// Defaults to 16.
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
        /// Gets or sets a value between indicating the blending percentage to apply to the drawing operation.
        /// Range 0..1; Defaults to 1.
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

        /// <summary>
        /// Gets or sets a value indicating the color blending mode to apply to the drawing operation.
        /// Defaults to <see cref="PixelColorBlendingMode.Normal"/>.
        /// </summary>
        public PixelColorBlendingMode ColorBlendingMode { get; set; } = PixelColorBlendingMode.Normal;

        /// <summary>
        /// Gets or sets a value indicating the alpha composition mode to apply to the drawing operation
        /// Defaults to <see cref="PixelAlphaCompositionMode.SrcOver"/>.
        /// </summary>
        public PixelAlphaCompositionMode AlphaCompositionMode { get; set; } = PixelAlphaCompositionMode.SrcOver;

        /// <summary>
        /// Creates a shallow copy of the <see cref="GraphicsOptions"/>.
        /// </summary>
        /// <returns>A new options instance.</returns>
        public GraphicsOptions Clone()
        {
            return new GraphicsOptions
            {
                AlphaCompositionMode = this.AlphaCompositionMode,
                Antialias = this.Antialias,
                AntialiasSubpixelDepth = this.AntialiasSubpixelDepth,
                BlendPercentage = this.BlendPercentage,
                ColorBlendingMode = this.ColorBlendingMode
            };
        }
    }
}
