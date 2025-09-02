using System;
using System.Collections.Generic;
using System.Linq;
using NX_TOOL_MANAGER.Models;


namespace NX_TOOL_MANAGER
{
    /// <summary>
    /// Parses NX ASCII tool_database.dat files into a structured object model.
    /// </summary>
    public static class ToolDatParser
    {
        /// <summary>
        /// Parses lines into a DatDocument object.
        /// </summary>
        public static DatDocument Parse(IEnumerable<string> lines)
        {
            var doc = new DatDocument();

            DatClass curClass = null;   // current class section
            DatRow curRow = null;       // current DATA row
            bool inFormat = false;
            bool inData = false;
            int lineNo = 0;

            foreach (var raw in lines)
            {
                lineNo++;
                var trimmed = raw.TrimStart();

                // --------------------------
                // Handle #CLASS or CLASS
                // --------------------------
                if (trimmed.StartsWith("#CLASS", StringComparison.OrdinalIgnoreCase))
                {
                    curClass = new DatClass { Name = trimmed.Substring(6).Trim() };
                    doc.Classes.Add(curClass);
                    inFormat = inData = false;
                    continue;
                }
                if (trimmed.StartsWith("CLASS", StringComparison.OrdinalIgnoreCase))
                {
                    curClass = new DatClass { Name = trimmed.Substring(5).Trim() };
                    doc.Classes.Add(curClass);
                    inFormat = inData = false;
                    continue;
                }

                // --------------------------
                // Blank line or comment
                // --------------------------
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#") || trimmed.StartsWith("//"))
                {
                    if (curClass == null)
                        doc.Head.Add(raw);
                    continue;
                }

                // --------------------------
                // FORMAT line (field headers)
                // --------------------------
                if (trimmed.StartsWith("FORMAT", StringComparison.OrdinalIgnoreCase))
                {
                    // Ignore doc example FORMATs before any class
                    if (curClass == null)
                        continue;

                    inFormat = true;
                    inData = false;

                    var after = trimmed.Substring("FORMAT".Length).Trim();
                    foreach (var token in TokensThatLookLikeFields(after))
                        curClass.FormatFields.Add(token);

                    continue;
                }

                // --------------------------
                // DATA row (tool entry)
                // --------------------------
                if (trimmed.StartsWith("DATA", StringComparison.OrdinalIgnoreCase))
                {
                    if (curClass == null)
                        throw new Exception($"DATA found before CLASS at line {lineNo}.");
                    if (curClass.FormatFields.Count == 0)
                        throw new Exception($"DATA found before FORMAT at line {lineNo} in class '{curClass.DisplayName}'.");

                    inData = true;
                    inFormat = false;

                    curRow = new DatRow();
                    curRow.RawLines.Add(raw);

                    var after = trimmed.Substring("DATA".Length).TrimStart();
                    if (!after.StartsWith("|"))
                        throw new Exception($"Line {lineNo}: DATA row must start with '|'.");

                    curRow.Values.AddRange(SplitPipeKeepEmpties(after));
                    MapToFields(curClass, curRow);
                    curClass.Rows.Add(curRow);

                    continue;
                }

                // --------------------------
                // FORMAT continuation
                // --------------------------
                if (inFormat)
                {
                    bool added = false;
                    foreach (var token in TokensThatLookLikeFields(trimmed))
                    {
                        curClass!.FormatFields.Add(token);
                        added = true;
                    }
                    if (!added)
                        inFormat = false; // stop if no fields detected
                    continue;
                }

                // --------------------------
                // DATA continuation
                // --------------------------
                if (inData && trimmed.StartsWith("|"))
                {
                    curRow!.RawLines.Add(raw);

                    var more = SplitPipeKeepEmpties(trimmed);
                    curRow.Values.AddRange(more);
                    MapToFields(curClass!, curRow);

                    continue;
                }

                // If none of the above, stop current mode
                inFormat = false;
                inData = false;
            }

            return doc;
        }

        /// <summary>
        /// Splits a pipe-delimited line, preserving empty values.
        /// </summary>
        private static List<string> SplitPipeKeepEmpties(string line)
        {
            var values = new List<string>();
            int i = 0;

            // Skip leading text until first pipe
            while (i < line.Length && line[i] != '|') i++;
            if (i >= line.Length) return values;

            while (i < line.Length)
            {
                if (line[i] != '|') break;
                i++; // skip pipe

                int start = i;
                while (i < line.Length && line[i] != '|') i++;
                var token = line.Substring(start, i - start).Trim();

                // Strip inline comment markers if any (rare)
                int hash = token.IndexOf('#');
                int slash = token.IndexOf("//", StringComparison.Ordinal);
                if (hash >= 0) token = token.Substring(0, hash).Trim();
                if (slash >= 0) token = token.Substring(0, slash).Trim();

                values.Add(token);
            }

            return values;
        }

        /// <summary>
        /// Identifies valid field tokens (A-Z, 0-9, underscore only).
        /// Filters out words from documentation prose.
        /// </summary>
        private static IEnumerable<string> TokensThatLookLikeFields(string text)
        {
            var parts = text.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                if (IsFieldToken(part))
                    yield return part.Trim();
            }
        }

        /// <summary>
        /// Checks if token is uppercase letters/numbers/underscore only.
        /// </summary>
        private static bool IsFieldToken(string token)
        {
            return token.Length > 0 && token.All(c =>
                (c >= 'A' && c <= 'Z') ||
                (c >= '0' && c <= '9') ||
                c == '_');
        }

        /// <summary>
        /// Aligns a row's values to the class's field count and rebuilds the map.
        /// </summary>
        private static void MapToFields(DatClass cls, DatRow row)
        {
            int fieldCount = cls.FormatFields.Count;

            // Pad missing values with empty strings
            if (row.Values.Count < fieldCount)
                row.Values.AddRange(Enumerable.Repeat(string.Empty, fieldCount - row.Values.Count));

            // Map values by field name (extras ignored)
            for (int i = 0; i < fieldCount; i++)
            {
                var key = cls.FormatFields[i];
                var val = row.Values[i];
                row.Map[key] = val;
            }
        }
    }
}
