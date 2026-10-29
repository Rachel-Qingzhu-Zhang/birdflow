using AvianVoice.Embodiment;
using UnityEngine;

namespace AvianVoice.Core
{
    /// <summary>
    /// Scene hygiene: prefer XR Origin Hands, disable duplicate rigs, single AudioListener.
    /// </summary>
    public static class AvianSceneBootstrapHelper
    {
        public static void Apply() => ApplyEarly();

        /// <summary>
        /// Call from Awake (AvianEarlyXrFix) before interactor OnEnable, or after scene load for audio/camera cleanup.
        /// </summary>
        public static void ApplyEarly()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (!scene.StartsWith("Avian") && !scene.StartsWith("Task_"))
                return;

            var origins = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Transform handsOrigin = null;
            Transform fallbackOrigin = null;
            foreach (var t in origins)
            {
                if (!t.name.Contains("XR Origin"))
                    continue;
                if (t.name.Contains("Hands"))
                    handsOrigin = t;
                else if (fallbackOrigin == null)
                    fallbackOrigin = t;
            }

            var active = handsOrigin != null ? handsOrigin : fallbackOrigin;
            if (active == null)
            {
                Debug.LogWarning("[AvianVoice] No XR Origin in scene. Add XR Origin Hands (XR Rig) from XRI Hands Demo samples.");
                return;
            }

            foreach (var t in origins)
            {
                if (!t.name.Contains("XR Origin") || t == active)
                    continue;
                Debug.LogWarning("[AvianVoice] Disabling duplicate XR rig: " + t.name);
                t.gameObject.SetActive(false);
            }

            ReparentBirdUnderHeadAnchor(active);
            EnsureHearing();
            DisableNonXrCameras(active);
        }

        /// <summary>
        /// One listener on the camera the player actually sees. Reflection cameras
        /// created with a Camera component also receive a listener, and two listeners
        /// silence the whole scene on Windows.
        /// </summary>
        public static void EnsureHearing()
        {
            var viewer = ResolveViewerCamera();
            if (viewer == null)
                return;

            var keep = viewer.GetComponent<AudioListener>();
            if (keep == null)
                keep = viewer.gameObject.AddComponent<AudioListener>();
            keep.enabled = true;

            var listeners = Object.FindObjectsByType<AudioListener>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < listeners.Length; i++)
            {
                var listener = listeners[i];
                if (listener == null || listener == keep)
                    continue;
                Object.Destroy(listener);
            }

            AudioListener.volume = 1f;
            AudioListener.pause = false;
        }

        static Camera ResolveViewerCamera()
        {
            var main = Camera.main;
            if (main != null && main.enabled && main.gameObject.activeInHierarchy && main.targetTexture == null)
                return main;

            Camera best = null;
            var bestDepth = float.NegativeInfinity;
            var cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            for (var i = 0; i < cameras.Length; i++)
            {
                var cam = cameras[i];
                if (cam == null || !cam.enabled || !cam.gameObject.activeInHierarchy || cam.targetTexture != null)
                    continue;
                if (cam.depth < bestDepth)
                    continue;
                bestDepth = cam.depth;
                best = cam;
            }

            return best;
        }

        static void ReparentBirdUnderHeadAnchor(Transform activeOrigin)
        {
            var head = activeOrigin.Find("Camera Offset");
            if (head == null)
            {
                foreach (var t in activeOrigin.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name != "Camera Offset")
                        continue;
                    head = t;
                    break;
                }
            }
            if (head == null)
                head = activeOrigin;

            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!ShouldReparentToHead(t))
                    continue;
                if (t.IsChildOf(activeOrigin))
                    continue;

                t.SetParent(head, true);
            }
        }

        static bool ShouldReparentToHead(Transform birdRoot)
        {
            if (birdRoot == null)
                return false;

            if (birdRoot.GetComponent<WorldPlacedBird>() != null)
                return false;

            // Parrot in the scene root is treated as a world prop unless explicitly parented under XR.
            if (birdRoot.name == "Parrot")
                return false;

            return birdRoot.name == "Crow" || birdRoot.name.Contains("AvianBody");
        }

        static void DisableNonXrCameras(Transform xrOrigin)
        {
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                if (cam.transform.IsChildOf(xrOrigin))
                    continue;
                if (cam.CompareTag("MainCamera"))
                    cam.enabled = false;
            }
        }

    }
}
