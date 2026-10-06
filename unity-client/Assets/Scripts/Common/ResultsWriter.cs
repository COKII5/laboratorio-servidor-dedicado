using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Lab.Common
{
    /// <summary>
    /// Guarda los resultados de los experimentos como CSV (coma como separador, punto decimal)
    /// en Application.persistentDataPath/Resultados.
    /// </summary>
    public static class ResultsWriter
    {
        public static string Folder => Path.Combine(Application.persistentDataPath, "Resultados");

        public static string Timestamp() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        /// <summary>Formato independiente del idioma del sistema (en es-CO el decimal sería coma).</summary>
        public static string Number(double value) => value.ToString("F3", CultureInfo.InvariantCulture);

        public static string AppendRow(string fileName, string header, string row) =>
            AppendRows(fileName, header, new[] { row });

        /// <summary>Agrega filas al archivo; si no existe, escribe primero el encabezado. Devuelve la ruta.</summary>
        public static string AppendRows(string fileName, string header, IEnumerable<string> rows)
        {
            Directory.CreateDirectory(Folder);
            string path = Path.Combine(Folder, fileName);
            bool isNew = !File.Exists(path);

            using (var writer = new StreamWriter(path, append: true, new UTF8Encoding(false)))
            {
                if (isNew) writer.WriteLine(header);
                foreach (string row in rows) writer.WriteLine(row);
            }

            return path;
        }
    }
}
