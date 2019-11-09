// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.Fonts;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Options for influencing the drawing functions.
    /// </summary>
    public class TextGraphicsOptions
    {
        private static readonly Lazy<TextGraphicsOptions> Lazy = new Lazy<TextGraphicsOptions>();
        private int antialiasSubpixelDepth;
        private float blendPercentage;
        private float tabWidth;
        private float dpiX;
        private float dpiY;

        /// <summary>
        /// Initializes a new instance of the <see cref="TextGraphicsOptions"/> class.
        /// </summary>
        public TextGraphicsOptions()
            : this(true)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TextGraphicsOptions" /> class.
        /// </summary>
        /// <param name="enableAntialiasing">If set to <c>true</c> [enable antialiasing].</param>
        public TextGraphicsOptions(bool enableAntialiasing)
        {
            this.ApplyKerning = true;
            this.TabWidth = 4F;
            this.WrapTextWidth = 0;
            this.HorizontalAlignment = HorizontalAlignment.Left;
            this.VerticalAlignment = VerticalAlignment.Top;

            this.antialiasSubpixelDepth = 16;
            this.ColorBlendingMode = PixelColorBlendingMode.Normal;
            this.AlphaCompositionMode = PixelAlphaCompositionMode.SrcOver;
            this.blendPercentage = 1F;
            this.Antialias = enableAntialiasing;
            this.dpiX = 72F;
            this.dpiY = 72F;
        }

        /// <summary>
        /// Gets the default <see cref="TextGraphicsOptions"/> instance.
        /// </summary>
        public static TextGraphicsOptions Default { get; } = Lazy.Value;

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
        /// Gets or sets a value indicating the color blending percentage to apply to the drawing operation
        /// </summary>
        public PixelColorBlendingMode ColorBlendingMode { get; set; }

        /// <summary>
        /// Gets or sets a value indicating the color blending percentage to apply to the drawing operation
        /// </summary>
        public PixelAlphaCompositionMode AlphaCompositionMode { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the text should be drawing with kerning enabled.
        /// </summary>
        public bool ApplyKerning { get; set; }

        /// <summary>
        /// Gets or sets a value indicating the number of space widths a tab should lock to.
        /// </summary>
        public float TabWidth
        {
            get
            {
                return this.tabWidth;
            }

            set
            {
                Guard.MustBeGreaterThanOrEqualTo(value, 0, nameof(this.TabWidth));
                this.tabWidth = value;
            }
        }

        /// <summary>
        /// Gets or sets a value, if greater than 0, indicating the width at which text should wrap.
        /// </summary>
        public float WrapTextWidth { get; set; }

        /// <summary>
        /// Gets or sets a value indicating the DPI to render text along the X axis.
        /// </summary>
        public float DpiX
        {
            get
            {
                return this.dpiX;
            }

            set
            {
                Guard.MustBeGreaterThanOrEqualTo(value, 0, nameof(this.DpiX));
                this.dpiX = value;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating the DPI to render text along the Y axis.
        /// </summary>
        public float DpiY
        {
            get
            {
                return this.dpiY;
            }

            set
            {
                Guard.MustBeGreaterThanOrEqualTo(value, 0, nameof(this.DpiY));
                this.dpiY = value;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating how to align the text relative to the rendering space.
        /// If <see cref="WrapTextWidth"/> is greater than zero it will align relative to the space
        /// defined by the location and width, if <see cref="WrapTextWidth"/> equals zero, and thus
        /// wrapping disabled, then the alignment is relative to the drawing location.
        /// </summary>
        public HorizontalAlignment HorizontalAlignment { get; set; }

        /// <summary>
        /// Gets or sets a value indicating how to align the text relative to the rendering space.
        /// </summary>
        public VerticalAlignment VerticalAlignment { get; set; }

        /// <summary>
        /// Performs an implicit conversion from <see cref="GraphicsOptions"/> to <see cref="TextGraphicsOptions"/>.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <returns>
        /// The result of the conversion.
        /// </returns>
        public static implicit operator TextGraphicsOptions(GraphicsOptions options)
        {
            return new TextGraphicsOptions(options.Antialias)
            {
                AntialiasSubpixelDepth = options.AntialiasSubpixelDepth,
                blendPercentage = options.BlendPercentage,
                ColorBlendingMode = options.ColorBlendingMode,
                AlphaCompositionMode = options.AlphaCompositionMode
            };
        }

        /// <summary>
        /// Performs an explicit conversion from <see cref="TextGraphicsOptions"/> to <see cref="GraphicsOptions"/>.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <returns>
        /// The result of the conversion.
        /// </returns>
        public static explicit operator GraphicsOptions(TextGraphicsOptions options)
        {
            return new GraphicsOptions(options.Antialias)
            {
                AntialiasSubpixelDepth = options.AntialiasSubpixelDepth,
                ColorBlendingMode = options.ColorBlendingMode,
                AlphaCompositionMode = options.AlphaCompositionMode,
                BlendPercentage = options.BlendPercentage
            };
        }
    }
}
