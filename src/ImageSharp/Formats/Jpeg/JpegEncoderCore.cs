// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
#nullable disable

using System.Buffers.Binary;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats.Jpeg.Components;
using SixLabors.ImageSharp.Formats.Jpeg.Components.Encoder;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Metadata.Profiles.Iptc;
using SixLabors.ImageSharp.Metadata.Profiles.Xmp;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Jpeg;

/// <summary>
/// Image encoder for writing an image to a stream as a jpeg.
/// </summary>
internal sealed unsafe partial class JpegEncoderCore
{
    /// <summary>
    /// The available encodable frame configs.
    /// </summary>
    private static readonly JpegFrameConfig[] FrameConfigs = CreateFrameConfigs();

    private readonly JpegEncoder encoder;

    /// <summary>
    /// The output stream. All attempted writes after the first error become no-ops.
    /// </summary>
    private Stream outputStream;

    /// <summary>
    /// Initializes a new instance of the <see cref="JpegEncoderCore"/> class.
    /// </summary>
    /// <param name="encoder">The parent encoder.</param>
    public JpegEncoderCore(JpegEncoder encoder)
        => this.encoder = encoder;

    public Block8x8F[] QuantizationTables { get; } = new Block8x8F[4];

    /// <summary>
    /// Encode writes the image to the jpeg baseline format with the given options.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="image">The image to write from.</param>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="cancellationToken">The token to request cancellation.</param>
    public void Encode<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Guard.NotNull(image, nameof(image));
        Guard.NotNull(stream, nameof(stream));

        if (image.Width > JpegConstants.MaxLength || image.Height > JpegConstants.MaxLength)
        {
            JpegThrowHelper.ThrowDimensionsTooLarge(image.Width, image.Height);
        }

        cancellationToken.ThrowIfCancellationRequested();

        this.outputStream = stream;
        Span<byte> buffer = stackalloc byte[20];

        ImageMetadata metadata = image.Metadata;
        JpegMetadata jpegMetadata = metadata.GetJpegMetadata();
        JpegFrameConfig frameConfig = this.GetFrameConfig(jpegMetadata);

        bool interleaved = this.encoder.Interleaved ?? jpegMetadata.Interleaved ?? true;
        using JpegFrame frame = new(image, frameConfig, interleaved);

        // Write the Start Of Image marker.
        this.WriteStartOfImage(buffer);

        // Write APP0 marker
        if (frameConfig.AdobeColorTransformMarkerFlag is null)
        {
            this.WriteJfifApplicationHeader(metadata, buffer);
        }

        // Write APP14 marker with adobe color extension
        else
        {
            this.WriteApp14Marker(frameConfig.AdobeColorTransformMarkerFlag.Value, buffer);
        }

        // Write Exif, XMP, ICC and IPTC profiles
        this.WriteProfiles(metadata, buffer);

        // Write the image dimensions.
        this.WriteStartOfFrame(image.Width, image.Height, frameConfig, buffer);

        // Write the Huffman tables.
        HuffmanScanEncoder scanEncoder = new(frame.BlocksPerMcu, stream);
        this.WriteDefineHuffmanTables(frameConfig.HuffmanTables, scanEncoder, buffer);

        // Write the quantization tables.
        this.WriteDefineQuantizationTables(frameConfig.QuantizationTables, this.encoder.Quality, jpegMetadata, buffer);

        // Write scans with actual pixel data
        using SpectralConverter<TPixel> spectralConverter = new(frame, image, this.QuantizationTables);
        this.WriteHuffmanScans(frame, frameConfig, spectralConverter, scanEncoder, buffer, cancellationToken);

        // Write the End Of Image marker.
        this.WriteEndOfImageMarker(buffer);

