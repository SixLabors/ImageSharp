// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Metadata.Profiles.Iptc
{
    /// <summary>
    /// Provides enumeration of all IPTC tags relevant for images.
    /// </summary>
    public enum IptcTag
    {
        /// <summary>
        /// Unknown.
        /// </summary>
        Unknown = -1,

        /// <summary>
        /// Record version identifying the version of the Information Interchange Model.
        /// Not repeatable. Max length is 2.
        /// </summary>
        RecordVersion = 0,

        /// <summary>
        /// Object type, not repeatable. Max Length is 67.
        /// </summary>
        ObjectType = 3,

        /// <summary>
        /// Object attribute. Max length is 68.
        /// </summary>
        ObjectAttribute = 4,

        /// <summary>
        /// Object Name, not repeatable. Max length is 64.
        /// </summary>
        Name = 5,

        /// <summary>
        /// Edit status, not repeatable. Max length is 64.
        /// </summary>
        EditStatus = 7,

        /// <summary>
        /// Editorial update, not repeatable. Max length is 2.
        /// </summary>
        EditorialUpdate = 8,

        /// <summary>
        /// Urgency, not repeatable. Max length is 2.
        /// </summary>
        Urgency = 10,

        /// <summary>
        /// Subject Reference. Max length is 236.
        /// </summary>
        SubjectReference = 12,

        /// <summary>
        /// Category, not repeatable. Max length is 3.
        /// </summary>
        Category = 15,

        /// <summary>
        /// Supplemental categories. Max length is 32.
        /// </summary>
        SupplementalCategories = 20,

        /// <summary>
        /// Fixture identifier, not repeatable. Max length is 32.
        /// </summary>
        FixtureIdentifier = 22,

        /// <summary>
        /// Keywords. Max length is 64.
        /// </summary>
        Keywords = 25,

        /// <summary>
        /// Location code. Max length is 3.
        /// </summary>
        LocationCode = 26,

        /// <summary>
        /// Location name. Max length is 64.
        /// </summary>
        LocationName = 27,

        /// <summary>
        /// Release date. Format should be CCYYMMDD.
        /// Not repeatable, max length is 8.
        /// <example>
        /// A date will be formatted as CCYYMMDD, e.g. "19890317" for 17 March 1989.
        /// </example>
        /// </summary>
        ReleaseDate = 30,

        /// <summary>
        /// Release time. Format should be HHMMSS±HHMM.
        /// Not repeatable, max length is 11.
        /// <example>
        /// A time value will be formatted as HHMMSS±HHMM, e.g. "090000+0200" for 9 o'clock Berlin time,
        /// two hours ahead of UTC.
        /// </example>
        /// </summary>
        ReleaseTime = 35,

        /// <summary>
        /// Expiration date. Format should be CCYYMMDD.
        /// Not repeatable, max length is 8.
        /// <example>
        /// A date will be formatted as CCYYMMDD, e.g. "19890317" for 17 March 1989.
        /// </example>
        /// </summary>
        ExpirationDate = 37,

        /// <summary>
        /// Expiration time. Format should be HHMMSS±HHMM.
        /// Not repeatable, max length is 11.
        /// <example>
        /// A time value will be formatted as HHMMSS±HHMM, e.g. "090000+0200" for 9 o'clock Berlin time,
        /// two hours ahead of UTC.
        /// </example>
        /// </summary>
        ExpirationTime = 38,

        /// <summary>
        /// Special instructions, not repeatable. Max length is 256.
        /// </summary>
        SpecialInstructions = 40,

        /// <summary>
        /// Action advised, not repeatable. Max length is 2.
        /// </summary>
        ActionAdvised = 42,

        /// <summary>
        /// Reference service. Max length is 10.
        /// </summary>
        ReferenceService = 45,

        /// <summary>
        /// Reference date. Format should be CCYYMMDD.
        /// Not repeatable, max length is 8.
        /// <example>
        /// A date will be formatted as CCYYMMDD, e.g. "19890317" for 17 March 1989.
        /// </example>
        /// </summary>
        ReferenceDate = 47,

        /// <summary>
        /// ReferenceNumber. Max length is 8.
        /// </summary>
        ReferenceNumber = 50,

        /// <summary>
        /// Created date. Format should be CCYYMMDD.
        /// Not repeatable, max length is 8.
        /// <example>
        /// A date will be formatted as CCYYMMDD, e.g. "19890317" for 17 March 1989.
        /// </example>
        /// </summary>
        CreatedDate = 55,

        /// <summary>
        /// Created time. Format should be HHMMSS±HHMM.
        /// Not repeatable, max length is 11.
        /// <example>
        /// A time value will be formatted as HHMMSS±HHMM, e.g. "090000+0200" for 9 o'clock Berlin time,
        /// two hours ahead of UTC.
        /// </example>
        /// </summary>
        CreatedTime = 60,

        /// <summary>
        /// Digital creation date. Format should be CCYYMMDD.
        /// Not repeatable, max length is 8.
        /// <example>
        /// A date will be formatted as CCYYMMDD, e.g. "19890317" for 17 March 1989.
        /// </example>
        /// </summary>
        DigitalCreationDate = 62,

        /// <summary>
        /// Digital creation time. Format should be HHMMSS±HHMM.
        /// Not repeatable, max length is 11.
        /// <example>
        /// A time value will be formatted as HHMMSS±HHMM, e.g. "090000+0200" for 9 o'clock Berlin time,
        /// two hours ahead of UTC.
        /// </example>
        /// </summary>
        DigitalCreationTime = 63,

        /// <summary>
        /// Originating program, not repeatable. Max length is 32.
        /// </summary>
        OriginatingProgram = 65,

        /// <summary>
        /// Program version, not repeatable. Max length is 10.
        /// </summary>
        ProgramVersion = 70,

        /// <summary>
        /// Object cycle, not repeatable. Max length is 1.
        /// </summary>
        ObjectCycle = 75,

        /// <summary>
        /// Byline. Max length is 32.
        /// </summary>
        Byline = 80,

        /// <summary>
        /// Byline title. Max length is 32.
        /// </summary>
        BylineTitle = 85,

        /// <summary>
        /// City, not repeatable. Max length is 32.
        /// </summary>
        City = 90,

        /// <summary>
        /// Sub location, not repeatable. Max length is 32.
        /// </summary>
        SubLocation = 92,

        /// <summary>
        /// Province/State, not repeatable. Max length is 32.
        /// </summary>
        ProvinceState = 95,

        /// <summary>
        /// Country code, not repeatable. Max length is 3.
        /// </summary>
        CountryCode = 100,

        /// <summary>
        /// Country, not repeatable. Max length is 64.
        /// </summary>
        Country = 101,

        /// <summary>
        /// Original transmission reference, not repeatable. Max length is 32.
        /// </summary>
        OriginalTransmissionReference = 103,

        /// <summary>
        /// Headline, not repeatable. Max length is 256.
        /// </summary>
        Headline = 105,

        /// <summary>
        /// Credit, not repeatable. Max length is 32.
        /// </summary>
        Credit = 110,

        /// <summary>
        /// Source, not repeatable. Max length is 32.
        /// </summary>
        Source = 115,

        /// <summary>
        /// Copyright notice, not repeatable. Max length is 128.
        /// </summary>
        CopyrightNotice = 116,

        /// <summary>
        /// Contact. Max length 128.
        /// </summary>
        Contact = 118,

        /// <summary>
        /// Caption, not repeatable. Max length is 2000.
        /// </summary>
        Caption = 120,

        /// <summary>
        /// Local caption.
        /// </summary>
        LocalCaption = 121,

        /// <summary>
        /// Caption writer. Max length is 32.
        /// </summary>
        CaptionWriter = 122,

        /// <summary>
        /// Image type, not repeatable. Max length is 2.
        /// </summary>
        ImageType = 130,

        /// <summary>
        /// Image orientation, not repeatable. Max length is 1.
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
