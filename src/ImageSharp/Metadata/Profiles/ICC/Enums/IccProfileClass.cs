// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc
{
    /// <summary>
    /// Profile Class Name
    /// </summary>
    public enum IccProfileClass : uint
    {
        /// <summary>
        /// Input profiles are generally used with devices such as scanners and
        /// digital cameras. The types of profiles available for use as Input
        /// profiles are N-component LUT-based, Three-component matrix-based,
        /// and monochrome.
        /// </summary>
        InputDevice = 0x73636E72,       // scnr

        /// <summary>
        /// This class of profiles represents display devices such as monitors.
        /// The types of profiles available for use as Display profiles are
        /// N-component LUT-based, Three-component matrix-based, and monochrome.
        /// </summary>
        DisplayDevice = 0x6D6E7472,     // mntr

        /// <summary>
        /// Output profiles are used to support devices such as printers and
        /// film recorders. The types of profiles available for use as Output
        /// profiles are N-component LUT-based and Monochrome.
        /// </summary>
        OutputDevice = 0x70727472,      // prtr

        /// <summary>
        /// This profile contains a pre-evaluated transform that cannot be undone,
        /// which represents a one-way link or connection between devices. It does
        /// not represent any device model nor can it be embedded into images.
        /// </summary>
        DeviceLink = 0x6C696E6B,        // link

        /// <summary>
        /// This profile provides the relevant information to perform a transformation
        /// between color encodings and the PCS. This type of profile is based on
        /// modeling rather than device measurement or characterization data.
        /// ColorSpace profiles may be embedded in images.
        /// </summary>
        ColorSpace = 0x73706163,        // spac

        /// <summary>
        /// This profile represents abstract transforms and does not represent any
        /// device model. Color transformations using Abstract profiles are performed
        /// from PCS to PCS. Abstract profiles cannot be embedded in images.
        /// </summary>
        Abstract = 0x61627374,          // abst

        /// <summary>
        /// NamedColor profiles can be thought of as sibling profiles to device profiles.
        /// For a given device there would be one or more device profiles to handle
        /// process color conversions and one or more named color profiles to handle
        /// named colors.
        /// </summary>
        NamedColor = 0x6E6D636C,        // nmcl
    }
}
