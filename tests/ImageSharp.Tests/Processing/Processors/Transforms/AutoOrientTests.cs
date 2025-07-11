// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Transforms;

[Trait("Category", "Processors")]
[GroupOutput("Transforms")]
public class AutoOrientTests
{
    public const string FlipTestFile = TestImages.Bmp.F;

    public static readonly TheoryData<ExifDataType, byte[]> InvalidOrientationValues
        = new()
        {
            { ExifDataType.Byte, [1] },
            { ExifDataType.SignedByte, [2] },
            { ExifDataType.SignedShort, BitConverter.GetBytes((short)3) },
            { ExifDataType.Long, BitConverter.GetBytes(4U) },
            { ExifDataType.SignedLong, BitConverter.GetBytes(5) }
        };

    public static readonly TheoryData<ushort> ExifOrientationValues
        = new()
        {
            ExifOrientationMode.Unknown,
            ExifOrientationMode.TopLeft,
            ExifOrientationMode.TopRight,
            ExifOrientationMode.BottomRight,
            ExifOrientationMode.BottomLeft,
            ExifOrientationMode.LeftTop,
            ExifOrientationMode.RightTop,
            ExifOrientationMode.RightBottom,
            ExifOrientationMode.LeftBottom
        };

    [Theory]
    [WithFile(FlipTestFile, nameof(ExifOrientationValues), PixelTypes.Rgba32)]
    public void AutoOrient_WorksForAllExifOrientations<TPixel>(TestImageProvider<TPixel> provider, ushort orientation)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        image.Metadata.ExifProfile = new ExifProfile();
        image.Metadata.ExifProfile.SetValue(ExifTag.Orientation, orientation);

        image.Mutate(x => x.AutoOrient());
        image.DebugSave(provider, orientation, appendPixelTypeToFileName: false);
        image.CompareToReferenceOutput(provider, orientation, appendPixelTypeToFileName: false);
    }

    [Theory]
    [WithFile(FlipTestFile, nameof(InvalidOrientationValues), PixelTypes.Rgba32)]
    public void AutoOrient_WorksWithCorruptExifData<TPixel>(TestImageProvider<TPixel> provider, ExifDataType dataType, byte[] orientation)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ExifProfile profile = new();
        profile.SetValue(ExifTag.JPEGTables, orientation);

        byte[] bytes = profile.ToByteArray();

        // Change the tag into ExifTag.Orientation
        bytes[16] = 18;
        bytes[17] = 1;

        // Change the data type
        bytes[18] = (byte)dataType;

        // Change the number of components
        bytes[20] = 1;

        byte[] orientationCodeData = new byte[8];
        Array.Copy(orientation, orientationCodeData, orientation.Length);

        ulong orientationCode = BitConverter.ToUInt64(orientationCodeData, 0);

        using Image<TPixel> image = provider.GetImage();
        using Image<TPixel> reference = image.Clone();
        image.Metadata.ExifProfile = new ExifProfile(bytes);
        image.Mutate(x => x.AutoOrient());
        image.DebugSave(provider, $"{dataType}-{orientationCode}", appendPixelTypeToFileName: false);
        ImageComparer.Exact.VerifySimilarity(image, reference);
    }
}
