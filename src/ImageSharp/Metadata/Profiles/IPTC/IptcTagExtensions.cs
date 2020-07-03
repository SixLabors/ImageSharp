// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Metadata.Profiles.Iptc
{
    /// <summary>
    /// Extension methods for IPTC tags.
    /// </summary>
    public static class IptcTagExtensions
    {
        /// <summary>
        /// Maximum length of the IPTC value with the given tag according to the specification.
        /// </summary>
        /// <param name="tag">The tag to check the max length for.</param>
        /// <returns>The maximum length.</returns>
        public static int MaxLength(this IptcTag tag)
        {
            return tag switch
            {
                IptcTag.RecordVersion => 2,
                IptcTag.ObjectType => 67,
                IptcTag.ObjectAttribute => 68,
                IptcTag.Name => 64,
                IptcTag.EditStatus => 64,
                IptcTag.EditorialUpdate => 2,
                IptcTag.Urgency => 1,
                IptcTag.SubjectReference => 236,
                IptcTag.Category => 3,
                IptcTag.SupplementalCategories => 32,
                IptcTag.FixtureIdentifier => 32,
                IptcTag.Keywords => 64,
                IptcTag.LocationCode => 3,
                IptcTag.LocationName => 64,
                IptcTag.ReleaseDate => 8,
                IptcTag.ReleaseTime => 11,
                IptcTag.ExpirationDate => 8,
                IptcTag.ExpirationTime => 11,
                IptcTag.SpecialInstructions => 256,
                IptcTag.ActionAdvised => 2,
                IptcTag.ReferenceService => 10,
                IptcTag.ReferenceDate => 8,
                IptcTag.ReferenceNumber => 8,
                IptcTag.CreatedDate => 8,
                IptcTag.CreatedTime => 11,
                IptcTag.DigitalCreationDate => 8,
                IptcTag.DigitalCreationTime => 11,
                IptcTag.OriginatingProgram => 32,
                IptcTag.ProgramVersion => 10,
                IptcTag.ObjectCycle => 1,
                IptcTag.Byline => 32,
                IptcTag.BylineTitle => 32,
                IptcTag.City => 32,
                IptcTag.SubLocation => 32,
                IptcTag.ProvinceState => 32,
                IptcTag.CountryCode => 3,
                IptcTag.Country => 64,
                IptcTag.OriginalTransmissionReference => 32,
                IptcTag.Headline => 256,
                IptcTag.Credit => 32,
                IptcTag.Source => 32,
                IptcTag.CopyrightNotice => 128,
                IptcTag.Contact => 128,
                IptcTag.Caption => 2000,
                IptcTag.CaptionWriter => 32,
                IptcTag.ImageType => 2,
                IptcTag.ImageOrientation => 1,
                _ => 256
            };
        }

        /// <summary>
        /// Determines if the given tag can be repeated according to the specification.
        /// </summary>
        /// <param name="tag">The tag to check.</param>
        /// <returns>True, if the tag can occur multiple times.</returns>
        public static bool IsRepeatable(this IptcTag tag)
        {
            switch (tag)
            {
                case IptcTag.RecordVersion:
                case IptcTag.ObjectType:
                case IptcTag.Name:
                case IptcTag.EditStatus:
                case IptcTag.EditorialUpdate:
                case IptcTag.Urgency:
                case IptcTag.Category:
                case IptcTag.FixtureIdentifier:
                case IptcTag.ReleaseDate:
                case IptcTag.ReleaseTime:
                case IptcTag.ExpirationDate:
                case IptcTag.ExpirationTime:
                case IptcTag.SpecialInstructions:
                case IptcTag.ActionAdvised:
                case IptcTag.CreatedDate:
                case IptcTag.CreatedTime:
                case IptcTag.DigitalCreationDate:
                case IptcTag.DigitalCreationTime:
                case IptcTag.OriginatingProgram:
                case IptcTag.ProgramVersion:
                case IptcTag.ObjectCycle:
                case IptcTag.City:
                case IptcTag.SubLocation:
                case IptcTag.ProvinceState:
                case IptcTag.CountryCode:
                case IptcTag.Country:
                case IptcTag.OriginalTransmissionReference:
                case IptcTag.Headline:
                case IptcTag.Credit:
                case IptcTag.Source:
                case IptcTag.CopyrightNotice:
                case IptcTag.Caption:
                case IptcTag.ImageType:
                case IptcTag.ImageOrientation:
                    return false;

                default:
                    return true;
            }
        }

        /// <summary>
        /// Determines if the tag is a datetime tag which needs to be formatted as CCYYMMDD.
        /// </summary>
        /// <param name="tag">The tag to check.</param>
        /// <returns>True, if its a datetime tag.</returns>
        public static bool IsDate(this IptcTag tag)
        {
            switch (tag)
            {
                case IptcTag.CreatedDate:
                case IptcTag.DigitalCreationDate:
                case IptcTag.ExpirationDate:
                case IptcTag.ReferenceDate:
                case IptcTag.ReleaseDate:
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Determines if the tag is a time tag which need to be formatted as HHMMSS±HHMM.
        /// </summary>
        /// <param name="tag">The tag to check.</param>
        /// <returns>True, if its a time tag.</returns>
        public static bool IsTime(this IptcTag tag)
        {
            switch (tag)
            {
                case IptcTag.CreatedTime:
                case IptcTag.DigitalCreationTime:
                case IptcTag.ExpirationTime:
                case IptcTag.ReleaseTime:
                    return true;

                default:
                    return false;
            }
        }
    }
}
