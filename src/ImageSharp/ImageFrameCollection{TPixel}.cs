// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections;
using System.Collections.Generic;

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Encapsulates a pixel-specific collection of <see cref="ImageFrame{T}"/> instances
    /// that make up an <see cref="Image{T}"/>.
    /// </summary>
    /// <typeparam name="TPixel">The type of the pixel.</typeparam>
    public sealed class ImageFrameCollection<TPixel> : ImageFrameCollection, IEnumerable<ImageFrame<TPixel>>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private readonly IList<ImageFrame<TPixel>> frames = new List<ImageFrame<TPixel>>();
        private readonly Image<TPixel> parent;

        internal ImageFrameCollection(Image<TPixel> parent, int width, int height, TPixel backgroundColor)
        {
            this.parent = parent ?? throw new ArgumentNullException(nameof(parent));

            // Frames are already cloned within the caller
            this.frames.Add(new ImageFrame<TPixel>(parent.GetConfiguration(), width, height, backgroundColor));
        }

        internal ImageFrameCollection(Image<TPixel> parent, int width, int height, MemoryGroup<TPixel> memorySource)
        {
            this.parent = parent ?? throw new ArgumentNullException(nameof(parent));

            // Frames are already cloned within the caller
            this.frames.Add(new ImageFrame<TPixel>(parent.GetConfiguration(), width, height, memorySource));
        }

        internal ImageFrameCollection(Image<TPixel> parent, IEnumerable<ImageFrame<TPixel>> frames)
        {
            Guard.NotNull(parent, nameof(parent));
            Guard.NotNull(frames, nameof(frames));

            this.parent = parent;

            // Frames are already cloned by the caller
            foreach (ImageFrame<TPixel> f in frames)
            {
                this.ValidateFrame(f);
                this.frames.Add(f);
            }

            // Ensure at least 1 frame was added to the frames collection
            if (this.frames.Count == 0)
            {
                throw new ArgumentException("Must not be empty.", nameof(frames));
            }
        }

        /// <summary>
        /// Gets the number of frames.
        /// </summary>
        public override int Count => this.frames.Count;

        /// <summary>
        /// Gets the root frame.
        /// </summary>
        public new ImageFrame<TPixel> RootFrame => this.frames.Count > 0 ? this.frames[0] : null;

        /// <inheritdoc />
        protected override ImageFrame NonGenericRootFrame => this.RootFrame;

        /// <summary>
        /// Gets the <see cref="ImageFrame{TPixel}"/> at the specified index.
        /// </summary>
        /// <value>
        /// The <see cref="ImageFrame{TPixel}"/>.
        /// </value>
        /// <param name="index">The index.</param>
        /// <returns>The <see cref="ImageFrame{TPixel}"/> at the specified index.</returns>
        public new ImageFrame<TPixel> this[int index] => this.frames[index];

        /// <inheritdoc />
        public override int IndexOf(ImageFrame frame)
        {
            return frame is ImageFrame<TPixel> specific ? this.IndexOf(specific) : -1;
        }

        /// <summary>
        /// Determines the index of a specific <paramref name="frame"/> in the <seealso cref="ImageFrameCollection{TPixel}"/>.
        /// </summary>
        /// <param name="frame">The <seealso cref="ImageFrame{TPixel}"/> to locate in the <seealso cref="ImageFrameCollection{TPixel}"/>.</param>
        /// <returns>The index of item if found in the list; otherwise, -1.</returns>
        public int IndexOf(ImageFrame<TPixel> frame) => this.frames.IndexOf(frame);

        /// <summary>
        /// Clones and inserts the <paramref name="source"/> into the <seealso cref="ImageFrameCollection{TPixel}"/> at the specified <paramref name="index"/>.
        /// </summary>
        /// <param name="index">The zero-based index to insert the frame at.</param>
        /// <param name="source">The <seealso cref="ImageFrame{TPixel}"/> to clone and insert into the <seealso cref="ImageFrameCollection{TPixel}"/>.</param>
        /// <exception cref="ArgumentException">Frame must have the same dimensions as the image.</exception>
        /// <returns>The cloned <see cref="ImageFrame{TPixel}"/>.</returns>
        public ImageFrame<TPixel> InsertFrame(int index, ImageFrame<TPixel> source)
        {
            this.ValidateFrame(source);
            ImageFrame<TPixel> clonedFrame = source.Clone(this.parent.GetConfiguration());
            this.frames.Insert(index, clonedFrame);
            return clonedFrame;
        }

        /// <summary>
        /// Clones the <paramref name="source"/> frame and appends the clone to the end of the collection.
        /// </summary>
        /// <param name="source">The raw pixel data to generate the <seealso cref="ImageFrame{TPixel}"/> from.</param>
        /// <returns>The cloned <see cref="ImageFrame{TPixel}"/>.</returns>
        public ImageFrame<TPixel> AddFrame(ImageFrame<TPixel> source)
        {
            this.ValidateFrame(source);
            ImageFrame<TPixel> clonedFrame = source.Clone(this.parent.GetConfiguration());
            this.frames.Add(clonedFrame);
            return clonedFrame;
        }

        /// <summary>
        /// Creates a new frame from the pixel data with the same dimensions as the other frames and inserts the
        /// new frame at the end of the collection.
        /// </summary>
        /// <param name="source">The raw pixel data to generate the <seealso cref="ImageFrame{TPixel}"/> from.</param>
        /// <returns>The new <see cref="ImageFrame{TPixel}"/>.</returns>
        public ImageFrame<TPixel> AddFrame(ReadOnlySpan<TPixel> source)
        {
            var frame = ImageFrame.LoadPixelData(
                this.parent.GetConfiguration(),
                source,
                this.RootFrame.Width,
                this.RootFrame.Height);
            this.frames.Add(frame);
            return frame;
        }

        /// <summary>
        /// Creates a new frame from the pixel data with the same dimensions as the other frames and inserts the
        /// new frame at the end of the collection.
        /// </summary>
        /// <param name="source">The raw pixel data to generate the <seealso cref="ImageFrame{TPixel}"/> from.</param>
        /// <returns>The new <see cref="ImageFrame{TPixel}"/>.</returns>
        public ImageFrame<TPixel> AddFrame(TPixel[] source)
        {
            Guard.NotNull(source, nameof(source));
            return this.AddFrame(source.AsSpan());
        }

        /// <summary>
        /// Removes the frame at the specified index and frees all freeable resources associated with it.
        /// </summary>
        /// <param name="index">The zero-based index of the frame to remove.</param>
        /// <exception cref="InvalidOperationException">Cannot remove last frame.</exception>
        public override void RemoveFrame(int index)
        {
            if (index == 0 && this.Count == 1)
            {
                throw new InvalidOperationException("Cannot remove last frame.");
            }

            ImageFrame<TPixel> frame = this.frames[index];
            this.frames.RemoveAt(index);
            frame.Dispose();
        }

        /// <inheritdoc />
        public override bool Contains(ImageFrame frame) =>
            frame is ImageFrame<TPixel> specific && this.Contains(specific);

        /// <summary>
        /// Determines whether the <seealso cref="ImageFrameCollection{TPixel}"/> contains the <paramref name="frame"/>.
        /// </summary>
        /// <param name="frame">The frame.</param>
        /// <returns>
        ///   <c>true</c> if the <seealso cref="ImageFrameCollection{TPixel}"/> contains the specified frame; otherwise, <c>false</c>.
        /// </returns>
        public bool Contains(ImageFrame<TPixel> frame) => this.frames.Contains(frame);

        /// <summary>
        /// Moves an <seealso cref="ImageFrame{TPixel}"/> from <paramref name="sourceIndex"/> to <paramref name="destinationIndex"/>.
        /// </summary>
        /// <param name="sourceIndex">The zero-based index of the frame to move.</param>
        /// <param name="destinationIndex">The index to move the frame to.</param>
        public override void MoveFrame(int sourceIndex, int destinationIndex)
        {
            if (sourceIndex == destinationIndex)
            {
                return;
            }

            ImageFrame<TPixel> frameAtIndex = this.frames[sourceIndex];
            this.frames.RemoveAt(sourceIndex);
            this.frames.Insert(destinationIndex, frameAtIndex);
        }

        /// <summary>
        /// Removes the frame at the specified index and creates a new image with only the removed frame
        /// with the same metadata as the original image.
        /// </summary>
        /// <param name="index">The zero-based index of the frame to export.</param>
        /// <exception cref="InvalidOperationException">Cannot remove last frame.</exception>
        /// <returns>The new <see cref="Image{TPixel}"/> with the specified frame.</returns>
        public new Image<TPixel> ExportFrame(int index)
        {
            ImageFrame<TPixel> frame = this[index];

            if (this.Count == 1 && this.frames.Contains(frame))
            {
                throw new InvalidOperationException("Cannot remove last frame.");
            }

            this.frames.Remove(frame);

            return new Image<TPixel>(this.parent.GetConfiguration(), this.parent.Metadata.DeepClone(), new[] { frame });
        }

        /// <summary>
        /// Creates an <see cref="Image{T}"/> with only the frame at the specified index
        /// with the same metadata as the original image.
        /// </summary>
        /// <param name="index">The zero-based index of the frame to clone.</param>
        /// <returns>The new <see cref="Image{TPixel}"/> with the specified frame.</returns>
        public new Image<TPixel> CloneFrame(int index)
        {
            ImageFrame<TPixel> frame = this[index];
            ImageFrame<TPixel> clonedFrame = frame.Clone();
            return new Image<TPixel>(this.parent.GetConfiguration(), this.parent.Metadata.DeepClone(), new[] { clonedFrame });
        }

        /// <summary>
        /// Creates a new <seealso cref="ImageFrame{TPixel}" /> and appends it to the end of the collection.
        /// </summary>
        /// <returns>
        /// The new <see cref="ImageFrame{TPixel}" />.
        /// </returns>
        public new ImageFrame<TPixel> CreateFrame()
        {
            var frame = new ImageFrame<TPixel>(
                this.parent.GetConfiguration(),
                this.RootFrame.Width,
                this.RootFrame.Height);
            this.frames.Add(frame);
            return frame;
        }

        /// <inheritdoc />
        protected override IEnumerator<ImageFrame> NonGenericGetEnumerator() => this.frames.GetEnumerator();

        /// <inheritdoc />
        protected override ImageFrame NonGenericGetFrame(int index) => this[index];

        /// <inheritdoc />
        protected override ImageFrame NonGenericInsertFrame(int index, ImageFrame source)
        {
            Guard.NotNull(source, nameof(source));

            if (source is ImageFrame<TPixel> compatibleSource)
            {
                return this.InsertFrame(index, compatibleSource);
            }

            ImageFrame<TPixel> result = this.CopyNonCompatibleFrame(source);
            this.frames.Insert(index, result);
            return result;
        }

        /// <inheritdoc />
        protected override ImageFrame NonGenericAddFrame(ImageFrame source)
        {
            Guard.NotNull(source, nameof(source));

            if (source is ImageFrame<TPixel> compatibleSource)
            {
                return this.AddFrame(compatibleSource);
            }

            ImageFrame<TPixel> result = this.CopyNonCompatibleFrame(source);
            this.frames.Add(result);
            return result;
        }

        /// <inheritdoc />
        protected override Image NonGenericExportFrame(int index) => this.ExportFrame(index);

        /// <inheritdoc />
        protected override Image NonGenericCloneFrame(int index) => this.CloneFrame(index);

        /// <inheritdoc />
        protected override ImageFrame NonGenericCreateFrame(Color backgroundColor) =>
            this.CreateFrame(backgroundColor.ToPixel<TPixel>());

        /// <inheritdoc />
        protected override ImageFrame NonGenericCreateFrame() => this.CreateFrame();

        /// <summary>
        /// Creates a new <seealso cref="ImageFrame{TPixel}" /> and appends it to the end of the collection.
        /// </summary>
        /// <param name="backgroundColor">The background color to initialize the pixels with.</param>
        /// <returns>
        /// The new <see cref="ImageFrame{TPixel}" />.
        /// </returns>
        public ImageFrame<TPixel> CreateFrame(TPixel backgroundColor)
        {
            var frame = new ImageFrame<TPixel>(
                this.parent.GetConfiguration(),
                this.RootFrame.Width,
                this.RootFrame.Height,
                backgroundColor);
            this.frames.Add(frame);
            return frame;
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

        internal void Dispose()
        {
            foreach (ImageFrame<TPixel> f in this.frames)
            {
                f.Dispose();
            }

            this.frames.Clear();
        }

        private ImageFrame<TPixel> CopyNonCompatibleFrame(ImageFrame source)
        {
            var result = new ImageFrame<TPixel>(
                this.parent.GetConfiguration(),
                source.Size(),
                source.Metadata.DeepClone());
            source.CopyPixelsTo(result.PixelBuffer.FastMemoryGroup);
            return result;
        }
    }
}
