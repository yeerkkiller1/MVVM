#define DEBUG_SUBSCRIPTIONS

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Collections.Concurrent;
using System.Threading;
using NUnit.Framework;
using Extensions;
using Interfaces;
using MemoryManagement;

namespace MVVM
{
    public class SubscriptionInfo
    {
        //ATTENTION! Yeah... better not store references to Observables in this...
        //  or else we will cause everything to leak...

        public WeakReference<IObservable> Observ;

        public CallInfo SubscriberLocation;
        //Apparently we can't get this info...
        //public CallInfo ObserverableLocation;

        public int ObservableHash;
        public Type ObservableType;
    }

    public static class GlobalObservableStats
    {
        public static int SubscriptionCount = 0;
        public static Dictionary<int, SubscriptionInfo> ActiveSubscriptions
            = new Dictionary<int, SubscriptionInfo>();

#if DEBUG_SUBSCRIPTIONS
        public static int CurHandlerId = 1;
#endif

        public static string[] SummarizeSubscriptions()
        {
            return ActiveSubscriptions
                .ToList()
                .Sort(x => x.Key)
                .Select(kvp => string.Format("{0} => {1} ({2}) {3}",
                    kvp.Value.ObservableHash, 
                    kvp.Key, 
                    kvp.Value.ObservableType, 
                    kvp.Value.SubscriberLocation))
                .ToArray();
        }
    }

    public interface IObservable
    {
        void ShouldHaveNoHandlers();
    }

    //We don't support nested calls (changing our value in
    //  onChange handlers)
    //NOT thread safe!
    public class Observable<T> : DisposeWatchable, IObservable
    {
#if !DEBUG_SUBSCRIPTIONS
        private int curHandlerId = 1;
#else
#endif

        class Resource : IComparable<Resource>
        {
            public int id;
            public Observable<T> observ;
            public int CompareTo(Resource other)
            {
                return id.CompareTo(other.id);
            }
        }
        private static ThreadResourceTracking.Resources<Resource> handlersOnThread
            = new ThreadResourceTracking.Resources<Resource>(observableFree);

        private static void observableFree(Resource resource)
        {
            resource.observ.Thread_UntrackResource(resource.id);
        }

        private T value;
        private bool hasBeenSet = false;
        private bool isSetting = false;

        private Dictionary<int, Action<T, int>> handlers
            = new Dictionary<int, Action<T, int>>();

        public bool HasBeenSet { get { return isSetting; } }

        public Observable() 
        {
        }

        public Observable(T value) : this()
        {
            this.value = value;
            hasBeenSet = true;
        }

        public T Get()
        {
            return value;
        }
        public void Set(T newValue)
        {
            hasBeenSet = true;

            if (EqualityComparer<T>.Default.Equals(value, newValue)) return;

            if (isSetting) throw new Exception("You cannot set the observable in an onChange handler.");

            try
            {
                isSetting = true;

                value = newValue;

                //Clone handlers in case they are modified when we iterate
                var handlers = this.handlers.ToList();
                foreach(var handler in handlers)
                {
                    handler.Value(value, handler.Key);
                }
            }
            finally
            {
                isSetting = false;
            }
        }

        public int Subscribe(
            Action<T> handler
            , bool callIfSet = true
#if DEBUG_SUBSCRIPTIONS
            , [CallerMemberName] string memberName = ""
            , [CallerFilePath] string sourceFilePath = ""
            , [CallerLineNumber] int sourceLineNumber = 0
#endif
)
        {
            Action<T, int> handlerInner = (T x, int y) => handler(x);
            return Subscribe(handlerInner, callIfSet 
#if DEBUG_SUBSCRIPTIONS
, memberName, sourceFilePath, sourceLineNumber
#endif
                );
        }

        public int Subscribe(
            Action<T, int> handler
            , bool callIfSet = true
#if DEBUG_SUBSCRIPTIONS
            , [CallerMemberName] string memberName = ""
            , [CallerFilePath] string sourceFilePath = ""
            , [CallerLineNumber] int sourceLineNumber = 0
#endif
            )
        {
            //Should be light enough to always leave on
            GlobalObservableStats.SubscriptionCount++;

#if DEBUG_SUBSCRIPTIONS
            int ourHandlerId = GlobalObservableStats.CurHandlerId++;
            GlobalObservableStats.ActiveSubscriptions.Add(ourHandlerId, new SubscriptionInfo
            {
                Observ = new WeakReference<IObservable>(this),
                SubscriberLocation = new CallInfo(memberName, sourceFilePath, sourceLineNumber),
                ObservableHash = this.GetHashCode(),
                ObservableType = typeof(T)
            });
#else
            int ourHandlerId = curHandlerId++;
#endif

            handlers.Add(ourHandlerId, handler);

            handlersOnThread.TrackResource(new Resource { id = ourHandlerId, observ = this });

            //Call handlers on subscribe (as we are expecting bindings, not events)
            if (callIfSet && hasBeenSet) handler(value, ourHandlerId);

            return ourHandlerId;
        }

        public void Unsubscribe(int id)
        {
            if (!handlers.ContainsKey(id)) throw new KeyNotFoundException();
            handlersOnThread.UntrackResource(new Resource { id = id });

            Thread_UntrackResource(id);
        }

        private void Thread_UntrackResource(int id)
        {
            //This means we were disposed BEFORE our Thread was disposed
            //  and so the Thread_UntrackResource called in dispose didn't
            //  hit the right thread... so we got called again when
            //  the thread disposed. Probably fine... it is unusually
            //  to explicitly dispose an Observable anyway.
            if (!handlers.ContainsKey(id)) return;

            GlobalObservableStats.SubscriptionCount--;
            GlobalObservableStats.ActiveSubscriptions.Remove(id);

            handlers.Remove(id);
        }

        public override void Dispose()
        {
            if (IsDisposed()) return;

            handlers
                .Select(x => x.Key)
                .ToList()
                .ForEach(Thread_UntrackResource);

            base.Dispose();
        }

        public void ShouldHaveNoHandlers()
        {
            if(handlers.Count > 0)
            {
                throw new Exception(string.Format("Nope, we have {0} handlers", handlers.Count));
            }
        }
    }
}
