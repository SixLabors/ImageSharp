// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Encapsulates a collection of <see cref="ImageFrame{T}"/> instances that make up an <see cref="Image{T}"/>.
    /// </summary>
    /// <typeparam name="TPixel">The type of the pixel.</typeparam>
    public interface IImageFrameCollection<TPixel> : IEnumerable<ImageFrame<TPixel>>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Gets the number of frames.
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
        /// Creates an <see cref="Image{T}"/> with only the frame at the specified index
        /// with the same metadata as the original image.
        /// </summary>
        /// <param name="index">The zero-based index of the frame to clone.</param>
        /// <returns>The new <see cref="Image{TPixel}"/> with the specified frame.</returns>
        Image<TPixel> CloneFrame(int index);

        /// <summary>
        /// Removes the frame at the specified index and creates a new image with only the removed frame
        /// with the same metadata as the original image.
        /// </summary>
        /// <param name="index">The zero-based index of the frame to export.</param>
        /// <exception cref="InvalidOperationException">Cannot remove last frame.</exception>
        /// <returns>The new <see cref="Image{TPixel}"/> with the specified frame.</returns>
        Image<TPixel> ExportFrame(int index);

        /// <summary>
        /// Removes the frame at the specified index and frees all freeable resources associated with it.
        /// </summary>
        /// <param name="index">The zero-based index of the frame to remove.</param>
        /// <exception cref="InvalidOperationException">Cannot remove last frame.</exception>
        void RemoveFrame(int index);

        /// <summary>
        /// Creates a new <seealso cref="ImageFrame{TPixel}"/> and appends it to the end of the collection.
        /// </summary>
        /// <returns>The new <see cref="ImageFrame{TPixel}"/>.</returns>
        ImageFrame<TPixel> CreateFrame();

        /// <summary>
        /// Clones the <paramref name="source"/> frame and appends the clone to the end of the collection.
        /// </summary>
        /// <param name="source">The raw pixel data to generate the <seealso cref="ImageFrame{TPixel}"/> from.</param>
        /// <returns>The cloned <see cref="ImageFrame{TPixel}"/>.</returns>
        ImageFrame<TPixel> AddFrame(ImageFrame<TPixel> source);

        /// <summary>
        /// Creates a new frame from the pixel data with the same dimensions as the other frames and inserts the
        /// new frame at the end of the collection.
        /// </summary>
        /// <param name="source">The raw pixel data to generate the <seealso cref="ImageFrame{TPixel}"/> from.</param>
        /// <returns>The new <see cref="ImageFrame{TPixel}"/>.</returns>
        ImageFrame<TPixel> AddFrame(TPixel[] source);

        /// <summary>
        /// Clones and inserts the <paramref name="source"/> into the <seealso cref="IImageFrameCollection{TPixel}"/> at the specified <paramref name="index"/>.
        /// </summary>
        /// <param name="index">The zero-based index to insert the frame at.</param>
        /// <param name="source">The <seealso cref="ImageFrame{TPixel}"/> to clone and insert into the <seealso cref="IImageFrameCollection{TPixel}"/>.</param>
        /// <exception cref="ArgumentException">Frame must have the same dimensions as the image.</exception>
        /// <returns>The cloned <see cref="ImageFrame{TPixel}"/>.</returns>
        ImageFrame<TPixel> InsertFrame(int index, ImageFrame<TPixel> source);

        /// <summary>
        /// Moves an <seealso cref="ImageFrame{TPixel}"/> from <paramref name="sourceIndex"/> to <paramref name="destinationIndex"/>.
        /// </summary>
        /// <param name="sourceIndex">The zero-based index of the frame to move.</param>
        /// <param name="destinationIndex">The index to move the frame to.</param>
        void MoveFrame(int sourceIndex, int destinationIndex);

        /// <summary>
        /// Determines the index of a specific <paramref name="frame"/> in the <seealso cref="IImageFrameCollection{TPixel}"/>.
        /// </summary>
        /// <param name="frame">The <seealso cref="ImageFrame{TPixel}"/> to locate in the <seealso cref="IImageFrameCollection{TPixel}"/>.</param>
        /// <returns>The index of item if found in the list; otherwise, -1.</returns>
        int IndexOf(ImageFrame<TPixel> frame);

        /// <summary>
        /// Determines whether the <seealso cref="IImageFrameCollection{TPixel}"/> contains the <paramref name="frame"/>.
        /// </summary>
        /// <param name="frame">The frame.</param>
        /// <returns>
        ///   <c>true</c> if the <seealso cref="IImageFrameCollection{TPixel}"/> contains the specified frame; otherwise, <c>false</c>.
        /// </returns>
        bool Contains(ImageFrame<TPixel> frame);
    }
}