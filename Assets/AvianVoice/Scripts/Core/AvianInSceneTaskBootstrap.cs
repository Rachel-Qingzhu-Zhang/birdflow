using AvianVoice.Audio;
using AvianVoice.Data;
using AvianVoice.Embodiment;
using AvianVoice.Tasks;
using AvianVoice.UI;
using UnityEngine;

namespace AvianVoice.Core
{
    /// <summary>
    /// Wires six Birdy tasks in Avian_Lab for in-scene switching (keys 1-6).
    /// </summary>
    public static class AvianInSceneTaskBootstrap
    {
        public static void EnsureForLabScene()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (!scene.StartsWith("Avian"))
                return;

            var xrOrigin = FindXrOrigin();
            if (xrOrigin == null)
            {
                Debug.LogWarning("[AvianVoice] In-scene tasks need XR Origin Hands in the scene.");
                return;
            }

            SessionScoreCache.Reset();
            BirdSceneRoles.ResetSession();
            AvianLabSceneLayout.ApplyRuntimeLayout();
            AvianHudLayerSetup.ApplyToXrRig();
            ApplyHudVisibility();
            BirdSceneRoles.EnsureSetup();
            SuppressAlternateBirdBodies();
            RoleMarker.EnsureMarkersExist();
            RoleMarker.SetMarkersForTask("meet");
            // LabBirdSceneLighting deferred: its point lights / MPBs were leaving the Fragilem17 mirror black.

            var taskRoot = GameObject.Find("TaskRoot") ?? new GameObject("TaskRoot");
            if (taskRoot.GetComponent<BirdSceneRolesLayout>() == null)
                taskRoot.AddComponent<BirdSceneRolesLayout>();
            if (BirdSceneRoles.Friend != null)
            {
                taskRoot.transform.position = BirdSceneRoles.Friend.position;
                taskRoot.transform.rotation = BirdSceneRoles.Friend.rotation;
            }

            var demonstrator = taskRoot.GetComponent<BirdFriendDemonstrator>() ?? taskRoot.AddComponent<BirdFriendDemonstrator>();
            var successChirp = taskRoot.GetComponent<SuccessChirpBurst>() ?? taskRoot.AddComponent<SuccessChirpBurst>();
            if (taskRoot.GetComponent<BirdExperienceAudio>() == null)
                taskRoot.AddComponent<BirdExperienceAudio>();

            var meet = taskRoot.GetComponent<MeetBirdTask>() ?? taskRoot.AddComponent<MeetBirdTask>();
            var breathSync = taskRoot.GetComponent<BreathSyncTask>() ?? taskRoot.AddComponent<BreathSyncTask>();
            var singing = taskRoot.GetComponent<SingingBranchTask>() ?? taskRoot.AddComponent<SingingBranchTask>();
            var callFriends = taskRoot.GetComponent<CallFriendsTask>() ?? taskRoot.AddComponent<CallFriendsTask>();
            var reflection = taskRoot.GetComponent<ReflectionTask>() ?? taskRoot.AddComponent<ReflectionTask>();

            meet.enabled = false;
            breathSync.enabled = false;
            singing.enabled = false;
            callFriends.enabled = false;
            reflection.enabled = false;

            var flight = xrOrigin.GetComponent<FlightControlTask>() ?? xrOrigin.AddComponent<FlightControlTask>();
            EnsureCharacterController(xrOrigin);
            if (xrOrigin.GetComponent<AvianXrThumbstickLocomotion>() == null)
                xrOrigin.AddComponent<AvianXrThumbstickLocomotion>();
            flight.enabled = false;

            EnsureMirror(xrOrigin);

            var manager = Object.FindAnyObjectByType<TaskManager>();
            if (manager == null)
            {
                var go = new GameObject("TaskManager");
                manager = go.AddComponent<TaskManager>();
            }

            manager.ConfigureBirdyTasks(
                TaskManager.TaskSwitchMode.InScene,
                meet,
                breathSync,
                flight,
                singing,
                callFriends,
                reflection);

            Debug.Log("[AvianVoice] Birdy 6-task mode ready. Keys 1-6 switch tasks, Space=breath, C=cheat.");
        }

        static GameObject FindXrOrigin()
        {
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name.Contains("XR Origin") && root.name.Contains("Hands"))
                    return root;
            }

            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name.Contains("XR Origin"))
                    return root;
            }

            return null;
        }

        static void EnsureMirror(GameObject xrOrigin)
        {
            AvianVoiceSettings settings = null;
            foreach (var asset in Resources.FindObjectsOfTypeAll<AvianVoiceSettings>())
            {
                if (asset != null && asset.name.Contains("AvianVoiceSettings"))
                {
                    settings = asset;
                    break;
                }
            }

            if (settings == null)
                settings = ScriptableObject.CreateInstance<AvianVoiceSettings>();

            var mirror = xrOrigin.GetComponent<MirrorBodySetup>() ?? xrOrigin.AddComponent<MirrorBodySetup>();
            mirror.Initialize(settings);

            if (BirdSceneRoles.Player != null)
                mirror.SetBirdBodyRoot(BirdSceneRoles.Player);

            // Meet starts with the world mirror live.
            mirror.SetMirrorActive(true);
        }

        static void EnsureCharacterController(GameObject xrOrigin)
        {
            if (xrOrigin.GetComponent<CharacterController>() != null)
                return;

            var cc = xrOrigin.AddComponent<CharacterController>();
            cc.center = new Vector3(0f, 1f, 0f);
            cc.height = 1.8f;
            cc.radius = 0.3f;
            cc.slopeLimit = 45f;
            cc.stepOffset = 0.3f;
        }

        static void ApplyHudVisibility()
        {
            AvianVoiceSettings settings = null;
            foreach (var asset in Resources.FindObjectsOfTypeAll<AvianVoiceSettings>())
            {
                if (asset != null && asset.name.Contains("AvianVoiceSettings"))
                {
                    settings = asset;
                    break;
                }
            }

            var show = settings == null || settings.showHudPanels;
            AvianHudVisibility.SetAllVisible(show);
        }

        /// <summary>
        /// Keep Lab embodiment on FriendParrot/PlayerParrot: hide pigeon FPV and cube placeholders.
        /// </summary>
        static void SuppressAlternateBirdBodies()
        {
            if (BirdSceneRoles.Friend == null || BirdSceneRoles.Player == null)
                return;

            foreach (var pigeon in Object.FindObjectsByType<PigeonFpvSetup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (pigeon != null && pigeon.gameObject.activeSelf)
                    pigeon.gameObject.SetActive(false);
            }

            foreach (var placeholder in Object.FindObjectsByType<AvianBodyPlaceholder>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (placeholder != null)
                    Object.Destroy(placeholder.gameObject);
            }
        }
    }
}
