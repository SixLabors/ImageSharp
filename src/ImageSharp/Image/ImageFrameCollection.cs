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
    internal sealed class ImageFrameCollection<TPixel> : IImageFrameCollection<TPixel>, IDisposable
        where TPixel : struct, IPixel<TPixel>
    {
        private readonly IList<ImageFrame<TPixel>> frames = new List<ImageFrame<TPixel>>();

        internal ImageFrameCollection(int width, int height)
        {
            this.Add(new ImageFrame<TPixel>(width, height));
        }

        internal ImageFrameCollection(IEnumerable<ImageFrame<TPixel>> frames)
        {
            Guard.NotNullOrEmpty(frames, nameof(frames));
            foreach (ImageFrame<TPixel> f in frames)
            {
                this.Add(f);
            }
        }

        /// <summary>
        /// Gets the count.
        /// </summary>
        public int Count => this.frames.Count;

        /// <summary>
        /// Gets the root frame.
        /// </summary>
        public ImageFrame<TPixel> RootFrame => this.frames.Count > 0 ? this.frames[0] : null;

        /// <summary>
        /// Gets or sets the <see cref="ImageFrame{TPixel}"/> at the specified index.
        /// </summary>
        /// <value>
        /// The <see cref="ImageFrame{TPixel}"/>.
        /// </value>
        /// <param name="index">The index.</param>
        /// <returns>The <see cref="ImageFrame{TPixel}"/> at the specified index.</returns>
        public ImageFrame<TPixel> this[int index]
        {
            get => this.frames[index];

            set
            {
                this.ValidateFrame(value);
                this.frames[index] = value;
            }
        }

        /// <summary>
        ///  Determines the index of a specific <paramref name="frame"/> in the <seealso cref="Image{TPixel}"/>.
        /// </summary>
        /// <param name="frame">The <seealso cref="ImageFrame{TPixel}"/> to locate in the <seealso cref="Image{TPixel}"/>.</param>
        /// <returns>The index of item if found in the list; otherwise, -1.</returns>
        public int IndexOf(ImageFrame<TPixel> frame) => this.frames.IndexOf(frame);

        /// <summary>
        ///  Inserts the <paramref name="frame"/> to the <seealso cref="Image{TPixel}"/> at the specified <paramref name="index"/>.
        /// </summary>
        /// <param name="index"> The zero-based index at which item should be inserted..</param>
        /// <param name="frame">The <seealso cref="ImageFrame{TPixel}"/> to insert into the <seealso cref="Image{TPixel}"/>.</param>
        public void Insert(int index, ImageFrame<TPixel> frame)
        {
            this.ValidateFrame(frame);
            this.frames.Insert(index, frame);
        }

        /// <summary>
        /// Removes the <seealso cref="ImageFrame{TPixel}"/> from the <seealso cref="Image{TPixel}"/> at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index of the item to remove.</param>
        /// <exception cref="InvalidOperationException">Cannot remove last frame.</exception>
        public void RemoveAt(int index)
        {
            if (index == 0 && this.Count == 1)
            {
                throw new InvalidOperationException("Cannot remove last frame.");
            }

            this.frames.RemoveAt(index);
        }

        /// <summary>
        /// Adds the specified frame.
        /// </summary>
        /// <param name="frame">The frame.</param>
        /// <exception cref="ArgumentException">Frame must have the same dimensions as the image - frame</exception>
        public void Add(ImageFrame<TPixel> frame)
        {
            this.ValidateFrame(frame);
            this.frames.Add(frame);
        }

        /// <summary>
        /// Determines whether the <seealso cref="Image{TPixel}"/> contains the <paramref name="frame"/>.
        /// </summary>
        /// <param name="frame">The frame.</param>
        /// <returns>
        ///   <c>true</c> if the <seealso cref="Image{TPixel}"/> the specified frame; otherwise, <c>false</c>.
        /// </returns>
        public bool Contains(ImageFrame<TPixel> frame)
        {
            return this.frames.Contains(frame);
        }

        /// <summary>
        /// Removes the specified frame.
        /// </summary>
        /// <param name="frame">The frame.</param>
        /// <returns>true if item is found in the <seealso cref="Image{TPixel}"/>; otherwise,</returns>
        /// <exception cref="InvalidOperationException">Cannot remove last frame</exception>
        public bool Remove(ImageFrame<TPixel> frame)
        {
            if (this.Count == 1 && this.frames.Contains(frame))
            {
                throw new InvalidOperationException("Cannot remove last frame.");
            }

            return this.frames.Remove(frame);
        }

        /// <inheritdoc/>
        IEnumerator<ImageFrame<TPixel>> IEnumerable<ImageFrame<TPixel>>.GetEnumerator() => this.frames.GetEnumerator();

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)this.frames).GetEnumerator();

        private void ValidateFrame(ImageFrame<TPixel> frame)
        {
            Guard.NotNull(frame, nameof(frame));

            if (this.Count != 0)
            {
                if (this.RootFrame.Width != frame.Width || this.RootFrame.Height != frame.Height)
                {
                    throw new ArgumentException("Frame must have the same dimensions as the image.", nameof(frame));
                }
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            foreach (ImageFrame<TPixel> f in this.frames)
            {
                f.Dispose();
            }

            this.frames.Clear();
        }
    }
}