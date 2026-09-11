// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jxl.IO.Jpeg;
using SixLabors.ImageSharp.Formats.Jxl.IO.Jpeg.Data;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

/// <summary>
/// Decodes JPEG XL files to JPEG.
/// </summary>
internal class JxlToJpegDecoder
{
    /// <summary>
    /// Content of the most recently parsed JPEG reconstruction box if any.
    /// </summary>
    private MemoryStream buffer = new();

    /// <summary>
    /// Decoded content of the most recently parsed JPEG reconstruction box
    /// is stored here.
    /// </summary>
    private readonly JpegData jpegData = new();

    /// <summary>
    /// True if the JPEG reconstruction box has undefined size (keeps going
    /// till End Of File).
    /// </summary>
    private bool boxUntilEof;

    /// <summary>
    /// Size of the most recently parsed JPEG reconstruction box contents.
    /// </summary>
    private long boxSize;

    /// <summary>
    /// Gets a value indicating whether the JPEG reconstruction output
    /// has been provided.
    /// </summary>
    public bool IsOutputSet => this.Output is not null;

    /// <summary>
    /// Gets a value indicating whether JPEG box was parsed.
    /// </summary>
    public bool IsParsingBox { get; private set; }

    /// <summary>
    /// Gets or sets a stream where JPEG reconstruction output will be written to.
    /// </summary>
    public Stream? Output { get; set; }

    public JpegData GetJpegData() => this.jpegData;

    public void StartBox(bool boxUntilEof, long contentsSize)
    {
        this.buffer.Dispose();
        this.buffer = new MemoryStream();
        this.IsParsingBox = true;

        if (boxUntilEof)
        {
            this.boxUntilEof = true;
        }
        else
        {
            this.boxSize = contentsSize;
        }
    }

    public void SetImageBundleJpegData(JxlImageBundle bundle)
    {
        if (this.IsOutputSet && this.jpegData is not null)
        {
            JpegData.SetJpegDataFromIcc(bundle.Metadata!.ColorEncoding!.Icc, this.jpegData);
            bundle.JpegData = this.jpegData;
        }
    }

    public void WriteOutput(JpegData jpegData)
    {
        long Write(Span<byte> buf)
        {
            this.Output!.Write(buf);
            return buf.Length;
        }

        if (!JpegWriter.WriteJpeg(jpegData, Write))
        {
            throw new InvalidOperationException("Couldn't emit JPEG data");
        }
    }

    /// <summary>
    /// Returns the number of EXIF markers in the JPEG file.
    /// </summary>
    /// <param name="jpegData">Input parsed JPEG data</param>
    /// <returns>Number of EXIFs in the JPEG</returns>
    public static int NumExifMarkers(JpegData jpegData) => jpegData.AppMarkerTypes.Count(x => x == JpegAppMarkerType.Exif);

    /// <summary>
    /// Returns the number of XMP markers in the JPEG file.
    /// </summary>
    /// <param name="jpegData">Input parsed JPEG data</param>
    /// <returns>Number of XMPs in the JPEG</returns>
    public static int NumXmpMarkers(JpegData jpegData) => jpegData.AppMarkerTypes.Count(x => x == JpegAppMarkerType.Xmp);

    /// <summary>
    /// Attempts to set EXIF data in the JPEG file.
    /// </summary>
    /// <param name="data">EXIF data</param>
    /// <param name="jpegData">JPEG file for EXIF data</param>
    /// <returns>If EXIF data was set, true; returns false if no EXIF marker is present, or is present but not enough data</returns>
    public static bool TrySetExif(Span<byte> data, JpegData jpegData)
    {
        int size = data.Length;
        ReadOnlySpan<byte> exifTag = JpegDataConstants.ExifTag;
        int exifTagSize = exifTag.Length;

        for (int i = 0; i < jpegData.AppData.Count; ++i)
        {
            if (jpegData.AppMarkerTypes[i] == JpegAppMarkerType.Exif)
            {
                Span<byte> dataSpan = CollectionsMarshal.AsSpan(jpegData.AppData[i]);

                if (dataSpan.Length != size + 3 + exifTagSize - 4)
                {
                    return false;
                }

                // The first 9 bytes are used for JPEG marker header.
                dataSpan[0] = 0xE1;

                // The second and third byte are already filled in correctly
                exifTag.CopyTo(dataSpan[3..]);

                // The first 4 bytes are the TIFF header from the box contents, and are
                // not included in the JPEG
                data[4..].CopyTo(dataSpan[(3 + exifTagSize)..]);

                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Attempts to set XMP data in the JPEG file.
    /// </summary>
    /// <param name="data">XMP data</param>
    /// <param name="jpegData">JPEG file for XMP data</param>
    /// <returns>If XMP data was set, true; returns false if no XMP marker is present, or is present but not enough data</returns>
    public static bool TrySetXmp(Span<byte> data, JpegData jpegData)
    {
        int size = data.Length;
        ReadOnlySpan<byte> xmpTag = JpegDataConstants.XmpTag;
        int xmpTagSize = xmpTag.Length;

        for (int i = 0; i < jpegData.AppData.Count; ++i)
        {
            if (jpegData.AppMarkerTypes[i] == JpegAppMarkerType.Xmp)
            {
                Span<byte> dataSpan = CollectionsMarshal.AsSpan(jpegData.AppData[i]);

                if (dataSpan.Length != size + 3 + xmpTagSize)
                {
                    return false;
                }

                // The first 9 bytes are used for JPEG marker header.
                dataSpan[0] = 0xE1;

                // The second and third byte are already filled in correctly
                xmpTag.CopyTo(dataSpan[3..]);

                data.CopyTo(dataSpan[(3 + xmpTagSize)..]);

                return true;
            }
        }

        return false;
    }

    public static bool ExifBoxContentSize(JpegData jpegData, ref long size)
    {
        size = 0;
        int exifTagLength = JpegDataConstants.ExifTag.Length;

        for (int i = 0; i < jpegData.AppData.Count; ++i)
        {
            if (jpegData.AppMarkerTypes[i] == JpegAppMarkerType.Exif)
            {
                if (jpegData.AppData[i].Count < 3 + exifTagLength)
                {
                    // too small for app marker header
                    return false;
                }

                // The first 4 bytes are the TIFF header from the box contents, and are
                // not included in the JPEG
                size = jpegData.AppData[i].Count + 4 - 3 - exifTagLength;
                return true;
            }
        }

        return false;
    }

    public static bool XmpBoxContentSize(JpegData jpegData, ref long size)
    {
        size = 0;
        int xmpTagLength = JpegDataConstants.XmpTag.Length;

        for (int i = 0; i < jpegData.AppData.Count; ++i)
        {
            if (jpegData.AppMarkerTypes[i] == JpegAppMarkerType.Xmp)
            {
                if (jpegData.AppData[i].Count < 3 + xmpTagLength)
                {
                    // too small for app marker header
                    return false;
                }

                size = jpegData.AppData[i].Count - 3 - xmpTagLength;
                return true;
            }
        }

        return false;
    }
}
