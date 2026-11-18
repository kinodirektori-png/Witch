using UnityEditor;
using UnityEngine;
using Witch.Gameplay;

namespace Witch.EditorTools
{
    /// <summary>Editor helpers for the local save file (Witch > Save File).</summary>
    public static class SaveFileMenu
    {
        [MenuItem("Witch/Save File/Delete Save File", priority = 20)]
        private static void DeleteSaveFile()
        {
            if (!EditorUtility.DisplayDialog(
                    "Delete save file?",
                    $"This deletes all progress saved on this computer:\n{SaveFile.FilePath}",
                    "Delete",
                    "Cancel"))
            {
                return;
            }

            SaveFile.Delete();
            Debug.Log($"Deleted save file at {SaveFile.FilePath}");
        }

        [MenuItem("Witch/Save File/Show Save Folder", priority = 21)]
        private static void ShowSaveFolder()
        {
            EditorUtility.RevealInFinder(Application.persistentDataPath);
        }
    }
}
