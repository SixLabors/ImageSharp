// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
#nullable disable

using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.ColorProfiles.Conversion.Icc;

/// <summary>
/// Color converter for ICC profiles
/// </summary>
internal abstract partial class IccConverterBase
{
    private static ConversionMethod GetConversionMethod(IccProfile profile, IccRenderingIntent renderingIntent) => profile.Header.Class switch
    {
        IccProfileClass.InputDevice or
        IccProfileClass.DisplayDevice or
        IccProfileClass.OutputDevice or
        IccProfileClass.ColorSpace => CheckMethod1(profile, renderingIntent),
        IccProfileClass.DeviceLink or IccProfileClass.Abstract => CheckMethod2(profile),
        _ => ConversionMethod.Invalid,
    };

    private static ConversionMethod CheckMethod1(IccProfile profile, IccRenderingIntent renderingIntent)
    {
        ConversionMethod method = CheckMethodD(profile, renderingIntent);
        if (method != ConversionMethod.Invalid)
        {
            return method;
        }

        method = CheckMethodA(profile, renderingIntent);
        if (method != ConversionMethod.Invalid)
        {
            return method;
        }

        method = CheckMethodA0(profile);
        if (method != ConversionMethod.Invalid)
        {
            return method;
        }

        method = CheckMethodTrc(profile);
        if (method != ConversionMethod.Invalid)
        {
            return method;
        }

        return ConversionMethod.Invalid;
    }

    private static ConversionMethod CheckMethodD(IccProfile profile, IccRenderingIntent renderingIntent)
    {
        if ((HasTag(profile, IccProfileTag.DToB0) || HasTag(profile, IccProfileTag.BToD0))
            && renderingIntent == IccRenderingIntent.Perceptual)
        {
            return ConversionMethod.D0;
        }

        if ((HasTag(profile, IccProfileTag.DToB1) || HasTag(profile, IccProfileTag.BToD1))
            && renderingIntent == IccRenderingIntent.MediaRelativeColorimetric)
        {
            return ConversionMethod.D1;
        }

        if ((HasTag(profile, IccProfileTag.DToB2) || HasTag(profile, IccProfileTag.BToD2))
            && renderingIntent == IccRenderingIntent.Saturation)
        {
            return ConversionMethod.D2;
        }

        if ((HasTag(profile, IccProfileTag.DToB3) || HasTag(profile, IccProfileTag.BToD3))
            && renderingIntent == IccRenderingIntent.AbsoluteColorimetric)
        {
            return ConversionMethod.D3;
        }

        return ConversionMethod.Invalid;
    }

    private static ConversionMethod CheckMethodA(IccProfile profile, IccRenderingIntent renderingIntent)
    {
        if ((HasTag(profile, IccProfileTag.AToB0) || HasTag(profile, IccProfileTag.BToA0))
            && renderingIntent == IccRenderingIntent.Perceptual)
        {
            return ConversionMethod.A0;
        }

        if ((HasTag(profile, IccProfileTag.AToB1) || HasTag(profile, IccProfileTag.BToA1))
            && renderingIntent == IccRenderingIntent.MediaRelativeColorimetric)
        {
            return ConversionMethod.A1;
        }

        if ((HasTag(profile, IccProfileTag.AToB2) || HasTag(profile, IccProfileTag.BToA2))
            && renderingIntent == IccRenderingIntent.Saturation)
        {
            return ConversionMethod.A2;
        }

        return ConversionMethod.Invalid;
    }

    private static ConversionMethod CheckMethodA0(IccProfile profile)
    {
        bool valid = HasTag(profile, IccProfileTag.AToB0) || HasTag(profile, IccProfileTag.BToA0);
        return valid ? ConversionMethod.A0 : ConversionMethod.Invalid;
    }

    private static ConversionMethod CheckMethodTrc(IccProfile profile)
    {
        if (HasTag(profile, IccProfileTag.RedMatrixColumn)
            && HasTag(profile, IccProfileTag.GreenMatrixColumn)
            && HasTag(profile, IccProfileTag.BlueMatrixColumn)
            && HasTag(profile, IccProfileTag.RedTrc)
            && HasTag(profile, IccProfileTag.GreenTrc)
            && HasTag(profile, IccProfileTag.BlueTrc))
        {
            return ConversionMethod.ColorTrc;
        }

        if (HasTag(profile, IccProfileTag.GrayTrc))
        {
            return ConversionMethod.GrayTrc;
        }

        return ConversionMethod.Invalid;
    }

    private static ConversionMethod CheckMethod2(IccProfile profile)
    {
        if (HasTag(profile, IccProfileTag.DToB0) || HasTag(profile, IccProfileTag.BToD0))
        {
            return ConversionMethod.D0;
        }

        if (HasTag(profile, IccProfileTag.AToB0) || HasTag(profile, IccProfileTag.AToB0))
        {
            return ConversionMethod.A0;
        }

        return ConversionMethod.Invalid;
    }

    private static bool HasTag(IccProfile profile, IccProfileTag tag)
        => profile.Entries.Any(t => t.TagSignature == tag);

    private static IccTagDataEntry GetTag(IccProfile profile, IccProfileTag tag)
        => Array.Find(profile.Entries, t => t.TagSignature == tag);

    private static T GetTag<T>(IccProfile profile, IccProfileTag tag)
        where T : IccTagDataEntry
        => profile.Entries.OfType<T>().FirstOrDefault(t => t.TagSignature == tag);
}
