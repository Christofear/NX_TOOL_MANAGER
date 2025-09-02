using System;
using System.Collections.Generic;
using System.Linq;
using NX_TOOL_MANAGER.Models;


namespace NX_TOOL_MANAGER
{
    /// <summary>
    /// Parses NX ASCII shank_database.dat files into a DatDocument.
    /// Supports "#CLASS ..." (with or without a space after '#'),
    /// top-level FORMAT/DATA (by bucketing into a synthetic GENERAL class),
    /// and #END_DATA markers.
    /// </summary>
    public static class ShankDatParser
    {
        private const string GENERAL_CLASS_NAME = "GENERAL";

        public static DatDocument Parse(IEnumerable<string> lines)
        {
            var doc = new DatDocument();

            DatClass curClass = null;   // current class section (or GENERAL)
            DatRow curRow = null;       // current DATA row being collected
            bool inFormat = false;
            bool inData = false;
            int lineNo = 0;

            foreach (var raw in lines)
            {
                lineNo++;
                var trimmed = raw.TrimStart();

                // Normalize comment-led lines: drop leading '#' and whitespace
                var noHash = trimmed.StartsWith("#")
                    ? trimmed.TrimStart('#', ' ', '\t')
                    : trimmed;

                // --------------------------
                // CLASS lines
                // --------------------------
                if (noHash.StartsWith("CLASS", StringComparison.OrdinalIgnoreCase))
                {
                    var after = noHash.Substring("CLASS".Length).Trim();
                    curClass = new DatClass { Name = string.IsNullOrWhiteSpace(after) ? "UNNAMED" : after };
                    doc.Classes.Add(curClass);
                    inFormat = inData = false;
                    continue;
                }

                // --------------------------
                // END_DATA marker explicitly closes a block
                // --------------------------
                if (noHash.StartsWith("END_DATA", StringComparison.OrdinalIgnoreCase))
                {
                    inFormat = false;
                    inData = false;
                    curRow = null;
                    continue;
                }

                // --------------------------
                // Blank/comment lines
                // (But let FORMAT/DATA that are commented with # through via noHash)
                // --------------------------
                if (string.IsNullOrWhiteSpace(trimmed) ||
                    trimmed.StartsWith("//") ||
                    (trimmed.StartsWith("#") &&
                     !noHash.StartsWith("FORMAT", StringComparison.OrdinalIgnoreCase) &&
                     !noHash.StartsWith("DATA", StringComparison.OrdinalIgnoreCase)))
                {
                    if (curClass == null)
                        doc.Head.Add(raw);
                    continue;
                }

                // --------------------------
                // FORMAT line – create GENERAL class if none yet
                // --------------------------
                if (noHash.StartsWith("FORMAT", StringComparison.OrdinalIgnoreCase))
                {
                    if (curClass == null)
                    {
                        curClass = new DatClass { Name = GENERAL_CLASS_NAME };
                        doc.Classes.Add(curClass);
                    }

                    inFormat = true;
                    inData = false;

                    var after = noHash.Substring("FORMAT".Length).Trim();
                    foreach (var token in TokensThatLookLikeFields(after))
                        curClass.FormatFields.Add(token);

                    continue;
                }

                // --------------------------
                // DATA row – requires a class + prior FORMAT
                // --------------------------
                if (noHash.StartsWith("DATA", StringComparison.OrdinalIgnoreCase))
                {
                    if (curClass == null)
                    {
                        curClass = new DatClass { Name = GENERAL_CLASS_NAME };
                        doc.Classes.Add(curClass);
                    }
                    if (curClass.FormatFields.Count == 0)
                        throw new Exception($"DATA found before FORMAT at line {lineNo} in class '{curClass.Name}'.");

                    inData = true;
                    inFormat = false;

                    curRow = new DatRow();
                    curRow.RawLines.Add(raw);

                    var after = noHash.Substring("DATA".Length).TrimStart();
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
                    foreach (var token in TokensThatLookLikeFields(noHash))
                    {
                        curClass!.FormatFields.Add(token);
                        added = true;
                    }
                    if (!added)
                        inFormat = false;
                    continue;
                }

                // --------------------------
                // DATA continuation (additional '|' lines)
                // --------------------------
                if (inData && noHash.StartsWith("|"))
                {
                    curRow!.RawLines.Add(raw);

                    var more = SplitPipeKeepEmpties(noHash);
                    curRow.Values.AddRange(more);
                    MapToFields(curClass!, curRow);

                    continue;
                }

                // Fallback: leave special modes
                inFormat = false;
                inData = false;
            }

            return doc;
        }

        /// <summary>
        /// Split a pipe-delimited line, preserving empty values (e.g., trailing '|').
        /// Input should begin at the first '|' (e.g., the substring after "DATA").
        /// </summary>
        private static List<string> SplitPipeKeepEmpties(string line)
        {
            var values = new List<string>();
            int i = 0;

            // Skip to first pipe
            while (i < line.Length && line[i] != '|') i++;
            if (i >= line.Length) return values;

            while (i < line.Length)
            {
                if (line[i] != '|') break;
                i++; // skip '|'

                int start = i;
                while (i < line.Length && line[i] != '|') i++;
                var token = line.Substring(start, i - start).Trim();

                // Strip inline comments if present
                int slash = token.IndexOf("//", StringComparison.Ordinal);
                if (slash >= 0) token = token.Substring(0, slash).Trim();
                int hash = token.IndexOf('#');
                if (hash >= 0) token = token.Substring(0, hash).Trim();

                values.Add(token);
            }

            return values;
        }

        /// <summary>
        /// Heuristic: tokens that look like field names (A-Z, 0-9, underscore).
        /// Filters out prose that can appear in commented lines.
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

        private static bool IsFieldToken(string token)
        {
            return token.Length > 0 && token.All(c =>
                (c >= 'A' && c <= 'Z') ||
                (c >= '0' && c <= '9') ||
                c == '_');
        }

        /// <summary>
        /// Align a row to the class field count and rebuild the key->value map.
        /// </summary>
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
    }
}
