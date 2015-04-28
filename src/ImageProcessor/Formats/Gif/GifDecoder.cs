namespace ImageProcessor.Formats
{
    using System;
    using System.IO;

    public class GifDecoder : IImageDecoder
    {
        public static int MaxCommentLength = 1024 * 8;

        public int HeaderSize
        {
            get
            {
                return 6;
            }
        }

        public bool IsSupportedFileExtension(string extension)
        {
            if (extension.StartsWith(".")) extension = extension.Substring(1);
            return extension.Equals("GIF", StringComparison.OrdinalIgnoreCase);
        }

        public bool IsSupportedFileFormat(byte[] header)
        {
            return header.Length >= 6 &&
                   header[0] == 0x47 && // G
                   header[1] == 0x49 && // I
                   header[2] == 0x46 && // F
                   header[3] == 0x38 && // 8
                  (header[4] == 0x39 || header[4] == 0x37) && // 9 or 7
                   header[5] == 0x61;   // a
        }

        public void Decode(Image image, Stream stream)
        {
            new GifDecoderCore().Decode(image, stream);
        }

        class GifDecoderCore
        {
            private const byte ExtensionIntroducer = 0x21;
            private const byte Terminator = 0;
            private const byte ImageLabel = 0x2C;
            private const byte EndIntroducer = 0x3B;
            private const byte ApplicationExtensionLabel = 0xFF;
            private const byte CommentLabel = 0xFE;
            private const byte ImageDescriptorLabel = 0x2C;
            private const byte PlainTextLabel = 0x01;
            private const byte GraphicControlLabel = 0xF9;

            private Image _image;
            private Stream _stream;
            private GifLogicalScreenDescriptor _descriptor;
            private byte[] _globalColorTable;
            private byte[] _currentFrame;
            private GifGraphicsControlExtension _graphicsControl;

            public void Decode(Image image, Stream stream)
            {
                _image = image;

                _stream = stream;
                _stream.Seek(6, SeekOrigin.Current);

                ReadLogicalScreenDescriptor();

                if (_descriptor.GlobalColorTableFlag == true)
                {
                    _globalColorTable = new byte[_descriptor.GlobalColorTableSize * 3];

                    // Read the global color table from the stream
                    stream.Read(_globalColorTable, 0, _globalColorTable.Length);
                }

                int nextFlag = stream.ReadByte();
                while (nextFlag != 0)
                {
                    if (nextFlag == ImageLabel)
                    {
                        ReadFrame();
                    }
                    else if (nextFlag == ExtensionIntroducer)
                    {
                        int gcl = stream.ReadByte();
                        switch (gcl)
                        {
                            case GraphicControlLabel:
                                ReadGraphicalControlExtension();
                                break;
                            case CommentLabel:
                                ReadComments();
                                break;
                            case ApplicationExtensionLabel:
                                Skip(12);
                                break;
                            case PlainTextLabel:
                                Skip(13);
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

                _stream.Read(buffer, 0, buffer.Length);

                byte packed = buffer[1];

                _graphicsControl = new GifGraphicsControlExtension();
                _graphicsControl.DelayTime = BitConverter.ToInt16(buffer, 2);
                _graphicsControl.TransparencyIndex = buffer[4];
                _graphicsControl.TransparencyFlag = (packed & 0x01) == 1;
                _graphicsControl.DisposalMethod = (DisposalMethod)((packed & 0x1C) >> 2);
            }

            private GifImageDescriptor ReadImageDescriptor()
            {
                byte[] buffer = new byte[9];

                _stream.Read(buffer, 0, buffer.Length);

                byte packed = buffer[8];

                GifImageDescriptor imageDescriptor = new GifImageDescriptor();
                imageDescriptor.Left = BitConverter.ToInt16(buffer, 0);
                imageDescriptor.Top = BitConverter.ToInt16(buffer, 2);
                imageDescriptor.Width = BitConverter.ToInt16(buffer, 4);
                imageDescriptor.Height = BitConverter.ToInt16(buffer, 6);
                imageDescriptor.LocalColorTableFlag = ((packed & 0x80) >> 7) == 1;
                imageDescriptor.LocalColorTableSize = 2 << (packed & 0x07);
                imageDescriptor.InterlaceFlag = ((packed & 0x40) >> 6) == 1;

                return imageDescriptor;
            }

            private void ReadLogicalScreenDescriptor()
            {
                byte[] buffer = new byte[7];

                _stream.Read(buffer, 0, buffer.Length);

                byte packed = buffer[4];

                _descriptor = new GifLogicalScreenDescriptor();
                _descriptor.Width = BitConverter.ToInt16(buffer, 0);
                _descriptor.Height = BitConverter.ToInt16(buffer, 2);
                _descriptor.Background = buffer[5];
                _descriptor.GlobalColorTableFlag = ((packed & 0x80) >> 7) == 1;
                _descriptor.GlobalColorTableSize = 2 << (packed & 0x07);

                if (_descriptor.GlobalColorTableSize > 255 * 4)
                {
                    throw new ImageFormatException(string.Format("Invalid gif colormap size '{0}'", _descriptor.GlobalColorTableSize));
                }

                if (_descriptor.Width > ImageBase.MaxWidth || _descriptor.Height > ImageBase.MaxHeight)
                {
                    throw new ArgumentOutOfRangeException(
                        string.Format(
                            "The input gif '{0}x{1}' is bigger then the max allowed size '{2}x{3}'",
                            _descriptor.Width,
                            _descriptor.Height,
                            ImageBase.MaxWidth,
                            ImageBase.MaxHeight));
                }
            }

            private void Skip(int length)
            {
                _stream.Seek(length, SeekOrigin.Current);

                int flag = 0;

                while ((flag = _stream.ReadByte()) != 0)
                {
                    _stream.Seek(flag, SeekOrigin.Current);
                }
            }

            private void ReadComments()
            {
                int flag = 0;

                while ((flag = _stream.ReadByte()) != 0)
                {
                    if (flag > MaxCommentLength)
                    {
                        throw new ImageFormatException(string.Format("Gif comment length '{0}' exceeds max '{1}'", flag, MaxCommentLength));
                    }

                    byte[] buffer = new byte[flag];

                    _stream.Read(buffer, 0, flag);

                    _image.Properties.Add(new ImageProperty("Comments", BitConverter.ToString(buffer)));
                }
            }

            private void ReadFrame()
            {
                GifImageDescriptor imageDescriptor = ReadImageDescriptor();

                byte[] localColorTable = ReadFrameLocalColorTable(imageDescriptor);

                byte[] indices = ReadFrameIndices(imageDescriptor);

                // Determine the color table for this frame. If there is a local one, use it
                // otherwise use the global color table.
                byte[] colorTable = localColorTable ?? this._globalColorTable;

                ReadFrameColors(indices, colorTable, imageDescriptor);

                Skip(0); // skip any remaining blocks
            }

            private byte[] ReadFrameIndices(GifImageDescriptor imageDescriptor)
            {
                int dataSize = _stream.ReadByte();

                LzwDecoder lzwDecoder = new LzwDecoder(_stream);

                byte[] indices = lzwDecoder.DecodePixels(imageDescriptor.Width, imageDescriptor.Height, dataSize);
                return indices;
            }

            private byte[] ReadFrameLocalColorTable(GifImageDescriptor imageDescriptor)
            {
                byte[] localColorTable = null;

                if (imageDescriptor.LocalColorTableFlag)
                {
                    localColorTable = new byte[imageDescriptor.LocalColorTableSize * 3];

                    _stream.Read(localColorTable, 0, localColorTable.Length);
                }

                return localColorTable;
            }

            private void ReadFrameColors(byte[] indices, byte[] colorTable, GifImageDescriptor descriptor)
            {
                int imageWidth = _descriptor.Width;
                int imageHeight = _descriptor.Height;

                if (_currentFrame == null)
                {
                    _currentFrame = new byte[imageWidth * imageHeight * 4];
                }

                byte[] lastFrame = null;

                if (_graphicsControl != null &&
                    _graphicsControl.DisposalMethod == DisposalMethod.RestoreToPrevious)
                {
                    lastFrame = new byte[imageWidth * imageHeight * 4];

                    Array.Copy(_currentFrame, lastFrame, lastFrame.Length);
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

                        if (_graphicsControl == null ||
                            _graphicsControl.TransparencyFlag == false ||
                            _graphicsControl.TransparencyIndex != index)
                        {
                            _currentFrame[offset * 4 + 0] = colorTable[index * 3 + 2];
                            _currentFrame[offset * 4 + 1] = colorTable[index * 3 + 1];
                            _currentFrame[offset * 4 + 2] = colorTable[index * 3 + 0];
                            _currentFrame[offset * 4 + 3] = (byte)255;
                        }

                        i++;
                    }
                }

                byte[] pixels = new byte[imageWidth * imageHeight * 4];

                Array.Copy(_currentFrame, pixels, pixels.Length);

                ImageBase currentImage = null;

                if (_image.Pixels == null)
                {
                    currentImage = _image;
                    currentImage.SetPixels(imageWidth, imageHeight, pixels);

                    if (_graphicsControl != null && _graphicsControl.DelayTime > 0)
                    {
                        _image.FrameDelay = _graphicsControl.DelayTime;
                    }
                }
                else
                {
                    ImageFrame frame = new ImageFrame();

                    currentImage = frame;
                    currentImage.SetPixels(imageWidth, imageHeight, pixels);

                    _image.Frames.Add(frame);
                }

                if (_graphicsControl != null)
                {
                    if (_graphicsControl.DisposalMethod == DisposalMethod.RestoreToBackground)
                    {
                        for (int y = descriptor.Top; y < descriptor.Top + descriptor.Height; y++)
                        {
                            for (int x = descriptor.Left; x < descriptor.Left + descriptor.Width; x++)
                            {
                                offset = y * imageWidth + x;

                                _currentFrame[offset * 4 + 0] = 0;
                                _currentFrame[offset * 4 + 1] = 0;
                                _currentFrame[offset * 4 + 2] = 0;
                                _currentFrame[offset * 4 + 3] = 0;
                            }
                        }
                    }
                    else if (_graphicsControl.DisposalMethod == DisposalMethod.RestoreToPrevious)
                    {
                        _currentFrame = lastFrame;
                    }
                }
            }
        }
    }
}
