using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Witch.Gameplay
{
    /// <summary>
    /// Reads and writes <see cref="SaveData"/> as JSON in <see cref="Application.persistentDataPath"/>.
    /// Writes go to a temp file first so a crash mid-save never leaves a half-written save behind.
    /// A save that can't be read is kept as a .bak file and the game starts fresh.
    /// </summary>
    public static class SaveFile
    {
        public const string FileName = "medea-save.json";

        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        private static string TempPath => FilePath + ".tmp";
        private static string BackupPath => FilePath + ".bak";

        public static bool Exists => File.Exists(FilePath);

        /// <summary>Writes the save. Returns false (and logs) if the disk write fails.</summary>
        public static bool Write(SaveData data)
        {
            if (data == null)
            {
                return false;
            }

            try
            {
                data.version = SaveData.CurrentVersion;
                string json = JsonUtility.ToJson(data, true);
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(TempPath, json);

                if (File.Exists(FilePath))
                {
                    File.Delete(FilePath);
                }

                File.Move(TempPath, FilePath);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"SaveFile: could not write '{FilePath}': {exception.Message}");
                return false;
            }
        }

        /// <summary>
        /// Loads the save. Returns false when there is no save, or it is unreadable or from a newer game version
        /// (that file is moved to .bak so it isn't lost, and the caller starts a fresh game).
        /// </summary>
        public static bool TryLoad(out SaveData data)
        {
            data = null;
            RecoverInterruptedWrite();

            if (!File.Exists(FilePath))
            {
                return false;
            }

            try
            {
                string json = File.ReadAllText(FilePath);
                data = JsonUtility.FromJson<SaveData>(json);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"SaveFile: save is unreadable, starting fresh ({exception.Message}).");
                data = null;
            }

            if (data == null || data.version <= 0 || data.version > SaveData.CurrentVersion)
            {
                if (data != null && data.version > SaveData.CurrentVersion)
                {
                    Debug.LogWarning($"SaveFile: save version {data.version} is newer than {SaveData.CurrentVersion}, starting fresh.");
                }

                BackUpAndRemove();
                data = null;
                return false;
            }

            Migrate(data);
            data.discoveredRecipes ??= new List<string>();
            data.stock ??= new List<SaveData.StockEntry>();
            data.unlockedLore ??= new List<string>();
            return true;
        }

        /// <summary>Deletes the save (and any temp or backup file), e.g. for "new game".</summary>
        public static void Delete()
        {
            TryDelete(FilePath);
            TryDelete(TempPath);
            TryDelete(BackupPath);
        }

        /// <summary>Upgrades an older save in place. Version 1 is the first layout, so there is nothing to do yet.</summary>
        private static void Migrate(SaveData data)
        {
            // Example for the future:
            // if (data.version < 2) { /* fill new fields */ data.version = 2; }
            data.version = SaveData.CurrentVersion;
        }

        /// <summary>If the app died between writing the temp file and moving it, use the temp file.</summary>
        private static void RecoverInterruptedWrite()
        {
            try
            {
                if (!File.Exists(FilePath) && File.Exists(TempPath))
                {
                    File.Move(TempPath, FilePath);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"SaveFile: could not recover temp save: {exception.Message}");
            }
        }

        private static void BackUpAndRemove()
        {
            try
            {
                TryDelete(BackupPath);
                File.Move(FilePath, BackupPath);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"SaveFile: could not back up bad save: {exception.Message}");
                TryDelete(FilePath);
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"SaveFile: could not delete '{path}': {exception.Message}");
            }
        }
    }
}
