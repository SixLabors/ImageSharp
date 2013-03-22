// License: CPOL at http://www.codeproject.com/info/cpol10.aspx


namespace System.Threading.Tasks
{
    using System.Collections.Generic;

    /// <summary>
    /// Extensions related to the <see cref="Task"/> classes. 
    /// Supports implementing "async"-style methods in C#4 using iterators.
    /// </summary>
    /// <remarks>
    /// I would call this TaskExtensions, except that clients must name the class to use methods like <see cref="FromResult{T}(T)"/>.
    /// Based on work from Await Tasks in C#4 using Iterators by Keith L Robertson.
    /// <see cref="http://www.codeproject.com/Articles/504197/Await-Tasks-in-Csharp4-using-Iterators"/>
    /// </remarks>
    public static class TaskEx
    {
        /// <summary>
        /// Return a Completed <see cref="Task{TResult}"/> with a specific <see cref="Task{TResult}.Result"/> value.
        /// </summary>
        /// <typeparam name="TResult">
        /// The result
        /// </typeparam>
        /// <param name="resultValue">
        /// The result Value.
        /// </param>
        /// <returns>
        /// The <see cref="Task"/>.
        /// </returns>
        public static Task<TResult> FromResult<TResult>(TResult resultValue)
        {
            var completionSource = new TaskCompletionSource<TResult>();
            completionSource.SetResult(resultValue);
            return completionSource.Task;
        }

        /// <summary>
        /// Transform an enumeration of <see cref="Task"/> into a single non-Result <see cref="Task"/>.
        /// </summary>
        /// <param name="tasks">
        /// The tasks.
        /// </param>
        /// <returns>
        /// The <see cref="Task"/>.
        /// </returns>
        public static Task ToTask(this IEnumerable<Task> tasks)
        {
            return ToTask<VoidResult>(tasks);
        }

        /// <summary>
        /// Transform an enumeration of <see cref="Task"/> into a single <see cref="Task{TResult}"/>.
        /// The final <see cref="Task"/> in <paramref name="tasks"/> must be a <see cref="Task{TResult}"/>.
        /// </summary>
        /// <typeparam name="TResult">
        /// The task results
        /// </typeparam>
        /// <param name="tasks">
        /// The tasks.
        /// </param>
        /// <returns>
        /// The <see cref="Task"/>.
        /// </returns>
        public static Task<TResult> ToTask<TResult>(this IEnumerable<Task> tasks)
        {
            var taskScheduler =
                SynchronizationContext.Current == null
                    ? TaskScheduler.Default : TaskScheduler.FromCurrentSynchronizationContext();
            var taskEnumerator = tasks.GetEnumerator();
            var completionSource = new TaskCompletionSource<TResult>();

            // Clean up the enumerator when the task completes.
            completionSource.Task.ContinueWith(t => taskEnumerator.Dispose(), taskScheduler);

            ToTaskDoOneStep(taskEnumerator, taskScheduler, completionSource, null);
            return completionSource.Task;
        }

        /// <summary>
        /// If the previous task Canceled or Faulted, complete the master task with the same <see cref="Task.Status"/>.
        /// Obtain the next <see cref="Task"/> from the <paramref name="taskEnumerator"/>.
        /// If none, complete the master task, possibly with the <see cref="Task{T}.Result"/> of the last task.
        /// Otherwise, set up the task with a continuation to come do this again when it completes.
        /// </summary>
        private static void ToTaskDoOneStep<TResult>(
            IEnumerator<Task> taskEnumerator, TaskScheduler taskScheduler,
            TaskCompletionSource<TResult> completionSource, Task completedTask)
        {
            // Check status of previous nested task (if any), and stop if Canceled or Faulted.
            TaskStatus status;
            if (completedTask == null)
            {
                // This is the first task from the iterator; skip status check.
            }
            else if ((status = completedTask.Status) == TaskStatus.Canceled)
            {
                completionSource.SetCanceled();
                return;
            }
            else if (status == TaskStatus.Faulted)
            {
                completionSource.SetException(completedTask.Exception);
                return;
            }

            // Check for cancellation before looking for the next task.
            // This causes a problem where the ultimate Task does not complete and fire any continuations; I don't know why.
            // So cancellation from the Token must be handled within the iterator itself.
            //if (cancellationToken.IsCancellationRequested) {
            //    completionSource.SetCanceled();
            //    return;
            //}

            // Find the next Task in the iterator; handle cancellation and other exceptions.
            Boolean haveMore;
            try
            {
                haveMore = taskEnumerator.MoveNext();

            }
            catch (OperationCanceledException cancExc)
            {
                //if (cancExc.CancellationToken == cancellationToken) completionSource.SetCanceled();
                //else completionSource.SetException(cancExc);
                completionSource.SetCanceled();
                return;
            }
            catch (Exception exc)
            {
                completionSource.SetException(exc);
                return;
            }

            if (!haveMore)
            {
                // No more tasks; set the result from the last completed task (if any, unless no result is requested).
                // We know it's not Canceled or Faulted because we checked at the start of this method.
                if (typeof(TResult) == typeof(VoidResult))
                {        // No result
                    completionSource.SetResult(default(TResult));
                }
                else if (!(completedTask is Task<TResult>))
                {     // Wrong result
                    completionSource.SetException(new InvalidOperationException(
                        "Asynchronous iterator " + taskEnumerator +
                            " requires a final result task of type " + typeof(Task<TResult>).FullName +
                            (completedTask == null ? ", but none was provided." :
                                "; the actual task type was " + completedTask.GetType().FullName)));
                }
                else
                {
                    completionSource.SetResult(((Task<TResult>)completedTask).Result);
                }
            }
            else
            {
                // When the nested task completes, continue by performing this function again.
                // Note: This is NOT a recursive call; the current method activation will complete
                // almost immediately and independently of the lambda continuation.
                taskEnumerator.Current.ContinueWith(
                    nextTask => ToTaskDoOneStep(taskEnumerator, taskScheduler, completionSource, nextTask),
                    taskScheduler);
            }
        }

        /// <summary>
        /// Internal marker type for using <see cref="ToTask{T}"/> to implement <see cref="ToTask"/>.
        /// </summary>
        private abstract class VoidResult
        {
        }
    }
}
