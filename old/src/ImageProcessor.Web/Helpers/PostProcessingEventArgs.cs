// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PostProcessingEventArgs.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   The post processing event arguments.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web.Helpers
{
    using System;

    /// <summary>
    /// The post processing event arguments.
    /// </summary>
    public class PostProcessingEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets the cached image path.
        /// </summary>
        public string CachedImagePath { get; set; }
    }
}
