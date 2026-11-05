// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles.Conversion.Icc;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Processing.Processors.Transforms.Linear;

namespace SixLabors.ImageSharp.ColorProfiles;

internal static partial class ColorProfileConverterExtensionsIcc
{
    /// <summary>
    /// Checks the device models and profile classes supported by the black-point detection algorithm.
    /// </summary>
    /// <param name="profile">The external ICC profile.</param>
    /// <returns>Whether the profile describes a supported device rather than an abstract or device-link transform.</returns>
    private static bool SupportsBlackPointCompensation(IccProfile profile)
        => profile.Header.DataColorSpace is IccColorSpaceType.Rgb or IccColorSpaceType.Gray or IccColorSpaceType.Cmyk
        && profile.Header.Class is IccProfileClass.InputDevice or IccProfileClass.DisplayDevice or IccProfileClass.OutputDevice or IccProfileClass.ColorSpace;

    /// <summary>
    /// Detects a neutral source or destination black point in unencoded D50 XYZ units.
    /// </summary>
    /// <param name="profile">The device profile whose black point is required.</param>
    /// <param name="selected">The transform already selected for the connection.</param>
    /// <param name="options">The connection's interpolation and curve policies.</param>
    /// <param name="pcsOptions">The existing D50 Lab/XYZ conversion options.</param>
    /// <param name="destination">Whether destination-table round-trip fitting is required.</param>
    /// <returns>The detected black point, or zero when the profile does not provide the required direction.</returns>
    private static Vector3 DetectBlackPoint(
        IccProfile profile,
        ConversionParams selected,
        ColorConversionOptions options,
        ColorConversionOptions pcsOptions,
        bool destination)
    {
        IccRenderingIntent intent = selected.Intent;
        if (profile.Header.Version.Major >= 4
            && intent is IccRenderingIntent.Perceptual or IccRenderingIntent.Saturation)
        {
            if (IccConverterBase.IsMatrixShaper(profile))
            {
                // Matrix-shaper eligibility depends on tags, not on the selected operator. A profile
                // can supply both matrix/TRC tags and intent tables; native detection still uses its relative black.
                TryDetectSourceBlackPoint(
                    profile,
                    IccRenderingIntent.MediaRelativeColorimetric,
                    options,
                    pcsOptions,
                    null,
                    out Vector3 matrixBlack);

                return matrixBlack;
            }

            // ICC v4 perceptual and saturation tables use the standard reference-medium black.
            // These PCS constants are independent of the device profile. LCMS cmssamp.c uses the same values.
            return new Vector3(.00336F, .0034731F, .00287F);
        }

        if (!destination || !IccConverterBase.HasIntentTable(profile, false, intent))
        {
            TryDetectSourceBlackPoint(profile, intent, options, pcsOptions, destination ? null : selected, out Vector3 sourceBlack);
            return sourceBlack;
        }

        CieLab initial = new(0F, 0F, 0F);
        if (intent == IccRenderingIntent.MediaRelativeColorimetric)
        {
            if (!TryDetectSourceBlackPoint(profile, intent, options, pcsOptions, null, out Vector3 initialBlack))
            {
                return Vector3.Zero;
            }

            CieXyz xyz = new(initialBlack);
            initial = CieLab.FromProfileConnectingSpace(pcsOptions, in xyz);
        }

        // Destination perceptual/saturation fitting starts at Lab zero. It requires the requested
        // reverse transform and relative forward transform, not a forward transform for the requested intent.
        if (!IccConverterBase.SupportsConversion(profile, false, intent)
            || !IccConverterBase.SupportsConversion(profile, true, IccRenderingIntent.MediaRelativeColorimetric))
        {
            return Vector3.Zero;
        }

        ConversionParams destinationReverse = selected;
        ConversionParams relativeForward = new(
            profile, true, IccRenderingIntent.MediaRelativeColorimetric, options.IccInterpolationMethod, options.UseSymmetricIccCurves);

        // LCMS cmsDetectDestinationBlackPoint samples 256 neutral lightness values, in L* units.
        // The stack workspace belongs to connection setup only; no image row retains or rents it.
        Span<double> ramp = stackalloc double[256];
        for (int i = 0; i < ramp.Length; i++)
        {
            CieLab sample = new(i * (100F / 255F), Math.Clamp(initial.A, -50F, 50F), Math.Clamp(initial.B, -50F, 50F));
            ramp[i] = RoundTripBlackPoint(sample, destinationReverse, relativeForward, pcsOptions).L;
        }

        // Preserve the end samples and enforce monotonicity in the same descending order as the reference algorithm.
        for (int i = 254; i > 0; i--)
        {
            ramp[i] = Math.Min(ramp[i], ramp[i + 1]);
        }

        double minimum = ramp[0];
        double maximum = ramp[255];
        if (!(minimum < maximum))
        {
            return Vector3.Zero;
        }

        if (intent == IccRenderingIntent.MediaRelativeColorimetric)
        {
            bool straight = true;
            for (int i = 0; i < ramp.Length; i++)
            {
                double lightness = i * (100D / 255D);
                straight &= lightness <= minimum + (.2D * (maximum - minimum)) || Math.Abs(lightness - ramp[i]) < 4D;
            }

            if (straight)
            {
                return initial.ToProfileConnectingSpace(pcsOptions).ToVector3();
            }
        }

        // Fit only the shadow portion after normalizing output lightness. Relative intent uses 10-50%;
        // perceptual and saturation use 3-25%. These windows belong to the Adobe black-point algorithm.
        double low = intent == IccRenderingIntent.MediaRelativeColorimetric ? .1D : .03D;
        double high = intent == IccRenderingIntent.MediaRelativeColorimetric ? .5D : .25D;
        double blackLightness = FitBlackPointLightness(ramp, minimum, maximum, low, high);
        CieLab black = new((float)blackLightness, initial.A, initial.B);
        return black.ToProfileConnectingSpace(pcsOptions).ToVector3();
    }

