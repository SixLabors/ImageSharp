// --------------------------------------------------------------------------------------------------------------------
// <copyright file="AsyncDuplicateLock.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Throttles duplicate requests.
// </summary>
// --------------------------------------------------------------------------------------------------------------------
namespace ImageProcessor.Web.Helpers
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Throttles duplicate requests.
    /// Based loosely on <see href="http://stackoverflow.com/a/21011273/427899"/>
    /// </summary>
    public sealed class AsyncDuplicateLock
    {
        /// <summary>
        /// The collection of semaphore slims.
        /// </summary>
        private static readonly ConcurrentDictionary<object, SemaphoreSlim> SemaphoreSlims
                                = new ConcurrentDictionary<object, SemaphoreSlim>();

        /// <summary>
        /// Locks against the given key.
        /// </summary>
        /// <param name="key">
        /// The key that identifies the current object.
        /// </param>
        /// <returns>
        /// The disposable <see cref="Task"/>.
        /// </returns>
        public IDisposable Lock(object key)
        {
            DisposableScope releaser = new DisposableScope(
            key,
            s =>
            {
                SemaphoreSlim locker;
                if (SemaphoreSlims.TryRemove(s, out locker))
                {
                    locker.Release();
                    locker.Dispose();
                }
            });

            SemaphoreSlim semaphore = SemaphoreSlims.GetOrAdd(key, new SemaphoreSlim(1, 1));
            semaphore.Wait();
            return releaser;
        }

        /// <summary>
        /// Asynchronously locks against the given key.
        /// </summary>
        /// <param name="key">
        /// The key that identifies the current object.
        /// </param>
        /// <returns>
        /// The disposable <see cref="Task"/>.
        /// </returns>
        public Task<IDisposable> LockAsync(object key)
        {
            DisposableScope releaser = new DisposableScope(
            key,
            s =>
            {
                SemaphoreSlim locker;
                if (SemaphoreSlims.TryRemove(s, out locker))
                {
                    locker.Release();
                    locker.Dispose();
                }
            });

            Task<IDisposable> releaserTask = Task.FromResult(releaser as IDisposable);
            SemaphoreSlim semaphore = SemaphoreSlims.GetOrAdd(key, new SemaphoreSlim(1, 1));

            Task waitTask = semaphore.WaitAsync();

            return waitTask.IsCompleted
                       ? releaserTask
                       : waitTask.ContinueWith(
                           (_, r) => (IDisposable)r,
                           releaser,
                           CancellationToken.None,
                           TaskContinuationOptions.ExecuteSynchronously,
                           TaskScheduler.Default);
        }

        /// <summary>
        /// The disposable scope.
        /// </summary>
        private sealed class DisposableScope : IDisposable
        {
            /// <summary>
            /// The key
            /// </summary>
            private readonly object key;

            /// <summary>
            /// The close scope action.
            /// </summary>
            private readonly Action<object> closeScopeAction;

            /// <summary>
            /// Initializes a new instance of the <see cref="DisposableScope"/> class.
            /// </summary>
            /// <param name="key">
            /// The key.
            /// </param>
            /// <param name="closeScopeAction">
            /// The close scope action.
            /// </param>
            public DisposableScope(object key, Action<object> closeScopeAction)
            {
                this.key = key;
                this.closeScopeAction = closeScopeAction;
            }

            /// <summary>
            /// The dispose.
            /// </summary>
            public void Dispose()
            {
                this.closeScopeAction(this.key);
            }
        }
    }
}