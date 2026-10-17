using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DTech.AddressableManagement.Internal.Delivery;
using DTech.AddressableManagement.Internal.Gateway;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.TestTools;

namespace DTech.AddressableManagement.Tests.Editor
{
    [TestFixture]
    internal sealed class DownloadTests
    {
        private static readonly AssetKey _hero = new AddressKey<GameObject>("Hero");
        private static readonly AssetKey _villain = new AddressKey<GameObject>("Villain");

        private FakeAddressablesGateway _gateway;
        private List<ContentFailure> _failures;

        [SetUp]
        public void SetUp()
        {
            _gateway = new FakeAddressablesGateway();
            _failures = new List<ContentFailure>();
        }

        [Test]
        public async Task GetDownloadSizeAsync_RemembersKeysThatNeedNothing()
        {
            using (AssetService service = Service())
            {
                Assert.That(await service.GetDownloadSizeAsync(_hero), Is.Zero);
                Assert.That(await service.GetDownloadSizeAsync(_hero), Is.Zero);

                Assert.That(_gateway.DownloadSizeCalls, Is.EqualTo(1));
            }
        }

        [Test]
        public async Task GetDownloadSizeAsync_QueriesEveryDistinctKeyOnce()
        {
            _gateway.DownloadSize = scope => scope.Keys.Count * 100L;
            using (AssetService service = Service())
            {
                long size = await service.GetDownloadSizeAsync(new[]
                {
                    _hero,
                    _villain,
                    _hero
                });

                Assert.That(size, Is.EqualTo(200L));
            }
        }

        [Test]
        public void GetDownloadSizeAsync_RejectsInvalidKeys()
        {
            using (AssetService service = Service())
            {
                Assert.That(() => service.GetDownloadSizeAsync(new AddressKey<GameObject>(string.Empty)),
                    Throws.ArgumentException);

                Assert.That(() => service.GetDownloadSizeAsync((AssetKey)null), Throws.ArgumentNullException);
            }
        }

        [Test]
        public async Task GetDownloadSizeAsync_IsMeasuredAgainAfterCatalogsChange()
        {
            _gateway.CatalogCheck = _ => new[]
            {
                "main"
            };

            using (AssetService service = Service())
            {
                await service.GetDownloadSizeAsync(_hero);
                await service.UpdateCatalogsAsync();
                await service.GetDownloadSizeAsync(_hero);

                Assert.That(_gateway.DownloadSizeCalls, Is.EqualTo(2));
            }
        }

        [Test]
        public async Task DownloadAssets_CompletesWithoutDownloadingWhenEverythingIsCached()
        {
            using (AssetService service = Service())
            {
                DownloadResult result = await service.DownloadAssets(new[]
                    {
                        _hero
                    }, DownloadOptions.Default)
                    .WaitForCompletionAsync();

                Assert.That(result.Outcome, Is.EqualTo(DownloadOutcome.AlreadyCached));
                Assert.That(_gateway.DownloadedScopes, Is.Empty);
            }
        }

        [Test]
        public async Task DownloadAssets_DownloadsVerifiesAndReleasesTheOperation()
        {
            var sizes = new Queue<long>(new[]
            {
                500L,
                0L
            });

            var operation = FakeContentOperation<bool>.Succeeded(true);
            _gateway.DownloadSize = _ => sizes.Dequeue();
            _gateway.DownloadOperation = _ => operation;
            using (AssetService service = Service())
            {
                IAssetDownloadResponse response = service.DownloadAssets(new[]
                {
                    _hero
                }, DownloadOptions.Default);

                DownloadResult result = await response.WaitForCompletionAsync();

                Assert.That(result.Outcome, Is.EqualTo(DownloadOutcome.Downloaded));
                Assert.That(response.TotalBytes, Is.EqualTo(500L));
                Assert.That(response.DownloadedBytes, Is.EqualTo(500L));
                Assert.That(response.Progress, Is.EqualTo(1f));
                Assert.That(response.State, Is.EqualTo(DownloadState.Completed));
                Assert.That(operation.ReleaseCalls, Is.EqualTo(1));
                Assert.That(await service.GetDownloadSizeAsync(_hero), Is.Zero, "a verified download is remembered");
                Assert.That(_gateway.DownloadSizeCalls, Is.EqualTo(2));
            }
        }

