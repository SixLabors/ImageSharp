// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.Fonts;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Options for influencing the drawing functions.
    /// </summary>
    public struct TextGraphicsOptions
    {
        private const int DefaultTextDpi = 72;

        /// <summary>
        /// Represents the default <see cref="TextGraphicsOptions"/>.
        /// </summary>
        public static readonly TextGraphicsOptions Default = new TextGraphicsOptions(true);

        private float? blendPercentage;

        private int? antialiasSubpixelDepth;

        private bool? antialias;

        private bool? applyKerning;

        private float? tabWidth;

        private float? dpiX;

        private float? dpiY;

        private PixelColorBlendingMode colorBlendingMode;

        private PixelAlphaCompositionMode alphaCompositionMode;

        private float wrapTextWidth;

        private HorizontalAlignment? horizontalAlignment;

        private VerticalAlignment? verticalAlignment;

        /// <summary>
        /// Initializes a new instance of the <see cref="TextGraphicsOptions" /> struct.
        /// </summary>
        /// <param name="enableAntialiasing">If set to <c>true</c> [enable antialiasing].</param>
        public TextGraphicsOptions(bool enableAntialiasing)
        {
            this.applyKerning = true;
            this.tabWidth = 4;
            this.wrapTextWidth = 0;
            this.horizontalAlignment = HorizontalAlignment.Left;
            this.verticalAlignment = VerticalAlignment.Top;

            this.antialiasSubpixelDepth = 16;
            this.colorBlendingMode = PixelColorBlendingMode.Normal;
            this.alphaCompositionMode = PixelAlphaCompositionMode.SrcOver;
            this.blendPercentage = 1;
            this.antialias = enableAntialiasing;
            this.dpiX = DefaultTextDpi;
            this.dpiY = DefaultTextDpi;
        }

        /// <summary>
        /// Gets or sets a value indicating whether antialiasing should be applied.
        /// </summary>
        public bool Antialias { get => this.antialias ?? true; set => this.antialias = value; }

        /// <summary>
        /// Gets or sets a value indicating the number of subpixels to use while rendering with antialiasing enabled.
        /// </summary>
        public int AntialiasSubpixelDepth { get => this.antialiasSubpixelDepth ?? 16; set => this.antialiasSubpixelDepth = value; }

        /// <summary>
        /// Gets or sets a value indicating the blending percentage to apply to the drawing operation
        /// </summary>
        public float BlendPercentage { get => (this.blendPercentage ?? 1).Clamp(0, 1); set => this.blendPercentage = value; }

        // In the future we could expose a PixelBlender<TPixel> directly on here
        // or some forms of PixelBlender factory for each pixel type. Will need
        // some API thought post V1.

        /// <summary>
        /// Gets or sets a value indicating the color blending percentage to apply to the drawing operation
        /// </summary>
        public PixelColorBlendingMode ColorBlendingMode { get => this.colorBlendingMode; set => this.colorBlendingMode = value; }

        /// <summary>
        /// Gets or sets a value indicating the color blending percentage to apply to the drawing operation
        /// </summary>
        public PixelAlphaCompositionMode AlphaCompositionMode { get => this.alphaCompositionMode; set => this.alphaCompositionMode = value; }

        /// <summary>
        /// Gets or sets a value indicating whether the text should be drawing with kerning enabled.
        /// </summary>
        public bool ApplyKerning { get => this.applyKerning ?? true; set => this.applyKerning = value; }

        /// <summary>
        /// Gets or sets a value indicating the number of space widths a tab should lock to.
        /// </summary>
        public float TabWidth { get => this.tabWidth ?? 4; set => this.tabWidth = value; }

        /// <summary>
        /// Gets or sets a value indicating if greater than zero determine the width at which text should wrap.
        /// </summary>
        public float WrapTextWidth { get => this.wrapTextWidth; set => this.wrapTextWidth = value; }

        /// <summary>
        /// Gets or sets a value indicating the DPI to render text along the X axis.
        /// </summary>
        public float DpiX { get => this.dpiX ?? DefaultTextDpi; set => this.dpiX = value; }

        /// <summary>
        /// Gets or sets a value indicating the DPI to render text along the Y axis.
        /// </summary>
        public float DpiY { get => this.dpiY ?? DefaultTextDpi; set => this.dpiY = value; }

        /// <summary>
        /// Gets or sets a value indicating how to align the text relative to the rendering space.
        /// If <see cref="WrapTextWidth"/> is greater than zero it will align relative to the space
        /// defined by the location and width, if <see cref="WrapTextWidth"/> equals zero, and thus
        /// wrapping disabled, then the alignment is relative to the drawing location.
        /// </summary>
        public HorizontalAlignment HorizontalAlignment { get => this.horizontalAlignment ?? HorizontalAlignment.Left; set => this.horizontalAlignment = value; }

        /// <summary>
        /// Gets or sets a value indicating how to align the text relative to the rendering space.
        /// </summary>
        public VerticalAlignment VerticalAlignment { get => this.verticalAlignment ?? VerticalAlignment.Top; set => this.verticalAlignment = value; }

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
                colorBlendingMode = options.ColorBlendingMode,
                alphaCompositionMode = options.AlphaCompositionMode
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