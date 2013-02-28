

namespace ImageProcessor.Web.Helpers
{
    using System;
    using System.Linq.Expressions;
    using ImageProcessor.Processors;

    public static class ProcessorFactory
    {
        public static T New<T>() where T:IGraphicsProcessor
        {
            Type t = typeof(T);
            Func<T> method = Expression.Lambda<Func<T>>(Expression.Block(t, new Expression[] { Expression.New(t) })).Compile();

            return method();
        }
    }

}
