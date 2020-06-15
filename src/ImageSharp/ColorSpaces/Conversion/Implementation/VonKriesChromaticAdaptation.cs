// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion
{
    /// <summary>
    /// Implementation of the von Kries chromatic adaptation model.
    /// </summary>
    /// <remarks>
    /// Transformation described here:
    /// http://www.brucelindbloom.com/index.html?Eqn_ChromAdapt.html
    /// </remarks>
    public sealed class VonKriesChromaticAdaptation : IChromaticAdaptation
    {
        private readonly CieXyzAndLmsConverter converter;

        /// <summary>
        /// Initializes a new instance of the <see cref="VonKriesChromaticAdaptation"/> class.
        /// </summary>
        public VonKriesChromaticAdaptation()
            : this(new CieXyzAndLmsConverter())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="VonKriesChromaticAdaptation"/> class.
        /// </summary>
        /// <param name="transformationMatrix">
        /// The transformation matrix used for the conversion (definition of the cone response domain).
        /// <see cref="LmsAdaptationMatrix"/>
        /// </param>
        public VonKriesChromaticAdaptation(Matrix4x4 transformationMatrix)
            : this(new CieXyzAndLmsConverter(transformationMatrix))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="VonKriesChromaticAdaptation"/> class.
        /// </summary>
        /// <param name="converter">The color converter</param>
        internal VonKriesChromaticAdaptation(CieXyzAndLmsConverter converter) => this.converter = converter;

        /// <inheritdoc/>
        public CieXyz Transform(in CieXyz source, in CieXyz sourceWhitePoint, in CieXyz destinationWhitePoint)
        {
            if (sourceWhitePoint.Equals(destinationWhitePoint))
            {
                return source;
            }

            Lms sourceColorLms = this.converter.Convert(source);
            Lms sourceWhitePointLms = this.converter.Convert(sourceWhitePoint);
            Lms targetWhitePointLms = this.converter.Convert(destinationWhitePoint);

            Vector3 vector = targetWhitePointLms.ToVector3() / sourceWhitePointLms.ToVector3();
            var targetColorLms = new Lms(Vector3.Multiply(vector, sourceColorLms.ToVector3()));

            return this.converter.Convert(targetColorLms);
        }

        /// <inheritdoc/>
        public void Transform(
            ReadOnlySpan<CieXyz> source,
            Span<CieXyz> destination,
            CieXyz sourceWhitePoint,
            in CieXyz destinationWhitePoint)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            if (sourceWhitePoint.Equals(destinationWhitePoint))
            {
                source.CopyTo(destination.Slice(0, count));
                return;
            }

            ref CieXyz sourceRef = ref MemoryMarshal.GetReference(source);
            ref CieXyz destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref CieXyz sp = ref Unsafe.Add(ref sourceRef, i);
                ref CieXyz dp = ref Unsafe.Add(ref destRef, i);

                Lms sourceColorLms = this.converter.Convert(sp);
                Lms sourceWhitePointLms = this.converter.Convert(sourceWhitePoint);
                Lms targetWhitePointLms = this.converter.Convert(destinationWhitePoint);

                Vector3 vector = targetWhitePointLms.ToVector3() / sourceWhitePointLms.ToVector3();
                var targetColorLms = new Lms(Vector3.Multiply(vector, sourceColorLms.ToVector3()));

                dp = this.converter.Convert(targetColorLms);
            }
        }
    }
}
