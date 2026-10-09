using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SLCSSNMPWalkCollector;
using SLCSSNMPWalkExplorerApi;

namespace SLCSSNMPWalkCollector.Tests
{
    [TestClass]
    public class CollectorLogicTests
    {
        [TestMethod]
        public void TryCompareOids_UsesNumericArcOrdering()
        {
            int comparison;

            Assert.IsTrue(WalkPrimitives.TryCompareOids("1.3.6.1.2.10", "1.3.6.1.2.9", out comparison));

            Assert.IsTrue(comparison > 0);
        }

        [TestMethod]
        public void IsValidOid_RejectsNonNumericArcs()
        {
            Assert.IsFalse(WalkPrimitives.IsValidOid("1.3.6.vendor"));
        }

        [TestMethod]
        public void IsValidOid_AcceptsNumericArcs()
        {
            Assert.IsTrue(WalkPrimitives.IsValidOid("1.3.6.1.2.1"));
        }

        [TestMethod]
        public void TryCompareOids_OrdersParentBeforeChild()
        {
            int comparison;

            Assert.IsTrue(WalkPrimitives.TryCompareOids("1.3.6.1.2", "1.3.6.1.2.1", out comparison));

            Assert.IsTrue(comparison < 0);
        }

        [TestMethod]
        public void EscapeJson_EncodesEmbeddedNewlinesAsSingleRecordContent()
        {
            string escaped = WalkPrimitives.EscapeJson("first\r\nsecond\"value");

            Assert.AreEqual("first\\r\\nsecond\\\"value", escaped);
        }

        [TestMethod]
        public void EscapeJson_EncodesControlCharacters()
        {
            string escaped = WalkPrimitives.EscapeJson("\b\f\t\u001f");

            Assert.AreEqual("\\b\\f\\t\\u001f", escaped);
        }

        [TestMethod]
        public void WalkConnectionConfiguration_Create_ParsesValidConnection()
        {
            WalkConnectionConfiguration configuration = WalkConnectionConfiguration.Create("192.0.2.10", 161, "test-community");

            Assert.AreEqual("192.0.2.10", configuration.Address.ToString());
            Assert.AreEqual(161, configuration.Port);
            Assert.AreEqual("test-community", configuration.Community);
        }

        [TestMethod]
        public void WalkConnectionConfiguration_Create_RejectsMissingCommunity()
        {
            Assert.ThrowsException<ArgumentException>(delegate
            {
                WalkConnectionConfiguration.Create("192.0.2.10", 161, " ");
            });
        }

        [TestMethod]
        public void WalkConnectionConfiguration_Create_RejectsNonIpv4Address()
        {
            Assert.ThrowsException<ArgumentException>(delegate
            {
                WalkConnectionConfiguration.Create("snmp.example.test", 161, "test-community");
            });
        }

        [TestMethod]
        public void WalkConnectionConfiguration_Create_RejectsInvalidPort()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(delegate
            {
                WalkConnectionConfiguration.Create("192.0.2.10", 0, "test-community");
            });
        }

        [TestMethod]
        public void WalkRunSettingsSnapshot_Create_AcceptsBoundedSettings()
        {
            WalkConnectionConfiguration connection = WalkConnectionConfiguration.Create("192.0.2.10", 161, "test-community");

            WalkRunSettingsSnapshot snapshot = WalkRunSettingsSnapshot.Create(connection, 1000, 3, 2, 100000, 4, false, 10, 10000, String.Empty, String.Empty, "run-123");

            Assert.AreSame(connection, snapshot.Connection);
            Assert.AreEqual(1000, snapshot.TimeoutMilliseconds);
            Assert.AreEqual(3, snapshot.Retries);
            Assert.AreEqual(100000, snapshot.MaximumWalkVariables);
            Assert.AreEqual(7, snapshot.DiscoveryRoots.Length);
            Assert.AreEqual("run-123", snapshot.RunCorrelationId);
        }

