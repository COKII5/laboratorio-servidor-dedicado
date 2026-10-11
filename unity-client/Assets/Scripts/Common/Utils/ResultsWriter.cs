using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Lab.Common
{
    public static class ResultsWriter
    {
        public static string Folder => Path.Combine(Application.persistentDataPath, "Resultados");

        public static string Timestamp() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        // Con la configuración es-CO el separador decimal sería la coma, que rompe el CSV.
        public static string Number(double value) => value.ToString("F3", CultureInfo.InvariantCulture);

        public static void AppendRow(string fileName, string header, string row) =>
            AppendRows(fileName, header, new[] { row });

        public static void AppendRows(string fileName, string header, IEnumerable<string> rows)
        {
            string path = Path.Combine(Folder, fileName);
            try
            {
                Directory.CreateDirectory(Folder);
                bool isNewFile = !File.Exists(path);
                using (var writer = new StreamWriter(path, append: true, new UTF8Encoding(false)))
                {
                    if (isNewFile) writer.WriteLine(header);
                    foreach (string row in rows) writer.WriteLine(row);
                }
                Debug.Log($"[Resultados] Guardado en {path}");
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogError($"[Resultados] No se pudo escribir {path}: {exception.Message}");
            }
        }
    }
}
