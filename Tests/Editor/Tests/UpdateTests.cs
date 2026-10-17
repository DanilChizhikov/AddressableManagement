using System;
using System.Collections.Generic;
using DTech.AddressableManagement.Editor.Update;
using NUnit.Framework;
using UnityEditor.PackageManager;

namespace DTech.AddressableManagement.Tests.Editor
{
    [TestFixture]
    internal sealed class UpdateTests
    {
        [Test]
        public void VersionTags_ParsesReleaseTags()
        {
            Assert.That(VersionTags.Parse("v1.2.3"), Is.EqualTo(new Version(1, 2, 3)));
            Assert.That(VersionTags.Parse(" 1.2.3 "), Is.EqualTo(new Version(1, 2, 3)));
            Assert.That(VersionTags.Parse("V1.2"), Is.EqualTo(new Version(1, 2, 0)));
        }

        [Test]
        public void VersionTags_RejectsPreReleaseAndGarbage()
        {
            Assert.That(VersionTags.Parse("v1.2.3-preview.1"), Is.Null);
            Assert.That(VersionTags.Parse("latest"), Is.Null);
            Assert.That(VersionTags.Parse(string.Empty), Is.Null);
            Assert.That(VersionTags.Parse(null), Is.Null);
        }

        [Test]
        public void VersionTags_MaxKeepsHighestVersion()
        {
            Assert.That(VersionTags.Max(null, new Version(0, 2, 0)), Is.EqualTo(new Version(0, 2, 0)));
            Assert.That(VersionTags.Max(new Version(0, 2, 0), null), Is.EqualTo(new Version(0, 2, 0)));
            Assert.That(VersionTags.Max(new Version(0, 9, 0), new Version(0, 10, 0)), Is.EqualTo(new Version(0, 10, 0)));
        }

        [Test]
        public void InstalledPackage_ExtractsGitUrlFromPackageId()
        {
            Assert.That(InstalledPackage.GitUrlFromPackageId(
                    "com.dtech.addressable-management@https://github.com/DanilChizhikov/AddressableManagement.git#v1.0.0"),
                Is.EqualTo("https://github.com/DanilChizhikov/AddressableManagement.git"));
            Assert.That(InstalledPackage.GitUrlFromPackageId(
                    "com.dtech.addressable-management@git@github.com:DanilChizhikov/AddressableManagement.git#v1.0.0"),
                Is.EqualTo("git@github.com:DanilChizhikov/AddressableManagement.git"));
            Assert.That(InstalledPackage.GitUrlFromPackageId(string.Empty), Is.Empty);
        }

        [Test]
        public void InstalledPackage_BuildsInstallIdentifierPerSource()
        {
            var version = new Version(1, 1, 0);
            var git = new InstalledPackage("com.dtech.addressable-management", new Version(1, 0, 0), PackageSource.Git,
                "https://github.com/DanilChizhikov/AddressableManagement.git");
            var registry = new InstalledPackage("com.dtech.addressable-management", new Version(1, 0, 0), PackageSource.Registry,
                string.Empty);

            Assert.That(git.InstallIdentifier(version), Is.EqualTo("https://github.com/DanilChizhikov/AddressableManagement.git#v1.1.0"));
            Assert.That(registry.InstallIdentifier(version), Is.EqualTo("com.dtech.addressable-management@1.1.0"));
        }

        [Test]
        public void GitHubApi_ParsesTagArray()
        {
            GitHubTag[] tags = GitHubApi.ParseArray<GitHubTag>("[{\"name\":\"v1.0.0\"},{\"name\":\"v1.1.0\"}]");

            Assert.That(tags.Length, Is.EqualTo(2));
            Assert.That(tags[1].name, Is.EqualTo("v1.1.0"));
            Assert.That(GitHubApi.ParseArray<GitHubTag>(string.Empty), Is.Empty);
        }

        [Test]
        public void ReleaseNotesLoader_CleanBodyDropsFullChangelogLine()
        {
            string body = "## Added\r\n- Update window\r\n\r\n**Full Changelog**: https://github.com/x/y/compare/v1.0.0...v1.1.0";

            Assert.That(ReleaseNotesLoader.CleanBody(body), Is.EqualTo("## Added\n- Update window"));
            Assert.That(ReleaseNotesLoader.CleanBody(null), Is.Empty);
        }

        [Test]
        public void ReleaseNotesLoader_SelectsPublishedVersionsAfterCurrentUpToLatestNewestFirst()
        {
            GitHubRelease[] releases = GitHubApi.ParseArray<GitHubRelease>(
                "[{\"tag_name\":\"v1.0.0\",\"name\":\"v1.0.0\",\"body\":\"old\"}," +
                "{\"tag_name\":\"v1.1.0\",\"name\":\"Loading\",\"body\":\"one\"}," +
                "{\"tag_name\":\"v1.3.0\",\"name\":\"v1.3.0\",\"body\":\"three\"}," +
                "{\"tag_name\":\"v1.2.0\",\"name\":\"v1.2.0\",\"body\":\"two\"}," +
                "{\"tag_name\":\"v1.2.5\",\"name\":\"Draft\",\"body\":\"draft\",\"draft\":true}," +
                "{\"tag_name\":\"v1.2.6\",\"name\":\"Preview\",\"body\":\"preview\",\"prerelease\":true}," +
                "{\"tag_name\":\"nightly\",\"name\":\"nightly\",\"body\":\"garbage\"}]");

            List<ReleaseNote> notes = ReleaseNotesLoader.Select(releases, new Version(1, 0, 0), new Version(1, 2, 0));

            Assert.That(notes.Count, Is.EqualTo(2));
            Assert.That(notes[0].Version, Is.EqualTo(new Version(1, 2, 0)));
            Assert.That(notes[1].Version, Is.EqualTo(new Version(1, 1, 0)));
            Assert.That(notes[1].Title, Is.EqualTo("Loading"));
        }

        [Test]
        public void UpdateSkips_SuppressesOnlySkippedVersionsUnlessSkipAlways()
        {
            var skipped = new UpdateSkipState { SkippedVersion = "1.1.0" };
            var always = new UpdateSkipState { SkipAlways = true };
            var none = new UpdateSkipState();

            Assert.That(UpdateSkips.IsSuppressed(skipped, new Version(1, 1, 0)), Is.True);
            Assert.That(UpdateSkips.IsSuppressed(skipped, new Version(1, 2, 0)), Is.False);
            Assert.That(UpdateSkips.IsSuppressed(always, new Version(9, 0, 0)), Is.True);
            Assert.That(UpdateSkips.IsSuppressed(none, new Version(1, 0, 0)), Is.False);
        }
    }
}
