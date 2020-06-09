// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections;
using System.Collections.Generic;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Encapsulates a pixel-agnostic collection of <see cref="ImageFrame"/> instances
    /// that make up an <see cref="Image"/>.
    /// </summary>
    public abstract class ImageFrameCollection : IEnumerable<ImageFrame>
    {
        /// <summary>
        /// Gets the number of frames.
        /// </summary>
        public abstract int Count { get; }

        /// <summary>
        /// Gets the root frame.
        /// </summary>
        public ImageFrame RootFrame => this.NonGenericRootFrame;

        /// <summary>
        /// Gets the root frame. (Implements <see cref="RootFrame"/>.)
        /// </summary>
        protected abstract ImageFrame NonGenericRootFrame { get; }

        /// <summary>
        /// Gets the <see cref="ImageFrame"/> at the specified index.
        /// </summary>
        /// <value>
        /// The <see cref="ImageFrame"/>.
        /// </value>
        /// <param name="index">The index.</param>
        /// <returns>The <see cref="ImageFrame"/> at the specified index.</returns>
        public ImageFrame this[int index] => this.NonGenericGetFrame(index);

        /// <summary>
        /// Determines the index of a specific <paramref name="frame"/> in the <seealso cref="ImageFrameCollection"/>.
        /// </summary>
        /// <param name="frame">The <seealso cref="ImageFrame"/> to locate in the <seealso cref="ImageFrameCollection"/>.</param>
        /// <returns>The index of item if found in the list; otherwise, -1.</returns>
        public abstract int IndexOf(ImageFrame frame);

        /// <summary>
        /// Clones and inserts the <paramref name="source"/> into the <seealso cref="ImageFrameCollection"/> at the specified <paramref name="index"/>.
        /// </summary>
        /// <param name="index">The zero-based index to insert the frame at.</param>
        /// <param name="source">The <seealso cref="ImageFrame"/> to clone and insert into the <seealso cref="ImageFrameCollection"/>.</param>
        /// <exception cref="ArgumentException">Frame must have the same dimensions as the image.</exception>
        /// <returns>The cloned <see cref="ImageFrame"/>.</returns>
        public ImageFrame InsertFrame(int index, ImageFrame source) => this.NonGenericInsertFrame(index, source);

        /// <summary>
        /// Clones the <paramref name="source"/> frame and appends the clone to the end of the collection.
        /// </summary>
        /// <param name="source">The raw pixel data to generate the <seealso cref="ImageFrame{TPixel}"/> from.</param>
        /// <returns>The cloned <see cref="ImageFrame{TPixel}"/>.</returns>
        public ImageFrame AddFrame(ImageFrame source) => this.NonGenericAddFrame(source);

        /// <summary>
        /// Removes the frame at the specified index and frees all freeable resources associated with it.
        /// </summary>
        /// <param name="index">The zero-based index of the frame to remove.</param>
        /// <exception cref="InvalidOperationException">Cannot remove last frame.</exception>
        public abstract void RemoveFrame(int index);

        /// <summary>
        /// Determines whether the <seealso cref="ImageFrameCollection{TPixel}"/> contains the <paramref name="frame"/>.
        /// </summary>
        /// <param name="frame">The frame.</param>
        /// <returns>
        ///   <c>true</c> if the <seealso cref="ImageFrameCollection{TPixel}"/> contains the specified frame; otherwise, <c>false</c>.
        /// </returns>
        public abstract bool Contains(ImageFrame frame);

        /// <summary>
        /// Moves an <seealso cref="ImageFrame{TPixel}"/> from <paramref name="sourceIndex"/> to <paramref name="destinationIndex"/>.
        /// </summary>
        /// <param name="sourceIndex">The zero-based index of the frame to move.</param>
        /// <param name="destinationIndex">The index to move the frame to.</param>
        public abstract void MoveFrame(int sourceIndex, int destinationIndex);

        /// <summary>
        /// Removes the frame at the specified index and creates a new image with only the removed frame
        /// with the same metadata as the original image.
        /// </summary>
        /// <param name="index">The zero-based index of the frame to export.</param>
        /// <exception cref="InvalidOperationException">Cannot remove last frame.</exception>
        /// <returns>The new <see cref="Image{TPixel}"/> with the specified frame.</returns>
        public Image ExportFrame(int index) => this.NonGenericExportFrame(index);

        /// <summary>
        /// Creates an <see cref="Image{T}"/> with only the frame at the specified index
        /// with the same metadata as the original image.
        /// </summary>
        /// <param name="index">The zero-based index of the frame to clone.</param>
        /// <returns>The new <see cref="Image{TPixel}"/> with the specified frame.</returns>
        public Image CloneFrame(int index) => this.NonGenericCloneFrame(index);

        /// <summary>
        /// Creates a new <seealso cref="ImageFrame{TPixel}" /> and appends it to the end of the collection.
        /// </summary>
        /// <returns>
        /// The new <see cref="ImageFrame{TPixel}" />.
        /// </returns>
        public ImageFrame CreateFrame() => this.NonGenericCreateFrame();

        /// <summary>
        /// Creates a new <seealso cref="ImageFrame{TPixel}" /> and appends it to the end of the collection.
        /// </summary>
        /// <param name="backgroundColor">The background color to initialize the pixels with.</param>
        /// <returns>
        /// The new <see cref="ImageFrame{TPixel}" />.
        /// </returns>
        public ImageFrame CreateFrame(Color backgroundColor) => this.NonGenericCreateFrame(backgroundColor);

        /// <inheritdoc />
        public IEnumerator<ImageFrame> GetEnumerator() => this.NonGenericGetEnumerator();

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        /// <summary>
        /// Implements <see cref="GetEnumerator"/>.
        /// </summary>
        /// <returns>The enumerator.</returns>
        protected abstract IEnumerator<ImageFrame> NonGenericGetEnumerator();

        /// <summary>
        /// Implements the getter of the indexer.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <returns>The frame.</returns>
        protected abstract ImageFrame NonGenericGetFrame(int index);

        /// <summary>
        /// Implements <see cref="InsertFrame"/>.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <param name="source">The frame.</param>
        /// <returns>The new frame.</returns>
        protected abstract ImageFrame NonGenericInsertFrame(int index, ImageFrame source);

        /// <summary>
        /// Implements <see cref="AddFrame"/>.
        /// </summary>
        /// <param name="source">The frame.</param>
        /// <returns>The new frame.</returns>
        protected abstract ImageFrame NonGenericAddFrame(ImageFrame source);

        /// <summary>
        /// Implements <see cref="ExportFrame"/>.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <returns>The new image.</returns>
        protected abstract Image NonGenericExportFrame(int index);

        /// <summary>
        /// Implements <see cref="CloneFrame"/>.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <returns>The new image.</returns>
        protected abstract Image NonGenericCloneFrame(int index);

        /// <summary>
        /// Implements <see cref="CreateFrame()"/>.
        /// </summary>
        /// <returns>The new frame.</returns>
        protected abstract ImageFrame NonGenericCreateFrame();

        /// <summary>
        /// Implements <see cref="CreateFrame()"/>.
        /// </summary>
        /// <param name="backgroundColor">The background color.</param>
        /// <returns>The new frame.</returns>
        protected abstract ImageFrame NonGenericCreateFrame(Color backgroundColor);
    }
}
