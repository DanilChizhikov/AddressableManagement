using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DTech.AddressableManagement.Internal.Delivery;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace DTech.AddressableManagement.Tests.Editor
{
    [TestFixture]
    internal sealed class LoadingTests
    {
        private const string HeroGuid = "0f7a1c2b3d4e5f60718293a4b5c6d7e8";

        private readonly List<Object> _created = new List<Object>();

        private FakeAddressablesGateway _gateway;
        private List<ContentFailure> _failures;

        [SetUp]
        public void SetUp()
        {
            _gateway = new FakeAddressablesGateway();
            _failures = new List<ContentFailure>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created)
            {
                Object.DestroyImmediate(created);
            }

            _created.Clear();
        }

        [Test]
        public async Task LoadAsset_KeysOfTheSameAssetShareOneLoad()
        {
            GameObject hero = Prefab("Hero");
            _gateway.Locate = (key, type) => Locations(type, "Assets/Hero.prefab");
            _gateway.LoadResult = _ => hero;
            using (AssetService service = Service())
            {
                IAssetHandler<GameObject> byGuid = service.LoadAsset(new ReferenceKey<GameObject>(HeroGuid));
                IAssetHandler<GameObject> byAddress = service.LoadAsset(new AddressKey<GameObject>("Hero"));

                Assert.That(await byGuid.WaitForCompletionAsync(), Is.SameAs(hero));
                Assert.That(await byAddress.WaitForCompletionAsync(), Is.SameAs(hero));
                Assert.That(_gateway.LoadedLocations, Has.Count.EqualTo(1));

                byGuid.Release();
                Assert.That(_gateway.LoadOperations[0].ReleaseCalls, Is.Zero, "another handler still holds the asset");

                byAddress.Release();
                byAddress.Release();
                Assert.That(_gateway.LoadOperations[0].ReleaseCalls, Is.EqualTo(1));
            }
        }

        [Test]
        public async Task LoadAsset_ReleasingDuringTheLoadReleasesTheOperationOnce()
        {
            _gateway.CompleteLoadsImmediately = false;
            using (AssetService service = Service())
            {
                IAssetHandler<GameObject> handler = service.LoadAsset(new AddressKey<GameObject>("Hero"));
                await WaitUntilAsync(() => _gateway.LoadOperations.Count == 1);

                handler.Release();
                Exception error = await AsyncCatch.ExceptionAsync(() => handler.WaitForCompletionAsync());
                _gateway.LoadOperations[0].Settle(Prefab("Hero"));

                Assert.That(error, Is.InstanceOf<OperationCanceledException>());
                Assert.That(_gateway.LoadOperations[0].ReleaseCalls, Is.EqualTo(1));
                Assert.That(handler.IsReleased, Is.True);
                Assert.That(handler.IsDone, Is.False);
            }
        }

        [Test]
        public async Task LoadAsset_AFailedLoadIsNotReused()
        {
            GameObject hero = Prefab("Hero");
            int loads = 0;
            _gateway.LoadResult = _ => loads++ == 0 ? (object)new InvalidOperationException("broken bundle") : hero;
            LogAssert.Expect(LogType.Error, new Regex(@"Load failed \(Unknown\).*broken bundle"));
            using (AssetService service = Service())
            {
                IAssetHandler<GameObject> failed = service.LoadAsset(new AddressKey<GameObject>("Hero"));
                Assert.That(await failed.WaitForCompletionAsync(), Is.Null);
                Assert.That(failed.IsFailed, Is.True);
                Assert.That(failed.Exception, Is.InstanceOf<InvalidOperationException>());
                Assert.That(_failures, Has.Count.EqualTo(1));
                Assert.That(_failures[0].Stage, Is.EqualTo(ContentStage.Load));
                Assert.That(_gateway.LoadOperations[0].ReleaseCalls, Is.EqualTo(1));

                IAssetHandler<GameObject> retried = service.LoadAsset(new AddressKey<GameObject>("Hero"));

                Assert.That(await retried.WaitForCompletionAsync(), Is.SameAs(hero));
                Assert.That(_gateway.LoadedLocations, Has.Count.EqualTo(2));

                failed.Release();
                retried.Release();
            }
        }

        [Test]
        public async Task LoadAsset_TransientFailuresFollowTheLoadRetryPolicy()
        {
            GameObject hero = Prefab("Hero");
            int loads = 0;
            _gateway.LoadResult = _ => loads++ == 0 ? (object)new InvalidOperationException("flaky") : hero;
            using (AssetService service = Service(retry: new RetryPolicy(2, TimeSpan.Zero)))
            {
                IAssetHandler<GameObject> handler = service.LoadAsset(new AddressKey<GameObject>("Hero"));

                Assert.That(await handler.WaitForCompletionAsync(), Is.SameAs(hero));
                Assert.That(_gateway.LoadedLocations, Has.Count.EqualTo(2));
                Assert.That(_failures, Is.Empty);
            }
        }

        [Test]
        public async Task LoadAsset_NetworkFailureMovesToTheNextMirror()
        {
            GameObject hero = Prefab("Hero");
            int loads = 0;
            _gateway.LoadResult = _ =>
                loads++ == 0 ? (object)new ContentDeliveryException(FailureKind.Network, "timeout") : hero;

            using (AssetService service = Service(new[]
            {
                "https://m1.example.com",
                "https://m2.example.com"
            }))
            {
                IAssetHandler<GameObject> handler = service.LoadAsset(new AddressKey<GameObject>("Hero"));

                Assert.That(await handler.WaitForCompletionAsync(), Is.SameAs(hero));
                Assert.That(service.Route.ActiveMirror, Is.EqualTo("https://m2.example.com"));
                Assert.That(_gateway.LoadOperations[0].ReleaseCalls, Is.EqualTo(1));
            }
        }

        [Test]
        public async Task LoadAsset_LifetimeTokenReleasesTheHandler()
        {
            _gateway.LoadResult = _ => Prefab("Hero");
            using (AssetService service = Service())
                using (var lifetime = new CancellationTokenSource())
                {
                    IAssetHandler<GameObject> handler = service.LoadAsset(new AddressKey<GameObject>("Hero"), lifetime.Token);
                    await handler.WaitForCompletionAsync();

                    lifetime.Cancel();
                    await WaitUntilAsync(() => handler.IsReleased);

                    Assert.That(_gateway.LoadOperations[0].ReleaseCalls, Is.EqualTo(1));
                }
        }

        [Test]
        public async Task UnloadAsset_ReleasesEveryHandlerOfTheKey()
        {
            _gateway.LoadResult = _ => Prefab("Hero");
            using (AssetService service = Service())
            {
                IAssetHandler<GameObject> first = service.LoadAsset(new AddressKey<GameObject>("Hero"));
                IAssetHandler<GameObject> second = service.LoadAsset(new AddressKey<GameObject>("Hero"));
                await first.WaitForCompletionAsync();
                await second.WaitForCompletionAsync();

                service.UnloadAsset(new AddressKey<GameObject>("Hero"));

                Assert.That(first.IsReleased, Is.True);
                Assert.That(second.IsReleased, Is.True);
                Assert.That(_gateway.LoadOperations[0].ReleaseCalls, Is.EqualTo(1));
            }
        }

        [Test]
        public async Task LoadAsset_ComponentKeyReturnsTheComponentOfTheLoadedPrefab()
        {
            GameObject hero = Prefab("Hero");
            var collider = hero.AddComponent<BoxCollider>();
            _gateway.LoadResult = _ => hero;
            using (AssetService service = Service())
            {
                IAssetHandler<BoxCollider> handler = service.LoadAsset(new ComponentKey<BoxCollider>(HeroGuid));

                Assert.That(await handler.WaitForCompletionAsync(), Is.SameAs(collider));
                Assert.That(_gateway.LocatedTypes, Is.EqualTo(new[]
                {
                    typeof(GameObject)
                }));
            }
        }

        [Test]
        public async Task LoadAsset_MissingComponentFailsWithTypeMismatch()
        {
            _gateway.LoadResult = _ => Prefab("Hero");
            LogAssert.Expect(LogType.Error, new Regex(@"Load failed \(TypeMismatch\).*not a Camera"));
            using (AssetService service = Service())
            {
                IAssetHandler<Camera> handler = service.LoadAsset(new ComponentKey<Camera>(HeroGuid));
                await handler.WaitForCompletionAsync();

                Assert.That(handler.IsFailed, Is.True);
                Assert.That(_failures, Has.Count.EqualTo(1));
                Assert.That(_failures[0].Kind, Is.EqualTo(FailureKind.TypeMismatch));
                Assert.That(_gateway.LoadOperations[0].ReleaseCalls, Is.EqualTo(1));

                handler.Release();
            }
        }

        [Test]
        public async Task LoadAsset_UnknownKeyFailsWithNotFound()
        {
            _gateway.Locate = (_, __) => new List<IResourceLocation>();
            LogAssert.Expect(LogType.Error, new Regex(@"Load failed \(NotFound\).*'Missing'"));
            using (AssetService service = Service())
            {
                IAssetHandler<GameObject> handler = service.LoadAsset(new AddressKey<GameObject>("Missing"));
                await handler.WaitForCompletionAsync();

                Assert.That(handler.IsFailed, Is.True);
                Assert.That(_failures, Has.Count.EqualTo(1));
                Assert.That(_failures[0].Kind, Is.EqualTo(FailureKind.NotFound));
                Assert.That(_gateway.LoadedLocations, Is.Empty);

                handler.Release();
            }
        }

        [Test]
        public void LoadAsset_InvalidKeyFailsWithoutTouchingAddressables()
        {
            LogAssert.Expect(LogType.Error, new Regex(@"Load failed \(InvalidKey\).*does not point at any content"));
            using (AssetService service = Service())
            {
                IAssetHandler<GameObject> handler = service.LoadAsset(new AddressKey<GameObject>(string.Empty));

                Assert.That(handler.IsDone, Is.True);
                Assert.That(handler.IsFailed, Is.True);
                Assert.That(_failures[0].Kind, Is.EqualTo(FailureKind.InvalidKey));
                Assert.That(_gateway.LocateCalls, Is.Zero);

                handler.Release();
            }
        }

        [Test]
        public async Task Result_AfterReleaseThrowsObjectDisposedException()
        {
            _gateway.LoadResult = _ => Prefab("Hero");
            using (AssetService service = Service())
            {
                IAssetHandler<GameObject> handler = service.LoadAsset(new AddressKey<GameObject>("Hero"));
                await handler.WaitForCompletionAsync();
                handler.Release();

                Assert.That(() => handler.Result, Throws.InstanceOf<ObjectDisposedException>());
            }
        }

        [Test]
        public async Task LoadAssets_LoadsEveryAssetOfTheLabel()
        {
            GameObject first = Prefab("First");
            GameObject second = Prefab("Second");
            _gateway.Locate = (key, type) => Locations(type, "Assets/First.prefab", "Assets/Second.prefab");
            _gateway.LoadResult = location => location.InternalId == "Assets/First.prefab" ? first : second;
            using (AssetService service = Service())
            {
                IAssetHandler<IReadOnlyList<GameObject>> handler = service.LoadAssets<GameObject>(new LabelKey("heroes"));

                Assert.That(await handler.WaitForCompletionAsync(), Is.EqualTo(new[]
                {
                    first,
                    second
                }));

                handler.Release();
                Assert.That(_gateway.LoadOperations[0].ReleaseCalls, Is.EqualTo(1));
                Assert.That(_gateway.LoadOperations[1].ReleaseCalls, Is.EqualTo(1));
            }
        }

        [Test]
        public async Task LoadAssets_AnEmptyLabelGivesAnEmptyList()
        {
            _gateway.Locate = (_, __) => new List<IResourceLocation>();
            using (AssetService service = Service())
            {
                IAssetHandler<IReadOnlyList<GameObject>> handler = service.LoadAssets<GameObject>(new LabelKey("nothing"));

                Assert.That(await handler.WaitForCompletionAsync(), Is.Empty);
                Assert.That(handler.IsFailed, Is.False);
            }
        }

        [Test]
        public async Task Dispose_ReleasesHandlersThatWereNotReleased()
        {
            _gateway.LoadResult = _ => Prefab("Hero");
            AssetService service = Service();
            IAssetHandler<GameObject> handler = service.LoadAsset(new AddressKey<GameObject>("Hero"));
            await handler.WaitForCompletionAsync();

            service.Dispose();

            Assert.That(handler.IsReleased, Is.True);
            Assert.That(_gateway.LoadOperations[0].ReleaseCalls, Is.EqualTo(1));
        }

        private static IList<IResourceLocation> Locations(Type type, params string[] internalIds)
        {
            var locations = new List<IResourceLocation>(internalIds.Length);
            foreach (string internalId in internalIds)
            {
                locations.Add(new ResourceLocationBase(internalId, internalId, "FakeProvider",
                    type));
            }

            return locations;
        }

        private static async Task WaitUntilAsync(Func<bool> condition)
        {
            for (int i = 0; i < 1000 && !condition(); i++)
            {
                await Task.Yield();
            }

            Assert.That(condition(), Is.True, "the condition was not reached");
        }

        private GameObject Prefab(string name)
        {
            var prefab = new GameObject(name);
            _created.Add(prefab);
            return prefab;
        }

        private AssetService Service(string[] mirrors = null, RetryPolicy retry = default)
        {
            var deliverySettings = new ContentDeliverySettings(null, mirrors, string.Empty, TimeSpan.FromSeconds(1d));
            var settings = ScriptableObject.CreateInstance<AssetServiceSettings>();
            settings.Delivery = deliverySettings;
            settings.LoadRetry = retry;
            settings.RebindShadersInEditor = false;

            var service = new AssetService(settings, _gateway);
            service.OnFailed += _failures.Add;
            return service;
        }
    }
}