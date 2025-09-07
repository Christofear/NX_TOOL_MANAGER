using NX_TOOL_MANAGER.Models;
using System.IO;
using System.Linq;
using System.Text;

namespace NX_TOOL_MANAGER.Services
{
    public static class DatWriter
    {
        public static void Write(string path, DatDocument doc)
        {
            using (var writer = new StreamWriter(path, false, Encoding.UTF8))
            {
                // 1. Write the original file header
                foreach (var line in doc.Head)
                {
                    writer.WriteLine(line);
                }

                // 2. Write each class section, preserving all its original lines
                foreach (var cls in doc.Classes)
                {
                    foreach (var line in cls.PreClassLines) writer.WriteLine(line);

                    writer.WriteLine(cls.ClassLine);

                    foreach (var line in cls.PreFormatLines) writer.WriteLine(line);
                    foreach (var line in cls.FormatLines) writer.WriteLine(line);
                    foreach (var line in cls.PreDataLines) writer.WriteLine(line);

                    // 3. Write the data rows
                    foreach (var row in cls.Rows)
                    {
                        var values = cls.FormatFields.Select(key => row.Get(key)).ToList();

                        // FIX 1: If all values in a row are blank, skip writing it entirely.
                        if (values.All(string.IsNullOrWhiteSpace))
                        {
                            continue;
                        }

                        // FIX 2: Join the values with a pipe separator. This correctly places
                        // pipes *between* items and avoids an extra one at the end.
                        var lineContent = string.Join(" | ", values);

                        // For new rows, generate a new DATA line
                        if (row.RawLines.Count == 0 || string.IsNullOrWhiteSpace(row.RawLines.FirstOrDefault()))
                        {
                            // FIX 3: Intelligently determine the correct indentation from the class's FORMAT line.
                            string indent = "    "; // Default fallback indent
                            if (cls.FormatLines.Any())
                            {
                                var formatLine = cls.FormatLines[0];
                                indent = formatLine.Substring(0, formatLine.Length - formatLine.TrimStart().Length);
                            }
                            writer.WriteLine($"{indent}DATA | {lineContent}");
                        }
                        else // For existing rows, use their original indentation
                        {
                            var originalLine = row.RawLines[0];
                            var indent = new string(' ', originalLine.Length - originalLine.TrimStart().Length);
                            var newLine = $"{indent}DATA | {lineContent}";
                            writer.WriteLine(newLine);
                        }
                    }

                    foreach (var line in cls.PostDataLines) writer.WriteLine(line);
                }
            }
        }
    }
}

