namespace ImageProcessor.Formats
{
    using System;
    using System.IO;

    internal class GifDecoderCore
    {
        /// <summary>
        /// The maximum comment length.
        /// </summary>
        private const int MaxCommentLength = 1024 * 8;

        private const byte ExtensionIntroducer = 0x21;
        private const byte Terminator = 0;
        private const byte ImageLabel = 0x2C;
        private const byte EndIntroducer = 0x3B;
        private const byte ApplicationExtensionLabel = 0xFF;
        private const byte CommentLabel = 0xFE;
        private const byte ImageDescriptorLabel = 0x2C;
        private const byte PlainTextLabel = 0x01;
        private const byte GraphicControlLabel = 0xF9;

        private Image image;
        private Stream currentStream;
        private byte[] globalColorTable;
        private byte[] currentFrame;
        private GifLogicalScreenDescriptor logicalScreenDescriptor;
        private GifGraphicsControlExtension graphicsControlExtension;

        public void Decode(Image image, Stream stream)
        {
            this.image = image;

            this.currentStream = stream;

            // Skip the identifier
            this.currentStream.Seek(6, SeekOrigin.Current);
            this.ReadLogicalScreenDescriptor();

            if (this.logicalScreenDescriptor.GlobalColorTableFlag)
            {
                this.globalColorTable = new byte[this.logicalScreenDescriptor.GlobalColorTableSize * 3];

                // Read the global color table from the stream
                stream.Read(this.globalColorTable, 0, this.globalColorTable.Length);
            }

            int nextFlag = stream.ReadByte();
            while (nextFlag != 0)
            {
                if (nextFlag == ImageLabel)
                {
                    this.ReadFrame();
                }
                else if (nextFlag == ExtensionIntroducer)
                {
                    int label = stream.ReadByte();
                    switch (label)
                    {
                        case GraphicControlLabel:
                            this.ReadGraphicalControlExtension();
                            break;
                        case CommentLabel:
                            this.ReadComments();
                            break;
                        case ApplicationExtensionLabel:
                            // TODO: Read Application extension
                            this.Skip(12);
                            break;
                        case PlainTextLabel:
                            this.Skip(13);
                            break;
                    }
                }
                else if (nextFlag == EndIntroducer)
                {
                    break;
                }

                nextFlag = stream.ReadByte();
            }
        }

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

        private void ReadApplicationBlockExtension()
        {
            // TODO: Implement
            throw new NotImplementedException();
        }

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

            if (this.logicalScreenDescriptor.Width > ImageBase.MaxWidth || this.logicalScreenDescriptor.Height > ImageBase.MaxHeight)
            {
                throw new ArgumentOutOfRangeException(
                    $"The input gif '{this.logicalScreenDescriptor.Width}x{this.logicalScreenDescriptor.Height}' is bigger then the max allowed size '{ImageBase.MaxWidth}x{ImageBase.MaxHeight}'");
            }
        }

        private void Skip(int length)
        {
            this.currentStream.Seek(length, SeekOrigin.Current);

            int flag;

            while ((flag = this.currentStream.ReadByte()) != 0)
            {
                this.currentStream.Seek(flag, SeekOrigin.Current);
            }
        }

        private void ReadComments()
        {
            int flag;

            while ((flag = this.currentStream.ReadByte()) != 0)
            {
                if (flag > MaxCommentLength)
                {
                    throw new ImageFormatException($"Gif comment length '{flag}' exceeds max '{MaxCommentLength}'");
                }

                byte[] buffer = new byte[flag];

                this.currentStream.Read(buffer, 0, flag);

                this.image.Properties.Add(new ImageProperty("Comments", BitConverter.ToString(buffer)));
            }
        }

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

        private byte[] ReadFrameIndices(GifImageDescriptor imageDescriptor)
        {
            int dataSize = this.currentStream.ReadByte();

            LzwDecoder lzwDecoder = new LzwDecoder(this.currentStream);

            byte[] indices = lzwDecoder.DecodePixels(imageDescriptor.Width, imageDescriptor.Height, dataSize);
            return indices;
        }

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

