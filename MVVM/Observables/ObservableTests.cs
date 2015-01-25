using Extensions;
using MemoryManagement;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MVVM
{
    public class ObservableTests
    {
        public static void TransformLeakTest_internal(Observable<double> test)
        {
            var testInt = test.Transform(x => (int)x);
        }
        [Test]
        public static void TransformLeakTest()
        {
            Observable<double> test;
            using (var guard = new ThreadGuard())
            {
                test = new Observable<double>(0.53);
                TransformLeakTest_internal(test);
                GCExts.ForceFullGC();
                test.Set(1);
            }
            test.ShouldHaveNoHandlers();
        }

        public static int SubscribeAndDie(Observable<int> observ)
        {
            var basicClass = new WeakActionTests.BasicClass();
            observ.Subscribe(basicClass.setValue);
            return basicClass.ourID;
        }

        [Test]
        public static void TestSubscribeLeak()
        {
            Observable<int> test;
            using (var guard = new ThreadGuard())
            {
                test = new Observable<int>(0);

                int id = SubscribeAndDie(test);
            }

            test.ShouldHaveNoHandlers();
        }

        public static void TestSubscribeStaysAlive_internal(Observable<int> observ, WeakActionTests.BasicClass basicClass)
        {
            //observ.Subscribe<WeakActionTests.BasicClass>(basicClass.setValue);
            observ.Subscribe(x => basicClass.setValue(x));
        }
        [Test]
        public static void TestSubscribeStaysAlive()
        {
            using (var guard = new ThreadGuard())
            {
                Observable<int> observ = new Observable<int>(-1);
                var basicClass = new WeakActionTests.BasicClass();
                TestSubscribeStaysAlive_internal(observ, basicClass);
                GCExts.ForceFullGC();

                observ.Set(5);
                Assert.AreEqual(5, basicClass.value);
            }
        }

        public static object[] ctorObserv<T>(T val, T val2)
        {
            return new object[]{new Observable<T>(val), val2};
        }

        public static IEnumerable<object> NumberObservableMix =
            Enumerable.Empty<object>()
            .Concat(Seed.IntMix.Zip(Seed.IntMix.Rotate(Seed.IntMix.Count() / 2), ctorObserv))
            .Concat(Seed.DoubleMix.Zip(Seed.DoubleMix.Rotate(Seed.DoubleMix.Count() / 2), ctorObserv));

        [Test, TestCaseSource("NumberObservableMix")]
        public static void SetsOnSubscribe<T>(Observable<T> observ, T val)
        {
            using (var guard = new ThreadGuard())
            {
                T watchingValue = default(T);
                observ.Subscribe(x => watchingValue = x);

                Assert.AreEqual(observ.Get(), watchingValue);
            }
        }

        [SetUp]
        public static void SetupForTests()
        {
            ThreadResourceTracking.SetupForTests();
        }

        [Test, TestCaseSource("NumberObservableMix")]
        public static void SubscriberIsCalled<T>(Observable<T> observ, T val)
        {
            using (var guard = new ThreadGuard())
            {
                T watchingValue = default(T);
                observ.Subscribe(x => watchingValue = x);

                observ.Set(val);

                Assert.AreEqual(watchingValue, val);
            }
        }

        [Test, TestCaseSource("NumberObservableMix")]
        public static void EquivalentValuesAreScreened<T>(Observable<T> observ, T val)
        {
            using (var guard = new ThreadGuard())
            {
                bool noMoreSetting = false;
                observ.Subscribe(x =>
                {
                    Assert.IsFalse(noMoreSetting);
                });

                observ.Set(val);
                noMoreSetting = true;
                observ.Set(val);
            }
        }

        public static IEnumerable<object> LocalTypeMix = Seed.TypeMix;
        [Test, TestCaseSource("LocalTypeMix")]
        public static void DefaultIsNotSignalled<T>(T typeValue)
        {
            using (var guard = new ThreadGuard())
            {
                Observable<T> observ = new Observable<T>();
                observ.Subscribe(x =>
                {
                    Assert.Fail();
                });

                bool isHit = false;
                observ.Set(default(T));
                observ.Subscribe(x =>
                {
                    isHit = true;
                });
                Assert.IsTrue(isHit);
            }
        }

        [Test, TestCaseSource("LocalTypeMix")]
        public static void TestUnsubscribe<T>(T type)
        {
            using (var guard = new ThreadGuard())
            {
                Observable<T> observ = new Observable<T>();
                var key = observ.Subscribe(x => { });
                Assert.Throws(Is.InstanceOf<Exception>(), observ.ShouldHaveNoHandlers);
                observ.Unsubscribe(key);
                Assert.DoesNotThrow(observ.ShouldHaveNoHandlers);
            }
        }

        [Test, TestCaseSource("LocalTypeMix")]
        public static void TestDispose<T>(T type)
        {
            using (var guard = new ThreadGuard())
            {
                Observable<T> observ = new Observable<T>();
                var key = observ.Subscribe(x => { });
                Assert.Throws(Is.InstanceOf<Exception>(), observ.ShouldHaveNoHandlers);
                observ.Dispose();
                Assert.DoesNotThrow(observ.ShouldHaveNoHandlers);
            }
        }
    }
}
