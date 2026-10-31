using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using PlaySense.Core;
using PlaySense.Data.Models;
using PlaySense.Storage;

namespace PlaySense.EditorTools
{
    /// <summary>
    /// Loaded .pss files shared by the Dashboard and Replay windows.
    /// Files are only re-read when they change on disk.
    /// </summary>
    [InitializeOnLoad]
    public static class PlaySenseSessionCache
    {
        public class Entry
        {
            public string Path;
            public string FileName;
            public SessionData Session;
            public DateTime Modified;
        }

        private static readonly List<Entry> s_Entries = new List<Entry>();
        private static bool s_Dirty = true;

        /// <summary>Raised after the list of sessions changes.</summary>
        public static event Action Changed;

        static PlaySenseSessionCache()
        {
            PlaySenseManager.SessionSaved += (session, path) => MarkDirty();
        }

        /// <summary>All sessions, newest first.</summary>
        public static List<Entry> Entries
        {
            get
            {
                if (s_Dirty)
                    Refresh();

                return s_Entries;
            }
        }

        public static void MarkDirty()
        {
            s_Dirty = true;
            if (Changed != null)
                Changed();
        }

        public static Entry Find(string path)
        {
            foreach (Entry entry in Entries)
            {
                if (entry.Path == path)
                    return entry;
            }

            return null;
        }

        /// <summary>Scene names that appear in any session, sorted.</summary>
        public static List<string> GetSceneNames()
        {
            List<string> names = new List<string>();
            foreach (Entry entry in Entries)
            {
                foreach (string scene in entry.Session.GetSceneNames())
                {
                    if (!names.Contains(scene))
                        names.Add(scene);
                }
            }

            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }

        public static void Refresh()
        {
            s_Dirty = false;

            Dictionary<string, Entry> previous = new Dictionary<string, Entry>();
            foreach (Entry entry in s_Entries)
                previous[entry.Path] = entry;

            s_Entries.Clear();

            foreach (string path in SessionStorage.GetSessionFiles())
            {
                DateTime modified = File.GetLastWriteTimeUtc(path);
                Entry entry;

                if (!previous.TryGetValue(path, out entry) || entry.Modified != modified)
                {
                    SessionData session = SessionStorage.Load(path);
                    if (session == null)
                        continue;

                    entry = new Entry
                    {
                        Path = path,
                        FileName = System.IO.Path.GetFileNameWithoutExtension(path),
                        Session = session,
                        Modified = modified
                    };
                }

                s_Entries.Add(entry);
            }
        }

        public static bool Delete(Entry entry)
        {
            if (entry == null)
                return false;

            try
            {
                File.Delete(entry.Path);

                string pdf = System.IO.Path.ChangeExtension(entry.Path, ".pdf");
                if (File.Exists(pdf))
                    File.Delete(pdf);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogError("[PlaySense] Could not delete session:\n" + exception);
                return false;
            }

            MarkDirty();
            return true;
        }
    }
}
