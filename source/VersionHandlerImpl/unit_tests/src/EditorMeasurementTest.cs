// <copyright file="EditorMeasurementTest.cs" company="Google Inc.">
// Copyright (C) 2019 Google Inc. All Rights Reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//  http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//    limitations under the License.
// </copyright>

namespace Google.VersionHandlerImpl.Tests {
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;
    using System.Net;

    using Google;

    /// <summary>
    /// Tests the EditorMeasurement class.
    /// </summary>
    [TestFixture]
    public class EditorMeasurementTest {

        /// <summary>
        /// Fake implementation of IPortableWebRequest that aggregates posted data.
        /// </summary>
        class FakePortableWebRequest : IPortableWebRequest {

            /// <summary>
            /// Fake implementation of IPortableWebRequestStatus that returns a completed request.
            /// </summary>
            private class RequestStatus : IPortableWebRequestStatus {

                /// <summary>
                /// Always returns true.
                /// </summary>
                public bool Complete { get { return true; } }

                /// <summary>
                /// Returns an empty result.
                /// </summary>
                public byte[] Result { get { return new byte[0]; } }

                /// <summary>
                /// Get the response headers.
                /// </summary>
                public IDictionary<string, string> Headers {
                    get {
                        return new Dictionary<string, string>();
                    }
                }

                /// <summary>
                /// Get the status code from the response headers.
                /// </summary>
                public HttpStatusCode Status { get { return HttpStatusCode.OK; } }
            }

            /// <summary>
            /// List of posted data.
            /// </summary>
            public List<KeyValuePair<string, string>> PostedUrlAndForms { get; private set; }

            /// <summary>
            /// Initialize the list of posted data.
            /// </summary>
            public FakePortableWebRequest() {
                PostedUrlAndForms = new List<KeyValuePair<string, string>>();
            }

            /// <summary>
            /// Start a post request that is immediately completed.
            /// </summary>
            public IPortableWebRequestStatus Post(
                    string url, IDictionary<string, string> headers,
                    IEnumerable<KeyValuePair<string, string>> formFields) {
                var formLines = new List<string>();
                foreach (var kv in formFields) {
                    var value = kv.Value;
                    // Change the random cache buster to 0.
                    if (kv.Key == "z") value = "0";
                    formLines.Add(String.Format("{0}={1}", kv.Key, value));
                }
                PostedUrlAndForms.Add(
                    new KeyValuePair<string, string>(url, String.Join("\n", formLines.ToArray())));
                return new RequestStatus();
            }

            public IPortableWebRequestStatus Get(string url, IDictionary<string, string> headers) {
                throw new NotImplementedException("PortableWebRequest.Get() should not be called");
            }
        }

        private const string GA_TRACKING_ID = "a-test-id";
        private const string PLUGIN_NAME = "my plugin";
        private const string SETTINGS_NAMESPACE = "com.foo.myplugin";
        private const string DATA_COLLECTION_DESCRIPTION = "to improve my plugin";
        private const string PRIVACY_POLICY = "http://a.link.to/a/privacy/policy";

        private ProjectSettings settings = new ProjectSettings("EditorMeasurementTest");
        private List<string> openedUrls = new List<string>();
        private FakePortableWebRequest webRequest;


        /// <summary>
        /// Isolate ProjectSettings from Unity APIs and global state.
        /// </summary>
        [SetUp]
        public void Setup() {
            ProjectSettings.persistenceEnabled = false;
            ProjectSettings.projectSettings = new InMemorySettings();
            ProjectSettings.systemSettings = new InMemorySettings();
            ProjectSettings.logger.Target = LogTarget.Console;
            ProjectSettings.checkoutFile = (filename, logger) => { return true; };
            EditorMeasurement.GloballyEnabled = true;
            EditorMeasurement.unityVersion = "5.6.1f1";
            EditorMeasurement.unityRuntimePlatform = "WindowsEditor";
            webRequest = new FakePortableWebRequest();
            PortableWebRequest.DefaultInstance = webRequest;
            openedUrls.Clear();
        }

