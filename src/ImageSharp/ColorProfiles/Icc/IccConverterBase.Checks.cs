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
    /// <summary>
    /// Selects one rendering intent for both sides of a profile connection.
    /// </summary>
    /// <param name="source">The source profile.</param>
    /// <param name="target">The target profile.</param>
    /// <returns>The table-based profile's intent when paired with curves; otherwise, the source profile's intent.</returns>
    public static IccRenderingIntent GetRenderingIntent(IccProfile source, IccProfile target)
    {
        IccRenderingIntent sourceIntent = source.Header.RenderingIntent;
        IccRenderingIntent targetIntent = target.Header.RenderingIntent;

        // Curve-only profiles have no intent-specific transform tables.
        // If only the target uses tables, select its rendering intent. The reverse connection then selects the same intent.
        // Use transform selection to distinguish curve-only profiles from profiles that provide both curves and tables.
        if (GetConversionMethod(source, sourceIntent, true) is ConversionMethod.ColorTrc or ConversionMethod.GrayTrc
            && GetConversionMethod(target, targetIntent, false) is not (ConversionMethod.ColorTrc or ConversionMethod.GrayTrc or ConversionMethod.Invalid))
        {
            return targetIntent;
        }

        // Two table-based profiles can recommend different intents. Retain source precedence in that case.
        return sourceIntent;
    }

    /// <summary>
    /// Tests whether the profile provides the requested direction, including the normal perceptual fallback.
    /// </summary>
    /// <param name="profile">The external profile whose transform availability is checked.</param>
    /// <param name="toPcs">Whether the transform converts device components to PCS values.</param>
    /// <param name="renderingIntent">The requested rendering intent.</param>
    /// <returns>Whether an existing table or curve transform can provide that direction.</returns>
    public static bool SupportsConversion(IccProfile profile, bool toPcs, IccRenderingIntent renderingIntent)
        => GetConversionMethod(profile, renderingIntent, toPcs) != ConversionMethod.Invalid;

    /// <summary>
    /// Checks whether RGB colorants and channel curves, or a grayscale curve, provide a matrix-shaper model.
    /// </summary>
    /// <param name="profile">The external profile whose tags are checked.</param>
    /// <returns>Whether matrix-shaper tags are present, even when a selected transform uses a lookup table.</returns>
    public static bool IsMatrixShaper(IccProfile profile)
        => profile.Header.DataColorSpace switch
        {
            IccColorSpaceType.Rgb => CheckMethodTrc(profile) == ConversionMethod.ColorTrc,
            IccColorSpaceType.Gray => HasTag(profile, IccProfileTag.GrayTrc),
            _ => false
        };

    /// <summary>
    /// Checks exact intent support for black-point detection, without perceptual table fallback.
    /// </summary>
    /// <param name="profile">The external profile whose tags are checked.</param>
    /// <param name="toPcs">Whether the direction converts device components to PCS values.</param>
    /// <param name="renderingIntent">The requested intent.</param>
    /// <returns>Whether an intent-specific table or a matrix-shaper model supports detection.</returns>
    public static bool SupportsBlackPointIntent(IccProfile profile, bool toPcs, IccRenderingIntent renderingIntent)
        => HasIntentTable(profile, toPcs, renderingIntent) || IsMatrixShaper(profile);

    /// <summary>
    /// Checks the exact AToB or BToA tag used by the native black-point detector's CLUT eligibility query.
    /// </summary>
    /// <param name="profile">The external profile whose tags are checked.</param>
    /// <param name="toPcs">Whether the direction converts device components to PCS values.</param>
    /// <param name="renderingIntent">The requested intent.</param>
    /// <returns>Whether the requested direction has its exact intent table.</returns>
    public static bool HasIntentTable(IccProfile profile, bool toPcs, IccRenderingIntent renderingIntent)
    {
        // LCMS cmsIsCLUT tests the legacy intent-tag family. MPE precedence and A0 fallback belong
        // to transform selection, not to this detector capability query. Absolute intent shares A1/B1.
        if (renderingIntent > IccRenderingIntent.AbsoluteColorimetric)
        {
            return false;
        }

        IccProfileTag tag = renderingIntent switch
        {
            IccRenderingIntent.Perceptual => toPcs ? IccProfileTag.AToB0 : IccProfileTag.BToA0,
            IccRenderingIntent.MediaRelativeColorimetric or IccRenderingIntent.AbsoluteColorimetric =>
                toPcs ? IccProfileTag.AToB1 : IccProfileTag.BToA1,
            _ => toPcs ? IccProfileTag.AToB2 : IccProfileTag.BToA2
        };

        return HasTag(profile, tag);
    }

    private static ConversionMethod GetConversionMethod(IccProfile profile, IccRenderingIntent renderingIntent, bool toPcs) => profile.Header.Class switch
    {
        IccProfileClass.InputDevice or
        IccProfileClass.DisplayDevice or
        IccProfileClass.OutputDevice or
        IccProfileClass.ColorSpace => CheckMethod1(profile, renderingIntent, toPcs),
        IccProfileClass.DeviceLink or IccProfileClass.Abstract => CheckMethod2(profile, toPcs),
        _ => ConversionMethod.Invalid,
    };

    private static ConversionMethod CheckMethod1(IccProfile profile, IccRenderingIntent renderingIntent, bool toPcs)
    {
        ConversionMethod method = CheckMethodD(profile, renderingIntent, toPcs);
        if (method != ConversionMethod.Invalid)
        {
            return method;
        }

        method = CheckMethodA(profile, renderingIntent, toPcs);
        if (method != ConversionMethod.Invalid)
        {
            return method;
        }

        method = CheckMethodA0(profile, toPcs);
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

    private static ConversionMethod CheckMethodD(IccProfile profile, IccRenderingIntent renderingIntent, bool toPcs)
    {
        if (HasTag(profile, toPcs ? IccProfileTag.DToB0 : IccProfileTag.BToD0)
            && renderingIntent == IccRenderingIntent.Perceptual)
        {
            return ConversionMethod.D0;
        }

        if (HasTag(profile, toPcs ? IccProfileTag.DToB1 : IccProfileTag.BToD1)
            && renderingIntent == IccRenderingIntent.MediaRelativeColorimetric)
        {
            return ConversionMethod.D1;
        }

        if (HasTag(profile, toPcs ? IccProfileTag.DToB2 : IccProfileTag.BToD2)
            && renderingIntent == IccRenderingIntent.Saturation)
        {
            return ConversionMethod.D2;
        }

        if (HasTag(profile, toPcs ? IccProfileTag.DToB3 : IccProfileTag.BToD3)
            && renderingIntent == IccRenderingIntent.AbsoluteColorimetric)
        {
            return ConversionMethod.D3;
        }

        return ConversionMethod.Invalid;
    }

    private static ConversionMethod CheckMethodA(IccProfile profile, IccRenderingIntent renderingIntent, bool toPcs)
    {
        if (HasTag(profile, toPcs ? IccProfileTag.AToB0 : IccProfileTag.BToA0)
            && renderingIntent == IccRenderingIntent.Perceptual)
        {
            return ConversionMethod.A0;
        }

        if (HasTag(profile, toPcs ? IccProfileTag.AToB1 : IccProfileTag.BToA1)
            && renderingIntent == IccRenderingIntent.MediaRelativeColorimetric)
        {
            return ConversionMethod.A1;
        }

        if (HasTag(profile, toPcs ? IccProfileTag.AToB2 : IccProfileTag.BToA2)
            && renderingIntent == IccRenderingIntent.Saturation)
        {
            return ConversionMethod.A2;
        }

        return ConversionMethod.Invalid;
    }

    private static ConversionMethod CheckMethodA0(IccProfile profile, bool toPcs)
    {
        bool valid = HasTag(profile, toPcs ? IccProfileTag.AToB0 : IccProfileTag.BToA0);
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

    private static ConversionMethod CheckMethod2(IccProfile profile, bool toPcs)
    {
        if (HasTag(profile, toPcs ? IccProfileTag.DToB0 : IccProfileTag.BToD0))
        {
            return ConversionMethod.D0;
        }

        if (HasTag(profile, toPcs ? IccProfileTag.AToB0 : IccProfileTag.BToA0))
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
