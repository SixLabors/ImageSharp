// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Metadata.Profiles.Iptc
{
    /// <summary>
    /// All iptc tags.
    /// </summary>
    public enum IptcTag
    {
        /// <summary>
        /// Unknown.
        /// </summary>
        Unknown = -1,

        /// <summary>
        /// Record version, not repeatable.
        /// </summary>
        RecordVersion = 0,

        /// <summary>
        /// Object type, not repeatable.
        /// </summary>
        ObjectType = 3,

        /// <summary>
        /// Object attribute.
        /// </summary>
        ObjectAttribute = 4,

        /// <summary>
        /// Object Name, not repeatable.
        /// </summary>
        Name = 5,

        /// <summary>
        /// Edit status, not repeatable.
        /// </summary>
        EditStatus = 7,

        /// <summary>
        /// Editorial update, not repeatable.
        /// </summary>
        EditorialUpdate = 8,

        /// <summary>
        /// Urgency, not repeatable.
        /// </summary>
        Urgency = 10,

        /// <summary>
        /// Subject Reference.
        /// </summary>
        SubjectReference = 12,

        /// <summary>
        /// Category, not repeatable.
        /// </summary>
        Category = 15,

        /// <summary>
        /// Supplemental categories.
        /// </summary>
        SupplementalCategories = 20,

        /// <summary>
        /// Fixture identifier, not repeatable.
        /// </summary>
        FixtureIdentifier = 22,

        /// <summary>
        /// Keywords.
        /// </summary>
        Keywords = 25,

        /// <summary>
        /// Location code.
        /// </summary>
        LocationCode = 26,

        /// <summary>
        /// Location name.
        /// </summary>
        LocationName = 27,

        /// <summary>
        /// Release date, not repeatable.
        /// </summary>
        ReleaseDate = 30,

        /// <summary>
        /// Release time, not repeatable.
        /// </summary>
        ReleaseTime = 35,

        /// <summary>
        /// Expiration date, not repeatable.
        /// </summary>
        ExpirationDate = 37,

        /// <summary>
        /// Expiration time, not repeatable.
        /// </summary>
        ExpirationTime = 38,

        /// <summary>
        /// Special instructions, not repeatable.
        /// </summary>
        SpecialInstructions = 40,

        /// <summary>
        /// Action advised, not repeatable.
        /// </summary>
        ActionAdvised = 42,

        /// <summary>
        /// Reference service.
        /// </summary>
        ReferenceService = 45,

        /// <summary>
        /// Reference date.
        /// </summary>
        ReferenceDate = 47,

        /// <summary>
        /// ReferenceNumber.
        /// </summary>
        ReferenceNumber = 50,

        /// <summary>
        /// Created date, not repeatable.
        /// </summary>
        CreatedDate = 55,

        /// <summary>
        /// Created time, not repeatable.
        /// </summary>
        CreatedTime = 60,

        /// <summary>
        /// Digital creation date, not repeatable.
        /// </summary>
        DigitalCreationDate = 62,

        /// <summary>
        /// Digital creation time, not repeatable.
        /// </summary>
        DigitalCreationTime = 63,

        /// <summary>
        /// Originating program, not repeatable.
        /// </summary>
        OriginatingProgram = 65,

        /// <summary>
        /// Program version, not repeatable.
        /// </summary>
        ProgramVersion = 70,

        /// <summary>
        /// Object cycle, not repeatable.
        /// </summary>
        ObjectCycle = 75,

        /// <summary>
        /// Byline.
        /// </summary>
        Byline = 80,

        /// <summary>
        /// Byline title.
        /// </summary>
        BylineTitle = 85,

        /// <summary>
        /// City, not repeatable.
        /// </summary>
        City = 90,

        /// <summary>
        /// Sub location, not repeatable.
        /// </summary>
        SubLocation = 92,

        /// <summary>
        /// Province/State, not repeatable.
        /// </summary>
        ProvinceState = 95,

        /// <summary>
        /// Country code, not repeatable.
        /// </summary>
        CountryCode = 100,

        /// <summary>
        /// Country, not repeatable.
        /// </summary>
        Country = 101,

        /// <summary>
        /// Original transmission reference, not repeatable.
        /// </summary>
        OriginalTransmissionReference = 103,

        /// <summary>
        /// Headline, not repeatable.
        /// </summary>
        Headline = 105,

        /// <summary>
        /// Credit, not repeatable.
        /// </summary>
        Credit = 110,

        /// <summary>
        /// Source, not repeatable.
        /// </summary>
        Source = 115,

        /// <summary>
        /// Copyright notice, not repeatable.
        /// </summary>
        CopyrightNotice = 116,

        /// <summary>
        /// Contact.
        /// </summary>
        Contact = 118,

        /// <summary>
        /// Caption, not repeatable.
        /// </summary>
        Caption = 120,

        /// <summary>
        /// Local caption.
        /// </summary>
        LocalCaption = 121,

        /// <summary>
        /// Caption writer.
        /// </summary>
        CaptionWriter = 122,

        /// <summary>
        /// Image type, not repeatable.
        /// </summary>
        ImageType = 130,

        /// <summary>
        /// Image orientation, not repeatable.
        /// </summary>
        ImageOrientation = 131,

        /// <summary>
        /// Custom field 1
        /// </summary>
        CustomField1 = 200,

        /// <summary>
        /// Custom field 2
        /// </summary>
        CustomField2 = 201,

        /// <summary>
        /// Custom field 3
        /// </summary>
        CustomField3 = 202,

        /// <summary>
        /// Custom field 4
        /// </summary>
        CustomField4 = 203,

        /// <summary>
        /// Custom field 5
        /// </summary>
        CustomField5 = 204,

        /// <summary>
        /// Custom field 6
        /// </summary>
        CustomField6 = 205,

        /// <summary>
        /// Custom field 7
        /// </summary>
        CustomField7 = 206,

        /// <summary>
        /// Custom field 8
        /// </summary>
        CustomField8 = 207,

        /// <summary>
        /// Custom field 9
        /// </summary>
        CustomField9 = 208,

        /// <summary>
        /// Custom field 10
        /// </summary>
        CustomField10 = 209,

        /// <summary>
        /// Custom field 11
        /// </summary>
        CustomField11 = 210,

        /// <summary>
        /// Custom field 12
        /// </summary>
        CustomField12 = 211,

        /// <summary>
        /// Custom field 13
        /// </summary>
        CustomField13 = 212,

        /// <summary>
        /// Custom field 14
        /// </summary>
        CustomField14 = 213,

        /// <summary>
        /// Custom field 15
        /// </summary>
        CustomField15 = 214,

        /// <summary>
        /// Custom field 16
        /// </summary>
        CustomField16 = 215,

        /// <summary>
        /// Custom field 17
        /// </summary>
        CustomField17 = 216,

        /// <summary>
        /// Custom field 18
        /// </summary>
        CustomField18 = 217,

        /// <summary>
        /// Custom field 19
        /// </summary>
        CustomField19 = 218,

        /// <summary>
        /// Custom field 20
        /// </summary>
        CustomField20 = 219,
    }
}
