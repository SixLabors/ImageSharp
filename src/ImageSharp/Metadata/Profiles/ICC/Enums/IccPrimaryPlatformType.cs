// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc
{
    /// <summary>
    /// Enumerates the primary platform/operating system framework for which the profile was created
    /// </summary>
    public enum IccPrimaryPlatformType : uint
    {
        /// <summary>
        /// No platform identified
        /// </summary>
        NotIdentified = 0x00000000,

        /// <summary>
        /// Apple Computer, Inc.
        /// </summary>
        AppleComputerInc = 0x4150504C,          // APPL

        /// <summary>
        /// Microsoft Corporation
        /// </summary>
        MicrosoftCorporation = 0x4D534654,      // MSFT

        /// <summary>
        /// Silicon Graphics, Inc.
        /// </summary>
        SiliconGraphicsInc = 0x53474920,        // SGI

        /// <summary>
        /// Sun Microsystems, Inc.
        /// </summary>
        SunMicrosystemsInc = 0x53554E57,        // SUNW
    }
}
