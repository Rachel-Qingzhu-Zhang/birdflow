#if UNITY_EDITOR
using System;
using System.Reflection;
using System.Text;
using AvianVoice.Audio;
using AvianVoice.Core;
using AvianVoice.Embodiment;
using AvianVoice.Tasks;
using UnityEditor;
using UnityEngine;

namespace AvianVoice.Editor
{
    /// <summary>
    /// Play-mode sweep of the six lab tasks. Logs one [AvianHealth] line per check.
    /// </summary>
    static class AvianFeatureHealthCheck
    {
        const string Flag = "AvianFeatureHealth";

        [MenuItem("Avian Voice/Run Feature Health Check")]
        static void RunFromMenu()
        {
            SessionState.SetBool(Flag, true);
            if (!EditorApplication.isPlaying)
            {
                EditorApplication.EnterPlaymode();
                return;
            }

            EditorApplication.delayCall += RunChecks;
        }

        [InitializeOnLoadMethod]
        static void ResumeAfterDomainReload()
        {
            if (!SessionState.GetBool(Flag, false))
                return;

            EditorApplication.delayCall += WaitAndRun;
        }

        static int waits;

        static void WaitAndRun()
        {
            if (EditorApplication.isCompiling || !EditorApplication.isPlaying)
            {
                if (waits++ < 600)
                    EditorApplication.delayCall += WaitAndRun;
                return;
            }

            if (waits++ < 90)
            {
                EditorApplication.delayCall += WaitAndRun;
                return;
            }

            SessionState.SetBool(Flag, false);
            try
            {
                RunChecks();
            }
            catch (Exception ex)
            {
                Debug.LogError("[AvianHealth] FAIL runner " + ex);
            }
            finally
            {
                EditorApplication.delayCall += () =>
                {
                    if (EditorApplication.isPlaying)
                        EditorApplication.ExitPlaymode();
                };
            }
        }

        static void RunChecks()
        {
            var report = new StringBuilder();
            CheckHearing(report);
            CheckTask(report, "meet", expectMirror: true, expectPond: false);
            CheckTask(report, "breath_sync", expectMirror: true, expectPond: false);
            CheckTask(report, "flying", expectMirror: false, expectPond: true);
            CheckFlightWings(report);
            CheckTask(report, "singing", expectMirror: true, expectPond: false);
            CheckSinging(report);
            CheckTask(report, "call_friends", expectMirror: false, expectPond: true);
            CheckCallFriends(report);
            CheckTask(report, "reflection", expectMirror: true, expectPond: false);
            Debug.Log("[AvianHealth] DONE\n" + report);
        }

        static void CheckHearing(StringBuilder report)
        {
            AvianSceneBootstrapHelper.EnsureHearing();
            var listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            var enabled = 0;
            string on = "";
            foreach (var listener in listeners)
            {
                if (listener == null || !listener.enabled)
                    continue;
                enabled++;
                on = listener.gameObject.name;
            }

            Line(report, enabled == 1, "listener", "enabled=" + enabled + " on=" + on + " volume=" + AudioListener.volume);

            var audio = BirdExperienceAudio.Ensure();
            var clips = audio != null && audio.HasPitchClip && audio.HasMelodyClip;
            Line(report, clips, "clips", audio == null ? "no BirdExperienceAudio" : "pitch=" + audio.HasPitchClip + " melody=" + audio.HasMelodyClip);

            if (audio != null && audio.HasPitchClip)
            {
                audio.PlayPitchStepNear(null, 0, 4, 1f);
                Line(report, audio.IsReferencePlaying, "pitch playback", "isPlaying=" + audio.IsReferencePlaying);
            }

            var mic = UnityEngine.Object.FindAnyObjectByType<MicrophoneCapture>();
            Line(report, mic != null && mic.IsCapturing, "microphone", mic == null ? "missing" : "capturing=" + mic.IsCapturing + " devices=" + Microphone.devices.Length);
        }

        static void CheckTask(StringBuilder report, string taskId, bool expectMirror, bool expectPond)
        {
            var manager = UnityEngine.Object.FindAnyObjectByType<TaskManager>();
            if (manager == null)
            {
                Line(report, false, taskId, "no TaskManager");
                return;
            }

            manager.LoadTaskById(taskId);
            var mirrorOn = BaroqueRenderersEnabled();
            var pondOn = PondEnabled();
            Line(report, mirrorOn == expectMirror && pondOn == expectPond, taskId,
                "current=" + manager.CurrentTaskId + " mirror=" + mirrorOn + " pond=" + pondOn);
        }

