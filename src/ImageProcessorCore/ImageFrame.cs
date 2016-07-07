// <copyright file="ImageFrame.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    /// <summary>
    /// Represents a single frame in a animation.
    /// </summary>
    /// <typeparam name="TPackedVector">
    /// The packed vector containing pixel information.
    /// </typeparam>
    public class ImageFrame<TPackedVector> : ImageBase<TPackedVector>
        where TPackedVector : IPackedVector, new()
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ImageFrame{TPackedVector}"/> class. 
        /// </summary>
        public ImageFrame()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageFrame{TPackedVector}"/> class. 
        /// </summary>
        /// <param name="frame">
        /// The frame to create the frame from.
        /// </param>
        public ImageFrame(ImageFrame<TPackedVector> frame)
            : base(frame)
        {
        }

        /// <inheritdoc />
        public override IPixelAccessor Lock()
        {
            return Bootstrapper.Instance.GetPixelAccessor<TPackedVector>(this);
        }
    }
}
