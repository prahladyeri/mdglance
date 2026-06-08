/**
 * AppSettings.cs
 * 
 * @author Prahlad Yeri <prahladyeri@yahoo.com>
 * @license MIT
 * @date 2026-06-08
 */
using Newtonsoft.Json;
using System.IO;

namespace mdglance.Helpers
{
    internal class AppSettings
    {
        private static string _filePath = "settings.json";

        public string LastOpened { get; set; } = "";
        public int SplitterPosition { get; set; } = 310;

        public static AppSettings Load()
        {
            if (!File.Exists(_filePath))
                return new AppSettings();

            string content = File.ReadAllText(_filePath);
            return JsonConvert.DeserializeObject<AppSettings>(content);
        }

        public void Save() 
        {
            //TODO: Add validate section or method
            string json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(_filePath, json);
        }
    }
}
