using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DTech.AddressableManagement.Internal.Delivery;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DTech.AddressableManagement.Tests.Editor
{
    [TestFixture]
    internal sealed class SceneTests
    {
        private const string SceneGuid = "1a2b3c4d5e6f708192a3b4c5d6e7f809";

        private FakeAddressablesGateway _gateway;
        private List<ContentFailure> _failures;

        [SetUp]
        public void SetUp()
        {
            _gateway = new FakeAddressablesGateway();
            _failures = new List<ContentFailure>();
        }

        [Test]
        public async Task LoadScene_ActivatesAtOnceByDefault()
        {
            using (AssetService service = Service())
            {
                ISceneHandler handler = service.LoadScene(new SceneKey(SceneGuid), default);
                await handler.WaitForCompletionAsync();

                Assert.That(handler.IsDone, Is.True);
                Assert.That(handler.IsActivated, Is.True);
                Assert.That(_gateway.SceneOptions[0].Mode, Is.EqualTo(LoadSceneMode.Single));
            }
        }

        [Test]
        public async Task LoadScene_DeferredActivationWaitsForActivateAsync()
        {
            using (AssetService service = Service())
            {
                var options = new SceneLoadOptions(LoadSceneMode.Additive, true, 50);
                ISceneHandler handler = service.LoadScene(new SceneKey(SceneGuid), options);
                await handler.WaitForCompletionAsync();

                Assert.That(handler.IsActivated, Is.False);

                await handler.ActivateAsync();

                Assert.That(handler.IsActivated, Is.True);
                Assert.That(_gateway.SceneOperations[0].ActivateCalls, Is.EqualTo(1));
            }
        }

        [Test]
        public async Task UnloadAsync_UnloadsTheSceneAndReleasesTheHandler()
        {
            using (AssetService service = Service())
            {
                ISceneHandler handler = service.LoadScene(new SceneKey(SceneGuid), default);
                await handler.WaitForCompletionAsync();

                await handler.UnloadAsync();
                await handler.UnloadAsync();

                Assert.That(handler.IsReleased, Is.True);
                Assert.That(_gateway.SceneOperations[0].UnloadCalls, Is.EqualTo(1));
            }
        }

        [Test]
        public async Task UnloadAsset_UnloadsScenesLoadedWithTheKey()
        {
            using (AssetService service = Service())
            {
                ISceneHandler handler = service.LoadScene(new SceneKey(SceneGuid), default);
                await handler.WaitForCompletionAsync();

                service.UnloadAsset(new SceneKey(SceneGuid));
                await Task.Yield();

                Assert.That(handler.IsReleased, Is.True);
                Assert.That(_gateway.SceneOperations[0].UnloadCalls, Is.EqualTo(1));
            }
        }

        [Test]
        public async Task LoadScene_FailureIsReportedAsASceneFailure()
        {
            _gateway.SceneFailure = _ => new InvalidOperationException("broken scene bundle");
            LogAssert.Expect(LogType.Error, new Regex(@"Scene failed \(Unknown\).*broken scene bundle"));
            using (AssetService service = Service())
            {
                ISceneHandler handler = service.LoadScene(new SceneKey(SceneGuid), default);
                await handler.WaitForCompletionAsync();

                Assert.That(handler.IsFailed, Is.True);
                Assert.That(_failures, Has.Count.EqualTo(1));
                Assert.That(_failures[0].Stage, Is.EqualTo(ContentStage.Scene));

                Exception error = await AsyncCatch.ExceptionAsync(() => handler.ActivateAsync());
                Assert.That(error, Is.InstanceOf<InvalidOperationException>());

                handler.Release();
            }
        }

        [Test]
        public async Task LoadScene_NetworkFailureMovesToTheNextMirror()
        {
            _gateway.SceneFailure = attempt =>
                attempt == 0 ? new ContentDeliveryException(FailureKind.Network, "timeout") : null;

            using (AssetService service = Service(new[]
            {
                "https://m1.example.com",
                "https://m2.example.com"
            }))
            {
                ISceneHandler handler = service.LoadScene(new SceneKey(SceneGuid), default);
                await handler.WaitForCompletionAsync();

                Assert.That(handler.IsFailed, Is.False);
                Assert.That(service.Route.ActiveMirror, Is.EqualTo("https://m2.example.com"));
                Assert.That(_gateway.SceneOperations[0].UnloadCalls, Is.EqualTo(1), "the failed attempt is released");
            }
        }

        [Test]
        public void LoadScene_InvalidKeyFailsWithoutLoading()
        {
            LogAssert.Expect(LogType.Error, new Regex(@"Scene failed \(InvalidKey\).*does not point at any scene"));
            using (AssetService service = Service())
            {
                ISceneHandler handler = service.LoadScene(new SceneKey(), default);

                Assert.That(handler.IsFailed, Is.True);
                Assert.That(_failures[0].Kind, Is.EqualTo(FailureKind.InvalidKey));
                Assert.That(_gateway.SceneOperations, Is.Empty);

                handler.Release();
            }
        }

        private AssetService Service(string[] mirrors = null)
        {
            var deliverySettings = new ContentDeliverySettings(null, mirrors, string.Empty, TimeSpan.FromSeconds(1d));
            var settings = ScriptableObject.CreateInstance<AssetServiceSettings>();
            settings.Delivery = deliverySettings;
            settings.RebindShadersInEditor = false;

            var service = new AssetService(settings, _gateway);
            service.OnFailed += _failures.Add;
            return service;
        }
    }
}