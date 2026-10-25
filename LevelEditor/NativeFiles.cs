using System;
using System.IO;
using UnityEngine;

namespace RTXOn
{
    /// <summary>
    /// Android's own file window for Import and Export (Adrian 2026-10-24: the Import / Export folders inside the app
    /// can't be reached on a phone). Pick: any file the phone can open (Download, Drive, WhatsApp...), copied into the
    /// app's cache first. Save: the player chooses where a file the game wrote goes. Plugins/Android/RTXOnFiles.java
    /// opens the window and answers through UnitySendMessage to this object. Only on Android (<see cref="Available"/>).
    /// </summary>
    public class NativeFiles : MonoBehaviour
    {
        const string ReceiverName = "RTXOn Native Files";
        const string JavaClass = "com.rtxon.files.RTXOnFiles";

        static NativeFiles receiver;
        static Action<string> picked, saved;

        public static bool Available => Application.platform == RuntimePlatform.Android;

        /// <summary>
        /// A file the player picks, for <paramref name="onPath"/> (a path in the app's cache). Nothing happens when the
        /// window is closed without a file; <paramref name="onProblem"/> hears about the wrong kind of file or an error.
        /// </summary>
        public static void Pick(string[] extensions, Action<string> onPath, Action<string> onProblem)
        {
            picked = answer =>
            {
                if (string.IsNullOrEmpty(answer)) return; // closed without a file
                if (answer.StartsWith("!"))
                {
                    onProblem?.Invoke("Could not open the file: " + answer.Substring(1));
                    return;
                }
                if (!HasExtension(answer, extensions))
                {
                    onProblem?.Invoke($"{Path.GetFileName(answer)} is not a .{string.Join(" / .", extensions)} file.");
                    return;
                }
                onPath(answer);
            };
            Call("pick", picked, Receiver());
        }

        /// <summary>
        /// Asks where <paramref name="source"/> (a file the game wrote) should go, named <paramref name="fileName"/>, and
        /// copies it there. <paramref name="onDone"/> gets the message to show.
        /// </summary>
        public static void Save(string source, string fileName, Action<string> onDone)
        {
            saved = answer => onDone?.Invoke(
                string.IsNullOrEmpty(answer) ? $"Not exported (cancelled). A copy is in {source}" :
                answer.StartsWith("!") ? "Export failed: " + answer.Substring(1) :
                $"Exported {answer}");
            Call("save", saved, Receiver(), source, fileName, MimeOf(fileName));
        }

        static bool HasExtension(string path, string[] extensions)
        {
            if (extensions == null || extensions.Length == 0) return true;
            foreach (var e in extensions)
                if (path.EndsWith("." + e.TrimStart('.'), StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        static string MimeOf(string fileName)
        {
            switch (Path.GetExtension(fileName).ToLowerInvariant())
            {
                case ".png": return "image/png";
                case ".jpg":
                case ".jpeg": return "image/jpeg";
                case ".json": return "application/json";
                case ".txt": return "text/plain";
                default: return "application/octet-stream"; // .rsrt, .rsrtp, .themert, .levelrsrt
            }
        }

        static string Receiver()
        {
            if (receiver == null)
            {
                var go = new GameObject(ReceiverName);
                DontDestroyOnLoad(go);
                receiver = go.AddComponent<NativeFiles>();
            }
            return ReceiverName;
        }

        static void Call(string method, Action<string> answer, params object[] args)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var files = new AndroidJavaClass(JavaClass))
                {
                    var all = new object[args.Length + 1];
                    all[0] = activity;
                    args.CopyTo(all, 1);
                    files.CallStatic(method, all);
                }
            }
            catch (Exception e)
            {
                answer?.Invoke("!" + e.Message);
            }
#else
            answer?.Invoke("!Android only");
#endif
        }

        // Called by RTXOnFiles.java (UnitySendMessage).
        public void OnPicked(string answer)
        {
            var a = picked;
            picked = null;
            a?.Invoke(answer);
        }

        public void OnSaved(string answer)
        {
            var a = saved;
            saved = null;
            a?.Invoke(answer);
        }
    }
}
