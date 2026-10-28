// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Companding;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc;

/// <content>
/// Provides logic for identifying canonical IEC 61966-2-1 (sRGB) matrix-TRC ICC profiles,
/// distinguishing them from appearance or device-specific variants.
/// </content>
public sealed partial class IccProfile
{
    // sRGB v2 Preference
    private static readonly IccProfileId StandardRgbV2 = new(0x3D0EB2DE, 0xAE9397BE, 0x9B6726CE, 0x8C0A43CE);

    // sRGB v4 Preference
    private static readonly IccProfileId StandardRgbV4 = new(0x34562ABF, 0x994CCD06, 0x6D2C5721, 0xD0D68C5D);

    // Compact sRGB v4 working profile. Its fitted coefficients differ from the rounded IEC coefficients.
    private static readonly IccProfileId CompactRgbV4 = new(0xA3B2ABDF, 0x5CA70312, 0xA855A4EC, 0x357AD1F3);

    /// <summary>
    /// Gets the supplied profile when conversion between its color space and sRGB is required.
    /// </summary>
    /// <param name="profile">The profile to check.</param>
    /// <param name="value">The supplied profile when conversion is required; otherwise, <see langword="null"/>.</param>
    /// <returns>Whether the profile is present and is not a canonical sRGB matrix-TRC profile.</returns>
    internal static bool TryGetIccProfileForColorConversion(IccProfile? profile, [NotNullWhen(true)] out IccProfile? value)
    {
        value = null;

        if (profile is null)
        {
            return false;
        }

        if (profile.IsCanonicalSrgbMatrixTrc())
        {
            return false;
        }

        value = profile;
        return true;
    }