        [Test]
        public async Task DownloadAssets_RetriesWhenVerificationFindsMissingBytes()
        {
            var sizes = new Queue<long>(new[]
            {
                500L,
                200L,
                0L
            });

            _gateway.DownloadSize = _ => sizes.Dequeue();
            using (AssetService service = Service())
            {
                var options = new DownloadOptions(new RetryPolicy(2, TimeSpan.Zero), false);
                IAssetDownloadResponse response = service.DownloadAssets(new[]
                {
                    _hero
                }, options);

                DownloadResult result = await response.WaitForCompletionAsync();

                Assert.That(result.Outcome, Is.EqualTo(DownloadOutcome.Downloaded));
                Assert.That(response.Attempt, Is.EqualTo(2));
                Assert.That(_gateway.DownloadedScopes, Has.Count.EqualTo(2));
            }
        }

        [Test]
        public async Task DownloadAssets_FailsAfterTheLastAttemptAndReportsOnce()
        {
            _gateway.DownloadSize = _ => 500L;
            _gateway.DownloadOperation = _ => FakeContentOperation<bool>.Failed(new InvalidOperationException("broken"));
            LogAssert.Expect(LogType.Error, new Regex(@"Download failed \(Unknown\).*broken"));
            using (AssetService service = Service())
            {
                var options = new DownloadOptions(new RetryPolicy(2, TimeSpan.Zero), false);
                DownloadResult result = await service.DownloadAssets(new[]
                    {
                        _hero
                    }, options)
                    .WaitForCompletionAsync();

                Assert.That(result.Outcome, Is.EqualTo(DownloadOutcome.Failed));
                Assert.That(result.Failure, Is.EqualTo(FailureKind.Unknown));
                Assert.That(_gateway.DownloadedScopes, Has.Count.EqualTo(2));
                Assert.That(_failures, Has.Count.EqualTo(1));
                Assert.That(_failures[0].Stage, Is.EqualTo(ContentStage.Download));
                Assert.That(_failures[0].Key, Is.EqualTo("Hero"));
            }
        }

        [Test]
        public async Task DownloadAssets_DoesNotRetryFailuresThatCannotRecover()
        {
            _gateway.DownloadSize = _ => 500L;
            _gateway.DownloadOperation = _ =>
                FakeContentOperation<bool>.Failed(new ContentDeliveryException(FailureKind.InvalidKey, "unknown key"));

            LogAssert.Expect(LogType.Error, new Regex(@"Download failed \(InvalidKey\).*unknown key"));

            using (AssetService service = Service())
            {
                var options = new DownloadOptions(new RetryPolicy(5, TimeSpan.Zero), false);
                DownloadResult result = await service.DownloadAssets(new[]
                    {
                        _hero
                    }, options)
                    .WaitForCompletionAsync();

                Assert.That(result.Failure, Is.EqualTo(FailureKind.InvalidKey));
                Assert.That(_gateway.DownloadedScopes, Has.Count.EqualTo(1));
            }
        }

        [Test]
        public async Task DownloadAssets_MovingToAnotherMirrorDoesNotUseAnAttempt()
        {
            var sizes = new Queue<long>(new[]
            {
                500L,
                0L
            });

            var operations = new Queue<ContentOperation<bool>>(new ContentOperation<bool>[]
            {
                FakeContentOperation<bool>.Failed(new ContentDeliveryException(FailureKind.Network, "timeout")),
                FakeContentOperation<bool>.Succeeded(true),
            });

            _gateway.DownloadSize = _ => sizes.Dequeue();
            _gateway.DownloadOperation = _ => operations.Dequeue();
            using (AssetService service = Service(null, new[]
            {
                "https://m1.example.com",
                "https://m2.example.com"
            }))
            {
                var options = new DownloadOptions(RetryPolicy.None, false);
                IAssetDownloadResponse response = service.DownloadAssets(new[]
                {
                    _hero
                }, options);

                DownloadResult result = await response.WaitForCompletionAsync();

                Assert.That(result.Outcome, Is.EqualTo(DownloadOutcome.Downloaded));
                Assert.That(response.Attempt, Is.EqualTo(1));
                Assert.That(service.Route.ActiveMirror, Is.EqualTo("https://m2.example.com"));
                Assert.That(_failures, Is.Empty);
            }
        }

