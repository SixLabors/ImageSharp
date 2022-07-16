// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Processing.Processors.Transforms;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// The resize options for resizing images against certain modes.
    /// </summary>
    public class ResizeOptions
    {
        /// <summary>
        /// Gets or sets the resize mode.
        /// </summary>
        public ResizeMode Mode { get; set; } = ResizeMode.Crop;

        /// <summary>
        /// Gets or sets the anchor position.
        /// </summary>
        public AnchorPositionMode Position { get; set; } = AnchorPositionMode.Center;

        /// <summary>
        /// Gets or sets the center coordinates.
        /// </summary>
        public PointF? CenterCoordinates { get; set; }

        /// <summary>
        /// Gets or sets the target size.
        /// </summary>
        public Size Size { get; set; }

        /// <summary>
        /// Gets or sets the sampler to perform the resize operation.
        /// </summary>
        public IResampler Sampler { get; set; } = KnownResamplers.Bicubic;

        /// <summary>
        /// Gets or sets a value indicating whether to compress
        /// or expand individual pixel colors the value on processing.
        /// </summary>
        public bool Compand { get; set; } = false;

        /// <summary>
        /// Gets or sets the target rectangle to resize into.
        /// </summary>
        public Rectangle? TargetRectangle { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to premultiply
        /// the alpha (if it exists) during the resize operation.
        /// </summary>
        public bool PremultiplyAlpha { get; set; } = true;

        /// <summary>
        /// Gets or sets the color to use as a background when padding an image.
        /// </summary>
        public Color PadColor { get; set; }
    }
}