        [TestMethod]
        public void WalkRunSettingsSnapshot_Create_RejectsInvalidWorkerCount()
        {
            WalkConnectionConfiguration connection = WalkConnectionConfiguration.Create("192.0.2.10", 161, "test-community");

            Assert.ThrowsException<ArgumentOutOfRangeException>(delegate
            {
                WalkRunSettingsSnapshot.Create(connection, 1000, 3, 2, 100000, 0, false, 10, 10000, String.Empty, String.Empty, String.Empty);
            });
        }

        [TestMethod]
        public void WalkRunSettingsSnapshot_CreateFromParameterValues_ParsesInvariantValues()
        {
            WalkRunSettingsSnapshot snapshot = WalkRunSettingsSnapshot.CreateFromParameterValues("192.0.2.10", "161", "test-community", "1000", "3", "2", "100000", "4", "false", "10", "10000", String.Empty, "1.3.6.1.1,1.3.6.1.4", "run-123");

            Assert.AreEqual("192.0.2.10", snapshot.Connection.Address.ToString());
            Assert.AreEqual(161, snapshot.Connection.Port);
            Assert.IsFalse(snapshot.UseGetBulk);
            CollectionAssert.AreEqual(new[] { "1.3.6.1.1", "1.3.6.1.4" }, snapshot.DiscoveryRoots);
            Assert.AreEqual("run-123", snapshot.RunCorrelationId);
        }

        [TestMethod]
        public void WalkRunSettingsSnapshot_CreateFromParameterValues_RejectsNonBooleanGetBulk()
        {
            Assert.ThrowsException<ArgumentException>(delegate
            {
                WalkRunSettingsSnapshot.CreateFromParameterValues("192.0.2.10", "161", "test-community", "1000", "3", "2", "100000", "4", "maybe", "10", "10000", String.Empty, String.Empty, String.Empty);
            });
        }

        [TestMethod]
        public void WalkRunSettingsSnapshot_Create_RejectsOverlappingDiscoveryRoots()
        {
            WalkConnectionConfiguration connection = WalkConnectionConfiguration.Create("192.0.2.10", 161, "test-community");

            Assert.ThrowsException<ArgumentException>(delegate
            {
                WalkRunSettingsSnapshot.Create(connection, 1000, 3, 2, 100000, 4, false, 10, 10000, String.Empty, "1.3.6.1.2,1.3.6.1.2.1", String.Empty);
            });
        }