    /// <summary>
    /// Detects source black with exact intent eligibility and the native CMYK ink-limit round trip.
    /// </summary>
    /// <param name="profile">The device profile whose black point is required.</param>
    /// <param name="intent">The requested source detection intent.</param>
    /// <param name="options">The connection's interpolation and curve policies.</param>
    /// <param name="pcsOptions">The existing D50 Lab/XYZ conversion options.</param>
    /// <param name="existingForward">The already selected forward transform, or null when detection needs another direction or intent.</param>
    /// <param name="black">The detected D50 XYZ black point, or zero when detection is unavailable.</param>
    /// <returns>Whether detection provides a black point for this profile and intent.</returns>
    private static bool TryDetectSourceBlackPoint(
        IccProfile profile,
        IccRenderingIntent intent,
        ColorConversionOptions options,
        ColorConversionOptions pcsOptions,
        ConversionParams? existingForward,
        out Vector3 black)
    {
        black = Vector3.Zero;
        bool cmykRoundTrip = intent == IccRenderingIntent.MediaRelativeColorimetric
            && profile.Header.Class == IccProfileClass.OutputDevice && profile.Header.DataColorSpace == IccColorSpaceType.Cmyk;
        CieLab lab;
        if (cmykRoundTrip)
        {
            // LCMS BlackPointUsingPerceptualBlack first checks perceptual input support without fallback.
            // Its documented result when that intent is absent is a successfully detected zero black point.
            if (!IccConverterBase.SupportsBlackPointIntent(profile, true, IccRenderingIntent.Perceptual))
            {
                return true;
            }

            if (!IccConverterBase.SupportsConversion(profile, false, IccRenderingIntent.Perceptual)
                || !IccConverterBase.SupportsConversion(profile, true, IccRenderingIntent.MediaRelativeColorimetric))
            {
                return false;
            }

            // Relative CMYK output black cannot use the darkest ink endpoint: that point can exceed
            // the profile's ink limit. The perceptual reverse table supplies attainable device black.
            ConversionParams reverse = new(
                profile, false, IccRenderingIntent.Perceptual, options.IccInterpolationMethod, options.UseSymmetricIccCurves);
            ConversionParams forward = existingForward ?? new(
                profile, true, IccRenderingIntent.MediaRelativeColorimetric, options.IccInterpolationMethod, options.UseSymmetricIccCurves);
            lab = RoundTripBlackPoint(new CieLab(0F, 0F, 0F), reverse, forward, pcsOptions);
            lab = new CieLab(MathF.Min(lab.L, 50F), 0F, 0F);
        }
        else
        {
            // Transform selection may fall back to A0. Detection intentionally does not: native
            // cmsIsIntentSupported requires the exact requested tag or independent matrix-shaper tags.
            if (!IccConverterBase.SupportsBlackPointIntent(profile, true, intent))
            {
                return false;
            }

            ConversionParams forward = existingForward ?? new(profile, true, intent, options.IccInterpolationMethod, options.UseSymmetricIccCurves);
            Vector4 endpoint = profile.Header.DataColorSpace == IccColorSpaceType.Cmyk ? Vector4.One : Vector4.Zero;
            lab = DecodeBlackPointPcs(forward.Converter.Convert(endpoint), forward, pcsOptions);
            lab = new CieLab(lab.L is < 0F or > 50F ? 0F : lab.L, 0F, 0F);
        }

        black = lab.ToProfileConnectingSpace(pcsOptions).ToVector3();
        return true;
    }

