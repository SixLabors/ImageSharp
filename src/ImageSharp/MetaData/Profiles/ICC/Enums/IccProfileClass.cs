// <copyright file="IccProfileClass.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    /// <summary>
    /// Profile Class Name
    /// </summary>
    internal enum IccProfileClass : uint
    {
        InputDevice = 0x73636E72,       // scnr
        DisplayDevice = 0x6D6E7472,     // mntr
        OutputDevice = 0x70727472,      // prtr
        DeviceLink = 0x6C696E6B,        // link
        ColorSpace = 0x73706163,        // spac
        Abstract = 0x61627374,          // abst
        NamedColor = 0x6E6D636C,        // nmcl
    }
}