        stream.Flush();
    }

    /// <summary>
    /// Write the start of image marker.
    /// </summary>
    private void WriteStartOfImage(Span<byte> buffer)
    {
        // Markers are always prefixed with 0xff.
        buffer[1] = JpegConstants.Markers.SOI;
        buffer[0] = JpegConstants.Markers.XFF;

        this.outputStream.Write(buffer, 0, 2);
    }

    /// <summary>
    /// Writes the application header containing the JFIF identifier plus extra data.
    /// </summary>
    /// <param name="meta">The image metadata.</param>
    /// <param name="buffer">Temporary buffer.</param>
    private void WriteJfifApplicationHeader(ImageMetadata meta, Span<byte> buffer)
    {
        // Write the JFIF headers (highest index first to avoid additional bound checks)
        buffer[10] = 0x01; // versionlo
        buffer[0] = JpegConstants.Markers.XFF;
        buffer[1] = JpegConstants.Markers.APP0; // Application Marker
        buffer[2] = 0x00;
        buffer[3] = 0x10;
        buffer[4] = 0x4a; // J
        buffer[5] = 0x46; // F
        buffer[6] = 0x49; // I
        buffer[7] = 0x46; // F
        buffer[8] = 0x00; // = "JFIF",'\0'
        buffer[9] = 0x01; // versionhi

        // Resolution. Big Endian
        Span<byte> hResolution = buffer.Slice(12, 2);
        Span<byte> vResolution = buffer.Slice(14, 2);

        if (meta.ResolutionUnits == PixelResolutionUnit.PixelsPerMeter)
        {
            // Scale down to PPI
            buffer[11] = (byte)PixelResolutionUnit.PixelsPerInch; // xyunits
            BinaryPrimitives.WriteInt16BigEndian(hResolution, (short)Math.Round(UnitConverter.MeterToInch(meta.HorizontalResolution)));
            BinaryPrimitives.WriteInt16BigEndian(vResolution, (short)Math.Round(UnitConverter.MeterToInch(meta.VerticalResolution)));
        }
        else
        {
            // We can simply pass the value.
            buffer[11] = (byte)meta.ResolutionUnits; // xyunits
            BinaryPrimitives.WriteInt16BigEndian(hResolution, (short)Math.Round(meta.HorizontalResolution));
            BinaryPrimitives.WriteInt16BigEndian(vResolution, (short)Math.Round(meta.VerticalResolution));
        }

        // No thumbnail
        buffer[17] = 0x00; // Thumbnail height
        buffer[16] = 0x00; // Thumbnail width

        this.outputStream.Write(buffer, 0, 18);
    }

    /// <summary>
    /// Writes the Define Huffman Table marker and tables.
    /// </summary>
    /// <param name="tableConfigs">The table configuration.</param>
    /// <param name="scanEncoder">The scan encoder.</param>
    /// <param name="buffer">Temporary buffer.</param>
    /// <exception cref="ArgumentNullException"><paramref name="tableConfigs"/> is <see langword="null"/>.</exception>
    private void WriteDefineHuffmanTables(JpegHuffmanTableConfig[] tableConfigs, HuffmanScanEncoder scanEncoder, Span<byte> buffer)
    {
        if (tableConfigs is null)
        {
            throw new ArgumentNullException(nameof(tableConfigs));
        }

        int markerlen = 2;

        for (int i = 0; i < tableConfigs.Length; i++)
        {
            markerlen += 1 + 16 + tableConfigs[i].Table.Values.Length;
        }

        this.WriteMarkerHeader(JpegConstants.Markers.DHT, markerlen, buffer);
        for (int i = 0; i < tableConfigs.Length; i++)
        {
            JpegHuffmanTableConfig tableConfig = tableConfigs[i];

            int header = (tableConfig.Class << 4) | tableConfig.DestinationIndex;
            this.outputStream.WriteByte((byte)header);
            this.outputStream.Write(tableConfig.Table.Count);
            this.outputStream.Write(tableConfig.Table.Values);

            scanEncoder.BuildHuffmanTable(tableConfig);
        }
    }

    /// <summary>
    /// Writes the APP14 marker to indicate the image is in RGB color space.
    /// </summary>
    /// <param name="colorTransform">The color transform byte.</param>
    /// <param name="buffer">Temporary buffer.</param>
    private void WriteApp14Marker(byte colorTransform, Span<byte> buffer)
    {
        this.WriteMarkerHeader(JpegConstants.Markers.APP14, 2 + Components.Decoder.AdobeMarker.Length, buffer);

        // Identifier: ASCII "Adobe" (highest index first to avoid additional bound checks).
        buffer[4] = 0x65;
        buffer[0] = 0x41;
        buffer[1] = 0x64;
        buffer[2] = 0x6F;
        buffer[3] = 0x62;

        // Version, currently 100.
        BinaryPrimitives.WriteInt16BigEndian(buffer.Slice(5, 2), 100);

        // Flags0
        BinaryPrimitives.WriteInt16BigEndian(buffer.Slice(7, 2), 0);

        // Flags1
        BinaryPrimitives.WriteInt16BigEndian(buffer.Slice(9, 2), 0);

        // Color transform byte
        buffer[11] = colorTransform;

        this.outputStream.Write(buffer.Slice(0, 12));
    }

    /// <summary>
    /// Writes the EXIF profile.
    /// </summary>
    /// <param name="exifProfile">The exif profile.</param>
    /// <param name="buffer">Temporary buffer.</param>
    private void WriteExifProfile(ExifProfile exifProfile, Span<byte> buffer)
    {
        if (exifProfile is null || exifProfile.Values.Count == 0)
        {
            return;
        }

        const int maxBytesApp1 = 65533; // 64k - 2 padding bytes
        const int maxBytesWithExifId = 65527; // Max - 6 bytes for EXIF header.

        byte[] data = exifProfile.ToByteArray();

        if (data.Length == 0)
        {
            return;
        }

        // We can write up to a maximum of 64 data to the initial marker so calculate boundaries.
        int exifMarkerLength = Components.Decoder.ProfileResolver.ExifMarker.Length;
        int remaining = exifMarkerLength + data.Length;
        int bytesToWrite = remaining > maxBytesApp1 ? maxBytesApp1 : remaining;
        int app1Length = bytesToWrite + 2;

        // Write the app marker, EXIF marker, and data
        this.WriteApp1Header(app1Length, buffer);
        this.outputStream.Write(Components.Decoder.ProfileResolver.ExifMarker);
        this.outputStream.Write(data, 0, bytesToWrite - exifMarkerLength);
        remaining -= bytesToWrite;

        // If the exif data exceeds 64K, write it in multiple APP1 Markers
        for (int idx = maxBytesWithExifId; idx < data.Length; idx += maxBytesWithExifId)
        {
            bytesToWrite = remaining > maxBytesWithExifId ? maxBytesWithExifId : remaining;
            app1Length = bytesToWrite + 2 + exifMarkerLength;

            this.WriteApp1Header(app1Length, buffer);

            // Write Exif00 marker
            this.outputStream.Write(Components.Decoder.ProfileResolver.ExifMarker);

            // Write the exif data
            this.outputStream.Write(data, idx, bytesToWrite);

            remaining -= bytesToWrite;
        }
    }

    /// <summary>
    /// Writes the IPTC metadata.
    /// </summary>
    /// <param name="iptcProfile">The iptc metadata to write.</param>
    /// <param name="buffer">Temporary buffer.</param>
    /// <exception cref="ImageFormatException">
    /// Thrown if the IPTC profile size exceeds the limit of 65533 bytes.
    /// </exception>
    private void WriteIptcProfile(IptcProfile iptcProfile, Span<byte> buffer)
    {
        const int maxBytes = 65533;
        if (iptcProfile is null || !iptcProfile.Values.Any())
        {
            return;
        }

        iptcProfile.UpdateData();
        byte[] data = iptcProfile.Data;
        if (data.Length == 0)
        {
            return;
        }

        if (data.Length > maxBytes)
        {
            throw new ImageFormatException($"Iptc profile size exceeds limit of {maxBytes} bytes");
        }

        int app13Length = 2 + Components.Decoder.ProfileResolver.AdobePhotoshopApp13Marker.Length +
                          Components.Decoder.ProfileResolver.AdobeImageResourceBlockMarker.Length +
                          Components.Decoder.ProfileResolver.AdobeIptcMarker.Length +
                          2 + 4 + data.Length;
        this.WriteAppHeader(app13Length, JpegConstants.Markers.APP13, buffer);
        this.outputStream.Write(Components.Decoder.ProfileResolver.AdobePhotoshopApp13Marker);
        this.outputStream.Write(Components.Decoder.ProfileResolver.AdobeImageResourceBlockMarker);
        this.outputStream.Write(Components.Decoder.ProfileResolver.AdobeIptcMarker);
        this.outputStream.WriteByte(0); // a empty pascal string (padded to make size even)
        this.outputStream.WriteByte(0);
        BinaryPrimitives.WriteInt32BigEndian(buffer, data.Length);
        this.outputStream.Write(buffer, 0, 4);
        this.outputStream.Write(data, 0, data.Length);
    }

    /// <summary>
    /// Writes the XMP metadata.
    /// </summary>
    /// <param name="xmpProfile">The XMP metadata to write.</param>
    /// <param name="buffer">Temporary buffer.</param>
    /// <exception cref="ImageFormatException">
    /// Thrown if the XMP profile size exceeds the limit of 65533 bytes.
    /// </exception>
    private void WriteXmpProfile(XmpProfile xmpProfile, Span<byte> buffer)
    {
        if (xmpProfile is null)
        {
            return;
        }

        const int xmpOverheadLength = 29;
        const int maxBytes = 65533;
        const int maxData = maxBytes - xmpOverheadLength;

        byte[] data = xmpProfile.Data;

        if (data is null || data.Length == 0)
        {
            return;
        }

        int dataLength = data.Length;
        int offset = 0;

        while (dataLength > 0)
        {
            int length = dataLength; // Number of bytes to write.

            if (length > maxData)
            {
                length = maxData;
            }

            dataLength -= length;

            int app1Length = 2 + Components.Decoder.ProfileResolver.XmpMarker.Length + length;
            this.WriteApp1Header(app1Length, buffer);
            this.outputStream.Write(Components.Decoder.ProfileResolver.XmpMarker);
            this.outputStream.Write(data, offset, length);

            offset += length;
        }
    }

    /// <summary>
    /// Writes the App1 header.
    /// </summary>
    /// <param name="app1Length">The length of the data the app1 marker contains.</param>
    /// <param name="buffer">Temporary buffer.</param>
    private void WriteApp1Header(int app1Length, Span<byte> buffer)
        => this.WriteAppHeader(app1Length, JpegConstants.Markers.APP1, buffer);

    /// <summary>
    /// Writes a AppX header.
    /// </summary>
    /// <param name="length">The length of the data the app marker contains.</param>
    /// <param name="appMarker">The app marker to write.</param>
    /// <param name="buffer">Temporary buffer.</param>
    private void WriteAppHeader(int length, byte appMarker, Span<byte> buffer)
    {
        buffer[0] = JpegConstants.Markers.XFF;
        buffer[1] = appMarker;
        buffer[2] = (byte)((length >> 8) & 0xFF);
        buffer[3] = (byte)(length & 0xFF);

        this.outputStream.Write(buffer, 0, 4);
    }

    /// <summary>
    /// Writes the ICC profile.
    /// </summary>
    /// <param name="iccProfile">The ICC profile to write.</param>
    /// <param name="buffer">Temporary buffer.</param>
    /// <exception cref="ImageFormatException">
    /// Thrown if any of the ICC profiles size exceeds the limit.
    /// </exception>
    private void WriteIccProfile(IccProfile iccProfile, Span<byte> buffer)
    {
        if (iccProfile is null)
        {
            return;
        }

        const int iccOverheadLength = 14;
        const int maxBytes = 65533;
        const int maxData = maxBytes - iccOverheadLength;

        byte[] data = iccProfile.ToByteArray();

        if (data is null || data.Length == 0)
        {
            return;
        }

        // Calculate the number of markers we'll need, rounding up of course.
        int dataLength = data.Length;
        int count = dataLength / maxData;

        if (count * maxData != dataLength)
        {
            count++;
        }

        // Per spec, counting starts at 1.
        int current = 1;
        int offset = 0;

        while (dataLength > 0)
        {
            int length = dataLength; // Number of bytes to write.

            if (length > maxData)
            {
                length = maxData;
            }

            dataLength -= length;

            buffer[0] = JpegConstants.Markers.XFF;
            buffer[1] = JpegConstants.Markers.APP2; // Application Marker
            int markerLength = length + 16;
            buffer[2] = (byte)((markerLength >> 8) & 0xFF);
            buffer[3] = (byte)(markerLength & 0xFF);

            this.outputStream.Write(buffer, 0, 4);

            // We write the highest index first, to have only one bound check.
            buffer[13] = (byte)count; // The total number of profiles.
            buffer[12] = (byte)current; // The position within the collection.
            buffer[11] = 0x00;
            buffer[0] = (byte)'I';
            buffer[1] = (byte)'C';
            buffer[2] = (byte)'C';
            buffer[3] = (byte)'_';
            buffer[4] = (byte)'P';
            buffer[5] = (byte)'R';
            buffer[6] = (byte)'O';
            buffer[7] = (byte)'F';
            buffer[8] = (byte)'I';
            buffer[9] = (byte)'L';
            buffer[10] = (byte)'E';

            this.outputStream.Write(buffer, 0, iccOverheadLength);
            this.outputStream.Write(data, offset, length);

            current++;
            offset += length;
        }
    }

    /// <summary>
    /// Writes the metadata profiles to the image.
    /// </summary>
    /// <param name="metadata">The image metadata.</param>
    /// <param name="buffer">Temporary buffer.</param>
    private void WriteProfiles(ImageMetadata metadata, Span<byte> buffer)
    {
        if (metadata is null)
        {
            return;
        }

        // For compatibility, place the profiles in the following order:
        // - APP1 EXIF
        // - APP1 XMP
        // - APP2 ICC
        // - APP13 IPTC
        metadata.SyncProfiles();
        this.WriteExifProfile(metadata.ExifProfile, buffer);
        this.WriteXmpProfile(metadata.XmpProfile, buffer);
        this.WriteIccProfile(metadata.IccProfile, buffer);
        this.WriteIptcProfile(metadata.IptcProfile, buffer);
    }

    /// <summary>
    /// Writes the Start Of Frame (Baseline) marker.
    /// </summary>
    /// <param name="width">The frame width.</param>
    /// <param name="height">The frame height.</param>
    /// <param name="frame">The frame configuration.</param>
    /// <param name="buffer">Temporary buffer.</param>
    private void WriteStartOfFrame(int width, int height, JpegFrameConfig frame, Span<byte> buffer)
    {
        JpegComponentConfig[] components = frame.Components;

        // Length (high byte, low byte), 8 + components * 3.
        int markerlen = 8 + (3 * components.Length);
        this.WriteMarkerHeader(JpegConstants.Markers.SOF0, markerlen, buffer);
        buffer[5] = (byte)components.Length;
        buffer[0] = 8; // Data Precision. 8 for now, 12 and 16 bit jpegs not supported
        buffer[1] = (byte)(height >> 8);
        buffer[2] = (byte)(height & 0xff); // (2 bytes, Hi-Lo), must be > 0 if DNL not supported
        buffer[3] = (byte)(width >> 8);
        buffer[4] = (byte)(width & 0xff); // (2 bytes, Hi-Lo), must be > 0 if DNL not supported

        // Components data
        for (int i = 0; i < components.Length; i++)
        {
            int i3 = 3 * i;
            Span<byte> bufferSpan = buffer.Slice(i3 + 6, 3);

            // Quantization table selector
            bufferSpan[2] = (byte)components[i].QuantizatioTableIndex;

            // Sampling factors
            // 4 bits
            int samplingFactors = (components[i].HorizontalSampleFactor << 4) | components[i].VerticalSampleFactor;
            bufferSpan[1] = (byte)samplingFactors;

            // Id
            bufferSpan[0] = components[i].Id;
        }

        this.outputStream.Write(buffer, 0, (3 * (components.Length - 1)) + 9);
    }

    /// <summary>
    /// Writes the StartOfScan marker.
    /// </summary>
    /// <param name="components">The collecction of component configuration items.</param>
    /// <param name="buffer">Temporary buffer.</param>
    private void WriteStartOfScan(Span<JpegComponentConfig> components, Span<byte> buffer)
    {
        // Write the SOS (Start Of Scan) marker "\xff\xda" followed by 12 bytes:
        // - the marker length "\x00\x0c",
        // - the number of components "\x03",
        // - component 1 uses DC table 0 and AC table 0 "\x01\x00",
        // - component 2 uses DC table 1 and AC table 1 "\x02\x11",
        // - component 3 uses DC table 1 and AC table 1 "\x03\x11",
        // - the bytes "\x00\x3f\x00". Section B.2.3 of the spec says that for
        // sequential DCTs, those bytes (8-bit Ss, 8-bit Se, 4-bit Ah, 4-bit Al)
        // should be 0x00, 0x3f, 0x00&lt;&lt;4 | 0x00.
        buffer[1] = JpegConstants.Markers.SOS;
        buffer[0] = JpegConstants.Markers.XFF;

        // Length (high byte, low byte), must be 6 + 2 * (number of components in scan)
        int sosSize = 6 + (2 * components.Length);
        buffer[4] = (byte)components.Length; // Number of components in a scan
        buffer[3] = (byte)sosSize;
        buffer[2] = 0x00;

        // Components data
        for (int i = 0; i < components.Length; i++)
        {
            int i2 = 2 * i;

            // Id
            buffer[i2 + 5] = components[i].Id;

            // Table selectors
            int tableSelectors = (components[i].DcTableSelector << 4) | components[i].AcTableSelector;
            buffer[i2 + 6] = (byte)tableSelectors;
        }

        buffer[sosSize - 1] = 0x00; // Ss - Start of spectral selection.
        buffer[sosSize] = 0x3f; // Se - End of spectral selection.
        buffer[sosSize + 1] = 0x00; // Ah + Ah (Successive approximation bit position high + low)
        this.outputStream.Write(buffer, 0, sosSize + 2);
    }

    /// <summary>
    /// Writes the EndOfImage marker.
    /// </summary>
    /// <param name="buffer">Temporary buffer.</param>
    private void WriteEndOfImageMarker(Span<byte> buffer)
    {
        buffer[1] = JpegConstants.Markers.EOI;
        buffer[0] = JpegConstants.Markers.XFF;
        this.outputStream.Write(buffer, 0, 2);
    }

    /// <summary>
    /// Writes scans for given config.
    /// </summary>
    /// <typeparam name="TPixel">The type of pixel format.</typeparam>
    /// <param name="frame">The current frame.</param>
    /// <param name="frameConfig">The frame configuration.</param>
    /// <param name="spectralConverter">The spectral converter.</param>
    /// <param name="encoder">The scan encoder.</param>
    /// <param name="buffer">Temporary buffer.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    private void WriteHuffmanScans<TPixel>(
        JpegFrame frame,
        JpegFrameConfig frameConfig,
        SpectralConverter<TPixel> spectralConverter,
        HuffmanScanEncoder encoder,
        Span<byte> buffer,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (frame.Components.Length == 1)
        {
            frame.AllocateComponents(fullScan: false);

            this.WriteStartOfScan(frameConfig.Components, buffer);
            encoder.EncodeScanBaselineSingleComponent(frame.Components[0], spectralConverter, cancellationToken);
        }
        else if (frame.Interleaved)
        {
            frame.AllocateComponents(fullScan: false);

            this.WriteStartOfScan(frameConfig.Components, buffer);
            encoder.EncodeScanBaselineInterleaved(frameConfig.EncodingColor, frame, spectralConverter, cancellationToken);
        }
        else
        {
            frame.AllocateComponents(fullScan: true);
            spectralConverter.ConvertFull();

            Span<JpegComponentConfig> components = frameConfig.Components;
            for (int i = 0; i < frame.Components.Length; i++)
            {
                this.WriteStartOfScan(components.Slice(i, 1), buffer);
                encoder.EncodeScanBaseline(frame.Components[i], cancellationToken);
            }
        }
    }

    /// <summary>
    /// Writes the header for a marker with the given length.
    /// </summary>
    /// <param name="marker">The marker to write.</param>
    /// <param name="length">The marker length.</param>
    /// <param name="buffer">Temporary buffer.</param>
    private void WriteMarkerHeader(byte marker, int length, Span<byte> buffer)
    {
        // Markers are always prefixed with 0xff.
        buffer[3] = (byte)(length & 0xff);
        buffer[2] = (byte)(length >> 8);
        buffer[1] = marker;
        buffer[0] = JpegConstants.Markers.XFF;

        this.outputStream.Write(buffer, 0, 4);
    }

    /// <summary>
    /// Writes the Define Quantization Marker and prepares tables for encoding.
    /// </summary>
    /// <remarks>
    /// We take quality values in a hierarchical order:
    /// <list type = "number" >
    ///     <item>Check if encoder has set quality.</item>
    ///     <item>Check if metadata has set quality.</item>
    ///     <item>Take default quality value from <see cref="Quantization.DefaultQualityFactor"/></item>
    /// </list>
    /// </remarks>
    /// <param name="configs">Quantization tables configs.</param>
    /// <param name="optionsQuality">Optional quality value from the options.</param>
    /// <param name="metadata">Jpeg metadata instance.</param>
    /// <param name="tmpBuffer">Temporary buffer.</param>
    private void WriteDefineQuantizationTables(JpegQuantizationTableConfig[] configs, int? optionsQuality, JpegMetadata metadata, Span<byte> tmpBuffer)
    {
        int dataLen = configs.Length * (1 + Block8x8.Size);

        // Marker + quantization table lengths.
        int markerlen = 2 + dataLen;
        this.WriteMarkerHeader(JpegConstants.Markers.DQT, markerlen, tmpBuffer);

        Span<byte> buffer = dataLen <= 256 ? stackalloc byte[dataLen] : new byte[dataLen];
        int offset = 0;

        Block8x8F workspaceBlock = default;

        for (int i = 0; i < configs.Length; i++)
        {
            JpegQuantizationTableConfig config = configs[i];

            int quality = GetQualityForTable(config.DestinationIndex, optionsQuality, metadata);
            Block8x8 scaledTable = Quantization.ScaleQuantizationTable(quality, config.Table);

            // write to the output stream
            buffer[offset++] = (byte)config.DestinationIndex;

            for (int j = 0; j < Block8x8.Size; j++)
            {
                buffer[offset++] = (byte)(uint)scaledTable[ZigZag.ZigZagOrder[j]];
            }

            // apply FDCT multipliers and inject to the destination index
            workspaceBlock.LoadFrom(ref scaledTable);
            FloatingPointDCT.AdjustToFDCT(ref workspaceBlock);

            this.QuantizationTables[config.DestinationIndex] = workspaceBlock;
        }

        // write filled buffer to the stream
        this.outputStream.Write(buffer);

        static int GetQualityForTable(int destIndex, int? encoderQuality, JpegMetadata metadata) => destIndex switch
        {
            0 => encoderQuality ?? metadata.LuminanceQuality ?? Quantization.DefaultQualityFactor,
            1 => encoderQuality ?? metadata.ChrominanceQuality ?? Quantization.DefaultQualityFactor,
            _ => encoderQuality ?? metadata.Quality,
        };
    }

    private JpegFrameConfig GetFrameConfig(JpegMetadata metadata)
    {
        JpegEncodingColor color = this.encoder.ColorType ?? metadata.ColorType ?? JpegEncodingColor.YCbCrRatio420;
        JpegFrameConfig frameConfig = Array.Find(
            FrameConfigs,
            cfg => cfg.EncodingColor == color);

        if (frameConfig == null)
        {
            throw new ArgumentException(nameof(color));
        }

        return frameConfig;
    }
}
