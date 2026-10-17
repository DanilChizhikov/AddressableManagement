using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DTech.AddressableManagement.Internal.Delivery;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DTech.AddressableManagement.Tests.Editor
{
    [TestFixture]
    internal sealed class DeliveryTests
    {
        private FakeAddressablesGateway _gateway;
        private List<ContentFailure> _failures;

        [SetUp]
        public void SetUp()
        {
            _gateway = new FakeAddressablesGateway();
            _failures = new List<ContentFailure>();
        }

        [Test]
        public async Task InitializeAsync_InstallsTheRewriterAndInitializesOnce()
        {
            using (AssetService service = Service(Settings()))
            {
                await Task.WhenAll(service.InitializeAsync(), service.InitializeAsync());
                await service.InitializeAsync();

                Assert.That(service.IsInitialized, Is.True);
                Assert.That(_gateway.InitializeCalls, Is.EqualTo(1));
                Assert.That(_gateway.InstalledRewriter, Is.Not.Null);
            }

            Assert.That(_gateway.InstalledRewriter, Is.Null, "Dispose removes the request rewriter");
        }

        [Test]
        public async Task InitializeAsync_AFailureIsReportedAndRetriedByTheNextCall()
        {
            _gateway.InitializeFailure = call => call == 0 ? new InvalidOperationException("broken catalog") : null;
            LogAssert.Expect(LogType.Error, new Regex(@"Initialization failed \(Unknown\).*broken catalog"));
            using (AssetService service = Service(Settings()))
            {
                Exception error = await AsyncCatch.ExceptionAsync(() => service.InitializeAsync());
                Assert.That(error, Is.InstanceOf<InvalidOperationException>());
                Assert.That(service.IsInitialized, Is.False);
                Assert.That(_failures, Has.Count.EqualTo(1));
                Assert.That(_failures[0].Stage, Is.EqualTo(ContentStage.Initialization));

                await service.InitializeAsync();

                Assert.That(service.IsInitialized, Is.True);
                Assert.That(_gateway.InitializeCalls, Is.EqualTo(2));
            }
        }

        [Test]
        public async Task InitializeAsync_ResetsTheCatalogCacheOnlyWhenEnabled()
        {
            using (AssetService service = Service(Settings()))
            {
                await service.InitializeAsync();
            }

            Assert.That(_gateway.VersionResetCalls, Is.Zero);

            AssetServiceSettings settings = Settings();
            settings.ResetCatalogCacheOnVersionChange = true;
            using (AssetService service = Service(settings))
            {
                await service.InitializeAsync();
            }

            Assert.That(_gateway.VersionResetCalls, Is.EqualTo(1));
        }

        [Test]
        public async Task InitializeAsync_ProbesMirrorsUntilOneAnswers()
        {
            _gateway.ProbeResult = url => url.StartsWith("https://m2.example.com", StringComparison.Ordinal);
            AssetServiceSettings settings = Settings(null, new[]
                {
                    "https://m1.example.com",
                    "https://m2.example.com"
                },
                "/health.txt");

            using (AssetService service = Service(settings))
            {
                await service.InitializeAsync();

                Assert.That(_gateway.ProbedUrls, Is.EqualTo(new[]
                {
                    "https://m1.example.com/health.txt",
                    "https://m2.example.com/health.txt",
                }));

                Assert.That(service.Route.ActiveMirror, Is.EqualTo("https://m2.example.com"));
                Assert.That(_failures, Is.Empty);
            }
        }

        [Test]
        public async Task InitializeAsync_AllProbesFailingResetsTheRouteAndReports()
        {
            _gateway.ProbeResult = _ => false;
            AssetServiceSettings settings = Settings(null, new[]
                {
                    "https://m1.example.com",
                    "https://m2.example.com"
                },
                "health.txt");

            LogAssert.Expect(LogType.Error, new Regex(@"Initialization failed \(Network\).*No mirror answered the probe"));
            using (AssetService service = Service(settings))
            {
                await service.InitializeAsync();

                Assert.That(service.Route.ActiveMirror, Is.EqualTo("https://m1.example.com"));
                Assert.That(_failures, Has.Count.EqualTo(1));
                Assert.That(_failures[0].Kind, Is.EqualTo(FailureKind.Network));
                Assert.That(service.IsInitialized, Is.True);
            }
        }

        [Test]
        public async Task UpdateCatalogsAsync_ReportsUpToDateAndUpdated()
        {
            _gateway.CatalogCheck = call => call == 0
                ? Array.Empty<string>()
                : new[]
                {
                    "main"
                };

            using (AssetService service = Service(Settings()))
            {
                Assert.That(await service.UpdateCatalogsAsync(), Is.EqualTo(CatalogState.UpToDate));
                Assert.That(await service.UpdateCatalogsAsync(), Is.EqualTo(CatalogState.Updated));
                Assert.That(_gateway.UpdatedCatalogIds, Is.EqualTo(new[]
                {
                    "main"
                }));

                Assert.That(service.CatalogState, Is.EqualTo(CatalogState.Updated));
            }
        }

        [Test]
        public async Task UpdateCatalogsAsync_ConcurrentCallsShareOneCheck()
        {
            var barrier = new TaskCompletionSource<bool>();
            _gateway.CatalogCheckBarrier = barrier.Task;
            using (AssetService service = Service(Settings()))
            {
                Task<CatalogState> first = service.UpdateCatalogsAsync();
                Task<CatalogState> second = service.UpdateCatalogsAsync();
                barrier.SetResult(true);
                await Task.WhenAll(first, second);

                Assert.That(_gateway.CatalogCheckCalls, Is.EqualTo(1));
            }
        }

        [Test]
        public async Task UpdateCatalogsAsync_NotFoundMovesToTheNextRootAndRetries()
        {
            _gateway.CatalogCheck = call => call == 0
                ? throw new ContentDeliveryException(FailureKind.NotFound, "404")
                : Array.Empty<string>();

            AssetServiceSettings settings = Settings(new[]
            {
                "https://a.example.com/live",
                "https://b.example.com/live"
            });

            using (AssetService service = Service(settings))
            {
                CatalogState state = await service.UpdateCatalogsAsync();

                Assert.That(state, Is.EqualTo(CatalogState.UpToDate));
                Assert.That(service.Route.ActiveRoot, Is.EqualTo("https://b.example.com/live"));
                Assert.That(_gateway.CatalogCheckCalls, Is.EqualTo(2));
                Assert.That(_failures, Is.Empty);
            }
        }

        [Test]
        public async Task UpdateCatalogsAsync_NetworkFailureMovesToTheNextMirror()
        {
            _gateway.CatalogCheck = call => call == 0
                ? throw new ContentDeliveryException(FailureKind.Network, "timeout")
                : new[]
                {
                    "main"
                };

            AssetServiceSettings settings = Settings(null, new[]
            {
                "https://m1.example.com",
                "https://m2.example.com"
            });

            using (AssetService service = Service(settings))
            {
                Assert.That(await service.UpdateCatalogsAsync(), Is.EqualTo(CatalogState.Updated));
                Assert.That(service.Route.ActiveMirror, Is.EqualTo("https://m2.example.com"));
            }
        }

        [Test]
        public async Task UpdateCatalogsAsync_ExhaustedRouteFailsAndReportsOnce()
        {
            _gateway.CatalogCheck = _ => throw new ContentDeliveryException(FailureKind.Network, "timeout");
            AssetServiceSettings settings = Settings(null, new[]
            {
                "https://m1.example.com",
                "https://m2.example.com"
            });

            LogAssert.Expect(LogType.Error, new Regex(@"CatalogUpdate failed \(Network\).*timeout"));
            using (AssetService service = Service(settings))
            {
                Assert.That(await service.UpdateCatalogsAsync(), Is.EqualTo(CatalogState.Failed));
                Assert.That(_gateway.CatalogCheckCalls, Is.EqualTo(2));
                Assert.That(_failures, Has.Count.EqualTo(1));
                Assert.That(_failures[0].Stage, Is.EqualTo(ContentStage.CatalogUpdate));
                Assert.That(_failures[0].Kind, Is.EqualTo(FailureKind.Network));
            }
        }

        [Test]
        public async Task UpdateCatalogsAsync_OfflineFailsWithoutTouchingTheRoute()
        {
            _gateway.SetIsInternetReachable(false);
            AssetServiceSettings settings = Settings(null, new[]
            {
                "https://m1.example.com",
                "https://m2.example.com"
            });

            LogAssert.Expect(LogType.Error, new Regex(@"CatalogUpdate failed \(Network\).*not reachable"));
            using (AssetService service = Service(settings))
            {
                Assert.That(await service.UpdateCatalogsAsync(), Is.EqualTo(CatalogState.Failed));
                Assert.That(_gateway.CatalogCheckCalls, Is.Zero);
                Assert.That(service.Route.ActiveMirror, Is.EqualTo("https://m1.example.com"));
                Assert.That(_failures[0].Kind, Is.EqualTo(FailureKind.Network));
            }
        }

        [Test]
        public async Task UpdateCatalogsAsync_CleansTheBundleCacheOnlyOutsideTheEditorAfterAnUpdate()
        {
            _gateway.CatalogCheck = _ => new[]
            {
                "main"
            };

            AssetServiceSettings settings = Settings();
            settings.CleanBundleCacheAfterCatalogUpdate = true;
            using AssetService service = Service(settings);
            await service.UpdateCatalogsAsync();
            Assert.That(_gateway.CleanBundleCacheCalls, Is.EqualTo(1));

            _gateway.SetIsEditor(true);
            await service.UpdateCatalogsAsync();
            Assert.That(_gateway.CleanBundleCacheCalls, Is.EqualTo(1));
        }

        [Test]
        public void Dispose_RejectsLaterCalls()
        {
            AssetService service = Service(Settings());
            service.Dispose();

            Exception error = null;
            try
            {
                service.InitializeAsync();
            }
            catch (Exception exception)
            {
                error = exception;
            }

            Assert.That(error, Is.InstanceOf<ObjectDisposedException>());
        }

        private static AssetServiceSettings Settings(string[] roots = null, string[] mirrors = null,
            string probePath = null)
        {
            var deliverySettings = new ContentDeliverySettings(roots, mirrors, probePath, TimeSpan.FromSeconds(1d));

            var serviceSettings = ScriptableObject.CreateInstance<AssetServiceSettings>();
            serviceSettings.Delivery = deliverySettings;
            return serviceSettings;
        }

        private AssetService Service(AssetServiceSettings settings)
        {
            var service = new AssetService(settings, _gateway);
            service.OnFailed += _failures.Add;
            return service;
        }
    }
}