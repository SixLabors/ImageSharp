// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff;

/// <summary>
/// Decodes hand-built TIFF files with unusual but valid strip and directory layouts, very large strips, and malformed data.
/// </summary>
[Collection("RunSerial")]
[Trait("Format", "Tiff")]
[ValidateDisposedMemoryAllocations]
public class TiffDecoderStripLayoutTests
{
    /// <summary>
    /// A missing RowsPerStrip, or one larger than the image height, means the whole image is one strip,
    /// for both chunky and planar data.
    /// </summary>
    /// <param name="planar">Whether the samples are stored planar.</param>
    /// <param name="width">The image width.</param>
    /// <param name="rowsPerStrip">The RowsPerStrip value, or <see langword="null"/> to omit the entry.</param>
    [Theory]
    [InlineData(false, 4000, null)]
    [InlineData(false, 4000, 100000u)]
    [InlineData(false, 4000, uint.MaxValue)]
    [InlineData(true, 4, null)]
    [InlineData(true, 4, 2u)]
    [InlineData(true, 4000, null)]
    [InlineData(true, 4000, 100000u)]
    [InlineData(true, 4000, uint.MaxValue)]
    public void Decode_RowsPerStripMissingOrAboveHeight_DecodesAllRows(bool planar, int width, uint? rowsPerStrip)
    {
        const int height = 2;
        TiffPage page = TiffPage.Create(width, height, [8, 8, 8], TiffPhotometricInterpretation.Rgb, planar, rowsPerStrip, CreateRgbStrips(width, height, 1, planar, height));

        using Image<Rgb24> image = Decode<Rgb24>(BuildTiff([page]));

        AssertRgb24Pixels(image);
    }