        [Test]
        public async Task DownloadAssets_OfflineAttemptFailsWithoutRequesting()
        {
            _gateway.DownloadSize = _ => 500L;
            _gateway.SetIsInternetReachable(false);
            LogAssert.Expect(LogType.Error, new Regex(@"Download failed \(Network\).*not reachable"));
            using (AssetService service = Service())
            {
                DownloadResult result = await service.DownloadAssets(new[]
                    {
                        _hero
                    }, new DownloadOptions(RetryPolicy.None, false))
                    .WaitForCompletionAsync();

                Assert.That(result.Failure, Is.EqualTo(FailureKind.Network));
                Assert.That(_gateway.DownloadedScopes, Is.Empty);
            }
        }

        [Test]
        public async Task DownloadAssets_WaitsWhileSuspended()
        {
            var sizes = new Queue<long>(new[]
            {
                500L,
                0L
            });

            _gateway.DownloadSize = _ => sizes.Dequeue();
            using (AssetService service = Service())
            {
                IDisposable suspension = service.SuspendDownloads();
                IAssetDownloadResponse response = service.DownloadAssets(new[]
                {
                    _hero
                }, DownloadOptions.Default);

                await WaitUntilAsync(() => response.State == DownloadState.Suspended);
                Assert.That(service.IsDownloadSuspended, Is.True);
                Assert.That(_gateway.DownloadedScopes, Is.Empty);

                suspension.Dispose();
                suspension.Dispose();
                DownloadResult result = await response.WaitForCompletionAsync();

                Assert.That(result.Outcome, Is.EqualTo(DownloadOutcome.Downloaded));
                Assert.That(service.IsDownloadSuspended, Is.False);
            }
        }

        [Test]
        public async Task DownloadAssets_CancelReleasesTheRunningOperation()
        {
            var operation = new FakeContentOperation<bool>();
            _gateway.DownloadSize = _ => 500L;
            _gateway.DownloadOperation = _ => operation;
            using (AssetService service = Service())
            {
                IAssetDownloadResponse response = service.DownloadAssets(new[]
                {
                    _hero
                }, DownloadOptions.Default);

                await WaitUntilAsync(() => response.State == DownloadState.Downloading);

                response.Cancel();
                DownloadResult result = await response.WaitForCompletionAsync();

                Assert.That(result.Outcome, Is.EqualTo(DownloadOutcome.Cancelled));
                Assert.That(operation.ReleaseCalls, Is.EqualTo(1));
                Assert.That(_failures, Is.Empty);
            }
        }

        [Test]
        public async Task DownloadAssets_UpdatesCatalogsFirstWhenAsked()
        {
            using (AssetService service = Service())
            {
                await service.DownloadAssets(new[]
                    {
                        _hero
                    }, new DownloadOptions(RetryPolicy.None, true))
                    .WaitForCompletionAsync();

                Assert.That(_gateway.CatalogCheckCalls, Is.EqualTo(1));
            }
        }

        [Test]
        public async Task DownloadRemoteContent_UsesTheCollectedLocationsAndRemembersTheResult()
        {
            var location = new ResourceLocationBase("Hero", "Assets/Hero.prefab", "provider",
                typeof(GameObject));

            _gateway.RemoteContent = new List<IResourceLocation>
            {
                location
            };

            var sizes = new Queue<long>(new[]
            {
                500L,
                0L
            });

            _gateway.DownloadSize = _ => sizes.Dequeue();
            using (AssetService service = Service())
            {
                DownloadResult result = await service.DownloadRemoteContent(DownloadOptions.Default).WaitForCompletionAsync();

                Assert.That(result.Outcome, Is.EqualTo(DownloadOutcome.Downloaded));
                Assert.That(_gateway.DownloadedScopes[0].Locations, Is.EqualTo(new[]
                {
                    location
                }));

                Assert.That(await service.GetRemoteContentSizeAsync(), Is.Zero);
                Assert.That(_gateway.RemoteContentCollections, Is.EqualTo(1));
                Assert.That(_gateway.DownloadSizeCalls, Is.EqualTo(2));
            }
        }

        private static async Task WaitUntilAsync(Func<bool> condition)
        {
            for (int i = 0; i < 1000 && !condition(); i++)
            {
                await Task.Yield();
            }

            Assert.That(condition(), Is.True, "the condition was not reached");
        }

        private AssetService Service(string[] roots = null, string[] mirrors = null)
        {
            var deliverySettings = new ContentDeliverySettings(roots, mirrors, string.Empty, TimeSpan.FromSeconds(1d));

            var settings = ScriptableObject.CreateInstance<AssetServiceSettings>();
            settings.Delivery = deliverySettings;

            var service = new AssetService(settings, _gateway);
            service.OnFailed += _failures.Add;
            return service;
        }
    }
}