        /// <summary>
        /// Create an EditorMeasurement instance.
        /// </summary>
        private EditorMeasurement CreateEditorMeasurement() {
            var analytics = new EditorMeasurement(settings, ProjectSettings.logger, GA_TRACKING_ID,
                                                  SETTINGS_NAMESPACE, PLUGIN_NAME,
                                                  DATA_COLLECTION_DESCRIPTION, PRIVACY_POLICY);
            analytics.displayDialog = (title, message, option0, option1, option2) => {
                throw new Exception("Unexpected dialog displayed");
            };
            analytics.openUrl = (url) => {
                openedUrls.Add(url);
            };
            analytics.ReportUnityVersion = false;
            analytics.ReportUnityPlatform = false;
            return analytics;
        }

        /// <summary>
        /// Concatenate query strings.
        /// </summary>
        [Test]
        void ConcatenateQueryStrings() {
            Assert.That(EditorMeasurement.ConcatenateQueryStrings(null, null), Is.EqualTo(null));
            Assert.That(EditorMeasurement.ConcatenateQueryStrings("foo", null), Is.EqualTo("foo"));
            Assert.That(EditorMeasurement.ConcatenateQueryStrings(null, "foo"), Is.EqualTo("foo"));
            Assert.That(EditorMeasurement.ConcatenateQueryStrings(null, "foo"), Is.EqualTo("foo"));
            Assert.That(EditorMeasurement.ConcatenateQueryStrings("foo=bar", "bish=bosh"),
                        Is.EqualTo("foo=bar&bish=bosh"));
            Assert.That(EditorMeasurement.ConcatenateQueryStrings("?foo=bar&", "bish=bosh"),
                        Is.EqualTo("foo=bar&bish=bosh"));
        }

        /// <summary>
        /// Test construction of an EditorMeasurement instance.
        /// </summary>
        [Test]
        public void Construct() {
            var analytics = CreateEditorMeasurement();
            Assert.That(analytics.PluginName, Is.EqualTo(PLUGIN_NAME));
            Assert.That(analytics.DataCollectionDescription,
                        Is.EqualTo(DATA_COLLECTION_DESCRIPTION));
            Assert.That(analytics.Enabled, Is.EqualTo(true));
            Assert.That(analytics.ConsentRequested, Is.EqualTo(false));
            Assert.That(analytics.Cookie, Is.EqualTo(""));
            Assert.That(analytics.SystemCookie, Is.EqualTo(""));
            Assert.That(openedUrls, Is.EqualTo(new List<string>()));
        }

        /// <summary>
        /// Create a display dialog delegate.
        /// </summary>
        /// <param name="selectedOption">0..2</param>
        /// <returns>Display dialog delegate.</returns>
        private EditorMeasurement.DisplayDialogDelegate CreateDisplayDialogDelegate(
                List<int> selectedOptions) {
            return (string title, string message, string option0, string option1,
                    string option2) => {
                Assert.That(title, Is.Not.Empty);
                Assert.That(message, Is.Not.Empty);
                Assert.That(option0, Is.Not.Empty);
                Assert.That(option1, Is.Not.Empty);
                Assert.That(option2, Is.Not.Empty);
                var selectedOption = selectedOptions[0];
                selectedOptions.RemoveAt(0);
                return selectedOption;
            };
        }

        /// <summary>
        /// Request for consent to enable analytics reporting, which is approved by the user.
        /// </summary>
        [Test]
        public void PromptToEnableYes() {
            var analytics = CreateEditorMeasurement();
            analytics.displayDialog = CreateDisplayDialogDelegate(new List<int> { 0 /* yes */ });
            Assert.That(analytics.Enabled, Is.EqualTo(true));
            Assert.That(analytics.ConsentRequested, Is.EqualTo(false));
            analytics.PromptToEnable();
            Assert.That(analytics.Enabled, Is.EqualTo(true));
            Assert.That(analytics.ConsentRequested, Is.EqualTo(true));
            Assert.That(openedUrls, Is.EqualTo(new List<string>()));
        }

