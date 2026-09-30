// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Cms;
using SixLabors.ImageSharp.Formats.Jxl.IO;
using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder;

internal class JxlExternalImageEncoder
{
    private static int JxlDataTypeBytes(JxlDataType dataType) => dataType switch
    {
        JxlDataType.Byte => 1,
        JxlDataType.UInt16 => 2,
        JxlDataType.Half => 2,
        JxlDataType.Single => 4,
        _ => 0
    };

    private static void ConvertFromExternalPlaneNoSizeCheck(Configuration configuration, Memory<byte> data, int xSize, int ySize, int stride, int bitsPerSample, JxlPixelFormat format, int c, JxlImageF channel)
    {
        if (format.DataType == JxlDataType.Byte)
        {
            if (bitsPerSample is 0 or > 8)
            {
                throw new InvalidOperationException("Invalid bits per sample.");
            }
        }
        else if (format.DataType == JxlDataType.UInt16)
        {
            if (bitsPerSample is <= 8 or > 16)
            {
                throw new InvalidOperationException("Invalid bits per sample.");
            }
        }
        else if (format.DataType is not JxlDataType.Half and not JxlDataType.Single)
        {
            throw new NotSupportedException($"Unsupported pixel format data type {format.DataType}.");
        }

        if (channel.XSize != xSize || channel.YSize != ySize)
        {
            throw new InvalidOperationException("Channel dimensions do not match.");
        }

        int bytesPerChannel = JxlDataTypeBytes(format.DataType);
        int bytesPerPixel = format.Channels * bytesPerChannel;

        if (xSize > int.MaxValue / bytesPerPixel)
        {
            throw new InvalidOperationException("Image dimensions are too large.");
        }

        int bytesPerRow = xSize * bytesPerPixel;

        if (bytesPerRow > stride)
        {
            throw new InvalidOperationException("Row stride is too small.");
        }

        int pixelOffset = c * bytesPerChannel;
        float scale = 1.0F / ((1 << bitsPerSample) - 1);

        bool littleEndian = format.Endianness == ByteOrder.LittleEndian;

        void ConvertRow(int y)
        {
            int offset = (y * stride) + pixelOffset;
            Memory<float> rowOut = channel.GetRowMemory(y);

            bool SaveValue(int index, float value)
            {
                rowOut.Span[index] = value;
                return true;
            }

            LoadFloatRow(data[offset..], xSize, bytesPerPixel, format.DataType, littleEndian, scale, SaveValue);
        }

        _ = Parallel.For(0, ySize, configuration.GetParallelOptions(), ConvertRow);
    }

    private static bool ConvertFromExternal(Configuration configuration, Memory<byte> data, int xSize, int ySize, int bitsPerSample, JxlPixelFormat format, int c, JxlPlane<float> channel)
    {
        int bytesPerChannel = JxlDataTypeBytes(format.DataType);

        if ((ulong)format.Channels > ulong.MaxValue / (ulong)bytesPerChannel)
        {
            throw new InvalidOperationException("Invalid format.");
        }

        ulong bytesPerPixel = (ulong)format.Channels * (ulong)bytesPerChannel;

        if ((ulong)xSize > ulong.MaxValue / bytesPerPixel)
        {
            throw new InvalidOperationException("Image dimensions are too large.");
        }

        ulong lastRowSize = (ulong)xSize * bytesPerPixel;

        if (!JxlMath.SafeRoundUpTo(lastRowSize, format.Align, out ulong rowSize))
        {
            throw new InvalidOperationException("Image dimensions are too large.");
        }

        if (xSize == 0 || ySize == 0)
        {
            throw new InvalidOperationException("Empty image.");
        }

        if (!JxlMath.SafeMultiply((int)rowSize, ySize - 1, out int bytesToRead) ||
            !JxlMath.SafeAdd(bytesToRead, (int)lastRowSize, out bytesToRead))
        {
            throw new InvalidOperationException("Image dimensions are too large.");
        }

        if (data.Length < bytesToRead)
        {
            throw new ArgumentException($"Buffer size is too small, expected: {bytesToRead} got: {data.Length}.");
        }

        if ((ulong)data.Length > rowSize * (ulong)ySize)
        {
            throw new ArgumentException("Buffer size is too large.");
        }

        return ConvertFromExternalPlaneNoSizeCheck(configuration, data, (ulong)xSize, ySize, rowSize, bitsPerSample, format, c, channel);
    }

    public static bool ConvertFromExternal(Configuration configuration, Memory<byte> bytes, int xSize, int ySize, JxlColorEncoding currentColorEncoding, int bitsPerSample, JxlPixelFormat format, JxlImageBundle imageBundle, bool setAlpha)
    {
        int colorChannels = currentColorEncoding.Channels;
        bool hasAlpha = format.Channels is 2 or 4;

        if (format.Channels < colorChannels)
        {
            throw new InvalidOperationException($"Expected {colorChannels} color channels, received only {format.Channels} channels.");
        }

        JxlImage3F color = new(configuration, xSize, ySize);

        for (int c = 0; c < colorChannels; c++)
        {
            if (!ConvertFromExternal(configuration, bytes, xSize, ySize, bitsPerSample, format, c, color.Plane(c)))
            {
                return false;
            }
        }

        if (colorChannels == 1)
        {
            if (!JxlImageOperations.CopyImage(color.Plane(0), color.Plane(1)))
            {
                throw new InvalidOperationException("Failed to copy plane");
            }

            if (!JxlImageOperations.CopyImage(color.Plane(0), color.Plane(2)))
            {
                throw new InvalidOperationException("Failed to copy plane");
            }
        }

        if (!imageBundle.SetFromImage(color, currentColorEncoding))
        {
            throw new InvalidOperationException("Could not set image bundle");
        }

        if (setAlpha)
        {
            JxlImageF alpha = new(configuration, xSize, ySize);

            if (hasAlpha)
            {
                if (!ConvertFromExternal(configuration, bytes, xSize, ySize, bitsPerSample, format, format.Channels - 1, alpha))
                {
                    return false;
                }
            }
            else
            {
                JxlImageOperations.FillImage(1.0F, alpha);
            }

            if (!imageBundle.TrySetAlpha(alpha))
            {
                throw new InvalidOperationException("Failed to set alpha channel");
            }
        }

        return true;
    }

    public static bool BufferToImageF(Configuration configuration, JxlPixelFormat pixelFormat, int xSize, int ySize, Memory<byte> buffer, JxlImageF channel)
    {
        int bitDepth = JxlDataTypeBytes(pixelFormat.DataType) * 8;
        return ConvertFromExternal(configuration, buffer, xSize, ySize, bitDepth, pixelFormat, 0, channel);
    }

    public static bool BufferToImageBundle(Configuration configuration, JxlPixelFormat pixelFormat, int xSize, int ySize, Memory<byte> buffer, JxlColorEncoding currentColorEncoding, JxlImageBundle imageBundle, bool setAlpha)
    {
        int bitDepth = JxlDataTypeBytes(pixelFormat.DataType) * 8;

        if (!ConvertFromExternal(configuration, buffer, xSize, ySize, currentColorEncoding, bitDepth, pixelFormat, imageBundle, setAlpha))
        {
            return false;
        }

        return imageBundle.VerifyMetadata();
    }
}
