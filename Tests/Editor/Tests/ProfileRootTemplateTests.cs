using DTech.AddressableManagement.Editor;
using NUnit.Framework;

namespace DTech.AddressableManagement.Tests.Editor
{
    [TestFixture]
    internal sealed class ProfileRootTemplateTests
    {
        [Test]
        public void FromLoadPath_TurnsBuildTargetAndVersionIntoRuntimeTokens()
        {
            string template = ProfileRootTemplate.FromLoadPath(
                "https://cdn.example.com/game/[BuildTarget]/[UnityEngine.Application.version]/", value => value);

            Assert.That(template, Is.EqualTo("https://cdn.example.com/game/{Platform}/{UnityEngine.Application.version}"));
        }

        [Test]
        public void FromLoadPath_EvaluatesOtherProfileTokens()
        {
            string template = ProfileRootTemplate.FromLoadPath("[CdnHost]/game/[BuildTarget]",
                value => value.Replace("[CdnHost]", "https://cdn.example.com"));

            Assert.That(template, Is.EqualTo("https://cdn.example.com/game/{Platform}"));
        }

        [Test]
        public void FromLoadPath_ConvertsTokensThatAppearAfterEvaluation()
        {
            string template = ProfileRootTemplate.FromLoadPath("[Remote.Base]",
                value => value.Replace("[Remote.Base]", "https://cdn.example.com/[BuildTarget]"));

            Assert.That(template, Is.EqualTo("https://cdn.example.com/{Platform}"));
        }

        [Test]
        public void FromLoadPath_KeepsRuntimeTokensUntouched()
        {
            string template = ProfileRootTemplate.FromLoadPath("https://cdn.example.com/{MyGame.Cdn.Folder}",
                value => value);

            Assert.That(template, Is.EqualTo("https://cdn.example.com/{MyGame.Cdn.Folder}"));
        }

        [Test]
        public void FromLoadPath_EmptyGivesEmpty()
        {
            Assert.That(ProfileRootTemplate.FromLoadPath("  ", value => value), Is.Empty);
        }

        [Test]
        public void IsHttpTemplate_AcceptsOnlyHttpUrls()
        {
            Assert.That(ProfileRootTemplate.IsHttpTemplate("https://cdn.example.com/x"), Is.True);
            Assert.That(ProfileRootTemplate.IsHttpTemplate("HTTP://cdn.example.com/x"), Is.True);
            Assert.That(ProfileRootTemplate.IsHttpTemplate("{UnityEngine.AddressableAssets.Addressables.RuntimePath}/x"),
                Is.False);
        }
    }
}