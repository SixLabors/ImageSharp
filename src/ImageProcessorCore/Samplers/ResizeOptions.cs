// <copyright file="ResizeOptions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Samplers
{
    using System.Collections.Generic;
    using System.Linq;

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
        public AnchorPosition Position { get; set; } = AnchorPosition.Center;

        /// <summary>
        /// Gets or sets the center coordinates.
        /// </summary>
        public IEnumerable<float> CenterCoordinates { get; set; } = Enumerable.Empty<float>();

        /// <summary>
        /// Gets or sets the target size.
        /// </summary>
        public Size Size { get; set; }

        public IResampler Sampler { get; set; } = new BicubicResampler();

        public bool Compand { get; set; }
    }
}
