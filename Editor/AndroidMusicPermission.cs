#if UNITY_ANDROID
using System.IO;
using System.Xml;
using UnityEditor.Android;

namespace RTXOn.EditorTools
{
    /// <summary>
    /// Android builds: adds the permissions Level Settings > Music > Choose File... needs to read song files from the
    /// phone (Music, Download...): READ_MEDIA_AUDIO (Android 13+) and READ_EXTERNAL_STORAGE (Android 12 and older).
    /// The game asks the player for them the first time the folder browser opens.
    /// </summary>
    public class AndroidMusicPermission : IPostGenerateGradleAndroidProject
    {
        const string AndroidNs = "http://schemas.android.com/apk/res/android";

        public int callbackOrder => 0;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string manifest = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifest)) return;
            var doc = new XmlDocument();
            doc.Load(manifest);
            var root = doc.DocumentElement;
            if (root == null) return;
            bool changed = Add(doc, root, "android.permission.READ_MEDIA_AUDIO", null);
            changed |= Add(doc, root, "android.permission.READ_EXTERNAL_STORAGE", "32");
            if (changed) doc.Save(manifest);
        }

        static bool Add(XmlDocument doc, XmlElement root, string permission, string maxSdk)
        {
            foreach (XmlNode node in root.ChildNodes)
                if (node is XmlElement e && e.Name == "uses-permission" && e.GetAttribute("name", AndroidNs) == permission)
                    return false;
            var add = doc.CreateElement("uses-permission");
            add.Attributes.Append(Attribute(doc, "name", permission));
            if (maxSdk != null) add.Attributes.Append(Attribute(doc, "maxSdkVersion", maxSdk));
            root.PrependChild(add);
            return true;
        }

        /// <summary>android:name="..." (with the manifest's android: prefix).</summary>
        static XmlAttribute Attribute(XmlDocument doc, string name, string value)
        {
            var a = doc.CreateAttribute("android", name, AndroidNs);
            a.Value = value;
            return a;
        }
    }
}
#endif