        /// <summary>
        /// Request for consent to enable analytics reporting, which is denied by the user.
        /// </summary>
        [Test]
        public void PromptToEnableNo() {
            var analytics = CreateEditorMeasurement();
            analytics.displayDialog = CreateDisplayDialogDelegate(new List<int> { 1 /* no */ });
            Assert.That(analytics.Enabled, Is.EqualTo(true));
            Assert.That(analytics.ConsentRequested, Is.EqualTo(false));
            analytics.PromptToEnable();
            Assert.That(analytics.Enabled, Is.EqualTo(false));
            Assert.That(analytics.ConsentRequested, Is.EqualTo(true));
            Assert.That(openedUrls, Is.EqualTo(new List<string>()));
        }

        /// <summary>
        /// Request for consent to enable analytics reporting, which results in the user displaying
        /// the privacy policy then disabling analytics.
        /// </summary>
        [Test]
        public void PromptToEnablePrivacyNo() {
            var analytics = CreateEditorMeasurement();
            var selectedOptions = new List<int> {
                2 /* privacy policy */,
                2 /* privacy policy (should be prompted again) */,
                1 /* no */
            };
            analytics.displayDialog = CreateDisplayDialogDelegate(selectedOptions);
            Assert.That(analytics.Enabled, Is.EqualTo(true));
            Assert.That(analytics.ConsentRequested, Is.EqualTo(false));
            analytics.PromptToEnable();
            Assert.That(analytics.Enabled, Is.EqualTo(false));
            Assert.That(analytics.ConsentRequested, Is.EqualTo(true));
            // All options should have been selected as the dialog is displayed again after
            // selecting privacy policy.
            Assert.That(selectedOptions.Count, Is.EqualTo(0));
            Assert.That(openedUrls,
                        Is.EqualTo(new List<string> { PRIVACY_POLICY, PRIVACY_POLICY }));
        }

        /// <summary>
        /// Request for consent to enable analytics reporting then restore default settings which
        /// should revoke consent.
        /// </summary>
        [Test]
        public void RestoreDefaultSettings() {
            var analytics = CreateEditorMeasurement();
            analytics.displayDialog = CreateDisplayDialogDelegate(new List<int> { 1 /* no */ });
            Assert.That(analytics.Enabled, Is.EqualTo(true));
            Assert.That(analytics.ConsentRequested, Is.EqualTo(false));
            analytics.PromptToEnable();
            Assert.That(analytics.Enabled, Is.EqualTo(false));
            Assert.That(analytics.ConsentRequested, Is.EqualTo(true));
            Assert.That(openedUrls, Is.EqualTo(new List<string>()));

            analytics.RestoreDefaultSettings();
            Assert.That(analytics.Enabled, Is.EqualTo(true));
            Assert.That(analytics.ConsentRequested, Is.EqualTo(false));
        }

        /// <summary>
        /// Verify cookies are generated after analytics is enabled.
        /// </summary>
        [Test]
        public void GenerateCookies() {
            var analytics = CreateEditorMeasurement();
            analytics.displayDialog = CreateDisplayDialogDelegate(new List<int> { 0 /* yes */ });
            Assert.That(analytics.Cookie, Is.EqualTo(""));
            Assert.That(analytics.SystemCookie, Is.EqualTo(""));
            analytics.PromptToEnable();
            Assert.That(analytics.Cookie, Is.Not.EqualTo(""));
            Assert.That(analytics.SystemCookie, Is.Not.EqualTo(""));
            Assert.That(openedUrls, Is.EqualTo(new List<string>()));
        }

        /// <summary>
        /// Verify consent is requested when trying reporting an event without consent.
        /// </summary>
        [Test]
        public void ReportWithoutConsent() {
            var analytics = CreateEditorMeasurement();
            var selectedOptions = new List<int> { 1 /* no */ };
            analytics.displayDialog = CreateDisplayDialogDelegate(selectedOptions);
            analytics.Report("/a/new/event", "something interesting");
            analytics.Report("/a/new/event", "something else");
            analytics.Report("/a/new/event",
                             new KeyValuePair<string, string>[] {
                                 new KeyValuePair<string, string>("foo", "bar"),
                                 new KeyValuePair<string, string>("bish", "bosh")
                             }, "something with parameters");
            Assert.That(selectedOptions, Is.EqualTo(new List<int>()));
            Assert.That(analytics.Enabled, Is.EqualTo(false));
            Assert.That(analytics.ConsentRequested, Is.EqualTo(true));
            Assert.That(webRequest.PostedUrlAndForms,
                        Is.EqualTo(new List<KeyValuePair<string, string>>()));
        }