    /// <summary>
    /// Detects canonical sRGB matrix+TRC profiles quickly and safely.
    /// Rules:
    /// 1) Accept known sRGB v2, v4 and compact working profiles by profile ID.
    /// 2) Require RGB, PCS=XYZ, ICC v2 or v4, and no A2B*/B2A* LUTs.
    /// 3) Require rTRC, gTRC, bTRC to be identical and to encode the sRGB transfer function.
    /// 4) Accept if rXYZ/gXYZ/bXYZ already match the D50-adapted sRGB colorants within tolerance.
    /// 5) If white point ≈ D65, adapt only the colorant columns to D50 using Bradford
    ///    via <see cref="VonKriesChromaticAdaptation.Transform(in CieXyz, ValueTuple{CieXyz, CieXyz}, Matrix4x4, Matrix4x4)"/> and then compare.
    /// This rejects channel-swapped and appearance profiles while allowing real sRGB.
    /// </summary>
    /// <remarks>
    /// Reference D50-adapted sRGB colorants from Bruce Lindbloom:
    /// <see href="http://brucelindbloom.com/index.html?Eqn_RGB_XYZ_Matrix.html"/>
    /// R=(0.4360747, 0.2225045, 0.0139322)
    /// G=(0.3850649, 0.7168786, 0.0971045)
    /// B=(0.1430804, 0.0606169, 0.7141733)
    /// </remarks>
    internal bool IsCanonicalSrgbMatrixTrc()
    {
        IccProfileHeader h = this.Header;

        // Fast path for known sRGB profile IDs
        if (h.Id == StandardRgbV2 || h.Id == StandardRgbV4 || h.Id == CompactRgbV4)
        {
            return true;
        }

        // Header gating to avoid parsing work for obvious non-matches
        if (h.FileSignature != "acsp")
        {
            return false;
        }

        if (h.DataColorSpace != IccColorSpaceType.Rgb)
        {
            return false;
        }

        if (h.ProfileConnectionSpace != IccColorSpaceType.CieXyz)
        {
            return false;
        }

        if (h.Version.Major is not 2 and not 4)
        {
            return false;
        }

        this.InitializeEntries();
        IccTagDataEntry[] entries = this.entries;

        // Reject device/display LUT profiles. We only accept matrix+TRC encodings.
        if (Has(entries, IccProfileTag.AToB0) || Has(entries, IccProfileTag.AToB1) || Has(entries, IccProfileTag.AToB2) ||
            Has(entries, IccProfileTag.BToA0) || Has(entries, IccProfileTag.BToA1) || Has(entries, IccProfileTag.BToA2))
        {
            return false;
        }

        // Required matrix+TRC tags
        if (!TryGetXyz(entries, IccProfileTag.MediaWhitePoint, out Vector3 wtpt))
        {
            return false;
        }

        if (!TryGetXyz(entries, IccProfileTag.RedMatrixColumn, out Vector3 rXYZ))
        {
            return false;
        }

        if (!TryGetXyz(entries, IccProfileTag.GreenMatrixColumn, out Vector3 gXYZ))
        {
            return false;
        }

        if (!TryGetXyz(entries, IccProfileTag.BlueMatrixColumn, out Vector3 bXYZ))
        {
            return false;
        }

        // Matching channel curves alone do not establish sRGB. Linear RGB and gamma-only profiles can use
        // the same primaries. Check the transfer function before allowing conversion to be skipped.
        if (!TryGetTrc(entries, IccProfileTag.RedTrc, out Trc tR))
        {
            return false;
        }

        if (!TryGetTrc(entries, IccProfileTag.GreenTrc, out Trc tG))
        {
            return false;
        }

        if (!TryGetTrc(entries, IccProfileTag.BlueTrc, out Trc tB))
        {
            return false;
        }

        if (!tR.Equals(tG) || !tR.Equals(tB) || !tR.IsSrgbLike())
        {
            return false;
        }

        // D50-adapted sRGB colorants (compare as columns: r,g,b), tight epsilon
        const float eps = 2e-3F;
        Vector3 rRef = new(0.4360747F, 0.2225045F, 0.0139322F);
        Vector3 gRef = new(0.3850649F, 0.7168786F, 0.0971045F);
        Vector3 bRef = new(0.1430804F, 0.0606169F, 0.7141733F);

        // First, accept if the stored colorants are already the D50 sRGB primaries.
        // Many v2 sRGB profiles store D50-adapted colorants while declaring wtpt≈D65.
        if (Near(rXYZ, rRef, eps) && Near(gXYZ, gRef, eps) && Near(bXYZ, bRef, eps))
        {
            return true;
        }

        // If the profile declares a D65 white, adapt the colorant columns to D50 and compare again.
        // We never adapt when they already match, to avoid compounding rounding.
        if (Near(wtpt, KnownIlluminants.D65.AsVector3Unsafe(), 2e-3F))
        {
            CieXyz fromWp = new(wtpt);          // Declared white
            CieXyz toWp = KnownIlluminants.D50; // PCS white
            Matrix4x4 matrix = KnownChromaticAdaptationMatrices.Bradford;
            Matrix4x4.Invert(matrix, out Matrix4x4 inverseMatrix);

            rXYZ = VonKriesChromaticAdaptation.Transform(new CieXyz(rXYZ), (fromWp, toWp), matrix, inverseMatrix).AsVector3Unsafe();
            gXYZ = VonKriesChromaticAdaptation.Transform(new CieXyz(gXYZ), (fromWp, toWp), matrix, inverseMatrix).AsVector3Unsafe();
            bXYZ = VonKriesChromaticAdaptation.Transform(new CieXyz(bXYZ), (fromWp, toWp), matrix, inverseMatrix).AsVector3Unsafe();
        }

        // Require identity mapping of primaries, no permutation
        if (!Near(rXYZ, rRef, eps) || !Near(gXYZ, gRef, eps) || !Near(bXYZ, bRef, eps))
        {
            return false;
        }

        return true;

        static bool Has(ReadOnlySpan<IccTagDataEntry> span, IccProfileTag tag)
        {
            for (int i = 0; i < span.Length; i++)
            {
                if (span[i].TagSignature == tag)
                {
                    return true;
                }
            }

            return false;
        }

        static bool TryGetXyz(ReadOnlySpan<IccTagDataEntry> span, IccProfileTag tag, out Vector3 xyz)
        {
            for (int i = 0; i < span.Length; i++)
            {
                IccTagDataEntry e = span[i];
                if (e.TagSignature != tag)
                {
                    continue;
                }

                if (e is IccXyzTagDataEntry x && x.Data is { Length: >= 1 })
                {
                    xyz = x.Data[0];
                    return true;
                }

                break;
            }

            xyz = default;
            return false;
        }

        static bool TryGetTrc(ReadOnlySpan<IccTagDataEntry> span, IccProfileTag tag, out Trc trc)
        {
            for (int i = 0; i < span.Length; i++)
            {
                IccTagDataEntry e = span[i];
                if (e.TagSignature != tag)
                {
                    continue;
                }

                if (e is IccParametricCurveTagDataEntry p)
                {
                    trc = Trc.FromParametric(p.Curve);
                    return true;
                }

                if (e is IccCurveTagDataEntry c)
                {
                    trc = Trc.FromCurveLut(c.CurveData);
                    return true;
                }

                break;
            }

            trc = default;
            return false;
        }

        static bool Near(in Vector3 a, in Vector3 b, float tol)
            => MathF.Abs(a.X - b.X) <= tol &&
               MathF.Abs(a.Y - b.Y) <= tol &&
               MathF.Abs(a.Z - b.Z) <= tol;
    }

