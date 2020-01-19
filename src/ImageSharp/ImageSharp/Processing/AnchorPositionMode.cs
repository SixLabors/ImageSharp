// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Enumerated anchor positions to apply to resized images.
    /// </summary>
    public enum AnchorPositionMode
    {
        /// <summary>
        /// Anchors the position of the image to the center of it's bounding container.
        /// </summary>
        Center,

        /// <summary>
        /// Anchors the position of the image to the top of it's bounding container.
        /// </summary>
        Top,

        /// <summary>
        /// Anchors the position of the image to the bottom of it's bounding container.
        /// </summary>
        Bottom,

        /// <summary>
        /// Anchors the position of the image to the left of it's bounding container.
        /// </summary>
        Left,

        /// <summary>
        /// Anchors the position of the image to the right of it's bounding container.
        /// </summary>
        Right,

        /// <summary>
        /// Anchors the position of the image to the top left side of it's bounding container.
        /// </summary>
        TopLeft,

        /// <summary>
        /// Anchors the position of the image to the top right side of it's bounding container.
        /// </summary>
        TopRight,

        /// <summary>
        /// Anchors the position of the image to the bottom right side of it's bounding container.
        /// </summary>
        BottomRight,

        /// <summary>
        /// Anchors the position of the image to the bottom left side of it's bounding container.
        /// </summary>
        BottomLeft
    }
}
