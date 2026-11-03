using System;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnityEngine.XR.VisionOSTests
{
    class RuntimeTests
    {
        [UnityTest]
        public IEnumerator SimpleTest()
        {
            // TODO: LXR-4117 Temporarily include a simple test that just passes for smoke test in playmode and players
            // HACK: It appears that we can fail CI runs if the player exits too quickly? Previous tests took up enough time to avoid this, but we need to
            // make sure we wait at least a little bit if no other tests are enabled.
            yield return new WaitForSeconds(5);
        }
    }
}
