// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

#pragma warning disable SA1117 // Parameters should be on same line or separate lines
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp;

/// <summary>
/// A structure encapsulating a 5x4 matrix used for transforming the color and alpha components of an image.
/// </summary>
public partial struct ColorMatrix
{
    [UnscopedRef]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ref Impl AsImpl() => ref Unsafe.As<ColorMatrix, Impl>(ref this);

    [UnscopedRef]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly ref readonly Impl AsROImpl() => ref Unsafe.As<ColorMatrix, Impl>(ref Unsafe.AsRef(in this));

    internal struct Impl : IEquatable<Impl>
    {
        public Vector4 X;
        public Vector4 Y;
        public Vector4 Z;
        public Vector4 W;
        public Vector4 V;

        public static Impl Identity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                Impl result;

                result.X = Vector4.UnitX;
                result.Y = Vector4.UnitY;
                result.Z = Vector4.UnitZ;
                result.W = Vector4.UnitW;
                result.V = Vector4.Zero;

                return result;
            }
        }

        public readonly bool IsIdentity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get =>
                (this.X == Vector4.UnitX)
                && (this.Y == Vector4.UnitY)
                && (this.Z == Vector4.UnitZ)
                && (this.W == Vector4.UnitW)
                && (this.V == Vector4.Zero);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Impl operator +(in Impl left, in Impl right)
        {
            Impl result;

            result.X = left.X + right.X;
            result.Y = left.Y + right.Y;
            result.Z = left.Z + right.Z;
            result.W = left.W + right.W;
            result.V = left.V + right.V;

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Impl operator -(in Impl left, in Impl right)
        {
            Impl result;

            result.X = left.X - right.X;
            result.Y = left.Y - right.Y;
            result.Z = left.Z - right.Z;
            result.W = left.W - right.W;
            result.V = left.V - right.V;

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Impl operator -(in Impl value)
        {
            Impl result;

            result.X = -value.X;
            result.Y = -value.Y;
            result.Z = -value.Z;
            result.W = -value.W;
            result.V = -value.V;

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Impl operator *(in Impl left, in Impl right)
        {
            Impl result;

            // result.X = Transform(left.X, in right);
            result.X = right.X * left.X.X;
            result.X += right.Y * left.X.Y;
            result.X += right.Z * left.X.Z;
            result.X += right.W * left.X.W;

            // result.Y = Transform(left.Y, in right);
            result.Y = right.X * left.Y.X;
            result.Y += right.Y * left.Y.Y;
            result.Y += right.Z * left.Y.Z;
            result.Y += right.W * left.Y.W;

            // result.Z = Transform(left.Z, in right);
            result.Z = right.X * left.Z.X;
            result.Z += right.Y * left.Z.Y;
            result.Z += right.Z * left.Z.Z;
            result.Z += right.W * left.Z.W;

            // result.W = Transform(left.W, in right);
            result.W = right.X * left.W.X;
            result.W += right.Y * left.W.Y;
            result.W += right.Z * left.W.Z;
            result.W += right.W * left.W.W;

            // result.V = Transform(left.V, in right);
            result.V = right.X * left.V.X;
            result.V += right.Y * left.V.Y;
            result.V += right.Z * left.V.Z;
            result.V += right.W * left.V.W;

            result.V += right.V;

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Impl operator *(in Impl left, float right)
        {
            Impl result;

            result.X = left.X * right;
            result.Y = left.Y * right;
            result.Z = left.Z * right;
            result.W = left.W * right;
            result.V = left.V * right;

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(in Impl left, in Impl right) =>
            (left.X == right.X)
            && (left.Y == right.Y)
            && (left.Z == right.Z)
            && (left.W == right.W)
            && (left.V == right.V);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(in Impl left, in Impl right) =>
            (left.X != right.X)
            && (left.Y != right.Y)
            && (left.Z != right.Z)
            && (left.W != right.W)
            && (left.V != right.V);

        [UnscopedRef]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref ColorMatrix AsColorMatrix() => ref Unsafe.As<Impl, ColorMatrix>(ref this);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Init(
            float m11, float m12, float m13, float m14,
            float m21, float m22, float m23, float m24,
            float m31, float m32, float m33, float m34,
            float m41, float m42, float m43, float m44,
            float m51, float m52, float m53, float m54)
        {
            this.X = new(m11, m12, m13, m14);
            this.Y = new(m21, m22, m23, m24);
            this.Z = new(m31, m32, m33, m34);
            this.W = new(m41, m42, m43, m44);
            this.V = new(m51, m52, m53, m54);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override readonly bool Equals([NotNullWhen(true)] object? obj)
            => (obj is ColorMatrix other) && this.Equals(in other.AsImpl());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool Equals(in Impl other) =>
            this.X.Equals(other.X)
            && this.Y.Equals(other.Y)
            && this.Z.Equals(other.Z)
            && this.W.Equals(other.W)
            && this.V.Equals(other.V);

        bool IEquatable<Impl>.Equals(Impl other) => this.Equals(in other);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override readonly int GetHashCode() => HashCode.Combine(this.X, this.Y, this.Z, this.W, this.V);
    }
}
