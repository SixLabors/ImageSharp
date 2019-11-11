// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.Fonts;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Options for influencing the drawing functions.
    /// </summary>
    public class TextGraphicsOptions : IDeepCloneable<TextGraphicsOptions>
    {
        private int antialiasSubpixelDepth = 16;
        private float blendPercentage = 1F;
        private float tabWidth = 4F;
        private float dpiX = 72F;
        private float dpiY = 72F;

        /// <summary>
        /// Initializes a new instance of the <see cref="TextGraphicsOptions"/> class.
        /// </summary>
        public TextGraphicsOptions()
        {
        }

        private TextGraphicsOptions(TextGraphicsOptions source)
        {
            this.AlphaCompositionMode = source.AlphaCompositionMode;
            this.Antialias = source.Antialias;
            this.AntialiasSubpixelDepth = source.AntialiasSubpixelDepth;
            this.ApplyKerning = source.ApplyKerning;
            this.BlendPercentage = source.BlendPercentage;
            this.ColorBlendingMode = source.ColorBlendingMode;
            this.DpiX = source.DpiX;
            this.DpiY = source.DpiY;
            this.HorizontalAlignment = source.HorizontalAlignment;
            this.TabWidth = source.TabWidth;
            this.WrapTextWidth = source.WrapTextWidth;
            this.VerticalAlignment = source.VerticalAlignment;
        }

        /// <summary>
        /// Gets the default <see cref="TextGraphicsOptions"/> instance.
        /// </summary>
        public static TextGraphicsOptions Default { get; } = new TextGraphicsOptions();

        /// <summary>
        /// Gets or sets a value indicating whether antialiasing should be applied.
        /// Defaults to true.
        /// </summary>
        public bool Antialias { get; set; } = true;

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

        /// <summary>
        /// Gets or sets a value indicating the color blending percentage to apply to the drawing operation.
        /// Defaults to <see cref= "PixelColorBlendingMode.Normal" />.
        /// </summary>
        public PixelColorBlendingMode ColorBlendingMode { get; set; } = PixelColorBlendingMode.Normal;

        /// <summary>
        /// Gets or sets a value indicating the color blending percentage to apply to the drawing operation
        /// Defaults to <see cref= "PixelAlphaCompositionMode.SrcOver" />.
        /// </summary>
        public PixelAlphaCompositionMode AlphaCompositionMode { get; set; } = PixelAlphaCompositionMode.SrcOver;

        /// <summary>
        /// Gets or sets a value indicating whether the text should be drawing with kerning enabled.
        /// Defaults to true;
        /// </summary>
        public bool ApplyKerning { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating the number of space widths a tab should lock to.
        /// Defaults to 4.
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
        /// Defaults to 0.
        /// </summary>
        public float WrapTextWidth { get; set; }

        /// <summary>
        /// Gets or sets a value indicating the DPI (Dots Per Inch) to render text along the X axis.
        /// Defaults to 72.
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
        /// Gets or sets a value indicating the DPI (Dots Per Inch) to render text along the Y axis.
        /// Defaults to 72.
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
        /// Defaults to <see cref="HorizontalAlignment.Left"/>.
        /// </summary>
        public HorizontalAlignment HorizontalAlignment { get; set; } = HorizontalAlignment.Left;

        /// <summary>
        /// Gets or sets a value indicating how to align the text relative to the rendering space.
        /// Defaults to <see cref="VerticalAlignment.Top"/>.
        /// </summary>
        public VerticalAlignment VerticalAlignment { get; set; } = VerticalAlignment.Top;

        /// <summary>
        /// Performs an implicit conversion from <see cref="GraphicsOptions"/> to <see cref="TextGraphicsOptions"/>.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <returns>
        /// The result of the conversion.
        /// </returns>
        public static implicit operator TextGraphicsOptions(GraphicsOptions options)
        {
            return new TextGraphicsOptions()
            {
                Antialias = options.Antialias,
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
            return new GraphicsOptions()
            {
                Antialias = options.Antialias,
                AntialiasSubpixelDepth = options.AntialiasSubpixelDepth,
                ColorBlendingMode = options.ColorBlendingMode,
                AlphaCompositionMode = options.AlphaCompositionMode,
                BlendPercentage = options.BlendPercentage
            };
        }

        /// <inheritdoc/>
        public TextGraphicsOptions DeepClone() => new TextGraphicsOptions(this);
    }
}
