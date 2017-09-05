// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.MetaData;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Encapsulates an imaged collection of frames.
    /// </summary>
    /// <typeparam name="TPixel">The type of the pixel.</typeparam>
    public sealed class ImageFrameCollection<TPixel> : IEnumerable<ImageFrame<TPixel>>
        where TPixel : struct, IPixel<TPixel>
    {
        private IList<ImageFrame<TPixel>> frames = new List<ImageFrame<TPixel>>();
        private readonly Image<TPixel> parent;

        internal ImageFrameCollection(Image<TPixel> parent)
        {
            this.parent = parent;
        }

        /// <summary>
        /// Gets the count.
        /// </summary>
        public int Count { get => this.frames.Count; }

        /// <summary>
        /// Gets the root frame.
        /// </summary>
        public ImageFrame<TPixel> RootFrame
        {
            get
            {
                if (this.frames.Count > 0)
                {
                    return this.frames[0];
                }

                return null;
            }
        }

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
            get
            {
                return this.frames[index];
            }

            set
            {
                this.ValidateFrameSize(value);
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
            this.ValidateFrameSize(frame);
            this.frames.Insert(index, frame);
        }

        /// <summary>
        /// Removes the <seealso cref="ImageFrame{TPixel}"/> from the <seealso cref="Image{TPixel}"/> at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index of the item to remove.</param>
        /// <exception cref="InvalidOperationException">Cannot remove last frame.</exception>
        public void RemoveAt(int index)
        {
            if (index > 0 || this.frames.Count > 1)
            {
                this.frames.RemoveAt(index);
            }

            throw new InvalidOperationException("Cannot remove last frame.");
        }

        /// <summary>
        /// Adds the specified frame.
        /// </summary>
        /// <param name="frame">The frame.</param>
        /// <exception cref="ArgumentException">Frame must have the same dimensions as the image - frame</exception>
        public void Add(ImageFrame<TPixel> frame)
        {
            this.ValidateFrameSize(frame);
            this.frames.Add(frame);
        }

        private void ValidateFrameSize(ImageFrame<TPixel> frame)
        {
            if (this.Count != 0)
            {
                if (this.parent.Width != frame.Width || this.parent.Height != frame.Height)
                {
                    throw new ArgumentException("Frame must have the same dimensions as the image", nameof(frame));
                }
            }
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
            if (this.frames.Count == 1 && this.frames.Contains(frame))
            {
                throw new InvalidOperationException("Cannot remove last frame");
            }

            return this.frames.Remove(frame);
        }

        /// <inheritdoc/>
        IEnumerator<ImageFrame<TPixel>> IEnumerable<ImageFrame<TPixel>>.GetEnumerator() => this.frames.GetEnumerator();

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)this.frames).GetEnumerator();
    }
}