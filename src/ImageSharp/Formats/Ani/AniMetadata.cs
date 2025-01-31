// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Ani;

internal class AniMetadata : IFormatMetadata<AniMetadata>
{

    public static AniMetadata FromFormatConnectingMetadata(FormatConnectingMetadata metadata) => throw new NotImplementedException();

    public void AfterImageApply<TPixel>(Image<TPixel> destination)
        where TPixel : unmanaged, IPixel<TPixel> => throw new NotImplementedException();

    public IDeepCloneable DeepClone() => throw new NotImplementedException();

    public PixelTypeInfo GetPixelTypeInfo() => throw new NotImplementedException();

    public FormatConnectingMetadata ToFormatConnectingMetadata() => throw new NotImplementedException();

    AniMetadata IDeepCloneable<AniMetadata>.DeepClone() => throw new NotImplementedException();
}