        /// <summary>
        /// Verify nothing is reported if the class is globally disabled.
        /// </summary>
        [Test]
        public void ReportWhenDisabled() {
            EditorMeasurement.GloballyEnabled = false;
            var analytics = CreateEditorMeasurement();
            analytics.Report("/a/new/event", "something interesting");
            analytics.Report("/a/new/event", "something else");
            analytics.Report("/a/new/event",
                             new KeyValuePair<string, string>[] {
                                 new KeyValuePair<string, string>("foo", "bar"),
                                 new KeyValuePair<string, string>("bish", "bosh")
                             }, "something with parameters");
            Assert.That(analytics.Enabled, Is.EqualTo(true));
            Assert.That(analytics.ConsentRequested, Is.EqualTo(false));
            Assert.That(webRequest.PostedUrlAndForms,
                        Is.EqualTo(new List<KeyValuePair<string, string>>()));
        }

        /// <summary>
        /// Create an expected URL and form string for the Google Analytics v1 measurement protocol.
        /// </summary>
        /// <param name="reportUrl">URL being reported.</param>
        /// <param name="reportName">Name / title of the URL.</param>
        /// <param name="cookie">Cookie used for the report.</param>
        private KeyValuePair<string, string> CreateMeasurementEvent(
                string reportUrl, string reportName, string cookie) {
            return new KeyValuePair<string, string>(
                "http://www.google-analytics.com/collect",
                String.Format("v=1\n" +
                              "tid={0}\n" +
                              "cid={1}\n" +
                              "t=pageview\n" +
                              "dl={2}\n" +
                              "dt={3}\n" +
                              "z=0",
                              GA_TRACKING_ID, cookie, reportUrl, reportName));
        }

        /// <summary>
        /// Create a list of expected URL and form strings for the Google Analytics v1 measurement
        /// protocol using the system and project cookies.
        /// </summary>
        /// <param name="analytics">Object to get cookies from.</param>
        /// <param name="reportUrl">URL being reported.</param>
        /// <param name="reportName">Name / title of the URL.</param>
        private KeyValuePair<string, string>[] CreateMeasurementEvents(
                EditorMeasurement analytics, string reportPath, string reportQuery,
                string reportAnchor, string reportName) {
            Func<string, string> createUrl = (string scope) => {
                return reportPath + reportQuery + (String.IsNullOrEmpty(reportQuery) ? "?" : "&") +
                    "scope=" + scope + reportAnchor;
            };
            return new KeyValuePair<string, string>[] {
                CreateMeasurementEvent(createUrl("project"), reportName, analytics.Cookie),
                CreateMeasurementEvent(createUrl("system"), reportName, analytics.SystemCookie),
            };
        }



