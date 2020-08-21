// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Implements basic math operations using tolerant comparison
    /// whenever an equality check is needed.
    /// </summary>
    internal readonly struct TolerantMath
    {
        private readonly double epsilon;

        private readonly double negEpsilon;

        /// <summary>
        /// A read-only default instance for <see cref="TolerantMath"/> using 1e-8 as epsilon.
        /// It is a field so it can be passed as an 'in' parameter.
        /// Does not necessarily fit all use cases!
        /// </summary>
        public static readonly TolerantMath Default = new TolerantMath(1e-8);

        public TolerantMath(double epsilon)
        {
            DebugGuard.MustBeGreaterThan(epsilon, 0, nameof(epsilon));

            this.epsilon = epsilon;
            this.negEpsilon = -epsilon;
        }

        /// <summary>
        /// <paramref name="a"/> == 0
        /// </summary>
        [MethodImpl(InliningOptions.ShortMethod)]
        public bool IsZero(double a) => a > this.negEpsilon && a < this.epsilon;

        /// <summary>
        /// <paramref name="a"/> &gt; 0
        /// </summary>
        [MethodImpl(InliningOptions.ShortMethod)]
        public bool IsPositive(double a) => a > this.epsilon;

        /// <summary>
        /// <paramref name="a"/> &lt; 0
        /// </summary>
        [MethodImpl(InliningOptions.ShortMethod)]
        public bool IsNegative(double a) => a < this.negEpsilon;

        /// <summary>
        /// <paramref name="a"/> == <paramref name="b"/>
        /// </summary>
        [MethodImpl(InliningOptions.ShortMethod)]
        public bool AreEqual(double a, double b) => this.IsZero(a - b);

        /// <summary>
        /// <paramref name="a"/> &gt; <paramref name="b"/>
        /// </summary>
        [MethodImpl(InliningOptions.ShortMethod)]
        public bool IsGreater(double a, double b) => a > b + this.epsilon;

        /// <summary>
        /// <paramref name="a"/> &lt; <paramref name="b"/>
        /// </summary>
        [MethodImpl(InliningOptions.ShortMethod)]
        public bool IsLess(double a, double b) => a < b - this.epsilon;

        /// <summary>
        /// <paramref name="a"/> &gt;= <paramref name="b"/>
        /// </summary>
        [MethodImpl(InliningOptions.ShortMethod)]
        public bool IsGreaterOrEqual(double a, double b) => a >= b - this.epsilon;

        /// <summary>
        /// <paramref name="a"/> &lt;= <paramref name="b"/>
        /// </summary>
        [MethodImpl(InliningOptions.ShortMethod)]
        public bool IsLessOrEqual(double a, double b) => b >= a - this.epsilon;

        [MethodImpl(InliningOptions.ShortMethod)]
        public double Ceiling(double a)
        {
            double rem = Math.IEEERemainder(a, 1);
            if (this.IsZero(rem))
            {
                return Math.Round(a);
            }

            return Math.Ceiling(a);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public double Floor(double a)
        {
            double rem = Math.IEEERemainder(a, 1);
            if (this.IsZero(rem))
            {
                return Math.Round(a);
            }

            return Math.Floor(a);
        }
    }
}