    /// <summary>
    /// Describes an ICC tone curve without copying its parameters or sampled values.
    /// </summary>
    private readonly struct Trc : IEquatable<Trc>
    {
        private readonly float[]? samples;
        private readonly byte kind; // 0 = none, 1 = parametric, 2 = sampled
        private readonly float g; // parametric payload or downsampled hash
        private readonly float a;
        private readonly float b;
        private readonly float c;
        private readonly float d;
        private readonly float e;
        private readonly float f;
        private readonly int n; // for sampled, length or a small signature

        /// <summary>
        /// Initializes a new instance of the <see cref="Trc"/> struct without copying the sampled array.
        /// </summary>
        /// <param name="kind">One for parametric curves; two for sampled curves.</param>
        /// <param name="g">The gamma coefficient or first sample.</param>
        /// <param name="a">The A coefficient or first-quarter sample.</param>
        /// <param name="b">The B coefficient or middle sample.</param>
        /// <param name="c">The C coefficient or third-quarter sample.</param>
        /// <param name="d">The D coefficient or last sample.</param>
        /// <param name="e">The E coefficient.</param>
        /// <param name="f">The F coefficient.</param>
        /// <param name="n">The parametric type or sampled array length.</param>
        /// <param name="samples">The profile's original sampled array, or null for parametric curves.</param>
        private Trc(byte kind, float g, float a, float b, float c, float d, float e, float f, int n, float[]? samples)
        {
            this.samples = samples;
            this.kind = kind;
            this.g = g;
            this.a = a;
            this.b = b;
            this.c = c;
            this.d = d;
            this.e = e;
            this.f = f;
            this.n = n;
        }

        /// <summary>
        /// Copies the fixed coefficients needed for curve comparison.
        /// </summary>
        /// <param name="c">The profile's parametric curve.</param>
        /// <returns>The curve descriptor.</returns>
        public static Trc FromParametric(IccParametricCurve c)
            => new(1, c.G, c.A, c.B, c.C, c.D, c.E, c.F, (int)c.Type, null);

        /// <summary>
        /// Describes the original sampled curve without allocating or copying sample storage.
        /// </summary>
        /// <param name="data">The profile's sampled curve.</param>
        /// <returns>The curve descriptor.</returns>
        public static Trc FromCurveLut(float[] data)
        {
            // Retain the original array for full sequence comparison. The fingerprint rejects different curves
            // cheaply, but matching five points is not enough to prove that the complete curves match.
            if (data == null)
            {
                return default;
            }

            int n = data.Length;
            if (n == 0)
            {
                return default;
            }

            // Downsample a few points to a robust fingerprint
            // Use fixed indices to avoid allocations
            float s0 = data[0];
            float s1 = data[n >> 2];
            float s2 = data[n >> 1];
            float s3 = data[(n * 3) >> 2];
            float s4 = data[n - 1];

            return new Trc(2, s0, s1, s2, s3, s4, 0F, 0F, n, data);
        }

        public override bool Equals(object? obj) => obj is Trc trc && this.Equals(trc);

        /// <summary>
        /// Compares stored curve values exactly before testing their agreement with sRGB.
        /// </summary>
        /// <param name="other">The other channel's curve.</param>
        /// <returns>Whether the complete stored curves match.</returns>
        public bool Equals(Trc other)
        {
            if (this.kind != other.kind)
            {
                return false;
            }

            if (this.kind == 0)
            {
                return false;
            }

            if (this.kind == 1)
            {
                // These values come from ICC integer encodings, not separate calculations. Equal stored
                // coefficients decode identically. A tolerance here would allow different channel curves.
                return this.n == other.n &&
                       this.g == other.g && this.a == other.a &&
                       this.b == other.b && this.c == other.c &&
                       this.d == other.d && this.e == other.e && this.f == other.f;
            }

            // Compare the fingerprint first, then every stored sample. ICC decodes each unsigned
            // sixteen-bit code to the same float bits, including positive zero. Byte comparison uses
            // the runtime's SIMD sequence comparison; float SequenceEqual compares samples individually.
            // These spans view the existing arrays. Neither curve is copied or converted.
            return this.n == other.n &&
                   this.g == other.g && this.a == other.a &&
                   this.b == other.b && this.c == other.c && this.d == other.d &&
                   MemoryMarshal.AsBytes(this.samples.AsSpan()).SequenceEqual(MemoryMarshal.AsBytes(other.samples.AsSpan()));
        }

        /// <summary>
        /// Checks the sRGB transfer function before its profile can be removed without conversion.
        /// </summary>
        /// <returns>Whether the curve encodes sRGB within its ICC storage precision.</returns>
        public bool IsSrgbLike()
        {
            if (this.kind == 1)
            {
                // ICC stores these coefficients as signed 16.16 numbers. Allow one stored unit for
                // their quantization. Gamma-only curves lack the linear segment and cannot match.
                const float parameterTolerance = 1F / 65536F;
                return (this.n == (int)IccParametricCurveType.SRgb || this.n == (int)IccParametricCurveType.Type5) &&
                       MathF.Abs(this.g - 2.4F) <= parameterTolerance &&
                       MathF.Abs(this.a - (1F / 1.055F)) <= parameterTolerance &&
                       MathF.Abs(this.b - (0.055F / 1.055F)) <= parameterTolerance &&
                       MathF.Abs(this.c - (1F / 12.92F)) <= parameterTolerance &&
                       MathF.Abs(this.d - 0.04045F) <= parameterTolerance &&
                       MathF.Abs(this.e) <= parameterTolerance &&
                       MathF.Abs(this.f) <= parameterTolerance;
            }

            if (this.kind != 2 || this.n < 2)
            {
                return false;
            }

            // ICC curve nodes are unsigned sixteen-bit codes. Compare rounded reference codes within
            // one stored level, rather than allowing float lookup-coordinate rounding to decide the result.
            // Midpoints are interpolated values, not stored codes: retain their continuous one-level error bound.
            const float sampleScale = ushort.MaxValue;
            const float sampleTolerance = 1F / sampleScale;
            float[] table = SRgbCompanding.GetExpandLookupTable();
            LutOperator lookup = new(table, false, table.Length - 1);
            ReadOnlySpan<float> samples = this.samples;
            ref float first = ref MemoryMarshal.GetReference(samples);
            float step = 1F / (this.n - 1);

            // Check the first endpoint separately. Every remaining node owns the segment immediately before it.
            if (!(MathF.Abs(MathF.Round(samples[0] * sampleScale) - MathF.Round(table[0] * sampleScale)) <= 1F))
            {
                return false;
            }

            int i = 1;

            if (Vector512.IsHardwareAccelerated)
            {
                nuint count = (this.n - i).Vector512Count<float>();
                Vector512<float> offsets = Vector512.Create(0F, 1F, 2F, 3F, 4F, 5F, 6F, 7F, 8F, 9F, 10F, 11F, 12F, 13F, 14F, 15F);
                Vector512<float> stepVector = Vector512.Create(step);
                Vector512<float> scale = Vector512.Create(sampleScale);
                Vector512<float> tolerance = Vector512.Create(sampleTolerance);

                // Each lane checks one node and its preceding segment. Unaligned loads keep both arrays in lane order.
                // Recreate integer coordinates for each block so repeated addition cannot accumulate input drift.
                for (nuint block = 0; block < count; block++, i += Vector512<float>.Count)
                {
                    Vector512<float> indices = Vector512.Create((float)i) + offsets;
                    Vector512<float> current = Vector512.LoadUnsafe(ref first, (uint)i);
                    Vector512<float> previous = Vector512.LoadUnsafe(ref first, (uint)(i - 1));
                    Vector512<float> expected = lookup.ConvertForward(indices * stepVector);
                    Vector512<float> difference = Vector512.Abs(Vector512.Round(current * scale) - Vector512.Round(expected * scale));
                    Vector512<float> midpoint = (previous + current) * Vector512.Create(0.5F);
                    Vector512<float> expectedMidpoint = lookup.ConvertForward((indices - Vector512.Create(0.5F)) * stepVector);

                    // Ordered comparisons also reject invalid NaN samples. No profile samples or shared table entries are changed.
                    if (!Vector512.LessThanOrEqualAll(difference, Vector512<float>.One) ||
                        !Vector512.LessThanOrEqualAll(Vector512.Abs(midpoint - expectedMidpoint), tolerance))
                    {
                        return false;
                    }
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                nuint count = (this.n - i).Vector256Count<float>();
                Vector256<float> offsets = Vector256.Create(0F, 1F, 2F, 3F, 4F, 5F, 6F, 7F);
                Vector256<float> stepVector = Vector256.Create(step);
                Vector256<float> scale = Vector256.Create(sampleScale);
                Vector256<float> tolerance = Vector256.Create(sampleTolerance);

                // Each lane checks one node and its preceding segment. Unaligned loads keep both arrays in lane order.
                // Recreate integer coordinates for each block so repeated addition cannot accumulate input drift.
                for (nuint block = 0; block < count; block++, i += Vector256<float>.Count)
                {
                    Vector256<float> indices = Vector256.Create((float)i) + offsets;
                    Vector256<float> current = Vector256.LoadUnsafe(ref first, (uint)i);
                    Vector256<float> previous = Vector256.LoadUnsafe(ref first, (uint)(i - 1));
                    Vector256<float> expected = lookup.ConvertForward(indices * stepVector);
                    Vector256<float> difference = Vector256.Abs(Vector256.Round(current * scale) - Vector256.Round(expected * scale));
                    Vector256<float> midpoint = (previous + current) * Vector256.Create(0.5F);
                    Vector256<float> expectedMidpoint = lookup.ConvertForward((indices - Vector256.Create(0.5F)) * stepVector);

                    // Ordered comparisons also reject invalid NaN samples. No profile samples or shared table entries are changed.
                    if (!Vector256.LessThanOrEqualAll(difference, Vector256<float>.One) ||
                        !Vector256.LessThanOrEqualAll(Vector256.Abs(midpoint - expectedMidpoint), tolerance))
                    {
                        return false;
                    }
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                nuint count = (this.n - i).Vector128Count<float>();
                Vector128<float> offsets = Vector128.Create(0F, 1F, 2F, 3F);
                Vector128<float> stepVector = Vector128.Create(step);
                Vector128<float> scale = Vector128.Create(sampleScale);
                Vector128<float> tolerance = Vector128.Create(sampleTolerance);

                // Each lane checks one node and its preceding segment. Unaligned loads keep both arrays in lane order.
                // Recreate integer coordinates for each block so repeated addition cannot accumulate input drift.
                for (nuint block = 0; block < count; block++, i += Vector128<float>.Count)
                {
                    Vector128<float> indices = Vector128.Create((float)i) + offsets;
                    Vector128<float> current = Vector128.LoadUnsafe(ref first, (uint)i);
                    Vector128<float> previous = Vector128.LoadUnsafe(ref first, (uint)(i - 1));
                    Vector128<float> expected = lookup.ConvertForward(indices * stepVector);
                    Vector128<float> difference = Vector128.Abs(Vector128.Round(current * scale) - Vector128.Round(expected * scale));
                    Vector128<float> midpoint = (previous + current) * Vector128.Create(0.5F);
                    Vector128<float> expectedMidpoint = lookup.ConvertForward((indices - Vector128.Create(0.5F)) * stepVector);

                    // Ordered comparisons also reject invalid NaN samples. No profile samples or shared table entries are changed.
                    if (!Vector128.LessThanOrEqualAll(difference, Vector128<float>.One) ||
                        !Vector128.LessThanOrEqualAll(Vector128.Abs(midpoint - expectedMidpoint), tolerance))
                    {
                        return false;
                    }
                }
            }

            // Narrower loops process only the remainder. With hardware intrinsics disabled, use these same comparisons for every node.
            for (; i < this.n; i++)
            {
                float expected = lookup.ConvertForward(i * step);
                float difference = MathF.Abs(MathF.Round(samples[i] * sampleScale) - MathF.Round(expected * sampleScale));
                float midpoint = (samples[i - 1] + samples[i]) * 0.5F;
                float expectedMidpoint = lookup.ConvertForward((i - 0.5F) * step);
                if (!(difference <= 1F) || !(MathF.Abs(midpoint - expectedMidpoint) <= sampleTolerance))
                {
                    return false;
                }
            }

            return true;
        }

        public override int GetHashCode()
        {
            int a = HashCode.Combine(this.kind, this.g, this.a, this.b, this.c, this.d, this.e);
            int b = HashCode.Combine(this.f, this.n);
            return HashCode.Combine(a, b);
        }
    }
}
