// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Linq;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion.Icc
{
    /// <summary>
    /// Color converter for ICC profiles
    /// </summary>
    internal abstract partial class IccConverterBase
    {
        private ConversionMethod GetConversionMethod(IccProfile profile, IccRenderingIntent renderingIntent)
        {
            switch (profile.Header.Class)
            {
                case IccProfileClass.InputDevice:
                case IccProfileClass.DisplayDevice:
                case IccProfileClass.OutputDevice:
                case IccProfileClass.ColorSpace:
                    return this.CheckMethod1(profile, renderingIntent);

                case IccProfileClass.DeviceLink:
                case IccProfileClass.Abstract:
                    return this.CheckMethod2(profile);

                default:
                    return ConversionMethod.Invalid;
            }
        }

        private ConversionMethod CheckMethod1(IccProfile profile, IccRenderingIntent renderingIntent)
        {
            ConversionMethod method = ConversionMethod.Invalid;

            method = this.CheckMethodD(profile, renderingIntent);
            if (method != ConversionMethod.Invalid)
            {
                return method;
            }

            method = this.CheckMethodA(profile, renderingIntent);
            if (method != ConversionMethod.Invalid)
            {
                return method;
            }

            method = this.CheckMethodA0(profile);
            if (method != ConversionMethod.Invalid)
            {
                return method;
            }

            method = this.CheckMethodTrc(profile);
            if (method != ConversionMethod.Invalid)
            {
                return method;
            }

            return ConversionMethod.Invalid;
        }

        private ConversionMethod CheckMethodD(IccProfile profile, IccRenderingIntent renderingIntent)
        {
            if ((this.HasTag(profile, IccProfileTag.DToB0) || this.HasTag(profile, IccProfileTag.BToD0))
                && renderingIntent == IccRenderingIntent.Perceptual)
            {
                return ConversionMethod.D0;
            }

            if ((this.HasTag(profile, IccProfileTag.DToB1) || this.HasTag(profile, IccProfileTag.BToD1))
                && renderingIntent == IccRenderingIntent.MediaRelativeColorimetric)
            {
                return ConversionMethod.D1;
            }

            if ((this.HasTag(profile, IccProfileTag.DToB2) || this.HasTag(profile, IccProfileTag.BToD2))
                && renderingIntent == IccRenderingIntent.Saturation)
            {
                return ConversionMethod.D2;
            }

            if ((this.HasTag(profile, IccProfileTag.DToB3) || this.HasTag(profile, IccProfileTag.BToD3))
                && renderingIntent == IccRenderingIntent.AbsoluteColorimetric)
            {
                return ConversionMethod.D3;
            }

            return ConversionMethod.Invalid;
        }

        private ConversionMethod CheckMethodA(IccProfile profile, IccRenderingIntent renderingIntent)
        {
            if ((this.HasTag(profile, IccProfileTag.AToB0) || this.HasTag(profile, IccProfileTag.BToA0))
                && renderingIntent == IccRenderingIntent.Perceptual)
            {
                return ConversionMethod.A0;
            }

            if ((this.HasTag(profile, IccProfileTag.AToB1) || this.HasTag(profile, IccProfileTag.BToA1))
                && renderingIntent == IccRenderingIntent.MediaRelativeColorimetric)
            {
                return ConversionMethod.A1;
            }

            if ((this.HasTag(profile, IccProfileTag.AToB2) || this.HasTag(profile, IccProfileTag.BToA2))
                && renderingIntent == IccRenderingIntent.Saturation)
            {
                return ConversionMethod.A2;
            }

            return ConversionMethod.Invalid;
        }

        private ConversionMethod CheckMethodA0(IccProfile profile)
        {
            bool valid = this.HasTag(profile, IccProfileTag.AToB0) || this.HasTag(profile, IccProfileTag.BToA0);
            return valid ? ConversionMethod.A0 : ConversionMethod.Invalid;
        }

        private ConversionMethod CheckMethodTrc(IccProfile profile)
        {
            if (this.HasTag(profile, IccProfileTag.RedMatrixColumn)
                && this.HasTag(profile, IccProfileTag.GreenMatrixColumn)
                && this.HasTag(profile, IccProfileTag.BlueMatrixColumn)
                && this.HasTag(profile, IccProfileTag.RedTrc)
                && this.HasTag(profile, IccProfileTag.GreenTrc)
                && this.HasTag(profile, IccProfileTag.BlueTrc))
            {
                return ConversionMethod.ColorTrc;
            }

            if (this.HasTag(profile, IccProfileTag.GrayTrc))
            {
                return ConversionMethod.GrayTrc;
            }

            return ConversionMethod.Invalid;
        }

        private ConversionMethod CheckMethod2(IccProfile profile)
        {
            if (this.HasTag(profile, IccProfileTag.DToB0) || this.HasTag(profile, IccProfileTag.BToD0))
            {
                return ConversionMethod.D0;
            }

            if (this.HasTag(profile, IccProfileTag.AToB0) || this.HasTag(profile, IccProfileTag.AToB0))
            {
                return ConversionMethod.A0;
            }

            return ConversionMethod.Invalid;
        }

        private bool HasTag(IccProfile profile, IccProfileTag tag)
        {
            return profile.Entries.Any(t => t.TagSignature == tag);
        }

        private IccTagDataEntry GetTag(IccProfile profile, IccProfileTag tag)
        {
            return profile.Entries.FirstOrDefault(t => t.TagSignature == tag);
        }

        private T GetTag<T>(IccProfile profile, IccProfileTag tag)
            where T : IccTagDataEntry
        {
            return profile.Entries.OfType<T>().FirstOrDefault(t => t.TagSignature == tag);
        }
    }
}