        private void ReadFrameColors(byte[] indices, byte[] colorTable, GifImageDescriptor descriptor)
        {
            int imageWidth = this.logicalScreenDescriptor.Width;
            int imageHeight = this.logicalScreenDescriptor.Height;

            if (this.currentFrame == null)
            {
                this.currentFrame = new byte[imageWidth * imageHeight * 4];
            }

            byte[] lastFrame = null;

            if (this.graphicsControlExtension != null &&
                this.graphicsControlExtension.DisposalMethod == DisposalMethod.RestoreToPrevious)
            {
                lastFrame = new byte[imageWidth * imageHeight * 4];

                Array.Copy(this.currentFrame, lastFrame, lastFrame.Length);
            }

            int offset = 0, i = 0, index = -1;

            int iPass = 0; // the interlace pass
            int iInc = 8; // the interlacing line increment
            int iY = 0; // the current interlaced line
            int writeY = 0; // the target y offset to write to

            for (int y = descriptor.Top; y < descriptor.Top + descriptor.Height; y++)
            {
                // Check if this image is interlaced.
                if (descriptor.InterlaceFlag)
                {
                    // If so then we read lines at predetermined offsets.
                    // When an entire image height worth of offset lines has been read we consider this a pass.
                    // With each pass the number of offset lines changes and the starting line changes.
                    if (iY >= descriptor.Height)
                    {
                        iPass++;
                        switch (iPass)
                        {
                            case 1:
                                iY = 4;
                                break;
                            case 2:
                                iY = 2;
                                iInc = 4;
                                break;
                            case 3:
                                iY = 1;
                                iInc = 2;
                                break;
                        }
                    }

                    writeY = iY + descriptor.Top;

                    iY += iInc;
                }
                else
                {
                    writeY = y;
                }

                for (int x = descriptor.Left; x < descriptor.Left + descriptor.Width; x++)
                {
                    offset = writeY * imageWidth + x;

                    index = indices[i];

                    if (this.graphicsControlExtension == null ||
                        this.graphicsControlExtension.TransparencyFlag == false ||
                        this.graphicsControlExtension.TransparencyIndex != index)
                    {
                        this.currentFrame[offset * 4 + 0] = colorTable[index * 3 + 2];
                        this.currentFrame[offset * 4 + 1] = colorTable[index * 3 + 1];
                        this.currentFrame[offset * 4 + 2] = colorTable[index * 3 + 0];
                        this.currentFrame[offset * 4 + 3] = (byte)255;
                    }

                    i++;
                }
            }

            byte[] pixels = new byte[imageWidth * imageHeight * 4];

            Array.Copy(this.currentFrame, pixels, pixels.Length);

            ImageBase currentImage;

            if (this.image.Pixels == null)
            {
                currentImage = this.image;
                currentImage.SetPixels(imageWidth, imageHeight, pixels);

                if (this.graphicsControlExtension != null && this.graphicsControlExtension.DelayTime > 0)
                {
                    this.image.FrameDelay = this.graphicsControlExtension.DelayTime;
                }
            }
            else
            {
                ImageFrame frame = new ImageFrame();

                currentImage = frame;
                currentImage.SetPixels(imageWidth, imageHeight, pixels);

                this.image.Frames.Add(frame);
            }

            if (this.graphicsControlExtension != null)
            {
                if (this.graphicsControlExtension.DisposalMethod == DisposalMethod.RestoreToBackground)
                {
                    for (int y = descriptor.Top; y < descriptor.Top + descriptor.Height; y++)
                    {
                        for (int x = descriptor.Left; x < descriptor.Left + descriptor.Width; x++)
                        {
                            offset = y * imageWidth + x;

                            this.currentFrame[offset * 4 + 0] = 0;
                            this.currentFrame[offset * 4 + 1] = 0;
                            this.currentFrame[offset * 4 + 2] = 0;
                            this.currentFrame[offset * 4 + 3] = 0;
                        }
                    }
                }
                else if (this.graphicsControlExtension.DisposalMethod == DisposalMethod.RestoreToPrevious)
                {
                    this.currentFrame = lastFrame;
                }
            }
        }
    }
}
