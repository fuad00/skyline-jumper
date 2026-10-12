using System;
using System.Collections.Generic;
using UnityEngine;

namespace StickmanJumper.Save
{
    /// <summary>Результат одного уровня (храним лучшее).</summary>
    [Serializable]
    public class LevelRecord
    {
        public string Id;
        public int Stars;
        public int BestPoints;
        /// <summary>Лучшее (самое близкое к флажку) расстояние, м. −1 — ещё не садился на цель.</summary>
        public float BestDistance = -1f;
    }

    /// <summary>Всё, что игра помнит между запусками. Version — чтобы потом безопасно менять формат.</summary>
    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 1;
        public int Version = CurrentVersion;
        public List<LevelRecord> Levels = new List<LevelRecord>();
        public bool Sound = true;
        public bool Music = true;
    }

    /// <summary>Где лежит сохранение (PlayerPrefs в игре, память в тестах).</summary>
    public interface ISaveStore
    {
        string Read();
        void Write(string json);
    }

    public sealed class PlayerPrefsSaveStore : ISaveStore
    {
        public const string Key = "SJ.Save";

        public string Read() => PlayerPrefs.GetString(Key, string.Empty);

        public void Write(string json)
        {
            PlayerPrefs.SetString(Key, json);
            PlayerPrefs.Save();
        }
    }

    /// <summary>Прогресс игрока: звёзды и рекорды уровней, настройки звука. Чистый класс (тестируется без сцены).</summary>
    public sealed class SaveService
    {
        readonly ISaveStore _store;

        public SaveData Data { get; private set; } = new SaveData();

        public SaveService(ISaveStore store)
        {
            _store = store;
        }

        public void Load()
        {
            Data = Parse(_store.Read());
        }

        public static SaveData Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return new SaveData();
            try
            {
                var data = JsonUtility.FromJson<SaveData>(json);
                if (data == null || data.Version > SaveData.CurrentVersion) return new SaveData();
                data.Levels ??= new List<LevelRecord>();
                data.Version = SaveData.CurrentVersion;
                return data;
            }
            catch (Exception)
            {
                return new SaveData(); // corrupt save: start fresh rather than crash
            }
        }

        public void Persist() => _store.Write(JsonUtility.ToJson(Data));

        public LevelRecord Get(string levelId)
        {
            foreach (var r in Data.Levels)
                if (r.Id == levelId) return r;
            return null;
        }

        public int Stars(string levelId) => Get(levelId)?.Stars ?? 0;

        public int TotalStars(IEnumerable<string> levelIds)
        {
            int sum = 0;
            foreach (var id in levelIds) sum += Stars(id);
            return sum;
        }

        /// <summary>Записать попытку: звёзды и очки — максимум, расстояние — минимум. true — побит рекорд расстояния.</summary>
        public bool Record(string levelId, int stars, int points, float distance)
        {
            var r = Get(levelId);
            if (r == null)
            {
                r = new LevelRecord { Id = levelId };
                Data.Levels.Add(r);
            }
            r.Stars = Mathf.Max(r.Stars, stars);
            r.BestPoints = Mathf.Max(r.BestPoints, points);
            bool newBest = stars > 0 && !float.IsNaN(distance) && (r.BestDistance < 0f || distance < r.BestDistance);
            if (newBest) r.BestDistance = distance;
            return newBest;
        }

        /// <summary>Уровень открыт, если он первый или предыдущий пройден хотя бы на одну звезду.</summary>
        public bool IsUnlocked(IReadOnlyList<string> levelIds, int index) => index <= 0 || Stars(levelIds[index - 1]) > 0;
    }
}