        static void CheckSinging(StringBuilder report)
        {
            var friend = BirdSceneRoles.Friend;
            Line(report, friend != null && friend.gameObject.activeInHierarchy, "singing friend",
                friend == null ? "missing" : "active=" + friend.gameObject.activeInHierarchy);

            var task = UnityEngine.Object.FindAnyObjectByType<SingingBranchTask>();
            var pose = friend != null ? friend.GetComponent<FriendBreathVisual>() : null;
            Invoke(task, "LateUpdate");
            Invoke(pose, "LateUpdate");
            var chest = ChestScale(friend);
            Line(report, chest > 1.15f, "singing chest", "spineScale=" + chest.ToString("0.00"));

            var wrong = InjectPitch(200f);
            Invoke(task, "Update");
            var wrongFlap = Field<bool>(task, "pitchCorrect");
            Line(report, !wrongFlap, "singing rejects off note", "pitch=" + wrong.ToString("0") + " flap=" + wrongFlap);

            var right = InjectPitch(330f);
            for (var i = 0; i < 8; i++)
                Invoke(task, "Update");
            var rightFlap = Field<bool>(task, "pitchCorrect");
            Line(report, rightFlap, "singing accepts E4", "pitch=" + right.ToString("0") + " flap=" + rightFlap);
        }

        static void CheckCallFriends(StringBuilder report)
        {
            var task = UnityEngine.Object.FindAnyObjectByType<CallFriendsTask>();
            var before = CountPerched();
            SetField(task, "listenUntil", 0f);
            InjectPitch(200f);
            for (var i = 0; i < 30; i++)
                Invoke(task, "Update");
            var afterWrong = CountPerched();
            Line(report, afterWrong == before && before > 0, "call ignores off note",
                "perched " + before + " -> " + afterWrong);

            InjectPitch(330f);
            SetField(task, "listenUntil", 0f);
            for (var i = 0; i < 40; i++)
                Invoke(task, "Update");
            var afterRight = CountPerched();
            Line(report, afterRight == before - 1, "call summons one bird",
                "perched " + before + " -> " + afterRight);
        }

        static void CheckFlightWings(StringBuilder report)
        {
            var bird = BirdSceneRoles.Player;
            var flight = bird != null ? bird.GetComponent<ParrotFlightPresentation>() : null;
            if (flight == null || !ParrotEmbodimentWiring.TryResolve(bird, out var refs) || refs.leftWing == null)
            {
                Line(report, false, "flight wings", "missing presentation or humerus");
                return;
            }

            var before = refs.leftWing.localRotation;
            Invoke(flight, "LateUpdate");
            Invoke(flight, "ApplyFlightWings");
            var after = refs.leftWing.localRotation;
            var opened = Quaternion.Angle(before, after) > 20f;
            Line(report, opened, "flight wings", "humerusDelta=" + Quaternion.Angle(before, after).ToString("0") + " deg");
        }

        static bool BaroqueRenderersEnabled()
        {
            var any = false;
            var found = false;
            foreach (var renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (renderer == null || !IsBaroque(renderer))
                    continue;
                found = true;
                if (renderer.enabled)
                    any = true;
            }

            return found && any;
        }

        static bool IsBaroque(MeshRenderer renderer)
        {
            var materials = renderer.sharedMaterials;
            if (materials == null)
                return false;
            foreach (var material in materials)
            {
                if (material == null)
                    continue;
                var name = material.name;
                if (name.Contains("Gold_material") || name.Contains("Realistic_Mirror") || name.Contains("bois"))
                    return true;
            }

            return false;
        }

        static bool PondEnabled()
        {
            var ponds = UnityEngine.Object.FindObjectsByType<PondWaterReflection>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var pond in ponds)
            {
                if (pond != null && pond.enabled)
                    return true;
            }

            return false;
        }

        static float ChestScale(Transform bird)
        {
            if (bird == null)
                return 0f;
            foreach (var bone in bird.GetComponentsInChildren<Transform>(true))
            {
                if (bone.name == "parrot_Spine3_bone")
                    return bone.localScale.x;
            }

            return 0f;
        }

        static int CountPerched()
        {
            var count = 0;
            var birds = UnityEngine.Object.FindObjectsByType<CallFriendsCompanionBehaviour>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var bird in birds)
            {
                if (bird != null && bird.IsPerched)
                    count++;
            }

            return count;
        }

        static float InjectPitch(float hz)
        {
            var analyzer = UnityEngine.Object.FindAnyObjectByType<VoiceAnalyzer>();
            if (analyzer == null)
                return 0f;

            var settings = AvianVoiceSettings.FindLoaded();
            var rate = settings != null && settings.sampleRate > 0 ? settings.sampleRate : 48000;
            var size = settings != null && settings.frameSize > 0 ? settings.frameSize : 1024;
            var samples = new float[size];
            for (var i = 0; i < samples.Length; i++)
                samples[i] = Mathf.Sin(2f * Mathf.PI * hz * i / rate) * 0.35f;
            analyzer.PushSamples(samples, samples.Length);
            return analyzer.Current.pitchHz;
        }

        static void Invoke(object target, string method)
        {
            if (target == null)
                return;
            var info = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            info?.Invoke(target, null);
        }

        static T Field<T>(object target, string name)
        {
            if (target == null)
                return default;
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
                return default;
            return (T)field.GetValue(target);
        }

        static void SetField(object target, string name, object value)
        {
            if (target == null)
                return;
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            field?.SetValue(target, value);
        }

        static void Line(StringBuilder report, bool pass, string name, string detail)
        {
            var line = (pass ? "PASS " : "FAIL ") + name + " | " + detail;
            report.Append(line).Append('\n');
            Debug.Log("[AvianHealth] " + line);
        }
    }
}
#endif
