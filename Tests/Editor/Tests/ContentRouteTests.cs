using System;
using DTech.AddressableManagement.Internal.Delivery;
using DTech.AddressableManagement.Internal.Diagnostics;
using NUnit.Framework;
using UnityEngine.AddressableAssets;

namespace DTech.AddressableManagement.Tests.Editor
{
    [TestFixture]
    internal sealed class ContentRouteTests
    {
        private const string CatalogUrl = "https://origin.example.com/game/Android/1.0/catalog_1.hash";

        [Test]
        public void Rewrite_KeepsTheUrlWhenNothingIsConfigured()
        {
            ContentRoute route = Route(Array.Empty<string>(), Array.Empty<string>());

            Assert.That(route.Rewrite(CatalogUrl), Is.EqualTo(CatalogUrl));
        }

        [Test]
        public void Rewrite_AppendsTheFileNameToTheActiveRoot()
        {
            ContentRoute route = Route(new[]
            {
                "https://cdn.example.com/content/live/"
            }, Array.Empty<string>());

            Assert.That(route.Rewrite(CatalogUrl), Is.EqualTo("https://cdn.example.com/content/live/catalog_1.hash"));
        }

        [Test]
        public void Rewrite_KeepsTheQueryString()
        {
            ContentRoute route = Route(new[]
            {
                "https://cdn.example.com/live"
            }, Array.Empty<string>());

            Assert.That(route.Rewrite("https://origin.example.com/a/b/x.bundle?v=3"),
                Is.EqualTo("https://cdn.example.com/live/x.bundle?v=3"));
        }

        [Test]
        public void Rewrite_ReplacesOnlyTheOriginWithAMirror()
        {
            ContentRoute route = Route(Array.Empty<string>(), new[]
            {
                "http://mirror.example.net:8080/ignored/path"
            });

            Assert.That(route.Rewrite(CatalogUrl),
                Is.EqualTo("http://mirror.example.net:8080/game/Android/1.0/catalog_1.hash"));
        }

        [Test]
        public void Rewrite_AppliesTheMirrorOnTopOfTheRoot()
        {
            ContentRoute route = Route(new[]
            {
                "https://cdn.example.com/live"
            }, new[]
            {
                "https://backup.example.net"
            });

            Assert.That(route.Rewrite(CatalogUrl), Is.EqualTo("https://backup.example.net/live/catalog_1.hash"));
        }

        [Test]
        public void Rewrite_LeavesNonHttpUrlsAlone()
        {
            ContentRoute route = Route(new[]
            {
                "https://cdn.example.com/live"
            }, new[]
            {
                "https://backup.example.net"
            });

            const string local = "file:///data/app/aa/Android/local.bundle";

            Assert.That(route.Rewrite(local), Is.EqualTo(local));
        }

        [Test]
        public void Configure_IgnoresEntriesThatAreNotAbsoluteHttpUrls()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            ContentRoute route = Route(new[]
                {
                    "cdn.example.com/live",
                    "https://cdn.example.com/live"
                },
                new[]
                {
                    "ftp://mirror.example.net",
                    "https://mirror.example.net"
                });

            Assert.That(route.ActiveRoot, Is.EqualTo("https://cdn.example.com/live"));
            Assert.That(route.ActiveMirror, Is.EqualTo("https://mirror.example.net"));
        }

        [Test]
        public void TryAdvance_NotFoundMovesToTheNextRoot()
        {
            ContentRoute route = Route(new[]
                {
                    "https://a.example.com/x",
                    "https://b.example.com/y"
                },
                new[]
                {
                    "https://m1.example.com",
                    "https://m2.example.com"
                });

            int raised = 0;
            route.OnChanged += _ => raised++;

            Assert.That(route.TryAdvance(FailureKind.NotFound, route.Generation), Is.True);
            Assert.That(route.ActiveRoot, Is.EqualTo("https://b.example.com/y"));
            Assert.That(route.ActiveMirror, Is.EqualTo("https://m1.example.com"));
            Assert.That(raised, Is.EqualTo(1));
        }

