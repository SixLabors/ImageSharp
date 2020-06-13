// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.ColorSpaces.Companding;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion
{
    /// <summary>
    /// Rec. 2020 (ITU-R Recommendation BT.2020F) working space.
    /// </summary>
    public sealed class Rec2020WorkingSpace : RgbWorkingSpace
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Rec2020WorkingSpace" /> class.
        /// </summary>
        /// <param name="referenceWhite">The reference white point.</param>
        /// <param name="chromaticityCoordinates">The chromaticity of the rgb primaries.</param>
        public Rec2020WorkingSpace(CieXyz referenceWhite, RgbPrimariesChromaticityCoordinates chromaticityCoordinates)
            : base(referenceWhite, chromaticityCoordinates)
        {
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public override float Compress(float channel) => Rec2020Companding.Compress(channel);

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public override float Expand(float channel) => Rec2020Companding.Expand(channel);
    }
}