        /// <summary>
        /// Report an event after obtaining consent.
        /// </summary>
        [Test]
        public void ReportWithConsent() {
            var analytics = CreateEditorMeasurement();
            var selectedOptions = new List<int> { 0 /* yes */ };
            analytics.displayDialog = CreateDisplayDialogDelegate(selectedOptions);
            analytics.Report("/a/new/event", "something interesting");
            analytics.Report("/a/new/event#neat", "something else");
            analytics.Report("/a/new/event?setting=foo", "something interesting");
            analytics.Report("/a/new/event?setting=bar#neat", "something else");
            analytics.Report("/a/new/event",
                             new KeyValuePair<string, string>[] {
                                 new KeyValuePair<string, string>("foo", "bar"),
                                 new KeyValuePair<string, string>("bish", "bosh")
                             }, "something with parameters");
            Assert.That(selectedOptions, Is.EqualTo(new List<int>()));
            var expectedEvents = new List<KeyValuePair<string, string>>();
            expectedEvents.AddRange(CreateMeasurementEvents(analytics, "/a/new/event", "", "",
                                                            "something interesting"));
            expectedEvents.AddRange(CreateMeasurementEvents(analytics, "/a/new/event", "", "#neat",
                                                            "something else"));
            expectedEvents.AddRange(CreateMeasurementEvents(analytics, "/a/new/event",
                                                            "?setting=foo", "",
                                                            "something interesting"));
            expectedEvents.AddRange(CreateMeasurementEvents(analytics, "/a/new/event",
                                                            "?setting=bar", "#neat",
                                                            "something else"));
            expectedEvents.AddRange(CreateMeasurementEvents(analytics,
                                                            "/a/new/event",
                                                            "?foo=bar&bish=bosh", "",
                                                            "something with parameters"));
            Assert.That(webRequest.PostedUrlAndForms, Is.EqualTo(expectedEvents));
        }

        /// <summary>
        /// Report an event after obtaining consent adding a base path, query and report name.
        /// </summary>
        [Test]
        public void ReportWithConsentWithBasePathQueryAndReportName() {
            var analytics = CreateEditorMeasurement();
            analytics.BasePath = "/myplugin";
            analytics.BaseQuery = "version=1.2.3";
            analytics.BaseReportName = "My Plugin: ";
            var selectedOptions = new List<int> { 0 /* yes */ };
            analytics.displayDialog = CreateDisplayDialogDelegate(selectedOptions);
            analytics.Report("/a/new/event", "something interesting");
            Assert.That(webRequest.PostedUrlAndForms,
                        Is.EqualTo(CreateMeasurementEvents(analytics,
                                                           "/myplugin/a/new/event",
                                                           "?version=1.2.3", "",
                                                           "My Plugin: something interesting")));
        }

        /// <summary>
        /// Report an event after obtaining consent adding a base path, query, common query
        /// parameters and report name.
        /// </summary>
        [Test]
        public void ReportWithConsentWithBasePathQueryCommonParamsAndReportName() {
            var analytics = CreateEditorMeasurement();
            analytics.ReportUnityVersion = true;
            analytics.ReportUnityPlatform = true;
            analytics.BasePath = "/myplugin";
            analytics.BaseQuery = "version=1.2.3";
            analytics.BaseReportName = "My Plugin: ";
            var selectedOptions = new List<int> { 0 /* yes */ };
            analytics.displayDialog = CreateDisplayDialogDelegate(selectedOptions);
            analytics.Report("/a/new/event", "something interesting");
            Assert.That(webRequest.PostedUrlAndForms,
                        Is.EqualTo(CreateMeasurementEvents(
                            analytics,
                            "/myplugin/a/new/event",
                            "?unityVersion=5.6.1f1&unityPlatform=WindowsEditor&version=1.2.3", "",
                            "My Plugin: something interesting")));
        }

        /// <summary>
        /// Report an event when opening a URL.
        /// </summary>
        [Test]
        public void OpenUrl() {
            var analytics = CreateEditorMeasurement();
            var selectedOptions = new List<int> { 0 /* yes */ };
            analytics.displayDialog = CreateDisplayDialogDelegate(selectedOptions);
            analytics.OpenUrl("https://github.com/googlesamples/unity-jar-resolver?do=something" +
                              "#version-handler-usage", "Version Handler Usage");
            Assert.That(selectedOptions, Is.EqualTo(new List<int>()));
            Assert.That(openedUrls,
                        Is.EqualTo(new List<string>() {
                                "https://github.com/googlesamples/unity-jar-resolver?do=something" +
                                "#version-handler-usage"
                            }));
            Assert.That(webRequest.PostedUrlAndForms,
                        Is.EqualTo(CreateMeasurementEvents(analytics,
                                                           "/github.com/googlesamples/" +
                                                           "unity-jar-resolver",
                                                           "?do=something",
                                                           "#version-handler-usage",
                                                           "Version Handler Usage")));
        }
    }
}
