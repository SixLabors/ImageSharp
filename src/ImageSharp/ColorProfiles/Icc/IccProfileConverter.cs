// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.ColorProfiles.Icc;

/// <summary>
/// Allows the conversion between ICC profiles.
/// </summary>
internal static class IccProfileConverter
{
    /// <summary>
    /// Performs a conversion of the image pixels based on the input and output ICC profiles.
    /// </summary>
    /// <param name="image">The image to convert.</param>
    /// <param name="inputIccProfile">The input ICC profile.</param>
    /// <param name="outputIccProfile">The output ICC profile. </param>
    public static void Convert(Image image, IccProfile inputIccProfile, IccProfile outputIccProfile)
        => image.AcceptVisitor(new IccProfileConverterVisitor(inputIccProfile, outputIccProfile));

    /// <summary>
    /// Performs a conversion of the image pixels based on the input and output ICC profiles.
    /// </summary>
    /// <typeparam name="TPixel">The type of pixel.</typeparam>
    /// <param name="image">The image to convert.</param>
    /// <param name="inputIccProfile">The input ICC profile.</param>
    /// <param name="outputIccProfile">The output ICC profile. </param>
    public static void Convert<TPixel>(Image<TPixel> image, IccProfile inputIccProfile, IccProfile outputIccProfile)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        IccDataToPcsConverter converterDataToPcs = new(inputIccProfile);
        IccPcsToDataConverter converterPcsToData = new(outputIccProfile);
        Configuration configuration = image.Configuration;

        image.ProcessPixelRows(accessor =>
        {
            ColorProfileConverter converter = new(new ColorConversionOptions()
            {
                SourceWhitePoint = new CieXyz(inputIccProfile.Header.PcsIlluminant),
                TargetWhitePoint = new CieXyz(outputIccProfile.Header.PcsIlluminant),
            });

            // TODO: Our Xxy/Lab conversion are dependent on the version number. We are applying the conversion using V4
            // but we should use the correct algorithm per version. This includes Lab/Lab Xyz/Xyz.
            using IMemoryOwner<Vector4> vectors = configuration.MemoryAllocator.Allocate<Vector4>(accessor.Width);
            Span<Vector4> vectorsSpan = vectors.GetSpan();

            // TODO: For debugging - remove.
            // It appears we have a scaling problem. The pcs values differ by on average 0.000001.
            Span<Vector4> temp = new Vector4[vectorsSpan.Length];

            for (int y = 0; y < accessor.Height; y++)
            {
                Span<TPixel> row = accessor.GetRowSpan(y);
                PixelOperations<TPixel>.Instance.ToVector4(configuration, row, vectorsSpan, PixelConversionModifiers.Scale);

                if (inputIccProfile.Header.ProfileConnectionSpace == IccColorSpaceType.CieLab &&
                    outputIccProfile.Header.ProfileConnectionSpace == IccColorSpaceType.CieXyz)
                {
                    for (int x = 0; x < vectorsSpan.Length; x++)
                    {
                        Vector4 pcs = converterDataToPcs.Calculate(vectorsSpan[x]);
                        temp[x] = pcs;
                        pcs = PcsToLab(pcs);
                        CieLab lab = new(pcs.X, pcs.Y, pcs.Z);
                        CieXyz xyz = converter.Convert<CieLab, CieXyz>(in lab);
                        pcs = XyzToPcs(pcs, xyz);

                        vectorsSpan[x] = converterPcsToData.Calculate(pcs);
                    }
                }
                else if (inputIccProfile.Header.ProfileConnectionSpace == IccColorSpaceType.CieXyz &&
                         outputIccProfile.Header.ProfileConnectionSpace == IccColorSpaceType.CieLab)
                {
                    for (int x = 0; x < vectorsSpan.Length; x++)
                    {
                        Vector4 pcs = converterDataToPcs.Calculate(vectorsSpan[x]);
                        CieXyz xyz = new(pcs.X, pcs.Y, pcs.Z);
                        CieLab lab = converter.Convert<CieXyz, CieLab>(in xyz);
                        pcs = LabToPcs(pcs, lab);
                        vectorsSpan[x] = converterPcsToData.Calculate(pcs);
                    }
                }
                else
                {
                    for (int x = 0; x < vectorsSpan.Length; x++)
                    {
                        Vector4 pcs = converterDataToPcs.Calculate(vectorsSpan[x]);
                        vectorsSpan[x] = converterPcsToData.Calculate(pcs);
                    }
                }

                PixelOperations<TPixel>.Instance.FromVector4Destructive(configuration, vectorsSpan, row, PixelConversionModifiers.Scale);
            }
        });

        image.Metadata.IccProfile = outputIccProfile;
    }

    private static unsafe Vector4 PcsToLab(Vector4 input)
    {
        Vector3* v = (Vector3*)&input;
        v[0] *= new Vector3(100f, 255, 255);
        v[0] -= new Vector3(0, 128F, 128F);
        return input;
    }

    private static unsafe Vector4 LabToPcs(Vector4 input, CieLab lab)
    {
        Vector3* v = (Vector3*)&input;
        v[0] = new Vector3(lab.L, lab.A + 128F, lab.B + 128F);
        v[0] /= 100F;
        return input;
    }

    private static unsafe Vector4 XyzToPcs(Vector4 input, CieXyz xyz)
    {
        Vector3* v = (Vector3*)&input;
        v[0] = xyz.ToVector3();
        v[0] *= 32768 / 65535f;
        return input;
    }

    private readonly struct IccProfileConverterVisitor : IImageVisitor
    {
        private readonly IccProfile inputIccProfile;
        private readonly IccProfile outputIccProfile;

        public IccProfileConverterVisitor(IccProfile inputIccProfile, IccProfile outputIccProfile)
        {
            this.inputIccProfile = inputIccProfile;
            this.outputIccProfile = outputIccProfile;
        }

        public void Visit<TPixel>(Image<TPixel> image)
            where TPixel : unmanaged, IPixel<TPixel> => Convert(image, this.inputIccProfile, this.outputIccProfile);
    }
}
