// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Options for influencing the drawing functions.
    /// </summary>
    public class GraphicsOptions : IDeepCloneable<GraphicsOptions>
    {
        private int antialiasSubpixelDepth = 16;
        private float blendPercentage = 1F;

        /// <summary>
        /// Initializes a new instance of the <see cref="GraphicsOptions"/> class.
        /// </summary>
        public GraphicsOptions()
        {
        }

        private GraphicsOptions(GraphicsOptions source)
        {
            this.AlphaCompositionMode = source.AlphaCompositionMode;
            this.Antialias = source.Antialias;
            this.AntialiasSubpixelDepth = source.AntialiasSubpixelDepth;
            this.BlendPercentage = source.BlendPercentage;
            this.ColorBlendingMode = source.ColorBlendingMode;
        }

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

        /// <inheritdoc/>
        public GraphicsOptions DeepClone() => new GraphicsOptions(this);
    }
}
