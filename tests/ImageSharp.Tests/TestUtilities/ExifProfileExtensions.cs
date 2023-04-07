// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Metadata.Profiles.Exif;

namespace SixLabors.ImageSharp.Tests.TestUtilities;

public static class ExifProfileExtensions
{
    public static IExifValue<T> GetValue<T>(this ExifProfile exifProfileInput, ExifTag<T> tag, bool assertTrue = true)
    {
        bool boolValue = exifProfileInput.TryGetValue(tag, out IExifValue<T> value);

        Assert.Equal(assertTrue, boolValue);

        return value;
    }
}
