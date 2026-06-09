using UnityEngine;

namespace AvianVoice.Embodiment
{
    /// <summary>
    /// Fixed Avian_Lab layout: mirror in front of XR Origin, parrots + perch behind the headset.
    /// </summary>
    public static class AvianLabSceneLayout
    {
        public const float EyeHeightMeters = 1.45f;
        public const float MirrorAheadMeters = 1.85f;
        public const float MirrorPanelHeightMeters = 1.45f;
        public const float ParrotsBehindMeters = 1.65f;
        public const float ParrotHalfSeparationMeters = 0.85f;
        public const float PerchGroundHeightMeters = 0.546f;
        public const float ParrotStandHeightMeters = 0.37f;

        public static bool TryResolveXrOrigin(out Transform xrOrigin, out Vector3 viewForward)
        {
            xrOrigin = null;
            viewForward = Vector3.forward;

            var xrObject = GameObject.Find("XR Origin Hands (XR Rig)") ?? GameObject.Find("XR Origin");
            if (xrObject == null)
                return false;

            xrOrigin = xrObject.transform;
            viewForward = xrOrigin.forward;
            viewForward.y = 0f;
            if (viewForward.sqrMagnitude < 0.01f)
                viewForward = Vector3.forward;
            viewForward.Normalize();
            return true;
        }

        public static Vector3 ViewerEyePoint(Transform xrOrigin)
        {
            return xrOrigin.position + Vector3.up * EyeHeightMeters;
        }

        public static Vector3 ResolveMirrorPosition(Transform xrOrigin, Vector3 viewForward)
        {
            return ResolveMirrorPositionNearBirds(
                xrOrigin,
                viewForward,
                MirrorAheadMeters,
                MirrorPanelHeightMeters);
        }

        /// <summary>
        /// Places the mirror between the perch birds and the viewer (closer to birds than XR-only offset).
        /// </summary>
        public static Vector3 ResolveMirrorPositionNearBirds(
            Transform xrOrigin,
            Vector3 viewForward,
            float aheadMeters,
            float panelHeightMeters)
        {
            var viewerEye = ViewerEyePoint(xrOrigin);
            var perch = GameObject.Find(BirdSceneRoles.PerchName);
            var standCenter = perch != null
                ? ResolveParrotStandCenter(perch.transform.position)
                : xrOrigin.position - viewForward * ParrotsBehindMeters
                  + Vector3.up * (PerchGroundHeightMeters + ParrotStandHeightMeters);

            var toViewer = viewerEye - standCenter;
            toViewer.y = 0f;
            if (toViewer.sqrMagnitude < 0.01f)
                toViewer = viewForward;
            toViewer.Normalize();

            var birdToViewerDistance = Vector3.Distance(
                new Vector3(standCenter.x, 0f, standCenter.z),
                new Vector3(viewerEye.x, 0f, viewerEye.z));
            var forwardMeters = Mathf.Clamp(aheadMeters, 0.6f, birdToViewerDistance - 0.25f);
            var fromBirds = Mathf.Clamp(forwardMeters * 0.55f, 0.75f, birdToViewerDistance - 0.2f);

            var mirrorPos = standCenter + toViewer * fromBirds;
            mirrorPos.y = viewerEye.y;
            return mirrorPos;
        }

        public static Quaternion ResolveMirrorRotation(Vector3 mirrorPosition, Vector3 viewerEye)
        {
            var faceViewer = viewerEye - mirrorPosition;
            faceViewer.y = 0f;
            if (faceViewer.sqrMagnitude < 0.01f)
                faceViewer = Vector3.back;
            return Quaternion.LookRotation(faceViewer.normalized, Vector3.up);
        }

        public static Vector3 ResolvePerchPosition(Transform xrOrigin, Vector3 viewForward)
        {
            return xrOrigin.position
                   - viewForward * ParrotsBehindMeters
                   + Vector3.up * PerchGroundHeightMeters;
        }

        public static Vector3 ResolveParrotStandCenter(Vector3 perchPosition)
        {
            return perchPosition + Vector3.up * ParrotStandHeightMeters;
        }

        public static void ResolveParrotPositions(
            Vector3 standCenter,
            Vector3 viewForward,
            out Vector3 friendPosition,
            out Vector3 playerPosition)
        {
            var right = Vector3.Cross(Vector3.up, viewForward);
            if (right.sqrMagnitude < 0.01f)
                right = Vector3.right;
            right.Normalize();

            friendPosition = standCenter - right * ParrotHalfSeparationMeters;
            playerPosition = standCenter + right * ParrotHalfSeparationMeters;
        }

        public static Quaternion ResolveParrotFacingMirror(Vector3 viewForward)
        {
            return Quaternion.LookRotation(viewForward, Vector3.up);
        }

        /// <summary>
        /// Forces perch + parrots behind XR Origin on every play (prevents runtime pull in front of camera).
        /// </summary>
        public static void ApplyRuntimeLayout()
        {
            if (WorldMirrorScenePlacement.HasManualLayoutInScene())
            {
                Debug.Log("[AvianVoice] Manual scene layout detected — keeping mirror, perch, and parrot transforms from the scene.");
                return;
            }

            if (!TryResolveXrOrigin(out var xrOrigin, out var viewForward))
            {
                Debug.LogWarning("[AvianVoice] AvianLabSceneLayout: XR Origin not found.");
                return;
            }

            var perch = GameObject.Find(BirdSceneRoles.PerchName);
            if (perch != null)
                perch.transform.position = ResolvePerchPosition(xrOrigin, viewForward);

            var standCenter = perch != null
                ? ResolveParrotStandCenter(perch.transform.position)
                : xrOrigin.position - viewForward * ParrotsBehindMeters + Vector3.up * (PerchGroundHeightMeters + ParrotStandHeightMeters);

            ResolveParrotPositions(standCenter, viewForward, out var friendPos, out var playerPos);
            var faceMirror = ResolveParrotFacingMirror(viewForward);

            ApplyBirdPose(BirdSceneRoles.FriendName, friendPos, faceMirror);
            ApplyBirdPose(BirdSceneRoles.PlayerName, playerPos, faceMirror);
        }

        static void ApplyBirdPose(string birdName, Vector3 position, Quaternion rotation)
        {
            var bird = GameObject.Find(birdName);
            if (bird == null)
                return;

            bird.transform.SetPositionAndRotation(position, rotation);

            var guard = bird.GetComponent<WorldPlacedBird>();
            if (guard == null)
                guard = bird.AddComponent<WorldPlacedBird>();
            guard.ConfigureForFixedWorldPlacement();
            guard.CommitWorldPose();
        }
    }
}
