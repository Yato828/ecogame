using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Wattshift
{
    [Serializable] public sealed class CampaignSave
    {
        public int Version = 3;
        public int MissionIndex;
        public CityState City;
        public CampaignRecord[] Records = Array.Empty<CampaignRecord>();
        public string[] InvestigatedIds = Array.Empty<string>();
        public bool ScannerEnabled;
        public bool Results;
        public float SpeedMultiplier = 1;
    }
    public sealed class CampaignSaveService
    {
        [Serializable] sealed class Header { public int Version; }
        public string FilePath { get; }
        public CampaignSaveService(string path) { FilePath = path; }
        public bool Exists => File.Exists(FilePath);
        public bool Save(CampaignSave state, out string message)
        {
            string temp = FilePath + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(FilePath)));
                byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(state, true));
                using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None)) { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                if (Exists) File.Replace(temp, FilePath, FilePath + ".bak"); else File.Move(temp, FilePath);
                message = "Смена и прогресс сохранены."; return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
            { message = "Не удалось сохранить: " + e.Message; return false; }
            finally { try { if (File.Exists(temp)) File.Delete(temp); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
        }
        public bool Load(out CampaignSave state, out string message)
        {
            state = null;
            try
            {
                if (!Exists) { message = "Сохранённая смена не найдена."; return false; }
                if (new FileInfo(FilePath).Length > 4 * 1024 * 1024) { message = "Слишком большой файл сохранения."; return false; }
                string text = File.ReadAllText(FilePath); Header header = JsonUtility.FromJson<Header>(text);
                if (header == null || header.Version != 3) { message = "Неподдерживаемая версия кампании."; return false; }
                state = JsonUtility.FromJson<CampaignSave>(text);
                if (state == null) { message = "Сохранение повреждено."; return false; }
                message = "Сохранение прочитано."; return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
            { message = "Не удалось прочитать: " + e.Message; return false; }
        }
    }
}
