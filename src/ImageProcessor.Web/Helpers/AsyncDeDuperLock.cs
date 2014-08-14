// --------------------------------------------------------------------------------------------------------------------
// <copyright file="AsyncDeDuperLock.cs" company="James South">
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
    public sealed class AsyncDeDuperLock
    {
        /// <summary>
        /// The semaphore slims.
        /// </summary>
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> SemaphoreSlims
                                = new ConcurrentDictionary<string, SemaphoreSlim>();

        /// <summary>
        /// The lock.
        /// </summary>
        /// <param name="key">
        /// The hash.
        /// </param>
        /// <returns>
        /// The <see cref="IDisposable"/>.
        /// </returns>
        public IDisposable Lock(string key)
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
        /// The lock async.
        /// </summary>
        /// <param name="key">
        /// The key.
        /// </param>
        /// <returns>
        /// The <see cref="Task"/>.
        /// </returns>
        public Task<IDisposable> LockAsync(string key)
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
            private readonly string key;

            /// <summary>
            /// The close scope action.
            /// </summary>
            private readonly Action<string> closeScopeAction;

            /// <summary>
            /// Initializes a new instance of the <see cref="DisposableScope"/> class.
            /// </summary>
            /// <param name="key">
            /// The key.
            /// </param>
            /// <param name="closeScopeAction">
            /// The close scope action.
            /// </param>
            public DisposableScope(string key, Action<string> closeScopeAction)
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