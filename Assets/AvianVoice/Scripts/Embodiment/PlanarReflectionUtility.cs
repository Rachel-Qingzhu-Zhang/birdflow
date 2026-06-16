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

        public static void SyncReflectionCameraFromViewer(Camera reflectionCam, Camera viewerCam = null)
        {
            viewerCam ??= WorldPlacedBird.FindMainCameraTransform()?.GetComponent<Camera>();
            if (viewerCam == null)
            {
                reflectionCam.fieldOfView = 60f;
                reflectionCam.aspect = 1.777f;
                return;
            }

            reflectionCam.fieldOfView = viewerCam.fieldOfView;
            reflectionCam.aspect = viewerCam.aspect;
        }

        /// <summary>
        /// Fits reflection-camera FOV and aspect so the mirror quad corners lie inside the frustum
        /// (in the already-configured reflected view matrix).
        /// </summary>
        public static void FitReflectionCameraToMirrorSurface(Camera reflectionCam, Transform mirrorQuad, float margin = 1.03f)
        {
            if (reflectionCam == null || mirrorQuad == null)
                return;

            var maxX = 0f;
            var maxY = 0f;
            var viewMatrix = reflectionCam.worldToCameraMatrix;

            foreach (var corner in EnumerateMirrorQuadCorners(mirrorQuad))
            {
                var viewSpace = viewMatrix.MultiplyPoint(corner);
                var depth = -viewSpace.z;
                if (depth <= 0.01f)
                    continue;

                maxX = Mathf.Max(maxX, Mathf.Abs(viewSpace.x / depth));
                maxY = Mathf.Max(maxY, Mathf.Abs(viewSpace.y / depth));
            }

            if (maxY < 0.0001f)
                return;

            reflectionCam.fieldOfView = Mathf.Clamp(Mathf.Atan(maxY) * 2f * Mathf.Rad2Deg * margin, 1f, 170f);
            reflectionCam.aspect = Mathf.Clamp(maxX / maxY, 0.05f, 10f);
        }

        static System.Collections.Generic.IEnumerable<Vector3> EnumerateMirrorQuadCorners(Transform quad)
        {
            var center = quad.position;
            var right = quad.right * (quad.lossyScale.x * 0.5f);
            var up = quad.up * (quad.lossyScale.y * 0.5f);
            yield return center - right - up;
            yield return center + right - up;
            yield return center + right + up;
            yield return center - right + up;
        }

        public static Matrix4x4 BuildReflectionMatrix(Vector3 planeNormal, Vector3 planePos, float planeOffset = 0f)
        {
            var offsetPos = planePos + planeNormal * planeOffset;
            var plane = new Vector4(
                planeNormal.x,
                planeNormal.y,
                planeNormal.z,
                -Vector3.Dot(planeNormal, offsetPos));
            return BuildReflectionMatrixFromPlane(plane);
        }

        /// <summary>
        /// Standard planar mirror: reflect the viewer camera view matrix across the mirror plane.
        /// </summary>
        public static void ApplyMirroredViewerMatrix(
            Camera reflectionCam,
            Camera viewerCam,
            Vector3 planePos,
            Vector3 planeNormal,
            float planeOffset = 0.07f)
        {
            reflectionCam.ResetWorldToCameraMatrix();
            var reflectionMatrix = BuildReflectionMatrix(planeNormal, planePos, planeOffset);
            reflectionCam.worldToCameraMatrix = viewerCam.worldToCameraMatrix * reflectionMatrix;
        }

        static Matrix4x4 BuildReflectionMatrixFromPlane(Vector4 plane)
        {
            var m = Matrix4x4.identity;
            m.m00 = 1f - 2f * plane.x * plane.x;
            m.m01 = -2f * plane.x * plane.y;
            m.m02 = -2f * plane.x * plane.z;
            m.m03 = -2f * plane.w * plane.x;

            m.m10 = -2f * plane.y * plane.x;
            m.m11 = 1f - 2f * plane.y * plane.y;
            m.m12 = -2f * plane.y * plane.z;
            m.m13 = -2f * plane.w * plane.y;

            m.m20 = -2f * plane.z * plane.x;
            m.m21 = -2f * plane.z * plane.y;
            m.m22 = 1f - 2f * plane.z * plane.z;
            m.m23 = -2f * plane.w * plane.z;
            return m;
        }

        public static Matrix4x4 FinalizeReflectionProjection(
            Camera reflectionCam,
            Vector3 planePos,
            Vector3 planeNormal,
            float planeOffset = 0.07f,
            bool useObliqueClip = false,
            float obliqueSideSign = 1f)
        {
            reflectionCam.ResetProjectionMatrix();

            if (useObliqueClip)
            {
                var clipPlane = CameraSpaceClipPlane(reflectionCam, planePos, planeNormal, obliqueSideSign, planeOffset);
                reflectionCam.projectionMatrix = CalculateObliqueMatrix(reflectionCam.projectionMatrix, clipPlane);
            }

            return GetGpuViewProjectionMatrix(reflectionCam);
        }

        public static Matrix4x4 PrepareReflectionCameraMatrices(
            Camera reflectionCam,
            Vector3 planePos,
            Vector3 planeNormal,
            Transform mirrorSurface = null,
            float planeOffset = 0.07f,
            bool useObliqueClip = false,
            float obliqueSideSign = 1f)
        {
            if (mirrorSurface != null)
                FitReflectionCameraToMirrorSurface(reflectionCam, mirrorSurface);
            else
                SyncReflectionCameraFromViewer(reflectionCam);

            return FinalizeReflectionProjection(
                reflectionCam,
                planePos,
                planeNormal,
                planeOffset,
                useObliqueClip,
                obliqueSideSign);
        }

        public static void ResizeReflectionRenderTexture(
            RenderTexture renderTexture,
            Camera reflectionCam,
            int longEdgePixels,
            int antiAliasing)
        {
            if (renderTexture == null || reflectionCam == null)
                return;

            longEdgePixels = Mathf.Clamp(longEdgePixels, 512, 8192);
            antiAliasing = NormalizeMsaa(antiAliasing);
            var aspect = Mathf.Max(reflectionCam.aspect, 0.01f);

            var width = longEdgePixels;
            var height = Mathf.Max(1, Mathf.RoundToInt(longEdgePixels / aspect));

            if (renderTexture.width == width
                && renderTexture.height == height
                && renderTexture.antiAliasing == antiAliasing)
                return;

            renderTexture.Release();
            renderTexture.width = width;
            renderTexture.height = height;
            renderTexture.antiAliasing = antiAliasing;
            renderTexture.Create();
        }

        public static Matrix4x4 GetGpuViewProjectionMatrix(Camera cam)
        {
            var gpuProjection = GL.GetGPUProjectionMatrix(cam.projectionMatrix, true);
            return gpuProjection * cam.worldToCameraMatrix;
        }

        static Vector4 CameraSpaceClipPlane(
            Camera cam,
            Vector3 planePos,
            Vector3 planeNormal,
            float sideSign,
            float planeOffset)
        {
            var offsetPos = planePos + planeNormal * planeOffset;
            var view = cam.worldToCameraMatrix;
            var cameraPos = view.MultiplyPoint(offsetPos);
            var cameraNormal = view.MultiplyVector(planeNormal).normalized * sideSign;
            return new Vector4(cameraNormal.x, cameraNormal.y, cameraNormal.z,
                -Vector3.Dot(cameraPos, cameraNormal));
        }

        static Matrix4x4 CalculateObliqueMatrix(Matrix4x4 projection, Vector4 clipPlane)
        {
            var q = projection.inverse * new Vector4(
                SignOrOne(clipPlane.x),
                SignOrOne(clipPlane.y),
                1f,
                1f);
            var c = clipPlane * (2f / Vector4.Dot(clipPlane, q));
            projection[2] = c.x - projection[3];
            projection[6] = c.y - projection[7];
            projection[10] = c.z - projection[11];
            projection[14] = c.w - projection[15];
            return projection;
        }

        static float SignOrOne(float value) => value >= 0f ? 1f : -1f;

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

        public static RenderTexture CreateReflectionRenderTexture(
            int longEdgePixels,
            bool matchViewerAspect = true,
            int antiAliasing = 4)
        {
            longEdgePixels = Mathf.Clamp(longEdgePixels, 512, 8192);
            antiAliasing = NormalizeMsaa(antiAliasing);

            var aspect = 1f;
            if (matchViewerAspect)
            {
                var viewerCam = WorldPlacedBird.FindMainCameraTransform()?.GetComponent<Camera>();
                if (viewerCam != null)
                    aspect = Mathf.Max(viewerCam.aspect, 0.01f);
            }

            int width;
            int height;
            if (aspect >= 1f)
            {
                width = longEdgePixels;
                height = Mathf.Max(1, Mathf.RoundToInt(longEdgePixels / aspect));
            }
            else
            {
                height = longEdgePixels;
                width = Mathf.Max(1, Mathf.RoundToInt(longEdgePixels * aspect));
            }

            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "MirrorReflectionRT",
                antiAliasing = antiAliasing,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false,
                anisoLevel = 1
            };
            return rt;
        }

        static int NormalizeMsaa(int samples)
        {
            if (samples <= 1)
                return 1;
            if (samples <= 2)
                return 2;
            if (samples <= 4)
                return 4;
            return 8;
        }
    }
}
