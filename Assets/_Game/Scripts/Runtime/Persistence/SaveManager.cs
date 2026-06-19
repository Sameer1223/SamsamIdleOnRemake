using System;
using System.IO;
using UnityEngine;

namespace SamsamIdleOn.Persistence
{
    public sealed class SaveManager
    {
        private const string SaveFileName = "save-data.json";

        private readonly string savePath;

        public SaveManager(string saveDirectory)
        {
            savePath = Path.Combine(saveDirectory, SaveFileName);
        }

        public string SavePath => savePath;

        public bool HasSave => File.Exists(savePath);

        public SaveData LoadOrCreate(DateTime utcNow)
        {
            if (!File.Exists(savePath))
            {
                return SaveData.CreateNew(utcNow);
            }

            try
            {
                string json = File.ReadAllText(savePath);
                SaveData loaded = JsonUtility.FromJson<SaveData>(json);
                SaveData saveData = loaded ?? SaveData.CreateNew(utcNow);
                saveData.EnsureDefaults();
                return saveData;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Failed to load save file at {savePath}. Starting a new save. {exception.Message}");
                return SaveData.CreateNew(utcNow);
            }
        }

        public void Save(SaveData saveData, DateTime utcNow, bool markClosed)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(savePath) ?? Application.persistentDataPath);
            saveData.EnsureDefaults();

            string timestamp = utcNow.ToString("O");
            saveData.lastSavedUtc = timestamp;

            if (markClosed)
            {
                saveData.lastClosedUtc = timestamp;
            }

            string json = JsonUtility.ToJson(saveData, true);
            File.WriteAllText(savePath, json);
        }

        public void DeleteSave()
        {
            if (File.Exists(savePath))
            {
                File.Delete(savePath);
            }
        }
    }
}
