using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace StickmanJumper.Maps
{
    /// <summary>
    /// Карты игрока на телефоне: по JSON-файлу на карту в папке maps (Application.persistentDataPath/maps в игре,
    /// временная папка в тестах). Испорченный файл пропускается, а не роняет игру.
    /// </summary>
    public sealed class MapStore
    {
        readonly string _folder;

        public MapStore(string folder)
        {
            _folder = folder;
        }

        /// <summary>Своя папка вместо папки на телефоне (автотест не трогает настоящие карты). null — обычная.</summary>
        public static string FolderOverride { get; set; }

        public static MapStore OnDevice() => new MapStore(FolderOverride ?? Path.Combine(Application.persistentDataPath, "maps"));

        public string Folder => _folder;

        /// <summary>Все карты, свежие сверху.</summary>
        public List<MapData> All()
        {
            var maps = new List<MapData>();
            if (!Directory.Exists(_folder)) return maps;
            foreach (var file in Directory.GetFiles(_folder, "*.json"))
            {
                var map = Read(file);
                if (map != null) maps.Add(map);
            }
            maps.Sort((a, b) => b.Updated.CompareTo(a.Updated));
            return maps;
        }

        public MapData Load(string id)
        {
            string file = PathOf(id);
            return file != null && File.Exists(file) ? Read(file) : null;
        }

        /// <summary>Сохранить (новой карте выдаётся Id). Пишем во временный файл и подменяем — не останется полузаписанной карты.</summary>
        public void Save(MapData map, bool touch = true)
        {
            if (string.IsNullOrEmpty(map.Id)) map.Id = NewId();
            if (touch) map.Updated = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Directory.CreateDirectory(_folder);
            string file = PathOf(map.Id), tmp = file + ".tmp";
            File.WriteAllText(tmp, JsonUtility.ToJson(map));
            if (File.Exists(file)) File.Delete(file);
            File.Move(tmp, file);
        }

        public void Delete(string id)
        {
            string file = PathOf(id);
            if (file != null && File.Exists(file)) File.Delete(file);
        }

        /// <summary>Новое имя «Карта N», которого ещё нет в списке.</summary>
        public static string FreeName(IEnumerable<MapData> maps, string word)
        {
            var taken = new HashSet<string>();
            foreach (var m in maps) taken.Add(m.Name);
            for (int i = 1; ; i++)
            {
                string name = $"{word} {i}";
                if (!taken.Contains(name)) return name;
            }
        }

        public static string NewId() => Guid.NewGuid().ToString("N").Substring(0, 12);

        string PathOf(string id)
        {
            if (string.IsNullOrEmpty(id) || id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || id.Contains("..")) return null;
            return Path.Combine(_folder, id + ".json");
        }

        static MapData Read(string file)
        {
            try
            {
                var map = JsonUtility.FromJson<MapData>(File.ReadAllText(file));
                if (map == null || map.Version > MapData.CurrentVersion) return null;
                map.Pieces ??= new List<MapPiece>();
                map.Platforms ??= new List<MapStrip>();
                map.Start ??= new MapStrip();
                map.Target ??= new MapStrip();
                map.Id = Path.GetFileNameWithoutExtension(file);
                return map;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
