using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Wattshift
{
    /// <summary>Versioned city state storage. Restore validation belongs to the deterministic service.</summary>
    public sealed class SaveService
    {
        [Serializable] sealed class Header { public int Version; }
        public string FilePath { get; }
        public SaveService(string filePath) { FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath)); }

        public bool TrySave(CityState state, out string message)
        {
            string temporary = FilePath + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(FilePath)));
                byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(state, true));
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                if (File.Exists(FilePath)) File.Replace(temporary, FilePath, FilePath + ".bak");
                else File.Move(temporary, FilePath);
                message = "Прогресс сохранён."; return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
            { message = "Не удалось сохранить прогресс: " + e.Message; return false; }
            finally { try { if (File.Exists(temporary)) File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
        }

        public bool TryLoad(out CityState state, out string message)
        {
            state = null;
            try
            {
                if (!File.Exists(FilePath)) { message = "Сохранение пока не найдено."; return false; }
                if (new FileInfo(FilePath).Length > 1024 * 1024) { message = "Файл сохранения слишком большой."; return false; }
                string json = File.ReadAllText(FilePath, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith("{"))
                { message = "Сохранение повреждено: ожидается JSON-объект."; return false; }
                Header header = JsonUtility.FromJson<Header>(json);
                if (header == null || header.Version != 1)
                { message = "Неподдерживаемая или отсутствующая версия сохранения."; return false; }
                state = JsonUtility.FromJson<CityState>(json);
                if (state == null) { message = "Сохранение повреждено."; return false; }
                message = "Сохранение прочитано."; return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
            { message = "Не удалось прочитать сохранение: " + e.Message; return false; }
        }
    }
}
