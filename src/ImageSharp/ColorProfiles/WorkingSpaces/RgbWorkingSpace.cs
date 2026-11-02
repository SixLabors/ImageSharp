// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.ColorProfiles.WorkingSpaces;

/// <summary>
/// Base class for all implementations of <see cref="RgbWorkingSpace"/>.
/// </summary>
public abstract class RgbWorkingSpace
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RgbWorkingSpace"/> class.
    /// </summary>
    /// <param name="referenceWhite">The reference white point.</param>
    /// <param name="chromaticityCoordinates">The chromaticity of the rgb primaries.</param>
    protected RgbWorkingSpace(CieXyz referenceWhite, RgbPrimariesChromaticityCoordinates chromaticityCoordinates)
    {
        this.WhitePoint = referenceWhite;
        this.ChromaticityCoordinates = chromaticityCoordinates;

        Matrix4x4 rgbToCieXyz = CreateRgbToCieXyzMatrix(referenceWhite, chromaticityCoordinates);
        this.RgbToCieXyzMatrix = rgbToCieXyz;

        _ = Matrix4x4.Invert(rgbToCieXyz, out Matrix4x4 cieXyzToRgb);
        this.CieXyzToRgbMatrix = cieXyzToRgb;
    }

    /// <summary>
    /// Gets the reference white point
    /// </summary>
    public CieXyz WhitePoint { get; }

    /// <summary>
    /// Gets the chromaticity of the rgb primaries.
    /// </summary>
    public RgbPrimariesChromaticityCoordinates ChromaticityCoordinates { get; }

    /// <summary>
    /// Gets the matrix transforming linear rgb coordinates in this working space to CIE XYZ.
    /// </summary>
    internal Matrix4x4 RgbToCieXyzMatrix { get; }

    /// <summary>
    /// Gets the inverse of <see cref="RgbToCieXyzMatrix"/>, transforming CIE XYZ to linear rgb
    /// coordinates in this working space.
    /// </summary>
    internal Matrix4x4 CieXyzToRgbMatrix { get; }

    /// <summary>
    /// Compresses the linear vectors to their nonlinear equivalents with respect to the energy.
    /// </summary>
    /// <param name="vectors">The span of vectors.</param>
    public abstract void Compress(Span<Vector4> vectors);

    /// <summary>
    /// Expands the nonlinear vectors to their linear equivalents with respect to the energy.
    /// </summary>
    /// <param name="vectors">The span of vectors.</param>
    public abstract void Expand(Span<Vector4> vectors);

    /// <summary>
    /// Compresses linear RGB colors to their nonlinear equivalents.
    /// </summary>
    /// <param name="vectors">The RGB colors, with components in X, Y and Z.</param>
    /// <remarks>Existing four-component bulk overrides also control this overload unless it is overridden.</remarks>
    public virtual void Compress(Span<Vector3> vectors) => this.Compand(vectors, false);

    /// <summary>
    /// Expands nonlinear RGB colors to their linear equivalents.
    /// </summary>
    /// <param name="vectors">The RGB colors, with components in X, Y and Z.</param>
    /// <remarks>Existing four-component bulk overrides also control this overload unless it is overridden.</remarks>
    public virtual void Expand(Span<Vector3> vectors) => this.Compand(vectors, true);

    /// <summary>
    /// Compresses the linear vector to its nonlinear equivalent with respect to the energy.
    /// </summary>
    /// <param name="vector">The vector.</param>
    /// <returns>The <see cref="Vector4"/>.</returns>
    public abstract Vector4 Compress(Vector4 vector);

    /// <summary>
    /// Compresses the linear vector to its nonlinear equivalent with respect to the energy.
    /// </summary>
    /// <param name="vector">The vector.</param>
    /// <returns>The <see cref="Vector4"/>.</returns>
    public abstract Vector4 Expand(Vector4 vector);

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        if (obj is null)
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj.GetType() == this.GetType())
        {
            RgbWorkingSpace other = (RgbWorkingSpace)obj;

            return this.WhitePoint.Equals(other.WhitePoint)
                && this.ChromaticityCoordinates.Equals(other.ChromaticityCoordinates);
        }

        return false;
    }

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(this.GetType(), this.WhitePoint, this.ChromaticityCoordinates);

    /// <summary>
    /// Adapts packed RGB to the existing bulk overrides of a custom working space.
    /// </summary>
    /// <param name="vectors">The packed RGB colors transformed in place.</param>
    /// <param name="expand">True to expand nonlinear colors; false to compress linear colors.</param>
    private void Compand(Span<Vector3> vectors, bool expand)
    {
        InlineArray16<Vector4> storage = default;
        Span<Vector4> buffer = storage;

        // Existing custom spaces only need to implement the four-component contract. Keep their bulk override in control,
        // with alpha one and the same bounded stack block for both directions. Built-in spaces use packed overrides instead.
        for (int offset = 0; offset < vectors.Length; offset += buffer.Length)
        {
            int count = Math.Min(buffer.Length, vectors.Length - offset);
            Span<Vector4> colors = buffer[..count];
            for (int i = 0; i < count; i++)
            {
                colors[i] = new Vector4(vectors[offset + i], 1F);
            }

            if (expand)
            {
                this.Expand(colors);
            }
            else
            {
                this.Compress(colors);
            }

            for (int i = 0; i < count; i++)
            {
                Vector4 color = colors[i];
                vectors[offset + i] = new Vector3(color.X, color.Y, color.Z);
            }
        }
    }

    /// <summary>
    /// Builds the linear RGB-to-XYZ matrix from the primaries and reference white.
    /// </summary>
    /// <param name="referenceWhite">The XYZ value that RGB white must produce.</param>
    /// <param name="chromaticityCoordinates">The xy chromaticities of the red, green and blue primaries.</param>
    /// <returns>The matrix that transforms linear RGB in this working space to XYZ.</returns>
    private static Matrix4x4 CreateRgbToCieXyzMatrix(CieXyz referenceWhite, RgbPrimariesChromaticityCoordinates chromaticityCoordinates)
    {
        float xr = chromaticityCoordinates.R.X;
        float xg = chromaticityCoordinates.G.X;
        float xb = chromaticityCoordinates.B.X;
        float yr = chromaticityCoordinates.R.Y;
        float yg = chromaticityCoordinates.G.Y;
        float yb = chromaticityCoordinates.B.Y;

        // Convert each xy primary to XYZ with unit luminance (Y = 1).
        float mXr = xr / yr;
        float mZr = (1 - xr - yr) / yr;

        float mXg = xg / yg;
        float mZg = (1 - xg - yg) / yg;

        float mXb = xb / yb;
        float mZb = (1 - xb - yb) / yb;

        Matrix4x4 xyzMatrix = new()
        {
            M11 = mXr,
            M21 = mXg,
            M31 = mXb,
            M12 = 1F,
            M22 = 1F,
            M32 = 1F,
            M13 = mZr,
            M23 = mZg,
            M33 = mZb,
            M44 = 1F
        };

        Matrix4x4.Invert(xyzMatrix, out Matrix4x4 inverseXyzMatrix);

        // Scale the three primaries so RGB white (1,1,1) maps to the reference white.
        Vector3 vector = Vector3.Transform(referenceWhite.AsVector3Unsafe(), inverseXyzMatrix);

        // Each row stores one primary's contribution to X, Y and Z for Vector3.Transform.
        return new Matrix4x4
        {
            M11 = vector.X * mXr,
            M21 = vector.Y * mXg,
            M31 = vector.Z * mXb,
            M12 = vector.X,
            M22 = vector.Y,
            M32 = vector.Z,
            M13 = vector.X * mZr,
            M23 = vector.Y * mZg,
            M33 = vector.Z * mZb,
            M44 = 1F
        };
    }
}
