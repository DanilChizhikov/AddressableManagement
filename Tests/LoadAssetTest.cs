using System.Collections;
using MbsCore.AddressableManagement.Infrastructure;
using MbsCore.AddressableManagement.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.TestTools;

namespace MbsCore.AddressableManagement.Tests
{
    [TestFixture]
    internal sealed class LoadAssetTest
    {
        private const string AssetGuid = "a5ca10e16d1ef494b93cd97b0be633ad";
        private const string AssetKey = "Assets/Prefabs/Cube.prefab";
        
        private AssetReference _reference;
        
        [SetUp]
        public void Setup()
        {
            _reference = new AssetReference(AssetGuid);
        }

        [UnityTest]
        public IEnumerator LoadAssetWithKey()
        {
            var service = new AssetService();
            IAssetResponse<GameObject> response = service.LoadAsset<GameObject>(AssetKey);

            yield return new WaitUntil(() => response.IsDone);

            bool hasResult = response.Result != null;
            service.Dispose();
            Assert.AreEqual(true, hasResult);
        }
        
        [UnityTest]
        public IEnumerator LoadAssetWithReference()
        {
            var service = new AssetService();
            IAssetResponse<GameObject> response = service.LoadAsset<GameObject>(_reference);

            yield return new WaitUntil(() => response.IsDone);

            bool hasResult = response.Result != null;
            service.Dispose();
            Assert.AreEqual(true, hasResult);
        }
    }
}