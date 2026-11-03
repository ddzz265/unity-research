using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.XR.Management;
using UnityEditor.XR.VisionOS;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnityEditor.XR.VisionOSTests
{
    [TestFixtureSource(typeof(TestSource))]
    class ProjectValidationTests
    {
        class TestSource : IEnumerable
        {
            public IEnumerator GetEnumerator()
            {
                foreach (var rule in VisionOSProjectValidation.GetAllRules())
                {
                    yield return new object[] {rule.Name, rule};
                }
            }
        }

        static readonly MethodInfo k_XRGeneralSettingsPerBuildTargetGetOrCreate = typeof(XRGeneralSettingsPerBuildTarget).GetMethod("GetOrCreate", BindingFlags.Static | BindingFlags.NonPublic);
        static readonly MethodInfo k_XRPackageInitializationBootstrapBeginPackageInitialization =
            Type.GetType("UnityEditor.XR.Management.XRPackageInitializationBootstrap, Unity.XR.Management.Editor")?
                .GetMethod("BeginPackageInitialization",
                    BindingFlags.NonPublic | BindingFlags.Static,
                    null,
                    new Type[] { },
                    null);

        readonly string m_RuleName;
        readonly VisionOSProjectValidation.RuleTestContainer m_TestContainer;

        /// <summary>
        /// Prime the settings assets or else we fail to find it when trying to enable the loader in CI batch mode runs
        /// </summary>
        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            // Initialize VisionOSSettings if it is not available
            var settings = VisionOSSettings.currentSettings;
            if (settings == null)
            {
                settings = VisionOSSettings.GetOrCreateSettings();
                AssetDatabase.CreateAsset(settings, "Assets/XR/Settings/VisionOSSettings.asset");
                AssetDatabase.SaveAssets();
                VisionOSSettings.currentSettings = settings;
            }

            Assert.IsNotNull(VisionOSSettings.currentSettings);

            // Check if General XR Settings for visionOS has been set up
            var visionOSXRSettings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(
                BuildPipeline.GetBuildTargetGroup(BuildTarget.VisionOS));

            // We can skip the reflection and asset initialization hacks if this settings object already exists
            if (visionOSXRSettings != null)
                return;

            Assert.IsNotNull(k_XRPackageInitializationBootstrapBeginPackageInitialization);
            k_XRPackageInitializationBootstrapBeginPackageInitialization.Invoke(null, null);

            Assert.IsNotNull(k_XRGeneralSettingsPerBuildTargetGetOrCreate);
            var generalSettings = k_XRGeneralSettingsPerBuildTargetGetOrCreate.Invoke(null, null) as XRGeneralSettingsPerBuildTarget;
            Assert.IsNotNull(generalSettings);
            generalSettings.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.VisionOS);
        }

        public ProjectValidationTests(string ruleName, VisionOSProjectValidation.RuleTestContainer testContainer)
        {
            m_RuleName = ruleName;
            m_TestContainer = testContainer;
        }

        // Using StepN_ method name prefix to enforce test order, because OrderAttribute doesn't work
        [UnityTest]
        public IEnumerator Step0_SetUpTest()
        {
            SkipTestIfRequested();

            // Wait a frame to allow things like URP to initialize
            // Specifically, the HDR Post Processing Tone Mapping test fails because the global volume profile is null if we don't wait a frame.
            // This is called inside its own test because we need to yield, we only want this to happen once per rule, and there is no [UnityOneTimeSetUp]
            yield return null;

            var message = m_TestContainer.SetUp?.Invoke();
            if (message != null)
                Assert.Fail(message);
        }

        [Test]
        public void Step1_ValidateRuleMetadata()
        {
            Assert.False(string.IsNullOrEmpty(m_RuleName), "RuleTestContainer.Name must be a non-null non-empty string.");

            var rule = m_TestContainer.Rule;
            Assert.NotNull(rule, "RuleTestContainer.Rule is null.");
            Assert.False(string.IsNullOrEmpty(rule.Category), "BuildValidationRule.Category must be a non-null non-empty string.");
            Assert.False(string.IsNullOrEmpty(rule.Message), "BuildValidationRule.Message must be a non-null non-empty string.");
        }

        [Test]
        public void Step2_EnsureRuleIsEnabled()
        {
            SkipTestIfRequested();
            Assert.IsTrue(m_TestContainer.Rule.IsRuleEnabled(), "Rule should be enabled in SetUp, but IsRuleEnabled returned false.");
        }

        [Test]
        public void Step3_EnsureCheckPredicateNotNull()
        {
            Assert.NotNull(m_TestContainer.Rule.CheckPredicate, $"Validation rules require a CheckPredicate method. {m_RuleName} must provide a CheckPredicate method.");
        }

        [Test]
        public void Step4_EnsureCheckPredicateFails()
        {
            SkipTestIfRequested();
            Assert.IsFalse(m_TestContainer.Rule.CheckPredicate(), "CheckPredicate should fail immediately after SetUp. The rule should be triggered at this phase but " +
                "CheckPredicate returned false.");
        }

        [Test]
        public void Step5_ExecuteFixItMethod()
        {
            SkipTestIfRequested();

            var rule = m_TestContainer.Rule;
            if (rule.FixItAutomatic)
            {
                Assert.NotNull(rule.FixIt, "Validation rules require a FixIt method when FixItAutomatic is true. " +
                    $"{m_RuleName} must provide a FixIt method or set FixItAutomatic to false.");

                rule.FixIt();
            }
            else
            {
                Assert.NotNull(m_TestContainer.TestFixIt, "TestContainer.TestFixIt is null. " +
                    "The test container must provide a TestFixIt method when FixItAutomatic is true or return true in SkipTest.");

                var message = m_TestContainer.TestFixIt();
                if (message != null)
                    Assert.Fail(message);
            }
        }

        [Test]
        public void Step6_EnsureCheckPredicateSucceedsAfterFixIt()
        {
            SkipTestIfRequested();
            Assert.IsTrue(m_TestContainer.Rule.CheckPredicate(), "CheckPredicate is still false after applying fix. FixIt or TestFixIt should cause the rule to not trigger.");
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            var message = m_TestContainer.TearDown?.Invoke();
            if (message != null)
                Assert.Fail(message);
        }

        void SkipTestIfRequested()
        {
            var message = m_TestContainer.SkipTest?.Invoke();
            if (message != null)
                Assert.Ignore(message); // Assert.Ignore will abort the callstack
        }
    }
}
