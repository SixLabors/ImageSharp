namespace ImageProcessorCore.Helpers
{
    using System;
    using System.Linq.Expressions;

    /// <summary>
    /// General purpose Expression utilities
    /// </summary>
    public static class ExpressionUtil
    {
        /// <summary>
        /// Create a function delegate representing a unary operation
        /// </summary>
        /// <typeparam name="TArg1">The parameter type</typeparam>
        /// <typeparam name="TResult">The return type</typeparam>
        /// <param name="body">Body factory</param>
        /// <returns>Compiled function delegate</returns>
        public static Func<TArg1, TResult> CreateExpression<TArg1, TResult>(
            Func<Expression, UnaryExpression> body)
        {
            ParameterExpression inp = Expression.Parameter(typeof(TArg1), "inp");
            try
            {
                return Expression.Lambda<Func<TArg1, TResult>>(body(inp), inp).Compile();
            }
            catch (Exception ex)
            {
                string msg = ex.Message; // avoid capture of ex itself
                return delegate { throw new InvalidOperationException(msg); };
            }
        }

        /// <summary>
        /// Create a function delegate representing a binary operation
        /// </summary>
        /// <typeparam name="TArg1">The first parameter type</typeparam>
        /// <typeparam name="TArg2">The second parameter type</typeparam>
        /// <typeparam name="TResult">The return type</typeparam>
        /// <param name="body">Body factory</param>
        /// <returns>Compiled function delegate</returns>
        public static Func<TArg1, TArg2, TResult> CreateExpression<TArg1, TArg2, TResult>(
            Func<Expression, Expression, BinaryExpression> body)
        {
            return CreateExpression<TArg1, TArg2, TResult>(body, false);
        }

        /// <summary>
        /// Create a function delegate representing a binary operation
        /// </summary>
        /// <param name="castArgsToResultOnFailure">
        /// If no matching operation is possible, attempt to convert
        /// TArg1 and TArg2 to TResult for a match? For example, there is no
        /// "decimal operator /(decimal, int)", but by converting TArg2 (int) to
        /// TResult (decimal) a match is found.
        /// </param>
        /// <typeparam name="TArg1">The first parameter type</typeparam>
        /// <typeparam name="TArg2">The second parameter type</typeparam>
        /// <typeparam name="TResult">The return type</typeparam>
        /// <param name="body">Body factory</param>
        /// <returns>Compiled function delegate</returns>
        public static Func<TArg1, TArg2, TResult> CreateExpression<TArg1, TArg2, TResult>(
            Func<Expression, Expression, BinaryExpression> body, bool castArgsToResultOnFailure)
        {
            ParameterExpression lhs = Expression.Parameter(typeof(TArg1), "lhs");
            ParameterExpression rhs = Expression.Parameter(typeof(TArg2), "rhs");
            try
            {
                try
                {
                    return Expression.Lambda<Func<TArg1, TArg2, TResult>>(body(lhs, rhs), lhs, rhs).Compile();
                }
                catch (InvalidOperationException)
                {
                    // If we show retry and the args aren't already "TValue, TValue, TValue"...
                    // convert both lhs and rhs to TResult (as appropriate)
                    if (castArgsToResultOnFailure && !(typeof(TArg1) == typeof(TResult) && typeof(TArg2) == typeof(TResult)))
                    {
                        Expression castLhs = typeof(TArg1) == typeof(TResult)
                            ? lhs
                            : (Expression)Expression.Convert(lhs, typeof(TResult));

                        Expression castRhs = typeof(TArg2) == typeof(TResult)
                            ? rhs
                            : (Expression)Expression.Convert(rhs, typeof(TResult));

                        return Expression.Lambda<Func<TArg1, TArg2, TResult>>(
                            body(castLhs, castRhs), lhs, rhs).Compile();
                    }

                    throw;
                }
            }
            catch (Exception ex)
            {
                string msg = ex.Message; // avoid capture of ex itself
                return delegate { throw new InvalidOperationException(msg); };
            }
        }
    }
}
