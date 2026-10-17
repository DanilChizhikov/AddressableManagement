using NUnit.Framework;
using UnityEngine;

namespace DTech.AddressableManagement.Tests.Editor
{
	[TestFixture]
	internal sealed class AssetKeyTests
	{
		private const string Guid = "0f7a1c2b3d4e5f60718293a4b5c6d7e8";
		
		        [Test]
        public void ReferenceKey_IsValidOnlyWithAWellFormedGuid()
        {
            Assert.That(new ReferenceKey<GameObject>(Guid).IsValid, Is.True);
            Assert.That(new ReferenceKey<GameObject>().IsValid, Is.False);
            Assert.That(new ReferenceKey<GameObject>("not-a-guid").IsValid, Is.False);
        }

        [Test]
        public void ReferenceKey_AppendsTheSubObjectNameToTheRuntimeKey()
        {
            var key = new ReferenceKey<Sprite>(Guid, "Icon");

            Assert.That(key.RuntimeKey, Is.EqualTo($"{Guid}[Icon]"));
            Assert.That(key.IsValid, Is.True);
        }

        [Test]
        public void ReferenceKey_WithoutSubObjectUsesTheGuidAlone()
        {
            Assert.That(new ReferenceKey<Texture2D>(Guid).RuntimeKey, Is.EqualTo(Guid));
        }

        [Test]
        public void AddressKey_IsValidOnlyWithANonEmptyAddress()
        {
            Assert.That(new AddressKey<GameObject>("Enemies/Goblin").IsValid, Is.True);
            Assert.That(new AddressKey<GameObject>(string.Empty).IsValid, Is.False);
            Assert.That(new AddressKey<GameObject>(null).RuntimeKey, Is.Empty);
        }

        [Test]
        public void LabelKey_UsesTheLabelAsRuntimeKey()
        {
            var key = new LabelKey("level-1");

            Assert.That(key.RuntimeKey, Is.EqualTo("level-1"));
            Assert.That(key.IsValid, Is.True);
            Assert.That(new LabelKey().IsValid, Is.False);
        }

        [Test]
        public void ComponentAndSceneKeys_UseTheGuidAsRuntimeKey()
        {
            Assert.That(new ComponentKey<Camera>(Guid).RuntimeKey, Is.EqualTo(Guid));
            Assert.That(new SceneKey(Guid).RuntimeKey, Is.EqualTo(Guid));
            Assert.That(new SceneKey().IsValid, Is.False);
        }

        [Test]
        public void Equals_RequiresTheSameKindAndRuntimeKey()
        {
            var first = new AddressKey<GameObject>("Hero");
            var second = new AddressKey<GameObject>("Hero");
            var otherAddress = new AddressKey<GameObject>("Villain");
            var otherKind = new AddressKey<Sprite>("Hero");

            Assert.That(first.Equals(second), Is.True);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
            Assert.That(first.Equals(otherAddress), Is.False);
            Assert.That(first.Equals(otherKind), Is.False);
            Assert.That(first.Equals((object)null), Is.False);
        }

        [Test]
        public void ToString_ReturnsTheRuntimeKey()
        {
            Assert.That(new AddressKey<GameObject>("Hero").ToString(), Is.EqualTo("Hero"));
        }

	}
}