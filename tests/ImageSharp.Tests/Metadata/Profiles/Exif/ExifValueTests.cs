// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Metadata.Profiles.Exif;

[Trait("Profile", "Exif")]
public class ExifValueTests
{
    private readonly ExifProfile profile;

    public ExifValueTests()
    {
        using (Image<Rgba32> image = TestFile.Create(TestImages.Jpeg.Baseline.Floorplan).CreateRgba32Image())
        {
            this.profile = image.Metadata.ExifProfile;
        }
    }

    private IExifValue<string> GetExifValue()
    {
        Assert.NotNull(this.profile);

        this.profile.TryGetValue(ExifTag.Software, out IExifValue<string> value);

        return value;
    }

    [Fact]
    public void IEquatable()
    {
        IExifValue<string> first = this.GetExifValue();
        IExifValue<string> second = this.GetExifValue();

        Assert.True(first == second);
        Assert.True(first.Equals(second));
    }

    [Fact]
    public void Properties()
    {
        IExifValue<string> value = this.GetExifValue();

        Assert.Equal(ExifDataType.Ascii, value.DataType);
        Assert.Equal(ExifTag.Software, value.Tag);
        Assert.False(value.IsArray);

        const string expected = "Windows Photo Editor 10.0.10011.16384";
        Assert.Equal(expected, value.ToString());
        Assert.Equal(expected, value.Value);
    }
}
