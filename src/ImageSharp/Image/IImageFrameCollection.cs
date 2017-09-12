// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections;
using System.Collections.Generic;

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Encapsulates an imaged collection of frames.
    /// </summary>
    /// <typeparam name="TPixel">The type of the pixel.</typeparam>
    public interface IImageFrameCollection<TPixel> : IEnumerable<ImageFrame<TPixel>>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Gets the count.
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Gets the root frame.
        /// </summary>
        ImageFrame<TPixel> RootFrame { get; }

        /// <summary>
        /// Gets or sets the <see cref="ImageFrame{TPixel}"/> at the specified index.
        /// </summary>
        /// <value>
        /// The <see cref="ImageFrame{TPixel}"/>.
        /// </value>
        /// <param name="index">The index.</param>
        /// <returns>The <see cref="ImageFrame{TPixel}"/> at the specified index.</returns>
        ImageFrame<TPixel> this[int index] { get; set; }

        /// <summary>
        ///  Determines the index of a specific <paramref name="frame"/> in the <seealso cref="Image{TPixel}"/>.
        /// </summary>
        /// <param name="frame">The <seealso cref="ImageFrame{TPixel}"/> to locate in the <seealso cref="Image{TPixel}"/>.</param>
        /// <returns>The index of item if found in the list; otherwise, -1.</returns>
        int IndexOf(ImageFrame<TPixel> frame);

        /// <summary>
        ///  Inserts the <paramref name="frame"/> to the <seealso cref="Image{TPixel}"/> at the specified <paramref name="index"/>.
        /// </summary>
        /// <param name="index"> The zero-based index at which item should be inserted..</param>
        /// <param name="frame">The <seealso cref="ImageFrame{TPixel}"/> to insert into the <seealso cref="Image{TPixel}"/>.</param>
        void Insert(int index, ImageFrame<TPixel> frame);

        /// <summary>
        /// Removes the <seealso cref="ImageFrame{TPixel}"/> from the <seealso cref="Image{TPixel}"/> at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index of the item to remove.</param>
        /// <exception cref="InvalidOperationException">Cannot remove last frame.</exception>
        void RemoveAt(int index);

        /// <summary>
        /// Adds the specified frame.
        /// </summary>
        /// <param name="frame">The frame.</param>
        /// <exception cref="ArgumentException">Frame must have the same dimensions as the image - frame</exception>
        void Add(ImageFrame<TPixel> frame);

        /// <summary>
        /// Determines whether the <seealso cref="Image{TPixel}"/> contains the <paramref name="frame"/>.
        /// </summary>
        /// <param name="frame">The frame.</param>
        /// <returns>
        ///   <c>true</c> if the <seealso cref="Image{TPixel}"/> the specified frame; otherwise, <c>false</c>.
        /// </returns>
        bool Contains(ImageFrame<TPixel> frame);

        /// <summary>
        /// Removes the specified frame.
        /// </summary>
        /// <param name="frame">The frame.</param>
        /// <returns>true if item is found in the <seealso cref="Image{TPixel}"/>; otherwise,</returns>
        /// <exception cref="InvalidOperationException">Cannot remove last frame</exception>
        bool Remove(ImageFrame<TPixel> frame);
    }
}