    /// <summary>
    /// Evaluates the unadjusted Lab-to-device-to-Lab round trip used only for profile black-point detection.
    /// </summary>
    /// <param name="lab">The input in ordinary L*, a*, b* units.</param>
    /// <param name="reverse">The selected PCS-to-device transform.</param>
    /// <param name="forward">The selected device-to-PCS transform.</param>
    /// <param name="options">The existing D50 Lab/XYZ conversion options.</param>
    /// <returns>The reconstructed Lab value without black-point compensation.</returns>
    private static CieLab RoundTripBlackPoint(CieLab lab, ConversionParams reverse, ConversionParams forward, ColorConversionOptions options)
    {
        Vector4 encoded;
        if (reverse.PcsType == IccColorSpaceType.CieLab)
        {
            encoded = lab.ToScaledVector4();
            if (reverse.Is16BitLutEntry)
            {
                // Legacy mft2 Lab uses 0xFF00 for L*=100. Keep this scale outside the device lookup.
                encoded *= 65280F / 65535F;
            }
        }
        else
        {
            encoded = lab.ToProfileConnectingSpace(options).ToScaledVector4();
        }

        return DecodeBlackPointPcs(forward.Converter.Convert(reverse.Converter.Convert(encoded)), forward, options);
    }

    /// <summary>
    /// Converts one transform's encoded PCS output through the existing Lab/XYZ APIs.
    /// </summary>
    /// <param name="encoded">The unmodified output from the parsed profile operator.</param>
    /// <param name="profile">The output's PCS type and legacy encoding.</param>
    /// <param name="options">The existing D50 Lab/XYZ conversion options.</param>
    /// <returns>The output in ordinary Lab units.</returns>
    private static CieLab DecodeBlackPointPcs(Vector4 encoded, ConversionParams profile, ColorConversionOptions options)
    {
        if (profile.PcsType == IccColorSpaceType.CieLab)
        {
            return CieLab.FromScaledVector4(profile.Is16BitLutEntry ? encoded * (65535F / 65280F) : encoded);
        }

        CieXyz xyz = CieXyz.FromScaledVector4(encoded);
        return CieLab.FromProfileConnectingSpace(options, in xyz);
    }

    /// <summary>
    /// Fits the destination shadow ramp with a quadratic and returns its bounded lightness intercept.
    /// </summary>
    /// <param name="ramp">The monotonic round-trip L* values, sampled uniformly from L*=0 to 100.</param>
    /// <param name="minimum">The ramp's black endpoint.</param>
    /// <param name="maximum">The ramp's white endpoint.</param>
    /// <param name="low">The included lower normalized shadow limit.</param>
    /// <param name="high">The excluded upper normalized shadow limit.</param>
    /// <returns>The fitted L* black point in [0,50], or zero when the regression has no usable root.</returns>
    private static double FitBlackPointLightness(ReadOnlySpan<double> ramp, double minimum, double maximum, double low, double high)
    {
        double sx = 0D;
        double sx2 = 0D;
        double sx3 = 0D;
        double sx4 = 0D;
        double sy = 0D;
        double syx = 0D;
        double syx2 = 0D;
        int count = 0;
        for (int i = 0; i < ramp.Length; i++)
        {
            double y = (ramp[i] - minimum) / (maximum - minimum);
            if (y >= low && y < high)
            {
                double x = i * (100D / 255D);
                double x2 = x * x;
                sx += x;
                sx2 += x2;
                sx3 += x2 * x;
                sx4 += x2 * x2;
                sy += y;
                syx += y * x;
                syx2 += y * x2;
                count++;
            }
        }

        if (count < 4)
        {
            return 0D;
        }

        // Normal equations are row-major for coefficients [constant, linear, quadratic]. All sums and
        // the shared solve use doubles because a shadow intercept is sensitive to cancellation.
        Span<double> matrix = stackalloc double[9] { count, sx, sx2, sx, sx2, sx3, sx2, sx3, sx4 };
        Span<double> coefficients = stackalloc double[3] { sy, syx, syx2 };
        if (!GaussianEliminationSolver.TrySolve(matrix, coefficients))
        {
            return 0D;
        }

        double a = coefficients[2];
        double b = coefficients[1];
        double c = coefficients[0];
        if (Math.Abs(a) < 1E-10D)
        {
            // The reference detector returns zero for a fit with negligible quadratic curvature.
            // Do not substitute a linear intercept, which would change its degenerate-ramp behavior.
            return 0D;
        }

        double discriminant = (b * b) - (4D * a * c);
        return discriminant <= 0D ? 0D : Math.Clamp((-b + Math.Sqrt(discriminant)) / (2D * a), 0D, 50D);
    }
}
