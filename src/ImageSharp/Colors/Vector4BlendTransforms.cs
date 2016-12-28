// <copyright file="Vector4BlendTransforms.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Numerics;

    /// <summary>
    /// Transform algorithms that match the equations defined in the W3C Compositing and Blending Level 1 specification.
    /// <see href="https://www.w3.org/TR/compositing-1/"/>
    /// </summary>
    public class Vector4BlendTransforms
    {
        /// <summary>
        /// The blending formula simply selects the source vector.
        /// </summary>
        /// <param name="backdrop">The backdrop vector.</param>
        /// <param name="source">The source vector.</param>
        /// <returns>
        /// The <see cref="Vector4"/>.
        /// </returns>
        public static Vector4 Normal(Vector4 backdrop, Vector4 source)
        {
            return new Vector4(source.X, source.Y, source.Z, source.W);
        }

        /// <summary>
        /// Blends two vectors by multiplication.
        /// </summary>
        /// <param name="backdrop">The backdrop vector.</param>
        /// <param name="source">The source vector.</param>
        /// <returns>
        /// The <see cref="Vector4"/>.
        /// </returns>
        public static Vector4 Multiply(Vector4 backdrop, Vector4 source)
        {
            Vector4 multiply = backdrop * source;
            multiply.W = backdrop.W;
            return multiply;
        }

        /// <summary>
        /// Multiplies the complements of the backdrop and source vector values, then complements the result.
        /// </summary>
        /// <param name="backdrop">The backdrop vector.</param>
        /// <param name="source">The source vector.</param>
        /// <returns>
        /// The <see cref="Vector4"/>.
        /// </returns>
        public static Vector4 Screen(Vector4 backdrop, Vector4 source)
        {
            Vector4 subtract = backdrop + source - (backdrop * source);
            subtract.W = backdrop.W;
            return subtract;
        }

        /// <summary>
        /// Multiplies or screens the colors, depending on the source vector value.
        /// </summary>
        /// <param name="backdrop">The backdrop vector.</param>
        /// <param name="source">The source vector.</param>
        /// <returns>
        /// The <see cref="Vector4"/>.
        /// </returns>
        public static Vector4 HardLight(Vector4 backdrop, Vector4 source)
        {
            return new Vector4(BlendOverlay(source.X, backdrop.X), BlendOverlay(source.Y, backdrop.Y), BlendOverlay(source.Z, backdrop.Z), backdrop.W);
        }

        /// <summary>
        /// Multiplies or screens the vectors, depending on the backdrop vector value.
        /// </summary>
        /// <param name="backdrop">The backdrop vector.</param>
        /// <param name="source">The source vector.</param>
        /// <returns>
        /// The <see cref="Vector4"/>.
        /// </returns>
        public static Vector4 Overlay(Vector4 backdrop, Vector4 source)
        {
            return new Vector4(BlendOverlay(backdrop.X, source.X), BlendOverlay(backdrop.Y, source.Y), BlendOverlay(backdrop.Z, source.Z), backdrop.W);
        }

        /// <summary>
        /// Selects the minimum of the backdrop and source vectors.
        /// </summary>
        /// <param name="backdrop">The backdrop vector.</param>
        /// <param name="source">The source vector.</param>
        /// <returns>
        /// The <see cref="Vector4"/>.
        /// </returns>
        public static Vector4 Darken(Vector4 backdrop, Vector4 source)
        {
            Vector4 result = Vector4.Min(backdrop, source);
            result.W = backdrop.W;
            return result;
        }

        /// <summary>
        /// Selects the max of the backdrop and source vector.
        /// </summary>
        /// <param name="backdrop">The backdrop vector.</param>
        /// <param name="source">The source vector.</param>
        /// <returns>
        /// The <see cref="Vector4"/>.
        /// </returns>
        public static Vector4 Lighten(Vector4 backdrop, Vector4 source)
        {
            Vector4 result = Vector4.Max(backdrop, source);
            result.W = backdrop.W;
            return result;
        }

        /// <summary>
        /// Selects the maximum or minimum of the vectors, depending on the source vector value.
        /// </summary>
        /// <param name="backdrop">The backdrop vector.</param>
        /// <param name="source">The source vector.</param>
        /// <returns>
        /// The <see cref="Vector4"/>.
        /// </returns>
        public static Vector4 SoftLight(Vector4 backdrop, Vector4 source)
        {
            return new Vector4(BlendSoftLight(backdrop.X, source.X), BlendSoftLight(backdrop.Y, source.Y), BlendSoftLight(backdrop.Z, source.Z), backdrop.W);
        }

        /// <summary>
        /// Increases the backdrop vector to reflect the source vector.
        /// </summary>
        /// <param name="backdrop">The backdrop vector.</param>
        /// <param name="source">The source vector.</param>
        /// <returns>
        /// The <see cref="Vector4"/>.
        /// </returns>
        public static Vector4 Dodge(Vector4 backdrop, Vector4 source)
        {
            return new Vector4(BlendDodge(backdrop.X, source.X), BlendDodge(backdrop.Y, source.Y), BlendDodge(backdrop.Z, source.Z), backdrop.W);
        }

        /// <summary>
        /// Decreases the backdrop vector to reflect the source vector.
        /// </summary>
        /// <param name="backdrop">The backdrop vector.</param>
        /// <param name="source">The source vector.</param>
        /// <returns>
        /// The <see cref="Vector4"/>.
        /// </returns>
        public static Vector4 Burn(Vector4 backdrop, Vector4 source)
        {
            return new Vector4(BlendBurn(backdrop.X, source.X), BlendBurn(backdrop.Y, source.Y), BlendBurn(backdrop.Z, source.Z), backdrop.W);
        }

        /// <summary>
        /// Subtracts the minimum of the two constituent vectors from the maximum vector.
        /// </summary>
        /// <param name="backdrop">The backdrop vector.</param>
        /// <param name="source">The source vector.</param>
        /// <returns>
        /// The <see cref="Vector4"/>.
        /// </returns>
        public static Vector4 Difference(Vector4 backdrop, Vector4 source)
        {
            Vector4 result = Vector4.Abs(backdrop - source);
            result.W = backdrop.W;
            return result;
        }

        /// <summary>
        /// Produces an effect similar to that of the <see cref="Difference"/> mode but lower in magnitude.
        /// </summary>
        /// <param name="backdrop">The backdrop vector.</param>
        /// <param name="source">The source vector.</param>
        /// <returns>
        /// The <see cref="Vector4"/>.
        /// </returns>
        public static Vector4 Exclusion(Vector4 backdrop, Vector4 source)
        {
            return new Vector4(BlendExclusion(backdrop.X, source.X), BlendExclusion(backdrop.Y, source.Y), BlendExclusion(backdrop.Z, source.Z), backdrop.W);
        }

        /// <summary>
        /// Linearly interpolates from one vector to another based on the given weighting.
        /// The two vectors are premultiplied before operating.
        /// </summary>
        /// <param name="backdrop">The backdrop vector.</param>
        /// <param name="source">The source vector.</param>
        /// <param name="amount">
        /// A value between 0 and 1 indicating the weight of the second source vector.
        /// At amount = 0, "from" is returned, at amount = 1, "to" is returned.
        /// </param>
        /// <returns>
        /// The <see cref="Vector4"/>
        /// </returns>
        public static Vector4 PremultipliedLerp(Vector4 backdrop, Vector4 source, float amount)
        {
            amount = amount.Clamp(0, 1);

            // Santize on zero alpha
            if (Math.Abs(backdrop.W) < Constants.Epsilon)
            {
                source.W *= amount;
                return source;
            }

            if (Math.Abs(source.W) < Constants.Epsilon)
            {
                return backdrop;
            }

            // Premultiply the source vector.
            // Oddly premultiplying the background vector creates dark outlines when pixels
            // Have low alpha values.
            source = new Vector4(source.X, source.Y, source.Z, 1) * (source.W * amount);

            // This should be implementing the following formula
            // https://en.wikipedia.org/wiki/Alpha_compositing
            // Vout =  Vs + Vb (1 - Vsa)
            // Aout = Vsa + Vsb (1 - Vsa)
            Vector3 inverseW = new Vector3(1 - source.W);
            Vector3 xyzB = new Vector3(backdrop.X, backdrop.Y, backdrop.Z);
            Vector3 xyzS = new Vector3(source.X, source.Y, source.Z);

            return new Vector4(xyzS + (xyzB * inverseW), source.W + (backdrop.W * (1 - source.W)));
        }

        /// <summary>
        /// Multiplies or screens the backdrop component, depending on the component value.
        /// </summary>
        /// <param name="b">The backdrop component.</param>
        /// <param name="s">The source component.</param>
        /// <returns>
        /// The <see cref="float"/>.
        /// </returns>
        private static float BlendOverlay(float b, float s)
        {
            return b <= .5F ? (2F * b * s) : (1F - (2F * (1F - b) * (1F - s)));
        }

        /// <summary>
        /// Darkens or lightens the backdrop component, depending on the source component value.
        /// </summary>
        /// <param name="b">The backdrop component.</param>
        /// <param name="s">The source component.</param>
        /// <returns>
        /// The <see cref="float"/>.
        /// </returns>
        private static float BlendSoftLight(float b, float s)
        {
            return s <= .5F ? ((2F * b * s) + (b * b * (1F - (2F * s)))) : (float)((Math.Sqrt(b) * ((2F * s) - 1F)) + (2F * b * (1F - s)));
        }

        /// <summary>
        /// Brightens the backdrop component to reflect the source component.
        /// </summary>
        /// <param name="b">The backdrop component.</param>
        /// <param name="s">The source component.</param>
        /// <returns>
        /// The <see cref="float"/>.
        /// </returns>
        private static float BlendDodge(float b, float s)
        {
            return Math.Abs(s - 1F) < Constants.Epsilon ? s : Math.Min(b / (1F - s), 1F);
        }

        /// <summary>
        /// Darkens the backdrop component to reflect the source component.
        /// </summary>
        /// <param name="b">The backdrop component.</param>
        /// <param name="s">The source component.</param>
        /// <returns>
        /// The <see cref="float"/>.
        /// </returns>
        private static float BlendBurn(float b, float s)
        {
            return Math.Abs(s) < Constants.Epsilon ? s : Math.Max(1F - ((1F - b) / s), 0F);
        }

        /// <summary>
        /// Darkens the backdrop component to reflect the source component.
        /// </summary>
        /// <param name="b">The backdrop component.</param>
        /// <param name="s">The source component.</param>
        /// <returns>
        /// The <see cref="float"/>.
        /// </returns>
        private static float BlendExclusion(float b, float s)
        {
            return b + s - (2F * b * s);
        }
    }
}
