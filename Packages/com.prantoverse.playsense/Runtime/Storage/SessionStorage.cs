using System;
using System.IO;
using UnityEngine;
using PlaySense.Data.Models;

namespace PlaySense.Storage
{
    /// <summary>
    /// Reads and writes .pss session files.
    /// Editor: &lt;project&gt;/PlaySenseSessions. Builds: &lt;persistentDataPath&gt;/PlaySenseSessions
    /// (the install folder of a build is often read-only).
    /// </summary>
    public static class SessionStorage
    {
        public const string Extension = ".pss";
        public const string FolderName = "PlaySenseSessions";

        public static string SessionsFolder
        {
            get
            {
                if (Application.isEditor)
                    return Path.GetFullPath(Path.Combine(Application.dataPath, "..", FolderName));

                return Path.Combine(Application.persistentDataPath, FolderName);
            }
        }

        /// <summary>Writes the session and returns its path, or null on failure.</summary>
        public static string Save(SessionData session)
        {
            if (session == null)
                return null;

            try
            {
                Directory.CreateDirectory(SessionsFolder);

                string shortId = string.IsNullOrEmpty(session.SessionId)
                    ? Guid.NewGuid().ToString("N").Substring(0, 6)
                    : session.SessionId.Replace("-", string.Empty).Substring(0, 6);

                string fileName = "Session_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + shortId + Extension;
                string path = Path.Combine(SessionsFolder, fileName);

                // Write to a temp file first so a crash mid-write never
                // leaves a half-written session behind.
                string tempPath = path + ".tmp";
                File.WriteAllText(tempPath, JsonUtility.ToJson(session, false));
                if (File.Exists(path))
                    File.Delete(path);
                File.Move(tempPath, path);

                return path;
            }
            catch (Exception exception)
            {
                Debug.LogError("[PlaySense] Could not save session:\n" + exception);
                return null;
            }
        }

        /// <summary>Session files, newest first.</summary>
        public static string[] GetSessionFiles()
        {
            string folder = SessionsFolder;
            if (!Directory.Exists(folder))
                return new string[0];

            string[] files = Directory.GetFiles(folder, "*" + Extension, SearchOption.TopDirectoryOnly);
            Array.Sort(files, (a, b) => File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a)));
            return files;
        }

        /// <summary>Loads a session, or returns null if it is missing or unreadable.</summary>
        public static SessionData Load(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                Debug.LogError("[PlaySense] Session not found:\n" + path);
                return null;
            }

            try
            {
                string json = File.ReadAllText(path);
                SessionData session = JsonUtility.FromJson<SessionData>(json);
                if (session == null)
                    return null;

                session.EnsureValid();

                // v1 files have no Version field. JsonUtility keeps the field
                // initializer's value for missing fields, so check the text.
                if (json.IndexOf("\"Version\"", StringComparison.Ordinal) < 0)
                    session.Version = 1;

                return session;
            }
            catch (Exception exception)
            {
                Debug.LogError("[PlaySense] Could not read session " + path + ":\n" + exception.Message);
                return null;
            }
        }
    }
}
