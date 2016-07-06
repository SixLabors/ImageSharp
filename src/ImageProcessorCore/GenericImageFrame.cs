// <copyright file="GenericImageFrame.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    using System;

    /// <summary>
    /// Represents a single frame in a animation.
    /// </summary>
    /// <typeparam name="TPacked">
    /// The packed vector pixels format.
    /// </typeparam>
    public abstract class GenericImageFrame<TPacked> : ImageBase<TPacked>, IImageFrame<TPacked>
        where TPacked : IPackedVector
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GenericImageFrame{TPacked}"/> class.
        /// </summary>
        protected GenericImageFrame()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GenericImageFrame{TPacked}"/> class.
        /// </summary>
        /// <param name="other">
        /// The other <see cref="GenericImageFrame{TPacked}"/> to create this instance from.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the given <see cref="GenericImageFrame{TPacked}"/> is null.
        /// </exception>
        protected GenericImageFrame(GenericImageFrame<TPacked> other)
            : base(other)
        {
        }
    }
}
