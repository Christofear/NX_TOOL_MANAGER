using System;
using System.Collections.Generic;
using System.Linq;
using NX_TOOL_MANAGER.Models;

namespace NX_TOOL_MANAGER
{
    /// <summary>
    /// Parses NX ASCII shank_database.dat files into a DatDocument.
    /// This parser is now data-driven, sorting rows into dedicated classes
    /// based on their RTYPE value (1 for index, 2 for shape).
    /// </summary>
    public static class ShankDatParser
    {
        private const string INDEX_CLASS_NAME = "SHANK_INDEX";
        private const string SHAPE_CLASS_NAME = "SHANK_SHAPE";

        public static DatDocument Parse(IEnumerable<string> lines)
        {
            var doc = new DatDocument();

            // Create the two dedicated classes that will hold our sorted data.
            var indexClass = new DatClass { Name = INDEX_CLASS_NAME, ParentDocument = doc };
            var shapeClass = new DatClass { Name = SHAPE_CLASS_NAME, ParentDocument = doc };

            doc.Classes.Add(indexClass);
            doc.Classes.Add(shapeClass);

            List<string> currentFormatFields = null;
            int lineNo = 0;

            foreach (var raw in lines)
            {
                lineNo++;
                var trimmed = raw.TrimStart();
                var noHash = trimmed.StartsWith("#") ? trimmed.TrimStart('#', ' ', '\t') : trimmed;

                // Capture header comments before any data processing
                if (currentFormatFields == null && (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#")))
                {
                    doc.Head.Add(raw);
                    continue;
                }

                // FORMAT line: This defines the structure for the DATA lines that follow.
                if (noHash.StartsWith("FORMAT", StringComparison.OrdinalIgnoreCase))
                {
                    currentFormatFields = new List<string>();
                    var after = noHash.Substring("FORMAT".Length).Trim();
                    foreach (var token in TokensThatLookLikeFields(after))
                        currentFormatFields.Add(token);
                    continue;
                }

                // DATA line: This is where we determine which class the row belongs to.
                if (noHash.StartsWith("DATA", StringComparison.OrdinalIgnoreCase))
                {
                    if (currentFormatFields == null)
                        throw new Exception($"Line {lineNo}: DATA found before any FORMAT declaration.");

                    var after = noHash.Substring("DATA".Length).TrimStart();
                    if (!after.StartsWith("|"))
                        throw new Exception($"Line {lineNo}: DATA row must start with '|'.");

                    var values = SplitPipeKeepEmpties(after);

                    // Find the index of the RTYPE column in the current format.
                    int rtypeIndex = currentFormatFields.FindIndex(f => f.Equals("RTYPE", StringComparison.OrdinalIgnoreCase));
                    if (rtypeIndex == -1)
                        continue; // Skip data rows that don't have an RTYPE.

                    string rtypeValue = (rtypeIndex < values.Count) ? values[rtypeIndex] : null;

                    DatClass targetClass = null;
                    if (rtypeValue == "1")
                    {
                        targetClass = indexClass;
                    }
                    else if (rtypeValue == "2")
                    {
                        targetClass = shapeClass;
                    }

                    if (targetClass != null)
                    {
                        // If this is the first row for this class, assign the format fields.
                        if (targetClass.FormatFields.Count == 0)
                        {
                            targetClass.FormatFields.AddRange(currentFormatFields);
                        }

                        var newRow = new DatRow { ParentClass = targetClass };
                        newRow.RawLines.Add(raw);
                        newRow.Values.AddRange(values);
                        MapToFields(targetClass, newRow);
                        targetClass.Rows.Add(newRow);
                    }
                }
            }

            return doc;
        }

        #region Unchanged Helper Methods
        public static IEnumerable<DatRow> GetIndexRows(DatDocument doc)
        {
            return doc.Classes.FirstOrDefault(c => c.Name.Equals(INDEX_CLASS_NAME, StringComparison.OrdinalIgnoreCase))?.Rows
                   ?? Enumerable.Empty<DatRow>();
        }

        public static IEnumerable<DatRow> GetShapeRowsFor(DatDocument doc, string librf)
        {
            if (string.IsNullOrWhiteSpace(librf)) return Enumerable.Empty<DatRow>();

            var shapeRows = doc.Classes.FirstOrDefault(c => c.Name.Equals(SHAPE_CLASS_NAME, StringComparison.OrdinalIgnoreCase))?.Rows
                            ?? Enumerable.Empty<DatRow>();

            return shapeRows
                .Where(r => r.Map.TryGetValue("LIBRF", out var v) && string.Equals(v, librf, StringComparison.OrdinalIgnoreCase))
                .OrderBy(r => ParseIntSafe(r, "SEQ"));
        }

        private static int ParseIntSafe(DatRow r, string field)
            => (r.Map.TryGetValue(field, out var v) && int.TryParse(v, out var n)) ? n : int.MinValue;

        private static List<string> SplitPipeKeepEmpties(string line)
        {
            var values = new List<string>();
            int i = 0;
            while (i < line.Length && line[i] != '|') i++;
            if (i >= line.Length) return values;
            while (i < line.Length)
            {
                if (line[i] != '|') break;
                i++;
                int start = i;
                while (i < line.Length && line[i] != '|') i++;
                var token = line.Substring(start, i - start).Trim();
                int slash = token.IndexOf("//", StringComparison.Ordinal);
                if (slash >= 0) token = token.Substring(0, slash).Trim();
                int hash = token.IndexOf('#');
                if (hash >= 0) token = token.Substring(0, hash).Trim();
                values.Add(token);
            }
            return values;
        }

        private static IEnumerable<string> TokensThatLookLikeFields(string text)
        {
            var parts = text.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                if (IsFieldToken(part)) yield return part.Trim();
            }
        }

        private static bool IsFieldToken(string token)
        {
            return token.Length > 0 && token.All(c =>
                (c >= 'A' && c <= 'Z') ||
                (c >= '0' && c <= '9') ||
                c == '_');
        }

        private static void MapToFields(DatClass cls, DatRow row)
        {
            int fieldCount = cls.FormatFields.Count;
            if (row.Values.Count < fieldCount)
                row.Values.AddRange(Enumerable.Repeat(string.Empty, fieldCount - row.Values.Count));
            for (int i = 0; i < fieldCount; i++)
            {
                var key = cls.FormatFields[i];
                var val = row.Values[i];
                row.Map[key] = val;
            }
        }
        #endregion
    }
}

