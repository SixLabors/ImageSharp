// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections;
using System.Collections.Generic;

namespace SixLabors.ImageSharp
{
    public abstract class ImageFrameCollection : IEnumerable<ImageFrame>
    {
        public IEnumerator<ImageFrame> GetEnumerator() => this.NonGenericGetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => this.NonGenericGetEnumerator();

        public abstract int Count { get; }

        public ImageFrame RootFrame => this.NonGenericRootFrame;

        protected abstract ImageFrame NonGenericRootFrame { get; }

        public ImageFrame this[int index] => this.NonGenericGetFrame(index);

        public abstract int IndexOf(ImageFrame frame);

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
        public ImageFrame CreateFrame() => this.CreateFrame(Color.Black);

        /// <summary>
        /// Creates a new <seealso cref="ImageFrame{TPixel}" /> and appends it to the end of the collection.
        /// </summary>
        /// <param name="backgroundColor">The background color to initialize the pixels with.</param>
        /// <returns>
        /// The new <see cref="ImageFrame{TPixel}" />.
        /// </returns>
        public ImageFrame CreateFrame(Color backgroundColor) => this.NonGenericCreateFrame(backgroundColor);
        

        protected abstract IEnumerator<ImageFrame> NonGenericGetEnumerator();

        protected abstract ImageFrame NonGenericGetFrame(int index);

        protected abstract ImageFrame NonGenericInsertFrame(int index, ImageFrame source);

        protected abstract ImageFrame NonGenericAddFrame(ImageFrame source);

        protected abstract Image NonGenericExportFrame(int index);
        
        protected abstract Image NonGenericCloneFrame(int index);
        
        protected abstract ImageFrame NonGenericCreateFrame(Color backgroundColor);
    }
}