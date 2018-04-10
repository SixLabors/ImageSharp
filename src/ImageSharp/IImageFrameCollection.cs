// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
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
        /// Gets the <see cref="ImageFrame{TPixel}"/> at the specified index.
        /// </summary>
        /// <value>
        /// The <see cref="ImageFrame{TPixel}"/>.
        /// </value>
        /// <param name="index">The index.</param>
        /// <returns>The <see cref="ImageFrame{TPixel}"/> at the specified index.</returns>
        ImageFrame<TPixel> this[int index] { get; }

        /// <summary>
        /// Clones the the frame at <paramref name="index"/> and generates a new images with all the same metadata from the orgional but with only the single frame on it.
        /// </summary>
        /// <param name="index"> The zero-based index at which item should be removed.</param>
        /// <exception cref="InvalidOperationException">Cannot remove last frame.</exception>
        /// <returns>The new <see cref="Image{TPixel}"/> with only the one frame on it.</returns>
        Image<TPixel> CloneFrame(int index);

        /// <summary>
        /// Removed the frame at <paramref name="index"/> and generates a new images with all the same metadata from the orgional but with only the single frame on it.
        /// </summary>
        /// <param name="index"> The zero-based index at which item should be removed.</param>
        /// <exception cref="InvalidOperationException">Cannot remove last frame.</exception>
        /// <returns>The new <see cref="Image{TPixel}"/> with only the one frame on it.</returns>
        Image<TPixel> ExportFrame(int index);

        /// <summary>
        /// Remove the frame at <paramref name="index"/> and frees all freeable resources associated with it.
        /// </summary>
        /// <param name="index"> The zero-based index at which item should be removed.</param>
        /// <exception cref="InvalidOperationException">Cannot remove last frame.</exception>
        void RemoveFrame(int index);

        /// <summary>
        /// Creates a new <seealso cref="ImageFrame{TPixel}"/> and appends it appends it to the end of the collection.
        /// </summary>
        /// <returns>The new <see cref="ImageFrame{TPixel}"/>.</returns>
        ImageFrame<TPixel> CreateFrame();

        /// <summary>
        /// Clones the <paramref name="source"/> frame and appends the clone to the end of the collection.
        /// </summary>
        /// <param name="source">The raw pixel data to generate <seealso cref="ImageFrame{TPixel}"/> from.</param>
        /// <returns>The cloned <see cref="ImageFrame{TPixel}"/>.</returns>
        ImageFrame<TPixel> AddFrame(ImageFrame<TPixel> source);

        /// <summary>
        /// Creates a new frame from the pixel data at the same dimensions at the current image and inserts the new frame
        /// into the <seealso cref="Image{TPixel}"/> at the end of the collection.
        /// </summary>
        /// <param name="source">The raw pixel data to generate <seealso cref="ImageFrame{TPixel}"/> from.</param>
        /// <returns>The new <see cref="ImageFrame{TPixel}"/>.</returns>
        ImageFrame<TPixel> AddFrame(TPixel[] source);

        /// <summary>
        /// Clones and inserts the <paramref name="source"/> into the <seealso cref="Image{TPixel}"/> at the specified <paramref name="index"/>.
        /// </summary>
        /// <param name="index"> The zero-based index at which item should be inserted.</param>
        /// <param name="source">The <seealso cref="ImageFrame{TPixel}"/> to clone and insert into the <seealso cref="Image{TPixel}"/>.</param>
        /// <exception cref="ArgumentException">Frame must have the same dimensions as the image - frame</exception>
        /// <returns>The cloned <see cref="ImageFrame{TPixel}"/>.</returns>
        ImageFrame<TPixel> InsertFrame(int index, ImageFrame<TPixel> source);

        /// <summary>
        /// Moves a <seealso cref="ImageFrame{TPixel}"/> from the <seealso cref="Image{TPixel}"/> at the specified index to the other index.
        /// </summary>
        /// <param name="sourceIndex">The zero-based index of the item to move.</param>
        /// <param name="destinationIndex">The zero-based index of the new index that should be inserted at.</param>
        /// <exception cref="InvalidOperationException">Cannot remove last frame.</exception>
        void MoveFrame(int sourceIndex, int destinationIndex);

        /// <summary>
        ///  Determines the index of a specific <paramref name="frame"/> in the <seealso cref="Image{TPixel}"/>.
        /// </summary>
        /// <param name="frame">The <seealso cref="ImageFrame{TPixel}"/> to locate in the <seealso cref="Image{TPixel}"/>.</param>
        /// <returns>The index of item if found in the list; otherwise, -1.</returns>
        int IndexOf(ImageFrame<TPixel> frame);

        /// <summary>
        /// Determines whether the <seealso cref="Image{TPixel}"/> contains the <paramref name="frame"/>.
        /// </summary>
        /// <param name="frame">The frame.</param>
        /// <returns>
        ///   <c>true</c> if the <seealso cref="Image{TPixel}"/> the specified frame; otherwise, <c>false</c>.
        /// </returns>
        bool Contains(ImageFrame<TPixel> frame);
    }
}