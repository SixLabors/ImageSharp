// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Companding;

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
                // parametric: exact parameter match and type match
                return this.n == other.n &&
                       this.g == other.g && this.a == other.a &&
                       this.b == other.b && this.c == other.c &&
                       this.d == other.d && this.e == other.e && this.f == other.f;
            }

            // Compare the fingerprint first, then every sample. No sample array is copied.
            return this.n == other.n &&
                   this.g == other.g && this.a == other.a &&
                   this.b == other.b && this.c == other.c && this.d == other.d &&
                   this.samples.AsSpan().SequenceEqual(other.samples);
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

            // Sampled ICC curves store unsigned sixteen-bit values. Check every node against the
            // shared sRGB expansion path within one stored level. Its lookup table is reused, not
            // allocated per profile. This work is profile selection, never a pixel-row operation.
            const float sampleTolerance = 1F / ushort.MaxValue;
            float[] table = SRgbCompanding.GetExpandLookupTable();
            float step = 1F / (this.n - 1);

            for (int i = 0; i < this.n; i++)
            {
                // X checks the stored node. Y checks the midpoint of the preceding segment, so a
                // sparse identity table [0,1] cannot pass just because both endpoints match sRGB.
                // Obtain the shared table once above; this loop performs no cache lookups or powers.
                Vector4 inputs = new(i * step, (i - 0.5F) * step, 0F, 0F);
                Vector4 expected = CompandingUtilities.Compand(inputs, table);
                if (!(MathF.Abs(this.samples![i] - expected.X) <= sampleTolerance))
                {
                    return false;
                }

                if (i > 0)
                {
                    float interpolated = (this.samples[i - 1] + this.samples[i]) * 0.5F;
                    if (!(MathF.Abs(interpolated - expected.Y) <= sampleTolerance))
                    {
                        return false;
                    }
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
