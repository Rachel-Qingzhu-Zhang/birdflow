using UnityEngine;

namespace AvianVoice.Embodiment
{
    /// <summary>
    /// Wires two scene-placed parrots (FriendParrot + PlayerParrot) for Birdy tasks.
    /// Does not duplicate or reposition birds — use the scene objects you place in the Editor.
    /// </summary>
    public static class BirdSceneRoles
    {
        public const string FriendName = "FriendParrot";
        public const string PlayerName = "PlayerParrot";
        public const string PerchName = "Cylinder";
        public const int MirrorLayer = 9;
        public const int MirrorDisplayLayer = 10;
        public const int UiLayer = 11;

        public static Transform Friend { get; private set; }
        public static Transform Player { get; private set; }

        public static bool TryGetFlockOrbitCenter(out Vector3 center)
        {
            if (Friend != null && Player != null)
            {
                center = Vector3.Lerp(Friend.position, Player.position, 0.5f);
                return true;
            }

            if (Friend != null)
            {
                center = Friend.position;
                return true;
            }

            if (Player != null)
            {
                center = Player.position;
                return true;
            }

            center = default;
            return false;
        }

        static bool birdsConfigured;

        public static void ResetSession()
        {
            birdsConfigured = false;
            Friend = null;
            Player = null;
        }

        public static void EnsureSetup()
        {
            if (!ResolveSceneBirds())
                return;

            if (!birdsConfigured)
                ConfigureBirdComponents();

            EnsureRoleMarkers();
            FinalizeScenePlacement();

            CommitPoses(Friend);
            CommitPoses(Player);

            LogLayoutDiagnostics();
        }

        static bool ResolveSceneBirds()
        {
            Friend = null;
            Player = null;

            foreach (var role in Object.FindObjectsByType<BirdSceneRole>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (role == null)
                    continue;

                if (role.Role == BirdSceneRole.RoleKind.Friend)
                    Friend = role.transform;
                else
                    Player = role.transform;
            }

            if (Friend == null)
                Friend = FindBirdTransform(FriendName);
            if (Player == null)
                Player = FindBirdTransform(PlayerName);

            if (Friend == null)
            {
                var legacy = FindLegacyParrot();
                if (legacy != null)
                {
                    legacy.name = FriendName;
                    Friend = legacy;
                    EnsureSceneRoleComponent(Friend, BirdSceneRole.RoleKind.Friend);
                }
            }

            if (Friend == null || Player == null)
            {
                Debug.LogError(
                    "[AvianVoice] BirdSceneRoles: need TWO parrots in Avian_Lab. " +
                    "Menu: Avian Voice > Place Two Parrots On Perch, then adjust positions and Save scene.");
                return false;
            }

            if (Friend == Player)
            {
                Debug.LogError("[AvianVoice] BirdSceneRoles: friend and player cannot be the same object.");
                return false;
            }

            Friend.gameObject.SetActive(true);
            Player.gameObject.SetActive(true);
            return true;
        }

        static void LogLayoutDiagnostics()
        {
            var friendSmr = GetBodyRenderer(Friend);
            var playerSmr = GetBodyRenderer(Player);
            var friendBounds = friendSmr != null ? friendSmr.bounds.size : Vector3.zero;
            var playerBounds = playerSmr != null ? playerSmr.bounds.size : Vector3.zero;

            Debug.Log(
                "[AvianVoice] BirdSceneRoles: scene parrots ready. " +
                $"distance={Vector3.Distance(Friend.position, Player.position):0.00}m, " +
                $"friendBounds={friendBounds}, playerBounds={playerBounds}. " +
                $"Friend '{Friend.name}'@{Friend.position} Player '{Player.name}'@{Player.position}");
        }

        static void ConfigureBirdComponents()
        {
            SetupBird(Friend, RoleMarker.Role.Friend);
            SetupBird(Player, RoleMarker.Role.Player);
            birdsConfigured = true;
        }

        static void SetupBird(Transform bird, RoleMarker.Role role)
        {
            var guard = bird.GetComponent<WorldPlacedBird>() ?? bird.gameObject.AddComponent<WorldPlacedBird>();
            guard.ConfigureForFixedWorldPlacement();

            var animator = bird.GetComponent<Animator>();
            if (animator != null)
            {
                animator.applyRootMotion = false;
                ParrotEmbodimentWiring.ApplyAnimatorMeditationDefaults(animator);
            }

            ParrotEmbodimentWiring.DisableConflictingParrotDemoScript(bird);

            if (role == RoleMarker.Role.Friend)
            {
                var playerBreath = bird.GetComponent<ParrotBreathEmbodiment>();
                if (playerBreath != null)
                    Object.Destroy(playerBreath);
                if (bird.GetComponent<FriendBreathVisual>() == null)
                    bird.gameObject.AddComponent<FriendBreathVisual>();
                if (bird.GetComponent<FriendTeachingCue>() == null)
                    bird.gameObject.AddComponent<FriendTeachingCue>();
            }
            else
            {
                var friendVisual = bird.GetComponent<FriendBreathVisual>();
                if (friendVisual != null)
                    Object.Destroy(friendVisual);
                if (bird.GetComponent<ParrotBreathEmbodiment>() == null)
                    bird.gameObject.AddComponent<ParrotBreathEmbodiment>();
            }

            var pin = bird.GetComponent<ParrotMainCtrlPin>() ?? bird.gameObject.AddComponent<ParrotMainCtrlPin>();
            pin.SetPinEnabled(true);
        }

        static void EnsureRoleMarkers()
        {
            AttachMarker(Friend, RoleMarker.Role.Friend);
            AttachMarker(Player, RoleMarker.Role.Player);
        }

        static void AttachMarker(Transform bird, RoleMarker.Role role)
        {
            if (bird == null)
                return;

            var marker = bird.GetComponent<RoleMarker>() ?? bird.gameObject.AddComponent<RoleMarker>();
            marker.Configure(role);
        }

        static void FinalizeScenePlacement()
        {
            RefreshPinPoses(Friend);
            RefreshPinPoses(Player);
            SetLayerRecursive(Friend.gameObject, MirrorLayer);
            SetLayerRecursive(Player.gameObject, MirrorLayer);
        }

        static void RefreshPinPoses(Transform bird)
        {
            var pin = bird?.GetComponent<ParrotMainCtrlPin>();
            pin?.CaptureRestPose();
        }

        static SkinnedMeshRenderer GetBodyRenderer(Transform bird)
        {
            if (bird == null)
                return null;

            SkinnedMeshRenderer best = null;
            var bestVolume = 0f;
            foreach (var smr in bird.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr == null || !smr.enabled)
                    continue;

                smr.updateWhenOffscreen = true;
                var volume = smr.bounds.size.sqrMagnitude;
                if (volume > bestVolume)
                {
                    bestVolume = volume;
                    best = smr;
                }
            }

            return best;
        }

        static void CommitPoses(Transform bird)
        {
            bird?.GetComponent<WorldPlacedBird>()?.CommitWorldPose();
        }

        static void EnsureSceneRoleComponent(Transform bird, BirdSceneRole.RoleKind role)
        {
            if (bird == null)
                return;

            var marker = bird.GetComponent<BirdSceneRole>() ?? bird.gameObject.AddComponent<BirdSceneRole>();
            marker.SetRole(role);
        }

        static Transform FindLegacyParrot()
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name == "Parrot")
                    return t;
            }

            return null;
        }

        static Transform FindBirdTransform(string objectName)
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name == objectName)
                    return t;
            }

            return null;
        }

        static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursive(child.gameObject, layer);
        }
    }
}
