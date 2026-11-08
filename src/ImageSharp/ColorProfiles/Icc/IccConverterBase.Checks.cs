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
    /// Checks if the profile provides a transform in the requested direction, including the perceptual fallback.
    /// </summary>
    /// <param name="profile">The profile to check.</param>
    /// <param name="toPcs">Whether the transform converts device components to PCS values.</param>
    /// <param name="renderingIntent">The requested rendering intent.</param>
    /// <returns>Whether the requested transform exists.</returns>
    public static bool SupportsConversion(IccProfile profile, bool toPcs, IccRenderingIntent renderingIntent)
        => GetConversionMethod(profile, renderingIntent, toPcs) != ConversionMethod.Invalid;

    /// <summary>
    /// Checks for RGB matrix and curve tags or a grayscale curve tag.
    /// </summary>
    /// <param name="profile">The profile to check.</param>
    /// <returns>Whether the profile has the tags, regardless of the selected transform.</returns>
    public static bool IsMatrixShaper(IccProfile profile)
        => profile.Header.DataColorSpace switch
        {
            IccColorSpaceType.Rgb => CheckMethodTrc(profile) == ConversionMethod.ColorTrc,
            IccColorSpaceType.Gray => HasTag(profile, IccProfileTag.GrayTrc),
            _ => false
        };

    /// <summary>
    /// Checks if the source profile can supply a black point for the selected intent.
    /// </summary>
    /// <param name="profile">The source profile.</param>
    /// <param name="renderingIntent">The selected rendering intent.</param>
    /// <returns>Whether the profile has a table for the intent or has matrix and curve tags.</returns>
    public static bool SupportsBlackPointIntent(IccProfile profile, IccRenderingIntent renderingIntent)
    {
        bool hasTable = renderingIntent switch
        {
            IccRenderingIntent.Perceptual => HasTag(profile, IccProfileTag.AToB0),
            IccRenderingIntent.MediaRelativeColorimetric or IccRenderingIntent.AbsoluteColorimetric => HasTag(profile, IccProfileTag.AToB1),
            IccRenderingIntent.Saturation => HasTag(profile, IccProfileTag.AToB2),
            _ => false
        };

        return hasTable || IsMatrixShaper(profile);
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

    /// <summary>
    /// Checks if the profile contains the specified tag.
    /// </summary>
    /// <param name="profile">The profile to search.</param>
    /// <param name="tag">The tag signature.</param>
    /// <returns>Whether the tag exists.</returns>
    private static bool HasTag(IccProfile profile, IccProfileTag tag) => GetTag(profile, tag) is not null;

    /// <summary>
    /// Finds the first entry with the specified tag.
    /// </summary>
    /// <param name="profile">The profile to search.</param>
    /// <param name="tag">The tag signature.</param>
    /// <returns>The entry, or null if the tag is absent.</returns>
    private static IccTagDataEntry GetTag(IccProfile profile, IccProfileTag tag) => GetTag<IccTagDataEntry>(profile, tag);

    /// <summary>
    /// Finds the first entry with the specified tag and type.
    /// </summary>
    /// <typeparam name="T">The required entry type.</typeparam>
    /// <param name="profile">The profile to search.</param>
    /// <param name="tag">The tag signature.</param>
    /// <returns>The entry, or null if no entry matches.</returns>
    private static T GetTag<T>(IccProfile profile, IccProfileTag tag)
        where T : IccTagDataEntry
    {
        // Profile setup checks several tags. Search the array directly to avoid a delegate allocation for each check.
        foreach (IccTagDataEntry entry in profile.Entries)
        {
            if (entry is T match && match.TagSignature == tag)
            {
                return match;
            }
        }

        return null;
    }
}
