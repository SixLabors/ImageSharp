// <copyright file="GifDecoderCore.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System;
    using System.IO;

    /// <summary>
    /// Performs the gif decoding operation.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    internal class GifDecoderCore<TColor, TPacked>
        where TColor : struct, IPackedPixel<TPacked>
        where TPacked : struct
    {
        /// <summary>
        /// The image to decode the information to.
        /// </summary>
        private Image<TColor, TPacked> decodedImage;

        /// <summary>
        /// The currently loaded stream.
        /// </summary>
        private Stream currentStream;

        /// <summary>
        /// The global color table.
        /// </summary>
        private byte[] globalColorTable;

        /// <summary>
        /// The next frame.
        /// </summary>
        private ImageFrame<TColor, TPacked> nextFrame;

        /// <summary>
        /// The area to restore.
        /// </summary>
        private Rectangle? restoreArea;

        /// <summary>
        /// The logical screen descriptor.
        /// </summary>
        private GifLogicalScreenDescriptor logicalScreenDescriptor;

        /// <summary>
        /// The graphics control extension.
        /// </summary>
        private GifGraphicsControlExtension graphicsControlExtension;

        /// <summary>
        /// Decodes the stream to the image.
        /// </summary>
        /// <param name="image">The image to decode to.</param>
        /// <param name="stream">The stream containing image data. </param>
        public void Decode(Image<TColor, TPacked> image, Stream stream)
        {
            this.decodedImage = image;

            this.currentStream = stream;

            // Skip the identifier
            this.currentStream.Skip(6);
            this.ReadLogicalScreenDescriptor();

            if (this.logicalScreenDescriptor.GlobalColorTableFlag)
            {
                this.globalColorTable = new byte[this.logicalScreenDescriptor.GlobalColorTableSize * 3];

                // Read the global color table from the stream
                stream.Read(this.globalColorTable, 0, this.globalColorTable.Length);
            }

            // Loop though the respective gif parts and read the data.
            int nextFlag = stream.ReadByte();
            while (nextFlag != GifConstants.Terminator)
            {
                if (nextFlag == GifConstants.ImageLabel)
                {
                    this.ReadFrame();
                }
                else if (nextFlag == GifConstants.ExtensionIntroducer)
                {
                    int label = stream.ReadByte();
                    switch (label)
                    {
                        case GifConstants.GraphicControlLabel:
                            this.ReadGraphicalControlExtension();
                            break;
                        case GifConstants.CommentLabel:
                            this.ReadComments();
                            break;
                        case GifConstants.ApplicationExtensionLabel:
                            this.Skip(12); // No need to read.
                            break;
                        case GifConstants.PlainTextLabel:
                            this.Skip(13); // Not supported by any known decoder.
                            break;
                    }
                }
                else if (nextFlag == GifConstants.EndIntroducer)
                {
                    break;
                }

                nextFlag = stream.ReadByte();
            }
        }

        /// <summary>
        /// Reads the graphic control extension.
        /// </summary>
        private void ReadGraphicalControlExtension()
        {
            byte[] buffer = new byte[6];

            this.currentStream.Read(buffer, 0, buffer.Length);

            byte packed = buffer[1];

            this.graphicsControlExtension = new GifGraphicsControlExtension
            {
                DelayTime = BitConverter.ToInt16(buffer, 2),
                TransparencyIndex = buffer[4],
                TransparencyFlag = (packed & 0x01) == 1,
                DisposalMethod = (DisposalMethod)((packed & 0x1C) >> 2)
            };
        }

        /// <summary>
        /// Reads the image descriptor
        /// </summary>
        /// <returns><see cref="GifImageDescriptor"/></returns>
        private GifImageDescriptor ReadImageDescriptor()
        {
            byte[] buffer = new byte[9];

            this.currentStream.Read(buffer, 0, buffer.Length);

            byte packed = buffer[8];

            GifImageDescriptor imageDescriptor = new GifImageDescriptor
            {
                Left = BitConverter.ToInt16(buffer, 0),
                Top = BitConverter.ToInt16(buffer, 2),
                Width = BitConverter.ToInt16(buffer, 4),
                Height = BitConverter.ToInt16(buffer, 6),
                LocalColorTableFlag = ((packed & 0x80) >> 7) == 1,
                LocalColorTableSize = 2 << (packed & 0x07),
                InterlaceFlag = ((packed & 0x40) >> 6) == 1
            };

            return imageDescriptor;
        }

        /// <summary>
        /// Reads the logical screen descriptor.
        /// </summary>
        private void ReadLogicalScreenDescriptor()
        {
            byte[] buffer = new byte[7];

            this.currentStream.Read(buffer, 0, buffer.Length);

            byte packed = buffer[4];

            this.logicalScreenDescriptor = new GifLogicalScreenDescriptor
            {
                Width = BitConverter.ToInt16(buffer, 0),
                Height = BitConverter.ToInt16(buffer, 2),
                BackgroundColorIndex = buffer[5],
                PixelAspectRatio = buffer[6],
                GlobalColorTableFlag = ((packed & 0x80) >> 7) == 1,
                GlobalColorTableSize = 2 << (packed & 0x07)
            };

            if (this.logicalScreenDescriptor.GlobalColorTableSize > 255 * 4)
            {
                throw new ImageFormatException(
                    $"Invalid gif colormap size '{this.logicalScreenDescriptor.GlobalColorTableSize}'");
            }

            if (this.logicalScreenDescriptor.Width > this.decodedImage.MaxWidth || this.logicalScreenDescriptor.Height > this.decodedImage.MaxHeight)
            {
                throw new ArgumentOutOfRangeException(
                    $"The input gif '{this.logicalScreenDescriptor.Width}x{this.logicalScreenDescriptor.Height}' is bigger then the max allowed size '{this.decodedImage.MaxWidth}x{this.decodedImage.MaxHeight}'");
            }
        }

        /// <summary>
        /// Skips the designated number of bytes in the stream.
        /// </summary>
        /// <param name="length">The number of bytes to skip.</param>
        private void Skip(int length)
        {
            this.currentStream.Skip(length);

            int flag;

            while ((flag = this.currentStream.ReadByte()) != 0)
            {
                this.currentStream.Skip(flag);
            }
        }

        /// <summary>
        /// Reads the gif comments.
        /// </summary>
        private void ReadComments()
        {
            int flag;

            while ((flag = this.currentStream.ReadByte()) != 0)
            {
                if (flag > GifConstants.MaxCommentLength)
                {
                    throw new ImageFormatException($"Gif comment length '{flag}' exceeds max '{GifConstants.MaxCommentLength}'");
                }

                byte[] buffer = new byte[flag];

                this.currentStream.Read(buffer, 0, flag);

                this.decodedImage.Properties.Add(new ImageProperty("Comments", BitConverter.ToString(buffer)));
            }
        }

        /// <summary>
        /// Reads an individual gif frame.
        /// </summary>
        private void ReadFrame()
        {
            GifImageDescriptor imageDescriptor = this.ReadImageDescriptor();

            byte[] localColorTable = this.ReadFrameLocalColorTable(imageDescriptor);

            byte[] indices = this.ReadFrameIndices(imageDescriptor);

            // Determine the color table for this frame. If there is a local one, use it
            // otherwise use the global color table.
            byte[] colorTable = localColorTable ?? this.globalColorTable;

            this.ReadFrameColors(indices, colorTable, imageDescriptor);

            // Skip any remaining blocks
            this.Skip(0);
        }

        /// <summary>
        /// Reads the frame indices marking the color to use for each pixel.
        /// </summary>
        /// <param name="imageDescriptor">The <see cref="GifImageDescriptor"/>.</param>
        /// <returns>The <see cref="T:byte[]"/></returns>
        private byte[] ReadFrameIndices(GifImageDescriptor imageDescriptor)
        {
            int dataSize = this.currentStream.ReadByte();
            LzwDecoder lzwDecoder = new LzwDecoder(this.currentStream);

            byte[] indices = lzwDecoder.DecodePixels(imageDescriptor.Width, imageDescriptor.Height, dataSize);

            return indices;
        }

        /// <summary>
        /// Reads the local color table from the current frame.
        /// </summary>
        /// <param name="imageDescriptor">The <see cref="GifImageDescriptor"/>.</param>
        /// <returns>The <see cref="T:byte[]"/></returns>
        private byte[] ReadFrameLocalColorTable(GifImageDescriptor imageDescriptor)
        {
            byte[] localColorTable = null;

            if (imageDescriptor.LocalColorTableFlag)
            {
                localColorTable = new byte[imageDescriptor.LocalColorTableSize * 3];

                this.currentStream.Read(localColorTable, 0, localColorTable.Length);
            }

            return localColorTable;
        }

        /// <summary>
        /// Reads the frames colors, mapping indices to colors.
        /// </summary>
        /// <param name="indices">The indexed pixels.</param>
        /// <param name="colorTable">The color table containing the available colors.</param>
        /// <param name="descriptor">The <see cref="GifImageDescriptor"/></param>
        private unsafe void ReadFrameColors(byte[] indices, byte[] colorTable, GifImageDescriptor descriptor)
        {
            int imageWidth = this.logicalScreenDescriptor.Width;
            int imageHeight = this.logicalScreenDescriptor.Height;

            ImageFrame<TColor, TPacked> previousFrame = null;

            ImageFrame<TColor, TPacked> currentFrame = null;

            ImageBase<TColor, TPacked> image;

            if (this.nextFrame == null)
            {
                image = this.decodedImage;

                image.Quality = colorTable.Length / 3;

                // This initializes the image to become fully transparent because the alpha channel is zero.
                image.InitPixels(imageWidth, imageHeight);
            }
            else
            {
                if (this.graphicsControlExtension != null &&
                    this.graphicsControlExtension.DisposalMethod == DisposalMethod.RestoreToPrevious)
                {
                    previousFrame = this.nextFrame;
                }

                currentFrame = this.nextFrame.Clone();

                image = currentFrame;

                RestoreToBackground(image);

                this.decodedImage.Frames.Add(currentFrame);
            }

            if (this.graphicsControlExtension != null && this.graphicsControlExtension.DelayTime > 0)
            {
                image.FrameDelay = this.graphicsControlExtension.DelayTime;
            }

            int i = 0;
            int interlacePass = 0; // The interlace pass
            int interlaceIncrement = 8; // The interlacing line increment
            int interlaceY = 0; // The current interlaced line

            using (PixelAccessor<TColor, TPacked> pixelAccessor = image.Lock())
            {
                using (PixelRow<TColor, TPacked> pixelRow = new PixelRow<TColor, TPacked>(imageWidth, ComponentOrder.XYZW))
                {
                    for (int y = descriptor.Top; y < descriptor.Top + descriptor.Height; y++)
                    {
                        // Check if this image is interlaced.
                        int writeY; // the target y offset to write to
                        if (descriptor.InterlaceFlag)
                        {
                            // If so then we read lines at predetermined offsets.
                            // When an entire image height worth of offset lines has been read we consider this a pass.
                            // With each pass the number of offset lines changes and the starting line changes.
                            if (interlaceY >= descriptor.Height)
                            {
                                interlacePass++;
                                switch (interlacePass)
                                {
                                  case 1:
                                      interlaceY = 4;
                                      break;
                                  case 2:
                                      interlaceY = 2;
                                      interlaceIncrement = 4;
                                      break;
                                  case 3:
                                      interlaceY = 1;
                                      interlaceIncrement = 2;
                                      break;
                                }
                            }

                            writeY = interlaceY + descriptor.Top;

                            interlaceY += interlaceIncrement;
                        }
                        else
                        {
                            writeY = y;
                        }

                        pixelRow.Reset();

                        byte* pixelBase = pixelRow.PixelBase;
                        for (int x = 0; x < descriptor.Width; x++)
                        {
                            int index = indices[i];

                            if (this.graphicsControlExtension == null ||
                                this.graphicsControlExtension.TransparencyFlag == false ||
                                this.graphicsControlExtension.TransparencyIndex != index)
                            {
                                int indexOffset = index * 3;
                                *(pixelBase + 0) = colorTable[indexOffset];
                                *(pixelBase + 1) = colorTable[indexOffset + 1];
                                *(pixelBase + 2) = colorTable[indexOffset + 2];
                                *(pixelBase + 3) = 255;
                            }

                            i++;
                            pixelBase += 4;
                        }

                        pixelAccessor.CopyFrom(pixelRow, writeY, descriptor.Left);
                    }
                }
            }

            if (previousFrame != null)
            {
                this.nextFrame = previousFrame;
                return;
            }

            if (currentFrame == null)
            {
                this.nextFrame = this.decodedImage.ToFrame();
            }
            else
            {
                this.nextFrame = currentFrame.Clone();
            }

            if (this.graphicsControlExtension != null &&
                this.graphicsControlExtension.DisposalMethod == DisposalMethod.RestoreToBackground)
            {
                this.restoreArea = new Rectangle(descriptor.Left, descriptor.Top, descriptor.Width, descriptor.Height);
            }
        }

        private void RestoreToBackground(ImageBase<TColor, TPacked> frame)
        {
            if (this.restoreArea == null)
            {
                return;
            }

            // Optimization for when the size of the frame is the same as the image size.
            if (this.restoreArea.Value.Width == this.decodedImage.Width &&
                this.restoreArea.Value.Height == this.decodedImage.Height)
            {
                using (PixelAccessor<TColor, TPacked> pixelAccessor = frame.Lock())
                {
                    pixelAccessor.Reset();
                }
            }
            else
            {
                using (PixelRow<TColor, TPacked> emptyRow = new PixelRow<TColor, TPacked>(this.restoreArea.Value.Width, ComponentOrder.XYZW))
                {
                    using (PixelAccessor<TColor, TPacked> pixelAccessor = frame.Lock())
                    {
                        for (int y = this.restoreArea.Value.Top; y < this.restoreArea.Value.Top + this.restoreArea.Value.Height; y++)
                        {
                            pixelAccessor.CopyFrom(emptyRow, y, this.restoreArea.Value.Left);
                        }
                    }
                }
            }

            this.restoreArea = null;
        }
    }
}
