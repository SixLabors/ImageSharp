// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections;
using System.Collections.Generic;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Encapsulates an imaged collection of frames.
    /// </summary>
    /// <typeparam name="TPixel">The type of the pixel.</typeparam>
    internal sealed class ImageFrameCollection<TPixel> : IImageFrameCollection<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private readonly IList<ImageFrame<TPixel>> frames = new List<ImageFrame<TPixel>>();
        private readonly Image<TPixel> parent;

        internal ImageFrameCollection(Image<TPixel> parent, int width, int height)
        {
            Guard.NotNull(parent, nameof(parent));

            this.parent = parent;

            // Frames are already cloned within the caller
            this.frames.Add(new ImageFrame<TPixel>(width, height));
        }

        internal ImageFrameCollection(Image<TPixel> parent, IEnumerable<ImageFrame<TPixel>> frames)
        {
            Guard.NotNull(parent, nameof(parent));
            Guard.NotNullOrEmpty(frames, nameof(frames));

            this.parent = parent;

            // Frames are already cloned by the caller
            foreach (ImageFrame<TPixel> f in frames)
            {
                this.ValidateFrame(f);
                this.frames.Add(f);
            }
        }

        /// <inheritdoc/>
        public int Count => this.frames.Count;

        /// <inheritdoc/>
        public ImageFrame<TPixel> RootFrame => this.frames.Count > 0 ? this.frames[0] : null;

        /// <inheritdoc/>
        public ImageFrame<TPixel> this[int index]
        {
            get => this.frames[index];
        }

        /// <inheritdoc/>
        public int IndexOf(ImageFrame<TPixel> frame) => this.frames.IndexOf(frame);

        /// <inheritdoc/>
        public ImageFrame<TPixel> InsertFrame(int index, ImageFrame<TPixel> frame)
        {
            this.ValidateFrame(frame);
            ImageFrame<TPixel> clonedFrame = frame.Clone();
            this.frames.Insert(index, clonedFrame);
            return clonedFrame;
        }

        /// <inheritdoc/>
        public ImageFrame<TPixel> AddFrame(ImageFrame<TPixel> frame)
        {
            this.ValidateFrame(frame);
            ImageFrame<TPixel> clonedFrame = frame.Clone();
            this.frames.Add(clonedFrame);
            return clonedFrame;
        }

        /// <inheritdoc/>
        public ImageFrame<TPixel> AddFrame(TPixel[] data)
        {
            var frame = ImageFrame.LoadPixelData(new Span<TPixel>(data), this.RootFrame.Width, this.RootFrame.Height);
            this.frames.Add(frame);
            return frame;
        }

        /// <inheritdoc/>
        public void RemoveFrame(int index)
        {
            if (index == 0 && this.Count == 1)
            {
                throw new InvalidOperationException("Cannot remove last frame.");
            }

            ImageFrame<TPixel> frame = this.frames[index];
            this.frames.RemoveAt(index);
            frame.Dispose();
        }

        /// <inheritdoc/>
        public bool Contains(ImageFrame<TPixel> frame)
        {
            return this.frames.Contains(frame);
        }

        /// <inheritdoc/>
        public void MoveFrame(int sourceIndex, int destIndex)
        {
            if (sourceIndex == destIndex)
            {
                return;
            }

            ImageFrame<TPixel> frameAtIndex = this.frames[sourceIndex];
            this.frames.RemoveAt(sourceIndex);
            this.frames.Insert(destIndex, frameAtIndex);
        }

        /// <inheritdoc/>
        public Image<TPixel> ExportFrame(int index)
        {
            ImageFrame<TPixel> frame = this[index];

            if (this.Count == 1 && this.frames.Contains(frame))
            {
                throw new InvalidOperationException("Cannot remove last frame.");
            }

            this.frames.Remove(frame);

            return new Image<TPixel>(this.parent.GetConfiguration(), this.parent.MetaData.Clone(), new[] { frame });
        }

        /// <inheritdoc/>
        public Image<TPixel> CloneFrame(int index)
        {
            ImageFrame<TPixel> frame = this[index];
            ImageFrame<TPixel> clonedFrame = frame.Clone();
            return new Image<TPixel>(this.parent.GetConfiguration(), this.parent.MetaData.Clone(), new[] { clonedFrame });
        }

        /// <inheritdoc/>
        public ImageFrame<TPixel> CreateFrame()
        {
            var frame = new ImageFrame<TPixel>(this.RootFrame.Width, this.RootFrame.Height);
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
    }
}