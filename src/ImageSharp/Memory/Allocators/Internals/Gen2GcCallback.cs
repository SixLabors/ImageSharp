// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

// Port of BCL internal utility:
// https://github.com/dotnet/runtime/blob/57bfe474518ab5b7cfe6bf7424a79ce3af9d6657/src/libraries/System.Private.CoreLib/src/System/Gen2GcCallback.cs
#if NETCOREAPP3_1_OR_GREATER
using System;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Memory.Internals
{
    /// <summary>
    /// Schedules a callback roughly every gen 2 GC (you may see a Gen 0 an Gen 1 but only once)
    /// (We can fix this by capturing the Gen 2 count at startup and testing, but I mostly don't care)
    /// </summary>
    internal sealed class Gen2GcCallback : CriticalFinalizerObject
    {
        private readonly Func<bool> callback0;
        private readonly Func<object, bool> callback1;
        private GCHandle weakTargetObj;

        private Gen2GcCallback(Func<bool> callback)
        {
            this.callback0 = callback;
        }

        private Gen2GcCallback(Func<object, bool> callback, object targetObj)
        {
            this.callback1 = callback;
            this.weakTargetObj = GCHandle.Alloc(targetObj, GCHandleType.Weak);
        }

        ~Gen2GcCallback()
        {
            if (this.weakTargetObj.IsAllocated)
            {
                // Check to see if the target object is still alive.
                object targetObj = this.weakTargetObj.Target;
                if (targetObj == null)
                {
                    // The target object is dead, so this callback object is no longer needed.
                    this.weakTargetObj.Free();
                    return;
                }

                // Execute the callback method.
                try
                {
                    if (!this.callback1(targetObj))
                    {
                        // If the callback returns false, this callback object is no longer needed.
                        this.weakTargetObj.Free();
                        return;
                    }
                }
                catch
                {
                    // Ensure that we still get a chance to resurrect this object, even if the callback throws an exception.
#if DEBUG
                    // Except in DEBUG, as we really shouldn't be hitting any exceptions here.
                    throw;
#endif
                }
            }
            else
            {
                // Execute the callback method.
                try
                {
                    if (!this.callback0())
                    {
                        // If the callback returns false, this callback object is no longer needed.
                        return;
                    }
                }
                catch
                {
                    // Ensure that we still get a chance to resurrect this object, even if the callback throws an exception.
#if DEBUG
                    // Except in DEBUG, as we really shouldn't be hitting any exceptions here.
                    throw;
#endif
                }
            }

            // Resurrect ourselves by re-registering for finalization.
            GC.ReRegisterForFinalize(this);
        }

        /// <summary>
        /// Schedule 'callback' to be called in the next GC.  If the callback returns true it is
        /// rescheduled for the next Gen 2 GC.  Otherwise the callbacks stop.
        /// </summary>
        public static void Register(Func<bool> callback)
        {
            // Create a unreachable object that remembers the callback function and target object.
            _ = new Gen2GcCallback(callback);
        }

        /// <summary>
        /// Schedule 'callback' to be called in the next GC.  If the callback returns true it is
        /// rescheduled for the next Gen 2 GC.  Otherwise the callbacks stop.
        ///
        /// NOTE: This callback will be kept alive until either the callback function returns false,
        /// or the target object dies.
        /// </summary>
        public static void Register(Func<object, bool> callback, object targetObj)
        {
            // Create a unreachable object that remembers the callback function and target object.
            _ = new Gen2GcCallback(callback, targetObj);
        }
    }
}
#endif
