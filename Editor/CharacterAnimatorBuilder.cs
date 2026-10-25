using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RTXOn.EditorTools
{
    /// <summary>
    /// "RTX ON > Build Character Animator": finds your Idle, Run, Jump and Crash clips by file or clip name (Mixamo
    /// names every clip "mixamo.com", so the FBX file name counts: Idle.fbx, Running.fbx, Jump.fbx, Death.fbx...),
    /// sets their FBX to Humanoid (Idle and Run looping), and makes CharacterAnimator.controller next to them:
    /// Idle -> Run when the ball starts, Jump on every take-off, back to Run on landing, Crash when the ball dies.
    /// Then puts it on the Ball's Ball Character in the open scene (adding that component if needed).
    /// Looks in the selected folder first (select the folder with your clips), else in all of Assets. One FBX with
    /// several clips (Blender actions named Idle, Run, Jump, Death) works too.
    /// </summary>
    public static class CharacterAnimatorBuilder
    {
        // Checked in this order, so "Running Jump" is a jump and "Falling Death" a crash.
        static readonly (string state, string[] words)[] Kinds =
        {
            ("Crash", new[] { "crash", "death", "dying", "die" }),
            ("Jump", new[] { "jump" }),
            ("Idle", new[] { "idle" }),
            ("Run", new[] { "run", "sprint", "jog" }),
        };

        [MenuItem("RTX ON/Build Character Animator", priority = 12)]
        public static void Build()
        {
            var paths = Find(SelectedFolders());
            if (paths.Count == 0) paths = Find(new[] { "Assets" });
            if (paths.Count == 0)
            {
                EditorUtility.DisplayDialog("RTX ON", "No Idle / Run / Jump / Crash clips found. Name the FBX files (or clips) " +
                    "with idle, run, jump and crash (or death) in them, select their folder and run this again.", "OK");
                return;
            }

            // Humanoid (so the clips play on any humanoid character), Idle and Run looping. One FBX can hold all four
            // (Blender actions), so each file is set up once and each take loops by its own name.
            var setUp = new HashSet<string>();
            foreach (var kv in paths)
                if (setUp.Add(kv.Value) && AssetImporter.GetAtPath(kv.Value) is ModelImporter mi)
                    SetUp(mi, Path.GetFileNameWithoutExtension(kv.Value));

            var clips = new Dictionary<string, AnimationClip>();
            foreach (var kv in paths)
            {
                var clip = LoadClip(kv.Value, kv.Key);
                if (clip != null) clips[kv.Key] = clip;
            }

            string folder = Path.GetDirectoryName(First(paths)).Replace('\\', '/');
            string controllerPath = folder + "/CharacterAnimator.controller";
            var controller = MakeController(controllerPath, clips);

            var ball = Object.FindFirstObjectByType<BallController>();
            string assigned = "";
            if (ball != null)
            {
                var character = ball.GetComponent<BallCharacter>();
                if (character == null) character = Undo.AddComponent<BallCharacter>(ball.gameObject);
                Undo.RecordObject(character, "Build Character Animator");
                character.controller = controller;
                EditorUtility.SetDirty(character);
                EditorSceneManager.MarkSceneDirty(ball.gameObject.scene);
                assigned = character.model == null
                    ? " Put on the Ball's Ball Character: now drag your rigged model (e.g. CuteBeard) into its Model slot and save the scene."
                    : " Put on the Ball's Ball Character; save the scene.";
            }
            else assigned = " Open the Gameplay scene and add Ball Character to the Ball, with this controller and your model.";

            var found = new List<string>();
            foreach (var k in Kinds)
                found.Add(k.state + (clips.TryGetValue(k.state, out var c) ? " = " + Path.GetFileName(paths[k.state]) : " (none)"));
            Debug.Log($"RTX ON: made {controllerPath}: {string.Join(", ", found)}.{assigned}");
            Selection.activeObject = controller;
        }

        static string First(Dictionary<string, string> d)
        {
            foreach (var v in d.Values) return v;
            return "Assets/x";
        }

        static string[] SelectedFolders()
        {
            var list = new List<string>();
            foreach (var guid in Selection.assetGUIDs)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;
                if (!AssetDatabase.IsValidFolder(path)) path = Path.GetDirectoryName(path).Replace('\\', '/');
                if (!list.Contains(path)) list.Add(path);
            }
            return list.ToArray();
        }

        /// <summary>State -> asset path of its clip, first match wins.</summary>
        static Dictionary<string, string> Find(string[] folders)
        {
            var result = new Dictionary<string, string>();
            if (folders.Length == 0) return result;
            var seen = new HashSet<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip", folders))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!seen.Add(path)) continue;
                // Every clip of the file: a Blender FBX with Idle, Run, Jump and Death actions gives all four.
                // A clip's own name wins; a clip named "mixamo.com" goes by the file name.
                foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (!(obj is AnimationClip clip) || clip.name.StartsWith("__preview__")) continue;
                    string state = KindOf(clip.name.ToLowerInvariant()) ?? KindOf(Path.GetFileNameWithoutExtension(path).ToLowerInvariant());
                    if (state != null && !result.ContainsKey(state)) result[state] = path;
                }
            }
            return result;
        }

        static string KindOf(string key)
        {
            foreach (var k in Kinds)
                foreach (var w in k.words)
                    if (key.Contains(w)) return k.state;
            return null;
        }

        static void SetUp(ModelImporter mi, string fileName)
        {
            bool changed = false;
            if (mi.animationType != ModelImporterAnimationType.Human)
            {
                mi.animationType = ModelImporterAnimationType.Human;
                mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                changed = true;
            }
            var takes = mi.clipAnimations;
            if (takes == null || takes.Length == 0) takes = mi.defaultClipAnimations;
            foreach (var t in takes)
            {
                string kind = KindOf(t.name.ToLowerInvariant()) ?? KindOf(fileName.ToLowerInvariant());
                if (kind == null) continue; // not one of the four: left as it is
                bool loop = kind == "Idle" || kind == "Run";
                if (t.loopTime != loop) { t.loopTime = loop; changed = true; }
            }
            if (takes.Length > 0) mi.clipAnimations = takes;
            if (changed) mi.SaveAndReimport();
        }

        static AnimationClip LoadClip(string path, string state)
        {
            AnimationClip any = null;
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (!(obj is AnimationClip clip) || clip.name.StartsWith("__preview__")) continue;
                var kind = KindOf(clip.name.ToLowerInvariant());
                if (kind == state) return clip;
                if (any == null && kind == null) any = clip; // "mixamo.com": the file name said which one
            }
            return any;
        }

        static AnimatorController MakeController(string path, Dictionary<string, AnimationClip> clips)
        {
            // Made fresh each time; the asset keeps its file so Ball Character keeps pointing at it.
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            foreach (var p in controller.parameters) controller.RemoveParameter(p);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Crash", AnimatorControllerParameterType.Trigger);

            var sm = controller.layers[0].stateMachine;
            foreach (var s in sm.states) sm.RemoveState(s.state);
            foreach (var t in sm.anyStateTransitions) sm.RemoveAnyStateTransition(t);

            AnimatorState State(string name, Vector3 at)
            {
                if (!clips.TryGetValue(name, out var clip)) return null;
                var s = sm.AddState(name, at);
                s.motion = clip;
                return s;
            }
            var idle = State("Idle", new Vector3(300, 0));
            var run = State("Run", new Vector3(300, 120));
            var jump = State("Jump", new Vector3(560, 60));
            var crash = State("Crash", new Vector3(560, 200));
            var ground = run != null ? run : idle;
            sm.defaultState = idle != null ? idle : ground != null ? ground : jump;

            AnimatorStateTransition Go(AnimatorState from, AnimatorState to, float fade)
            {
                var t = from.AddTransition(to);
                t.hasExitTime = false;
                t.duration = fade;
                return t;
            }
            if (idle != null && run != null)
            {
                Go(idle, run, 0.15f).AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
                Go(run, idle, 0.2f).AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            }
            if (jump != null)
            {
                var t = sm.AddAnyStateTransition(jump);
                t.hasExitTime = false;
                t.duration = 0.05f;
                t.canTransitionToSelf = true; // chained jump pads start the jump again
                t.AddCondition(AnimatorConditionMode.If, 0f, "Jump");
                if (ground != null) Go(jump, ground, 0.12f).AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
            }
            if (crash != null)
            {
                var t = sm.AddAnyStateTransition(crash);
                t.hasExitTime = false;
                t.duration = 0.05f;
                t.canTransitionToSelf = false;
                t.AddCondition(AnimatorConditionMode.If, 0f, "Crash");
            }
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }
    }
}
