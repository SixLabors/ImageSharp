// <copyright file="ProgressEventArgs.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    /// <summary>
    /// Contains event data related to the progress made processing an image.
    /// </summary>
    public class ProgressEventArgs : System.EventArgs
    {
        /// <summary>
        /// Gets or sets the number of rows processed.
        /// </summary>
        public int RowsProcessed { get; set; }

        /// <summary>
        /// Gets or sets the total number of rows.
        /// </summary>
        public int TotalRows { get; set; }
    }
}