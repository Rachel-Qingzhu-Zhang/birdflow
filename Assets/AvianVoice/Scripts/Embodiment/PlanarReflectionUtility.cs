using UnityEngine;

namespace AvianVoice.Embodiment
{
    /// <summary>
    /// Shared planar reflection math and camera setup for mirror and pond water.
    /// </summary>
    public static class PlanarReflectionUtility
    {
        public static Vector3 ReflectPointAcrossPlane(Vector3 point, Vector3 planePos, Vector3 planeNormal)
        {
            var delta = point - planePos;
            return point - 2f * Vector3.Dot(delta, planeNormal) * planeNormal;
        }

        public static Vector3 ReflectDirection(Vector3 direction, Vector3 planeNormal)
        {
            return direction - 2f * Vector3.Dot(direction, planeNormal) * planeNormal;
        }

        public static void ConfigureReflectionCamera(
            Camera cam,
            RenderTexture target,
            LayerMask cullingMask,
            float clipDistance)
        {
            cam.targetTexture = target;
            cam.cullingMask = cullingMask;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = clipDistance;
            cam.depth = -10;
            cam.allowHDR = true;
            cam.clearFlags = CameraClearFlags.Skybox;
            SyncReflectionCameraFromViewer(cam);
        }

        public static void SyncReflectionCameraFromViewer(Camera reflectionCam)
        {
            var viewerCam = WorldPlacedBird.FindMainCameraTransform()?.GetComponent<Camera>();
            if (viewerCam == null)
            {
                reflectionCam.fieldOfView = 60f;
                return;
            }

            reflectionCam.fieldOfView = viewerCam.fieldOfView;
            reflectionCam.aspect = viewerCam.aspect;
        }

        public static void UpdateReflectionCameraPose(
            Camera reflectionCam,
            Transform viewerTransform,
            Vector3 planePos,
            Vector3 planeNormal)
        {
            if (viewerTransform == null || reflectionCam == null)
                return;

            var reflectedPos = ReflectPointAcrossPlane(viewerTransform.position, planePos, planeNormal);
            var reflectedForward = ReflectDirection(viewerTransform.forward, planeNormal);
            var reflectedUp = ReflectDirection(viewerTransform.up, planeNormal);

            reflectionCam.transform.SetPositionAndRotation(
                reflectedPos,
                Quaternion.LookRotation(reflectedForward, reflectedUp));
            SyncReflectionCameraFromViewer(reflectionCam);
        }

        public static Vector3 ResolvePlaneNormalTowardViewer(Transform planeSurface, Transform viewer)
        {
            var planeNormal = planeSurface.up;
            if (viewer == null)
                return planeNormal;

            var toViewer = viewer.position - planeSurface.position;
            if (Vector3.Dot(planeNormal, toViewer) < 0f)
                planeNormal = -planeNormal;
            return planeNormal;
        }

        public static LayerMask BuildDefaultReflectionMask()
        {
            var excluded = (1 << BirdSceneRoles.MirrorDisplayLayer) | (1 << BirdSceneRoles.UiLayer);
            return ~(excluded);
        }

        public static void EnsureBirdRenderersVisible()
        {
            foreach (var bird in new[] { BirdSceneRoles.Friend, BirdSceneRoles.Player })
            {
                if (bird == null)
                    continue;

                foreach (var renderer in bird.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer is SkinnedMeshRenderer skinned)
                        skinned.updateWhenOffscreen = true;
                }
            }
        }
    }
}