        [TestMethod]
        public void WalkArtifactCatalog_ListArtifacts_ReturnsOnlyCommittedPairs()
        {
            string directory = Path.Combine(Path.GetTempPath(), "SLC-S-SNMPWalkCollector.Tests", Guid.NewGuid().ToString("N"));
            const string completedFileName = "SNMPWalk_192.0.2.10_20260101T000000000Z.walk";
            const string orphanFileName = "SNMPWalk_192.0.2.11_20260101T000000000Z.walk";
            Directory.CreateDirectory(directory);

            try
            {
                File.WriteAllText(Path.Combine(directory, completedFileName), "{\"oid\":\"1.3.6.1\",\"value\":\"value\"}\r\n{\"oid\":\"1.3.6.1.2\",\"value\":\"second value\"}\r\n");
                File.WriteAllText(Path.Combine(directory, completedFileName + ".metadata.json"), CreateMetadata(completedFileName, "2026-01-01T00:01:00.0000000Z"));
                File.WriteAllText(Path.Combine(directory, orphanFileName + ".metadata.json"), CreateMetadata(orphanFileName, "2026-01-01T00:02:00.0000000Z"));
                File.WriteAllText(Path.Combine(directory, completedFileName + ".metadata.json.partial"), CreateMetadata(completedFileName, "2026-01-01T00:03:00.0000000Z"));

                WalkArtifactCatalog catalog = new WalkArtifactCatalog(directory);
                var artifacts = catalog.ListArtifacts();

                Assert.AreEqual(1, artifacts.Count);
                Assert.AreEqual("SNMPWalk_192.0.2.10_20260101T000000000Z", artifacts[0].Id);
                Assert.AreEqual(1, artifacts[0].RootOutcomes.Count);
                StringAssert.Contains(catalog.SerializeArtifacts(), "\"artifacts\":[{");

                string rawArtifact;
                Assert.IsTrue(catalog.TryReadRawArtifact(artifacts[0].Id, out rawArtifact));
                StringAssert.Contains(rawArtifact, "\"oid\":\"1.3.6.1\"");
                Assert.IsFalse(catalog.TryReadRawArtifact("..\\not-an-artifact", out rawArtifact));

                string bindings;
                Assert.IsTrue(catalog.TrySearchBindings(artifacts[0].Id, "1.3.6.1", "second", out bindings));
                StringAssert.Contains(bindings, "\"oid\":\"1.3.6.1.2\"");
                Assert.ThrowsException<ArgumentException>(delegate { catalog.TrySearchBindings(artifacts[0].Id, "not-an-oid", String.Empty, out bindings); });
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void WalkArtifactCatalog_TryBuildTree_ReturnsBoundedHierarchyForCommittedArtifact()
        {
            string directory = Path.Combine(Path.GetTempPath(), "SLC-S-SNMPWalkCollector.Tests", Guid.NewGuid().ToString("N"));
            const string fileName = "SNMPWalk_192.0.2.10_20260101T000000000Z.walk";
            Directory.CreateDirectory(directory);

            try
            {
                File.WriteAllText(Path.Combine(directory, fileName), "{\"oid\":\"1.3.6.1.2\",\"value\":\"first\"}\r\n{\"oid\":\"1.3.6.1.10\",\"value\":\"second\"}\r\n");
                File.WriteAllText(Path.Combine(directory, fileName + ".metadata.json"), CreateMetadata(fileName, "2026-01-01T00:01:00.0000000Z"));
                WalkArtifactCatalog catalog = new WalkArtifactCatalog(directory);
                string tree;

                Assert.IsTrue(catalog.TryBuildTree("SNMPWalk_192.0.2.10_20260101T000000000Z", out tree));
                StringAssert.Contains(tree, "\"oid\":\"root\",\"label\":\"Observed OIDs\",\"bindings\":2");
                StringAssert.Contains(tree, "\"oid\":\"1.3.6.1.2\",\"label\":\"2\",\"bindings\":1,\"value\":\"first\"");
                StringAssert.Contains(tree, "\"oid\":\"1.3.6.1.10\",\"label\":\"10\",\"bindings\":1,\"value\":\"second\"");
                Assert.IsFalse(catalog.TryBuildTree("missing", out tree));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void WalkArtifactCatalog_TryBuildTree_StopsAfterOneThousandBindings()
        {
            string directory = Path.Combine(Path.GetTempPath(), "SLC-S-SNMPWalkCollector.Tests", Guid.NewGuid().ToString("N"));
            const string fileName = "SNMPWalk_192.0.2.10_20260101T000000000Z.walk";
            Directory.CreateDirectory(directory);

            try
            {
                StringBuilder rawArtifact = new StringBuilder();
                for (int index = 0; index < 1001; index++)
                {
                    rawArtifact.Append("{\"oid\":\"1.3.6.1.2.").Append(index).Append("\",\"value\":\"value\"}\r\n");
                }

                File.WriteAllText(Path.Combine(directory, fileName), rawArtifact.ToString());
                File.WriteAllText(Path.Combine(directory, fileName + ".metadata.json"), CreateMetadata(fileName, "2026-01-01T00:01:00.0000000Z"));
                WalkArtifactCatalog catalog = new WalkArtifactCatalog(directory);
                string tree;

                Assert.IsTrue(catalog.TryBuildTree("SNMPWalk_192.0.2.10_20260101T000000000Z", out tree));
                StringAssert.Contains(tree, "\"oid\":\"root\",\"label\":\"Observed OIDs\",\"bindings\":1000");
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void QaSnmpClient_Walk_TerminatesWithOutsideSubtree_WhenOidLeavesRootPrefix()
        {
            var queue = new Queue<IList<WalkVariable>>();
            queue.Enqueue(new List<WalkVariable> { new WalkVariable("1.3.6.1.2.1.1.1.0", "sysDescr") });
            queue.Enqueue(new List<WalkVariable> { new WalkVariable("1.3.6.1.2.1.2.1.0", "ifIndex") });

            var transport = new FakeSnmpTransport
            {
                OnGetNext = delegate { return queue.Dequeue(); }
            };
            var client = new QaSnmpClient(transport);

            RootWalkResult result = client.Walk(CollectorVersion.V1, "1.3.6.1.2.1.1", 0, false, 10, delegate { return true; }, delegate { });

            Assert.AreEqual("outside-subtree", result.TerminalState);
            Assert.AreEqual(1, result.BindingCount);
            Assert.AreEqual("1.3.6.1.2.1.1.1.0", result.LastOid);
            Assert.AreEqual("sysDescr", result.Variables[0].Value);
        }

        [TestMethod]
        public void QaSnmpClient_Walk_TerminatesWithRepeatedOid_WhenAgentReturnsSameOidTwice()
        {
            var transport = new FakeSnmpTransport
            {
                OnGetNext = delegate { return new List<WalkVariable> { new WalkVariable("1.3.6.1.2.1.1.1.0", "val") }; }
            };
            var client = new QaSnmpClient(transport);

            RootWalkResult result = client.Walk(CollectorVersion.V1, "1.3.6.1.2.1.1", 0, false, 10, delegate { return true; }, delegate { });

            Assert.AreEqual("repeated-oid", result.TerminalState);
            Assert.AreEqual(1, result.BindingCount);
            StringAssert.Contains(result.Error, "repeated OID sequence");
        }

        [TestMethod]
        public void QaSnmpClient_Walk_TerminatesWithNonIncreasingOid_WhenAgentReturnsDecreasingOid()
        {
            var queue = new Queue<IList<WalkVariable>>();
            queue.Enqueue(new List<WalkVariable> { new WalkVariable("1.3.6.1.2.1.1.2.0", "val2") });
            queue.Enqueue(new List<WalkVariable> { new WalkVariable("1.3.6.1.2.1.1.1.0", "val1") });

            var transport = new FakeSnmpTransport
            {
                OnGetNext = delegate { return queue.Dequeue(); }
            };
            var client = new QaSnmpClient(transport);

            RootWalkResult result = client.Walk(CollectorVersion.V1, "1.3.6.1.2.1.1", 0, false, 10, delegate { return true; }, delegate { });

            Assert.AreEqual("non-increasing-oid", result.TerminalState);
            Assert.AreEqual(1, result.BindingCount);
            StringAssert.Contains(result.Error, "non-increasing OID");
        }

        [TestMethod]
        public void QaSnmpClient_Walk_TerminatesWithInvalidOid_WhenAgentReturnsMalformedOid()
        {
            var transport = new FakeSnmpTransport
            {
                OnGetNext = delegate { return new List<WalkVariable> { new WalkVariable("1.3.6.1.2.1.1.invalid.0", "val") }; }
            };
            var client = new QaSnmpClient(transport);

            RootWalkResult result = client.Walk(CollectorVersion.V1, "1.3.6.1.2.1.1", 0, false, 10, delegate { return true; }, delegate { });

            Assert.AreEqual("invalid-oid", result.TerminalState);
            Assert.AreEqual(0, result.BindingCount);
            StringAssert.Contains(result.Error, "invalid OID");
        }

        [TestMethod]
        public void QaSnmpClient_Walk_TerminatesWithEndOfMib_WhenValueIsEndOfMibView()
        {
            var transport = new FakeSnmpTransport
            {
                OnGetNext = delegate { return new List<WalkVariable> { new WalkVariable("1.3.6.1.2.1.1.1.0", "SNMP End-of-MIB-View") }; }
            };
            var client = new QaSnmpClient(transport);

            RootWalkResult result = client.Walk(CollectorVersion.V1, "1.3.6.1.2.1.1", 0, false, 10, delegate { return true; }, delegate { });

            Assert.AreEqual("end-of-mib", result.TerminalState);
            Assert.AreEqual(0, result.BindingCount);
        }

        [TestMethod]
        public void QaSnmpClient_Walk_TerminatesWithUnexpectedBindingCount_WhenGetNextReturnsMultipleVariables()
        {
            var transport = new FakeSnmpTransport
            {
                OnGetNext = delegate
                {
                    return new List<WalkVariable>
                    {
                        new WalkVariable("1.3.6.1.2.1.1.1.0", "first"),
                        new WalkVariable("1.3.6.1.2.1.1.2.0", "second")
                    };
                }
            };
            var client = new QaSnmpClient(transport);

            RootWalkResult result = client.Walk(CollectorVersion.V1, "1.3.6.1.2.1.1", 0, false, 10, delegate { return true; }, delegate { });

            Assert.AreEqual("unexpected-binding-count", result.TerminalState);
            Assert.AreEqual(0, result.BindingCount);
            StringAssert.Contains(result.Error, "returned 2 bindings");
        }

        [TestMethod]
        public void QaSnmpClient_Walk_GetNextRetriesOnTransientError_AndSucceeds()
        {
            int callCount = 0;
            var transport = new FakeSnmpTransport
            {
                OnGetNext = delegate
                {
                    callCount++;
                    if (callCount <= 2)
                    {
                        throw new TimeoutException("SNMP request timed out.");
                    }

                    if (callCount == 3)
                    {
                        return new List<WalkVariable> { new WalkVariable("1.3.6.1.2.1.1.1.0", "sysDescr") };
                    }

                    return new List<WalkVariable> { new WalkVariable("1.3.6.1.2.1.2.1.0", "outside") };
                }
            };
            var client = new QaSnmpClient(transport);

            RootWalkResult result = client.Walk(CollectorVersion.V1, "1.3.6.1.2.1.1", 2, false, 10, delegate { return true; }, delegate { });

            Assert.AreEqual("outside-subtree", result.TerminalState);
            Assert.AreEqual(1, result.BindingCount);
            Assert.AreEqual(2, result.RetryCount);
        }

        [TestMethod]
        public void QaSnmpClient_Walk_GetNextTerminatesWithRequestFailed_WhenRetriesExhausted()
        {
            var transport = new FakeSnmpTransport
            {
                OnGetNext = delegate { throw new TimeoutException("Persistent timeout."); }
            };
            var client = new QaSnmpClient(transport);

            RootWalkResult result = client.Walk(CollectorVersion.V1, "1.3.6.1.2.1.1", 2, false, 10, delegate { return true; }, delegate { });

            Assert.AreEqual("request-failed", result.TerminalState);
            Assert.AreEqual(2, result.RetryCount);
            StringAssert.Contains(result.Error, "after 3 attempt(s)");
        }

        [TestMethod]
        public void QaSnmpClient_Walk_GetNextRetriesOnEmptyResponse_UntilExhausted()
        {
            var transport = new FakeSnmpTransport
            {
                OnGetNext = delegate { return new List<WalkVariable>(); }
            };
            var client = new QaSnmpClient(transport);

            RootWalkResult result = client.Walk(CollectorVersion.V1, "1.3.6.1.2.1.1", 1, false, 10, delegate { return true; }, delegate { });

            Assert.AreEqual("request-failed", result.TerminalState);
            Assert.AreEqual(1, result.RetryCount);
            StringAssert.Contains(result.Error, "empty response");
        }

        [TestMethod]
        public void QaSnmpClient_Walk_GetBulkWithAdaptiveFallback_HalvesRepetitionsOnTooBig()
        {
            int bulkCallCount = 0;
            var transport = new FakeSnmpTransport
            {
                OnGetBulk = delegate (string oid, int maxReps)
                {
                    bulkCallCount++;
                    if (oid == "1.3.6.1.2.1.1")
                    {
                        if (maxReps == 10)
                        {
                            return new BulkTransportResponse(new List<WalkVariable>(), 1); // 1 = tooBig
                        }

                        if (maxReps == 5)
                        {
                            return new BulkTransportResponse(new List<WalkVariable>
                            {
                                new WalkVariable("1.3.6.1.2.1.1.1.0", "v1"),
                                new WalkVariable("1.3.6.1.2.1.1.2.0", "v2"),
                            }, 0);
                        }
                    }

                    return new BulkTransportResponse(new List<WalkVariable>
                    {
                        new WalkVariable("1.3.6.1.2.1.2.0", "outside")
                    }, 0);
                }
            };
            var client = new QaSnmpClient(transport);

            RootWalkResult result = client.Walk(CollectorVersion.V2c, "1.3.6.1.2.1.1", 1, true, 10, delegate { return true; }, delegate { });

            Assert.AreEqual("outside-subtree", result.TerminalState);
            Assert.AreEqual(2, result.BindingCount);
            Assert.IsTrue(bulkCallCount >= 2);
            Assert.IsTrue(result.RetryCount >= 1);
        }

        [TestMethod]
        public void QaSnmpClient_Walk_GetBulkWithAdaptiveFallback_HalvesRepetitionsOnGenErr()
        {
            var transport = new FakeSnmpTransport
            {
                OnGetBulk = delegate (string oid, int maxReps)
                {
                    if (oid == "1.3.6.1.2.1.1")
                    {
                        if (maxReps == 8)
                        {
                            return new BulkTransportResponse(new List<WalkVariable>(), 5); // 5 = genErr
                        }

                        if (maxReps == 4)
                        {
                            return new BulkTransportResponse(new List<WalkVariable>
                            {
                                new WalkVariable("1.3.6.1.2.1.1.1.0", "v1")
                            }, 0);
                        }
                    }

                    return new BulkTransportResponse(new List<WalkVariable>
                    {
                        new WalkVariable("1.3.6.1.2.2.0", "outside")
                    }, 0);
                }
            };
            var client = new QaSnmpClient(transport);

            RootWalkResult result = client.Walk(CollectorVersion.V2c, "1.3.6.1.2.1.1", 1, true, 8, delegate { return true; }, delegate { });

            Assert.AreEqual("outside-subtree", result.TerminalState);
            Assert.AreEqual(1, result.BindingCount);
            Assert.IsTrue(result.RetryCount >= 1);
        }

        [TestMethod]
        public void QaSnmpClient_Walk_GetBulkTerminatesWithRequestFailed_OnAuthorizationError()
        {
            var transport = new FakeSnmpTransport
            {
                OnGetBulk = delegate
                {
                    return new BulkTransportResponse(new List<WalkVariable>(), 16); // 16 = authorizationError
                }
            };
            var client = new QaSnmpClient(transport);

            RootWalkResult result = client.Walk(CollectorVersion.V2c, "1.3.6.1.2.1.1", 0, true, 10, delegate { return true; }, delegate { });

            Assert.AreEqual("request-failed", result.TerminalState);
            Assert.AreEqual("authorizationError", result.Error);
        }

        [TestMethod]
        public void CollectionBudget_TryReserve_DecrementsBudget_AndRejectsWhenExhausted()
        {
            var budget = new CollectionBudget(2);

            Assert.AreEqual(2, budget.Remaining);
            Assert.IsFalse(budget.IsExhausted);

            Assert.IsTrue(budget.TryReserve());
            Assert.AreEqual(1, budget.Remaining);
            Assert.IsFalse(budget.IsExhausted);

            Assert.IsTrue(budget.TryReserve());
            Assert.AreEqual(0, budget.Remaining);
            Assert.IsTrue(budget.IsExhausted);

            Assert.IsFalse(budget.TryReserve());
            Assert.AreEqual(0, budget.Remaining);
            Assert.IsTrue(budget.IsExhausted);
        }

        [TestMethod]
        public void CollectionBudget_TryReserve_IsThreadSafeUnderConcurrency()
        {
            const int totalBudget = 1000;
            const int workers = 10;
            const int attemptsPerWorker = 200;

            var budget = new CollectionBudget(totalBudget);
            int totalReserved = 0;
            var tasks = new Task[workers];

            for (int i = 0; i < workers; i++)
            {
                tasks[i] = Task.Factory.StartNew(delegate
                {
                    int localCount = 0;
                    for (int j = 0; j < attemptsPerWorker; j++)
                    {
                        if (budget.TryReserve())
                        {
                            localCount++;
                        }
                    }

                    Interlocked.Add(ref totalReserved, localCount);
                });
            }

            Task.WaitAll(tasks);

            Assert.AreEqual(totalBudget, totalReserved);
            Assert.AreEqual(0, budget.Remaining);
            Assert.IsTrue(budget.IsExhausted);
        }

        [TestMethod]
        public void QaSnmpClient_Walk_TerminatesWithGlobalSafetyCap_WhenBudgetExhausted()
        {
            var budget = new CollectionBudget(1);
            var queue = new Queue<IList<WalkVariable>>();
            queue.Enqueue(new List<WalkVariable> { new WalkVariable("1.3.6.1.2.1.1.1.0", "v1") });
            queue.Enqueue(new List<WalkVariable> { new WalkVariable("1.3.6.1.2.1.1.2.0", "v2") });

            var transport = new FakeSnmpTransport
            {
                OnGetNext = delegate { return queue.Dequeue(); }
            };
            var client = new QaSnmpClient(transport);

            RootWalkResult result = client.Walk(CollectorVersion.V1, "1.3.6.1.2.1.1", 0, false, 10, budget.TryReserve, delegate { });

            Assert.AreEqual("global-safety-cap", result.TerminalState);
            Assert.AreEqual(1, result.BindingCount);
            StringAssert.Contains(result.Error, "global walk safety cap");
        }

        [TestMethod]
        public void QaSnmpClient_WalkPrefixes_AssignsSkippedGlobalSafetyCap_ToUnstartedRoots()
        {
            var connection = WalkConnectionConfiguration.Create("192.0.2.10", 161, "community");
            var snapshot = WalkRunSettingsSnapshot.Create(connection, 1000, 0, 1, 1, 1, false, 10, 10000, String.Empty, "1.3.6.1.2.1.1,1.3.6.1.2.1.2", "corr-1");
            var settings = WalkSettings.Create(snapshot);

            var transport = new FakeSnmpTransport
            {
                OnGetNext = delegate (CollectorVersion ver, string oid)
                {
                    return new List<WalkVariable> { new WalkVariable(oid + ".1", "val") };
                }
            };
            var client = new QaSnmpClient(transport);

            List<RootWalkResult> results = QaSnmpClient.WalkPrefixes(client, settings, CollectorVersion.V1, delegate { }, delegate { return client; });

            Assert.AreEqual(2, results.Count);
            Assert.AreEqual("global-safety-cap", results[0].TerminalState);
            Assert.AreEqual("skipped-global-safety-cap", results[1].TerminalState);
            Assert.AreEqual(0, results[1].BindingCount);
            StringAssert.Contains(results[1].Error, "before this root started");
        }

        [TestMethod]
        public void WalkMetadata_ToJson_ValidatesSchemaVersion2_AndOmitsCommunityString()
        {
            const string secretCommunity = "super-secret-snmp-password";
            var rootResult = new RootWalkResult("1.3.6.1.2.1.1", new List<WalkVariable>
            {
                new WalkVariable("1.3.6.1.2.1.1.1.0", "sysDescr")
            }, "1.3.6.1.2.1.1.1.0", "outside-subtree", null, 0);

            var metadata = new WalkMetadata(
                "SNMPWalk_192.0.2.10_test.walk",
                new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 1, 1, 0, 1, 0, DateTimeKind.Utc),
                "192.0.2.10",
                161,
                "SNMPv2c",
                2,
                true,
                5000,
                new[] { "1.3.6.1.2.1.1" },
                "corr-\"id\"\r\ntest",
                1,
                new[] { rootResult });

            string json = metadata.ToJson();

            StringAssert.Contains(json, "\"schemaVersion\":2");
            StringAssert.Contains(json, "\"rawFileName\":\"SNMPWalk_192.0.2.10_test.walk\"");
            StringAssert.Contains(json, "\"rawFormat\":\"jsonl\"");
            StringAssert.Contains(json, "\"runCorrelationId\":\"corr-\\\"id\\\"\\r\\ntest\"");
            StringAssert.Contains(json, "\"isComplete\":true");
            StringAssert.Contains(json, "\"partitionRecommended\":false");
            Assert.IsFalse(json.Contains(secretCommunity));
        }

        [TestMethod]
        public void RootWalkOutcome_PartitionRecommended_TrueOnlyWhenBindingThresholdMetAndComplete()
        {
            var variables = new List<WalkVariable>();
            for (int i = 0; i < 100; i++)
            {
                variables.Add(new WalkVariable("1.3.6.1.2.1.1." + i, "val"));
            }

            var completeResult = new RootWalkResult("1.3.6.1.2.1.1", variables, "1.3.6.1.2.1.1.99", "outside-subtree", null, 0);
            var incompleteResult = new RootWalkResult("1.3.6.1.2.1.1", variables, "1.3.6.1.2.1.1.99", "request-failed", "timeout", 0);

            var outcomeComplete100 = new RootWalkOutcome(completeResult, 100);
            var outcomeIncomplete100 = new RootWalkOutcome(incompleteResult, 100);
            var outcomeCompleteThreshold101 = new RootWalkOutcome(completeResult, 101);

            Assert.IsTrue(outcomeComplete100.PartitionRecommended);
            Assert.IsFalse(outcomeIncomplete100.PartitionRecommended);
            Assert.IsFalse(outcomeCompleteThreshold101.PartitionRecommended);
        }

        [TestMethod]
        public void RootWalkOutcome_IsComplete_TrueForOutsideSubtreeAndEndOfMib_FalseForErrors()
        {
            var vars = new List<WalkVariable>();
            var outsideResult = new RootWalkResult("1.3.6.1", vars, "1.3.6.1", "outside-subtree", null, 0);
            var endOfMibResult = new RootWalkResult("1.3.6.1", vars, "1.3.6.1", "end-of-mib", null, 0);
            var safetyCapResult = new RootWalkResult("1.3.6.1", vars, "1.3.6.1", "safety-cap", null, 0);
            var requestFailedResult = new RootWalkResult("1.3.6.1", vars, "1.3.6.1", "request-failed", "err", 0);

            Assert.IsTrue(new RootWalkOutcome(outsideResult, 100).IsComplete);
            Assert.IsTrue(new RootWalkOutcome(endOfMibResult, 100).IsComplete);
            Assert.IsFalse(new RootWalkOutcome(safetyCapResult, 100).IsComplete);
            Assert.IsFalse(new RootWalkOutcome(requestFailedResult, 100).IsComplete);
        }

        private sealed class FakeSnmpTransport : ISnmpTransport
        {
            public Func<CollectorVersion, string, IList<WalkVariable>> OnGet { get; set; }

            public Func<CollectorVersion, string, IList<WalkVariable>> OnGetNext { get; set; }

            public Func<string, int, BulkTransportResponse> OnGetBulk { get; set; }

            public IList<WalkVariable> SendGet(CollectorVersion version, string oid)
            {
                return OnGet != null ? OnGet(version, oid) : new List<WalkVariable>();
            }

            public IList<WalkVariable> SendGetNext(CollectorVersion version, string oid)
            {
                return OnGetNext != null ? OnGetNext(version, oid) : new List<WalkVariable>();
            }

            public BulkTransportResponse SendGetBulk(string oid, int maxRepetitions)
            {
                return OnGetBulk != null ? OnGetBulk(oid, maxRepetitions) : new BulkTransportResponse(new List<WalkVariable>(), 0);
            }
        }

        private static string CreateMetadata(string rawFileName, string completedAtUtc)
        {
            return "{\"rawFileName\":\"" + rawFileName + "\",\"startedAtUtc\":\"2026-01-01T00:00:00.0000000Z\",\"completedAtUtc\":\"" + completedAtUtc + "\",\"targetAddress\":\"192.0.2.10\",\"targetPort\":161,\"snmpVersion\":\"SNMPv2c\",\"concurrentWalkWorkers\":2,\"useGetBulk\":false,\"totalBindings\":1,\"isComplete\":true,\"rootOutcomes\":[{\"rootOid\":\"1.3.6.1\",\"bindingCount\":1,\"lastOid\":\"1.3.6.1\",\"terminalState\":\"end-of-mib\",\"isComplete\":true,\"partitionRecommended\":false,\"retryCount\":0}]}";
        }
    }
}