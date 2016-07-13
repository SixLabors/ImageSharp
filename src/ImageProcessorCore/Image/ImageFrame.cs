// <copyright file="ImageFrame.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    /// <summary>
    /// Represents a single frame in a animation.
    /// </summary>
    /// <typeparam name="T">
    /// The packed vector containing pixel information.
    /// </typeparam>
    public class ImageFrame<T, TP> : ImageBase<T, TP>
        where T : IPackedVector<TP>, new()
        where TP : struct
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ImageFrame{T}"/> class. 
        /// </summary>
        public ImageFrame()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageFrame{T}"/> class. 
        /// </summary>
        /// <param name="frame">
        /// The frame to create the frame from.
        /// </param>
        public ImageFrame(ImageFrame<T, TP> frame)
            : base(frame)
        {
        }

        /// <inheritdoc />
        public override IPixelAccessor<T, TP> Lock()
        {
            return Bootstrapper.Instance.GetPixelAccessor<T, TP>(this);
        }
    }
}