    /// <summary>
    /// An uncompressed strip larger than the single buffer allocation limit is read in rows, so the image
    /// decodes whenever its pixel buffer fits in memory, with the same pixels as without the limit.
    /// </summary>
    /// <param name="planar">Whether the samples are stored planar.</param>
    /// <param name="rowsPerStrip">The number of rows per strip.</param>
    [Theory]
    [InlineData(false, 700)]
    [InlineData(false, 600)]
    [InlineData(true, 700)]
    [InlineData(true, 600)]
    public void Decode_UncompressedStripAboveBufferLimit_DecodesAllRows(bool planar, int rowsPerStrip)
    {
        // With 3 x 16-bit samples, a strip of 600 rows needs 3.7 MB (1.2 MB per plane), more than the 1 MB
        // allocation limit, while the decoded L8 image needs only 0.7 MB.
        const int width = 1024;
        const int height = 700;
        TiffPage page = TiffPage.Create(width, height, [16, 16, 16], TiffPhotometricInterpretation.Rgb, planar, (uint)rowsPerStrip, CreateRgbStrips(width, height, 2, planar, rowsPerStrip));
        byte[] tiff = BuildTiff([page]);

        using Image<L8> expected = Decode<L8>(tiff);
        using Image<L8> image = Decode<L8>(tiff, CreateConfiguration(1));

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (expected[x, y] != image[x, y])
                {
                    Assert.Fail($"Pixel ({x}, {y}) is {image[x, y]}, expected {expected[x, y]}.");
                }
            }
        }
    }

    /// <summary>
    /// A frame that does not fit in the allocation limit reports its real dimensions.
    /// </summary>
    [Fact]
    public void Decode_FrameAboveAllocationLimit_ThrowsWithRealDimensions()
    {
        TiffPage page = TiffPage.Create(20000, 15000, [16, 16, 16, 16], TiffPhotometricInterpretation.Rgb, false, null, [new byte[4096]]);

        InvalidImageContentException ex = Assert.Throws<InvalidImageContentException>(() => Decode<L8>(BuildTiff([page]), CreateConfiguration(100)).Dispose());

        Assert.Contains("20000x15000", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// TIFF allows directories at any file offset, so a chain that goes backwards in the file is valid.
    /// </summary>
    [Fact]
    public void Decode_DirectoriesStoredInReverseFileOrder_DecodesAllFrames()
    {
        TiffPage first = TiffPage.Create(2, 2, [8], TiffPhotometricInterpretation.BlackIsZero, false, 2, [[10, 11, 12, 13]]);
        TiffPage second = TiffPage.Create(2, 2, [8], TiffPhotometricInterpretation.BlackIsZero, false, 2, [[20, 21, 22, 23]]);

        using Image<L8> image = Decode<L8>(BuildTiff([first, second], reverseIfdOrder: true));

        Assert.Equal(2, image.Frames.Count);
        Assert.Equal(10, image.Frames[0][0, 0].PackedValue);
        Assert.Equal(13, image.Frames[0][1, 1].PackedValue);
        Assert.Equal(20, image.Frames[1][0, 0].PackedValue);
        Assert.Equal(23, image.Frames[1][1, 1].PackedValue);
    }

    /// <summary>
    /// A directory chain that returns to a directory it has already read is a loop and fails fast.
    /// </summary>
    /// <param name="pageCount">The number of directories in the loop.</param>
    /// <param name="reverseIfdOrder">Whether the directories are stored in reverse file order.</param>
    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    public void Decode_DirectoryChainRevisitsDirectory_ThrowsImageFormatException(int pageCount, bool reverseIfdOrder)
    {
        TiffPage[] pages = new TiffPage[pageCount];
        for (int i = 0; i < pageCount; i++)
        {
            pages[i] = TiffPage.Create(2, 2, [8], TiffPhotometricInterpretation.BlackIsZero, false, 2, [new byte[4]]);
        }

        byte[] tiff = BuildTiff(pages, reverseIfdOrder, loopToFirstIfd: true);

        ImageFormatException ex = Assert.Throws<ImageFormatException>(() => Decode<L8>(tiff).Dispose());
        Assert.Contains("circular", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Gets the expected value of a sample of the synthesised images.
    /// </summary>
    /// <param name="x">The x coordinate.</param>
    /// <param name="y">The y coordinate.</param>
    /// <param name="channel">The channel index.</param>
    /// <returns>The sample value.</returns>
    private static byte Sample(int x, int y, int channel) => (byte)((x * 7) + (y * 101) + (channel * 53));

    /// <summary>
    /// Asserts that every pixel of the image matches <see cref="Sample(int, int, int)"/>.
    /// </summary>
    /// <param name="image">The decoded image.</param>
    private static void AssertRgb24Pixels(Image<Rgb24> image)
    {
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                Rgb24 expected = new(Sample(x, y, 0), Sample(x, y, 1), Sample(x, y, 2));
                if (!expected.Equals(image[x, y]))
                {
                    Assert.Fail($"Pixel ({x}, {y}) is {image[x, y]}, expected {expected}.");
                }
            }
        }
    }

    /// <summary>
    /// Creates the strip data of an RGB image whose samples are <see cref="Sample(int, int, int)"/>, stored as
    /// 8-bit samples, or as 16-bit samples holding the same value in both bytes. Planar strips are ordered by plane.
    /// </summary>
    /// <param name="width">The image width.</param>
    /// <param name="height">The image height.</param>
    /// <param name="bytesPerSample">The bytes per sample, 1 or 2.</param>
    /// <param name="planar">Whether the samples are stored planar.</param>
    /// <param name="rowsPerStrip">The number of rows per strip.</param>
    /// <returns>The strips.</returns>
    private static byte[][] CreateRgbStrips(int width, int height, int bytesPerSample, bool planar, int rowsPerStrip)
    {
        int stripsPerPlane = (height + rowsPerStrip - 1) / rowsPerStrip;
        int planes = planar ? 3 : 1;
        int samplesPerPixel = planar ? 1 : 3;
        byte[][] strips = new byte[planes * stripsPerPlane][];
        for (int plane = 0; plane < planes; plane++)
        {
            for (int strip = 0; strip < stripsPerPlane; strip++)
            {
                int top = strip * rowsPerStrip;
                int rows = Math.Min(rowsPerStrip, height - top);
                byte[] data = new byte[width * rows * samplesPerPixel * bytesPerSample];
                for (int y = 0; y < rows; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        for (int sample = 0; sample < samplesPerPixel; sample++)
                        {
                            byte value = Sample(x, top + y, plane + sample);
                            int index = (((y * width) + x) * samplesPerPixel) + sample;
                            data.AsSpan(index * bytesPerSample, bytesPerSample).Fill(value);
                        }
                    }
                }

                strips[(plane * stripsPerPlane) + strip] = data;
            }
        }

        return strips;
    }

    /// <summary>
    /// Decodes a TIFF file from memory.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="tiff">The encoded TIFF.</param>
    /// <param name="configuration">The configuration, or <see langword="null"/> for the default.</param>
    /// <returns>The decoded image.</returns>
    private static Image<TPixel> Decode<TPixel>(byte[] tiff, Configuration configuration = null)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using MemoryStream stream = new(tiff, false);
        return Image.Load<TPixel>(new DecoderOptions { Configuration = configuration ?? Configuration.Default }, stream);
    }

    /// <summary>
    /// Creates a configuration whose memory allocator has the given allocation limit.
    /// </summary>
    /// <param name="allocationLimitMegabytes">The allocation limit in megabytes.</param>
    /// <returns>The configuration.</returns>
    private static Configuration CreateConfiguration(int allocationLimitMegabytes)
    {
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = MemoryAllocator.Create(new MemoryAllocatorOptions { AllocationLimitMegabytes = allocationLimitMegabytes });
        return configuration;
    }

    /// <summary>
    /// Writes a little-endian TIFF. The strip data of every page follows the header, and the directories follow
    /// the strip data in page order, or in reverse page order when <paramref name="reverseIfdOrder"/> is set.
    /// </summary>
    /// <param name="pages">The pages.</param>
    /// <param name="reverseIfdOrder">Whether to store the directories in reverse page order.</param>
    /// <param name="loopToFirstIfd">Whether the last directory links back to the first one.</param>
    /// <returns>The encoded TIFF.</returns>
    private static byte[] BuildTiff(TiffPage[] pages, bool reverseIfdOrder = false, bool loopToFirstIfd = false)
    {
        List<byte> bytes = [0x49, 0x49, 42, 0, 0, 0, 0, 0];

        uint[][] stripOffsets = new uint[pages.Length][];
        for (int i = 0; i < pages.Length; i++)
        {
            stripOffsets[i] = new uint[pages[i].Strips.Length];
            for (int s = 0; s < pages[i].Strips.Length; s++)
            {
                stripOffsets[i][s] = (uint)bytes.Count;
                bytes.AddRange(pages[i].Strips[s]);
            }
        }

        uint[] ifdOffsets = new uint[pages.Length];
        int[] nextIfdPositions = new int[pages.Length];
        for (int n = 0; n < pages.Length; n++)
        {
            int i = reverseIfdOrder ? pages.Length - 1 - n : n;

            // Directories must start on a word boundary.
            if ((bytes.Count & 1) != 0)
            {
                bytes.Add(0);
            }

            ifdOffsets[i] = (uint)bytes.Count;
            List<(ExifTagValue Tag, ExifDataType Type, uint[] Values)> entries = pages[i].GetEntries(stripOffsets[i]);

            // Values that do not fit in an entry are stored straight after the directory.
            int valuesOffset = bytes.Count + 2 + (entries.Count * 12) + 4;
            List<byte> values = [];
            AddUInt16(bytes, (ushort)entries.Count);
            foreach ((ExifTagValue tag, ExifDataType type, uint[] entryValues) in entries)
            {
                AddUInt16(bytes, (ushort)tag);
                AddUInt16(bytes, (ushort)type);
                AddUInt32(bytes, (uint)entryValues.Length);

                List<byte> target = bytes;
                if (entryValues.Length * (type == ExifDataType.Short ? 2 : 4) > 4)
                {
                    AddUInt32(bytes, (uint)(valuesOffset + values.Count));
                    target = values;
                }

                int start = target.Count;
                foreach (uint value in entryValues)
                {
                    if (type == ExifDataType.Short)
                    {
                        AddUInt16(target, (ushort)value);
                    }
                    else
                    {
                        AddUInt32(target, value);
                    }
                }

                while (target == bytes && bytes.Count - start < 4)
                {
                    bytes.Add(0);
                }
            }

            nextIfdPositions[i] = bytes.Count;
            AddUInt32(bytes, 0);
            bytes.AddRange(values);
        }

        byte[] tiff = [.. bytes];
        BinaryPrimitives.WriteUInt32LittleEndian(tiff.AsSpan(4), ifdOffsets[0]);
        for (int i = 0; i < pages.Length; i++)
        {
            uint next = i + 1 < pages.Length ? ifdOffsets[i + 1] : loopToFirstIfd ? ifdOffsets[0] : 0;
            BinaryPrimitives.WriteUInt32LittleEndian(tiff.AsSpan(nextIfdPositions[i]), next);
        }

        return tiff;
    }

    /// <summary>
    /// Appends a little-endian 16-bit value.
    /// </summary>
    /// <param name="bytes">The target.</param>
    /// <param name="value">The value.</param>
    private static void AddUInt16(List<byte> bytes, ushort value)
    {
        bytes.Add((byte)value);
        bytes.Add((byte)(value >> 8));
    }

    /// <summary>
    /// Appends a little-endian 32-bit value.
    /// </summary>
    /// <param name="bytes">The target.</param>
    /// <param name="value">The value.</param>
    private static void AddUInt32(List<byte> bytes, uint value)
    {
        AddUInt16(bytes, (ushort)value);
        AddUInt16(bytes, (ushort)(value >> 16));
    }

    /// <summary>
    /// Describes one uncompressed page of a synthesised TIFF.
    /// </summary>
    private sealed class TiffPage
    {
        /// <summary>
        /// Gets the image width.
        /// </summary>
        public int Width { get; private init; }

        /// <summary>
        /// Gets the image height.
        /// </summary>
        public int Height { get; private init; }

        /// <summary>
        /// Gets the bits of each sample; the length is the number of samples per pixel.
        /// </summary>
        public ushort[] BitsPerSample { get; private init; }

        /// <summary>
        /// Gets the photometric interpretation.
        /// </summary>
        public TiffPhotometricInterpretation Photometric { get; private init; }

        /// <summary>
        /// Gets a value indicating whether the samples are stored planar.
        /// </summary>
        public bool Planar { get; private init; }

        /// <summary>
        /// Gets the RowsPerStrip value, or <see langword="null"/> to omit the entry.
        /// </summary>
        public uint? RowsPerStrip { get; private init; }

        /// <summary>
        /// Gets the strip data.
        /// </summary>
        public byte[][] Strips { get; private init; }

        /// <summary>
        /// Creates a page.
        /// </summary>
        /// <param name="width">The image width.</param>
        /// <param name="height">The image height.</param>
        /// <param name="bitsPerSample">The bits of each sample.</param>
        /// <param name="photometric">The photometric interpretation.</param>
        /// <param name="planar">Whether the samples are stored planar.</param>
        /// <param name="rowsPerStrip">The RowsPerStrip value, or <see langword="null"/> to omit the entry.</param>
        /// <param name="strips">The strip data.</param>
        /// <returns>The page.</returns>
        public static TiffPage Create(int width, int height, ushort[] bitsPerSample, TiffPhotometricInterpretation photometric, bool planar, uint? rowsPerStrip, byte[][] strips)
            => new()
            {
                Width = width,
                Height = height,
                BitsPerSample = bitsPerSample,
                Photometric = photometric,
                Planar = planar,
                RowsPerStrip = rowsPerStrip,
                Strips = strips,
            };

        /// <summary>
        /// Gets the directory entries of the page, sorted by tag.
        /// </summary>
        /// <param name="stripOffsets">The file offset of each strip.</param>
        /// <returns>The entries.</returns>
        public List<(ExifTagValue Tag, ExifDataType Type, uint[] Values)> GetEntries(uint[] stripOffsets)
        {
            uint[] bitsPerSample = new uint[this.BitsPerSample.Length];
            for (int i = 0; i < bitsPerSample.Length; i++)
            {
                bitsPerSample[i] = this.BitsPerSample[i];
            }

            uint[] stripByteCounts = new uint[this.Strips.Length];
            for (int i = 0; i < stripByteCounts.Length; i++)
            {
                stripByteCounts[i] = (uint)this.Strips[i].Length;
            }

            List<(ExifTagValue Tag, ExifDataType Type, uint[] Values)> entries =
            [
                (ExifTagValue.ImageWidth, ExifDataType.Long, [(uint)this.Width]),
                (ExifTagValue.ImageLength, ExifDataType.Long, [(uint)this.Height]),
                (ExifTagValue.BitsPerSample, ExifDataType.Short, bitsPerSample),
                (ExifTagValue.Compression, ExifDataType.Short, [(uint)TiffCompression.None]),
                (ExifTagValue.PhotometricInterpretation, ExifDataType.Short, [(uint)this.Photometric]),
                (ExifTagValue.StripOffsets, ExifDataType.Long, stripOffsets),
                (ExifTagValue.SamplesPerPixel, ExifDataType.Short, [(uint)this.BitsPerSample.Length]),
                (ExifTagValue.StripByteCounts, ExifDataType.Long, stripByteCounts),
            ];

            if (this.RowsPerStrip.HasValue)
            {
                entries.Add((ExifTagValue.RowsPerStrip, ExifDataType.Long, [this.RowsPerStrip.Value]));
            }

            if (this.Planar)
            {
                entries.Add((ExifTagValue.PlanarConfiguration, ExifDataType.Short, [(uint)TiffPlanarConfiguration.Planar]));
            }

            entries.Sort((a, b) => a.Tag.CompareTo(b.Tag));
            return entries;
        }
    }
}