        [Test]
        public void TryAdvance_TransportFailuresMoveToTheNextMirror()
        {
            foreach (FailureKind kind in new[]
            {
                FailureKind.Network,
                FailureKind.ServerError,
                FailureKind.Corrupted
            })
            {
                ContentRoute route = Route(Array.Empty<string>(), new[]
                {
                    "https://m1.example.com",
                    "https://m2.example.com"
                });

                Assert.That(route.TryAdvance(kind, route.Generation), Is.True, kind.ToString());
                Assert.That(route.ActiveMirror, Is.EqualTo("https://m2.example.com"), kind.ToString());
            }
        }

        [Test]
        public void TryAdvance_OtherFailuresDoNotMove()
        {
            ContentRoute route = Route(new[]
                {
                    "https://a.example.com/x",
                    "https://b.example.com/y"
                },
                new[]
                {
                    "https://m1.example.com",
                    "https://m2.example.com"
                });

            Assert.That(route.TryAdvance(FailureKind.ClientError, route.Generation), Is.False);
            Assert.That(route.TryAdvance(FailureKind.InvalidKey, route.Generation), Is.False);
            Assert.That(route.ActiveRoot, Is.EqualTo("https://a.example.com/x"));
            Assert.That(route.ActiveMirror, Is.EqualTo("https://m1.example.com"));
        }

        [Test]
        public void TryAdvance_ReturnsFalseWhenTheListIsExhausted()
        {
            ContentRoute route = Route(Array.Empty<string>(), new[]
            {
                "https://m1.example.com",
                "https://m2.example.com"
            });

            Assert.That(route.TryAdvance(FailureKind.Network, route.Generation), Is.True);
            Assert.That(route.TryAdvance(FailureKind.Network, route.Generation), Is.False);
            Assert.That(route.ActiveMirror, Is.EqualTo("https://m2.example.com"));
        }

        [Test]
        public void TryAdvance_AStaleGenerationRetriesWithoutMovingAgain()
        {
            ContentRoute route = Route(Array.Empty<string>(),
                new[]
                {
                    "https://m1.example.com",
                    "https://m2.example.com",
                    "https://m3.example.com"
                });

            int observed = route.Generation;

            Assert.That(route.TryAdvance(FailureKind.Network, observed), Is.True);
            Assert.That(route.TryAdvance(FailureKind.Network, observed), Is.True);
            Assert.That(route.ActiveMirror, Is.EqualTo("https://m2.example.com"));
        }

        [Test]
        public void Reset_GoesBackToTheFirstEntries()
        {
            ContentRoute route = Route(new[]
                {
                    "https://a.example.com/x",
                    "https://b.example.com/y"
                },
                new[]
                {
                    "https://m1.example.com",
                    "https://m2.example.com"
                });

            route.TryAdvance(FailureKind.NotFound, route.Generation);
            route.TryAdvance(FailureKind.Network, route.Generation);

            route.Reset();

            Assert.That(route.ActiveRoot, Is.EqualTo("https://a.example.com/x"));
            Assert.That(route.ActiveMirror, Is.EqualTo("https://m1.example.com"));
        }

        [Test]
        public void Apply_ReplacesTheEntriesAndRaisesOnChanged()
        {
            ContentRoute route = Route(new[]
            {
                "https://a.example.com/x"
            }, Array.Empty<string>());

            IContentRoute changed = null;
            route.OnChanged += value => changed = value;

            route.Apply(new ContentDeliverySettings(new[]
                {
                    "https://z.example.com/live"
                }, null, null,
                TimeSpan.FromSeconds(3d)));

            Assert.That(changed, Is.SameAs(route));
            Assert.That(route.ActiveRoot, Is.EqualTo("https://z.example.com/live"));
            Assert.That(route.ActiveMirror, Is.Null);
        }

        [Test]
        public void Configure_EvaluatesThePlatformToken()
        {
            ContentRoute route = Route(new[]
            {
                "https://cdn.example.com/{Platform}/live"
            }, Array.Empty<string>());

            Assert.That(route.ActiveRoot,
                Is.EqualTo($"https://cdn.example.com/{PlatformMappingService.GetPlatformPathSubFolder()}/live"));
        }

        private static ContentRoute Route(string[] roots, string[] mirrors)
        {
            var settings = new ContentDeliverySettings(roots, mirrors, string.Empty,
                TimeSpan.FromSeconds(3d));

            return new ContentRoute(settings, new AssetLog());
        }
    }
}