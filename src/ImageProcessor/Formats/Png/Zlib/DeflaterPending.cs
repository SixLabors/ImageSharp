// <copyright file="DeflaterPending.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Formats
{
    /// <summary>
    /// This class stores the pending output of the Deflater.
    ///
    /// author of the original java version : Jochen Hoenicke
    /// </summary>
    public class DeflaterPending : PendingBuffer
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DeflaterPending"/> class.
        /// Construct instance with default buffer size
        /// </summary>
        public DeflaterPending()
            : base(DeflaterConstants.PendingBufSize)
        {
        }
    }
}
