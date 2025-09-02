using System;
using System.Collections.Generic;
using System.Linq;
using NX_TOOL_MANAGER.Models;


namespace NX_TOOL_MANAGER
{
    /// <summary>
    /// Parses NX ASCII holder_database.dat files into a DatDocument.
    /// - Top-level FORMAT/DATA (no CLASS) becomes a synthetic class "HOLDER_INDEX" (RTYPE=1 rows).
    /// - Subsequent "# CLASS ..." sections contain RTYPE=2 "step" rows for preview.
    /// </summary>
    public static class HolderDatParser
    {
        private const string INDEX_CLASS = "HOLDER_INDEX";

        public static DatDocument Parse(IEnumerable<string> lines)
        {
            var doc = new DatDocument();

            DatClass curClass = null;   // current class (either INDEX or a real CLASS)
            DatRow curRow = null;       // current DATA row being accumulated
            bool inFormat = false;
            bool inData = false;
            int lineNo = 0;

            foreach (var raw in lines)
            {
                lineNo++;
                var trimmed = raw.TrimStart();

                // Normalize lines that start with '#' so we can match keywords like CLASS/FORMAT/DATA/END_DATA
                var noHash = trimmed.StartsWith("#")
                    ? trimmed.TrimStart('#', ' ', '\t')
                    : trimmed;

                // --------------------------
                // CLASS markers ("#CLASS Foo", "# CLASS Foo", "CLASS Foo")
                // --------------------------
                if (noHash.StartsWith("CLASS", StringComparison.OrdinalIgnoreCase))
                {
                    var after = noHash.Substring("CLASS".Length).Trim();
                    curClass = new DatClass { Name = string.IsNullOrWhiteSpace(after) ? "UNNAMED" : after };
                    doc.Classes.Add(curClass);
                    inFormat = inData = false;
                    curRow = null;
                    continue;
                }

                // --------------------------
                // END_DATA section delimiter (resets state)
                // --------------------------
                if (noHash.StartsWith("END_DATA", StringComparison.OrdinalIgnoreCase))
                {
                    inFormat = false;
                    inData = false;
                    curRow = null;
                    // Keep curClass; a following FORMAT may continue in same class
                    continue;
                }

                // --------------------------
                // Skip blanks and non-keyword comments
                // --------------------------
                if (string.IsNullOrWhiteSpace(trimmed) ||
                    trimmed.StartsWith("//") ||
                    (trimmed.StartsWith("#")
                     && !noHash.StartsWith("FORMAT", StringComparison.OrdinalIgnoreCase)
                     && !noHash.StartsWith("DATA", StringComparison.OrdinalIgnoreCase)
                     && !noHash.StartsWith("CLASS", StringComparison.OrdinalIgnoreCase)
                     && !noHash.StartsWith("END_DATA", StringComparison.OrdinalIgnoreCase)))
                {
                    if (curClass == null)
                        doc.Head.Add(raw);
                    continue;
                }

                // --------------------------
                // FORMAT line (field headers)
                // - Before any CLASS, we treat it as the INDEX class
                // --------------------------
                if (noHash.StartsWith("FORMAT", StringComparison.OrdinalIgnoreCase))
                {
                    if (curClass == null)
                    {
                        // Create synthetic INDEX class for the top-of-file block
                        curClass = new DatClass { Name = INDEX_CLASS };
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
                // DATA row
                // - Before any CLASS, DATA belongs to INDEX class
                // --------------------------
                if (noHash.StartsWith("DATA", StringComparison.OrdinalIgnoreCase))
                {
                    if (curClass == null)
                    {
                        curClass = new DatClass { Name = INDEX_CLASS };
                        doc.Classes.Add(curClass);
                    }

                    // If format is missing, keep going (we'll still capture values)
                    if (curClass.FormatFields.Count == 0)
                    {
                        // Not fatal — log if you want: Debug.WriteLine(...)
                    }

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
                // FORMAT continuation: more headers on following lines
                // --------------------------
                if (inFormat)
                {
                    bool added = false;
                    foreach (var token in TokensThatLookLikeFields(noHash))
                    {
                        curClass!.FormatFields.Add(token);
                        added = true;
                    }
                    if (!added) inFormat = false;
                    continue;
                }

                // --------------------------
                // DATA continuation: multi-line DATA using leading '|'
                // --------------------------
                if (inData && noHash.StartsWith("|"))
                {
                    curRow!.RawLines.Add(raw);
                    var more = SplitPipeKeepEmpties(noHash);
                    curRow.Values.AddRange(more);
                    MapToFields(curClass!, curRow);
                    continue;
                }

                // Nothing matched: exit any mode
                inFormat = false;
                inData = false;
            }

            return doc;
        }

        // ---------------------- Helpers you can use from your VM/UI ----------------------

        /// <summary>
        /// Returns just the "index" rows (RTYPE=1) that belong in the grid.
        /// </summary>
        public static IEnumerable<DatRow> GetIndexRows(DatDocument doc)
        {
            var index = doc.Classes.FirstOrDefault(c =>
                c.Name.Equals(INDEX_CLASS, StringComparison.OrdinalIgnoreCase) ||
                c.Name.Equals("GENERAL", StringComparison.OrdinalIgnoreCase)); // if you previously used "GENERAL"
            if (index == null) return Enumerable.Empty<DatRow>();

            // Filter to RTYPE == 1 (safe if field missing)
            return index.Rows.Where(r => r.Map.TryGetValue("RTYPE", out var rtype) && rtype == "1");
        }

        /// <summary>
        /// Returns all RTYPE=2 step rows across any class for a given LIBRF.
        /// Use this to drive your right-side preview.
        /// </summary>
        public static IEnumerable<DatRow> GetShapeRowsFor(DatDocument doc, string librf)
        {
            if (string.IsNullOrWhiteSpace(librf)) return Enumerable.Empty<DatRow>();
            var all = doc.Classes.Where(c => !c.Name.Equals(INDEX_CLASS, StringComparison.OrdinalIgnoreCase));
            return all.SelectMany(c => c.Rows)
                      .Where(r => r.Map.TryGetValue("RTYPE", out var rtype) && rtype == "2")
                      .Where(r => r.Map.TryGetValue("LIBRF", out var v) && string.Equals(v, librf, StringComparison.OrdinalIgnoreCase))
                      .OrderBy(r => ParseIntSafe(r, "SEQ"));
        }

        private static int ParseIntSafe(DatRow r, string field)
            => (r.Map.TryGetValue(field, out var v) && int.TryParse(v, out var n)) ? n : int.MinValue;

        // ---------------------- Core tokenization/mapping ----------------------

        private static List<string> SplitPipeKeepEmpties(string line)
        {
            var values = new List<string>();
            int i = 0;

            // Seek first pipe
            while (i < line.Length && line[i] != '|') i++;
            if (i >= line.Length) return values;

            while (i < line.Length)
            {
                if (line[i] != '|') break;
                i++; // skip pipe

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

            // Pad values if short
            if (row.Values.Count < fieldCount)
                row.Values.AddRange(Enumerable.Repeat(string.Empty, fieldCount - row.Values.Count));

            // Map by field; ignore extras
            for (int i = 0; i < fieldCount; i++)
            {
                var key = cls.FormatFields[i];
                var val = row.Values[i];
                row.Map[key] = val;
            }
        }
    }
}
