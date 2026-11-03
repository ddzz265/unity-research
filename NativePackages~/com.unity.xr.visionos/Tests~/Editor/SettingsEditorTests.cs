using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnityEditor.XR.VisionOSTests
{
    class SettingsEditorTests
    {
        readonly Type k_ProjectSettingsWindowType = Type.GetType("UnityEditor.ProjectSettingsWindow, UnityEditor");

        [UnityTest]
        public IEnumerator OpenSettingsEditorWithoutErrors()
        {
            SettingsService.OpenProjectSettings("Project/XR Plug-in Management/Apple visionOS");
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            Assert.NotNull(k_ProjectSettingsWindowType);
            var projectSettingsWindow = EditorWindow.GetWindow(k_ProjectSettingsWindowType);
            if (projectSettingsWindow != null)
                projectSettingsWindow.Close();
        }
    }
}
