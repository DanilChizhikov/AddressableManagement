using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace DTech.AddressableManagement.Tests.PlayMode
{
    [TestFixture]
    [PrebuildSetup(typeof(PlayModeContent))]
    [PostBuildCleanup(typeof(PlayModeContent))]
    internal sealed class AddressablesPlayModeTests
    {
        private AssetServiceSettings _settings;
        private AssetService _service;
        private List<ContentFailure> _failures;

        [SetUp]
        public void SetUp()
        {
            _failures = new List<ContentFailure>();
            _settings = ScriptableObject.CreateInstance<AssetServiceSettings>();
            _service = new AssetService(_settings);
            _service.OnFailed += _failures.Add;
        }

        [TearDown]
        public void TearDown()
        {
            _service.Dispose();
            Object.Destroy(_settings);
        }

        [Test]
        public async Task LoadAsset_LoadsAPrefabByItsGuid()
        {
            IAssetHandler<GameObject> handler =
                    _service.LoadAsset(new ReferenceKey<GameObject>(PlayModeContent.GuidOf(PlayModeContent.HeroPath)));

            GameObject hero = await handler.WaitForCompletionAsync();

            Assert.That(hero, Is.Not.Null);
            Assert.That(hero.name, Is.EqualTo("Hero"));
            handler.Release();
        }

        [Test]
        public async Task LoadAsset_ReturnsTheComponentOfAPrefab()
        {
            IAssetHandler<BoxCollider> handler =
                    _service.LoadAsset(new ComponentKey<BoxCollider>(PlayModeContent.GuidOf(PlayModeContent.HeroPath)));

            BoxCollider collider = await handler.WaitForCompletionAsync();

            Assert.That(collider, Is.Not.Null);
            Assert.That(collider.gameObject.name, Is.EqualTo("Hero"));
            handler.Release();
        }

        [Test]
        public async Task LoadAsset_LoadsASpriteSubAssetByGuidAndByAddress()
        {
            IAssetHandler<Sprite> byGuid =
                    _service.LoadAsset(new ReferenceKey<Sprite>(PlayModeContent.GuidOf(PlayModeContent.IconPath), "Icon"));
            IAssetHandler<Sprite> byAddress = _service.LoadAsset(new AddressKey<Sprite>(PlayModeContent.IconAddress));

            Sprite first = await byGuid.WaitForCompletionAsync();
            Sprite second = await byAddress.WaitForCompletionAsync();

            Assert.That(first, Is.Not.Null);
            Assert.That(second, Is.Not.Null);
            Assert.That(first.name, Is.EqualTo("Icon"));
            byGuid.Release();
            byAddress.Release();
        }

        [Test]
        public async Task LoadAsset_KeysOfOneAssetShareItAndTheLastReleaseUnloadsIt()
        {
            IAssetHandler<GameObject> byGuid =
                    _service.LoadAsset(new ReferenceKey<GameObject>(PlayModeContent.GuidOf(PlayModeContent.HeroPath)));
            IAssetHandler<GameObject> byAddress = _service.LoadAsset(new AddressKey<GameObject>(PlayModeContent.HeroAddress));

            GameObject first = await byGuid.WaitForCompletionAsync();
            GameObject second = await byAddress.WaitForCompletionAsync();
            Assert.That(second, Is.SameAs(first));
            int instanceId = first.GetInstanceID();

            byGuid.Release();
            await FramesAsync(3);
            Assert.That(Resources.InstanceIDToObject(instanceId) != null, Is.True, "the other handler keeps the asset loaded");

            byAddress.Release();
            await WaitUntilAsync(() => Resources.InstanceIDToObject(instanceId) == null, 300);
            Assert.That(Resources.InstanceIDToObject(instanceId) == null, Is.True, "the asset is unloaded with its bundle");
        }

        [Test]
        public async Task LoadAssets_LoadsEveryAssetOfTheLabel()
        {
            IAssetHandler<IReadOnlyList<GameObject>> handler =
                    _service.LoadAssets<GameObject>(new LabelKey(PlayModeContent.Label));

            IReadOnlyList<GameObject> assets = await handler.WaitForCompletionAsync();

            var names = new List<string>();
            foreach (GameObject asset in assets)
            {
                names.Add(asset.name);
            }

            Assert.That(names, Is.EquivalentTo(new[] { "Hero", "Villain" }));
            handler.Release();
        }

        [Test]
        public async Task LoadAsset_AnUnknownAddressFailsWithNotFound()
        {
            LogAssert.Expect(LogType.Error, new Regex("NotFound"));
            IAssetHandler<GameObject> handler = _service.LoadAsset(new AddressKey<GameObject>("dtech-am/missing"));

            Assert.That(await handler.WaitForCompletionAsync(), Is.Null);
            Assert.That(handler.IsFailed, Is.True);
            Assert.That(_failures, Has.Count.EqualTo(1));
            Assert.That(_failures[0].Kind, Is.EqualTo(FailureKind.NotFound));
        }

        [Test]
        public async Task Downloads_LocalContentNeedsNothing()
        {
            var keys = new AssetKey[] { new LabelKey(PlayModeContent.Label), new AddressKey<Sprite>(PlayModeContent.IconAddress) };

            Assert.That(await _service.GetDownloadSizeAsync(keys), Is.Zero);
            DownloadResult result = await _service.DownloadAssets(keys, DownloadOptions.Default).WaitForCompletionAsync();
            Assert.That(result.Outcome, Is.EqualTo(DownloadOutcome.AlreadyCached));
            Assert.That(await _service.GetRemoteContentSizeAsync(), Is.Zero);
        }

        [Test]
        public async Task LoadScene_LoadsAdditivelyActivatesOnRequestAndUnloads()
        {
            var key = new SceneKey(PlayModeContent.GuidOf(PlayModeContent.ScenePath));
            ISceneHandler handler = _service.LoadScene(key, new SceneLoadOptions(LoadSceneMode.Additive, true, 100));

            await handler.WaitForCompletionAsync();
            Assert.That(handler.IsFailed, Is.False);
            Assert.That(handler.IsActivated, Is.False);

            await handler.ActivateAsync();
            Scene scene = handler.Result;
            Assert.That(handler.IsActivated, Is.True);
            Assert.That(scene.isLoaded, Is.True);
            Assert.That(GameObject.Find(PlayModeContent.SceneMarker), Is.Not.Null);

            await handler.UnloadAsync();
            Assert.That(handler.IsReleased, Is.True);
            Assert.That(scene.isLoaded, Is.False);
        }

        private static async Task FramesAsync(int count)
        {
            for (int i = 0; i < count; i++)
            {
                await Awaitable.NextFrameAsync();
            }
        }

        private static async Task WaitUntilAsync(Func<bool> condition, int maxFrames)
        {
            for (int i = 0; i < maxFrames && !condition(); i++)
            {
                await Awaitable.NextFrameAsync();
            }
        }
    }
}
