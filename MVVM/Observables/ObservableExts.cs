using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MVVM
{
    public static class ObservableExts
    {
        public static void SubscribeOnNewThread<T, ActionOwner>(
            this Observable<T> observ,
            Action<T> action)
        {
            //TODO: To make this work we need a ThreadResourceTracking clone
            //  thingy, so we can track our resources in a way that is attached
            //  to the old thread, and so we terminate our thread when the old
            //  thread terminates...
            throw new NotImplementedException();

            BlockingCollection<T> newValues = new BlockingCollection<T>();

            //The new thread that runs the action
            Task.Factory.StartNew(() =>
            {
                foreach (T newValue in newValues.GetConsumingEnumerable())
                {
                    action(newValue);
                }
                Debugger.Break();
            });
            //Send the new values to the new thread
            observ.Subscribe(x =>
            {
                newValues.Add(x);
            });
        }

        public static Observable<NewType> Transform<T, NewType>(
            this Observable<T> observ,
            Func<T, NewType> transform
        )
        {
            Observable<NewType> newObserv = new Observable<NewType>(transform(observ.Get()));

            observ.Subscribe(newValue =>
            {
                newObserv.Set(transform(newValue));
            });

            return newObserv;
        }

        public static void When<T>(this Observable<T> observ, Action callback)
        {
            When(observ, x => true, callback);
        }

        public static void When<T>(this Observable<T> observ, Func<T, bool> predicate, Action callback)
        {
            observ.Subscribe((newValue, id) =>
            {
                if (predicate(newValue))
                {
                    observ.Unsubscribe(id);
                    callback();
                }
            });
        }
    }
}
