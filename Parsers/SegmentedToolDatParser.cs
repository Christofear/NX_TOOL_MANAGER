using System;
using System.Collections.Generic;
using System.Linq;
using NX_TOOL_MANAGER.Models;

namespace NX_TOOL_MANAGER.Services
{
    /// <summary>
    /// Parses NX ASCII segmented_tool_database.dat files into a structured object model.
    /// This parser correctly handles CLASS, FORMAT, DATA, and END_DATA blocks.
    /// </summary>
    public static class SegmentedToolDatParser
    {
        private enum ParserState { BeforeFirstClass, InClassHeader, InFormat, InData, InClassFooter }

        public static DatDocument Parse(IEnumerable<string> lines)
        {
            var doc = new DatDocument();
            var state = ParserState.BeforeFirstClass;
            DatClass currentClass = null;
            DatRow currentRow = null;
            bool unitsFound = false;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                var trimmedUpper = trimmed.ToUpperInvariant();

                // Normalize commented lines to find keywords
                var noHash = trimmed.StartsWith("#") ? trimmed.TrimStart('#', ' ', '\t') : trimmed;
                var noHashUpper = noHash.ToUpperInvariant();

                // --- Handle State Transitions ---

                if (noHashUpper.StartsWith("CLASS"))
                {
                    currentClass = new DatClass { ParentDocument = doc };
                    doc.Classes.Add(currentClass);

                    currentClass.ClassLine = line;
                    currentClass.Name = noHash.Substring(5).Trim();
                    state = ParserState.InClassHeader;
                    continue;
                }

                if (noHashUpper.StartsWith("END_DATA"))
                {
                    state = ParserState.InClassFooter;
                    if (currentClass != null) currentClass.PostDataLines.Add(line);
                    continue;
                }

                if (state == ParserState.InClassHeader && noHashUpper.StartsWith("FORMAT"))
                {
                    state = ParserState.InFormat;
                }
                else if ((state == ParserState.InFormat || state == ParserState.InClassFooter) && noHashUpper.StartsWith("DATA"))
                {
                    state = ParserState.InData;
                }

                // --- Process line based on current state ---
                switch (state)
                {
                    case ParserState.BeforeFirstClass:
                        doc.Head.Add(line);
                        if (!unitsFound && trimmed.StartsWith("#"))
                        {
                            var commentContent = trimmed.TrimStart('#', ' ');
                            if (commentContent.StartsWith("Unit", StringComparison.OrdinalIgnoreCase))
                            {
                                var parts = commentContent.Split(':');
                                if (parts.Length > 1)
                                {
                                    doc.Units = parts[1].Trim();
                                    unitsFound = true;
                                }
                            }
                        }
                        break;

                    case ParserState.InClassHeader:
                        currentClass.PreFormatLines.Add(line);
                        break;

                    case ParserState.InFormat:
                        currentClass.FormatLines.Add(line);
                        var formatContent = noHash.Substring("FORMAT".Length).Trim();
                        foreach (var token in TokensThatLookLikeFields(formatContent))
                            currentClass.FormatFields.Add(token);
                        state = ParserState.InClassFooter;
                        break;

                    case ParserState.InClassFooter:
                        if (currentClass != null)
                        {
                            currentClass.PostDataLines.Add(line);
                        }
                        break;

                    case ParserState.InData:
                        currentRow = new DatRow { ParentClass = currentClass };
                        currentRow.RawLines.Add(line);

                        var after = noHash.Substring("DATA".Length).TrimStart();
                        currentRow.Values.AddRange(SplitPipeKeepEmpties(after));
                        MapToFields(currentClass, currentRow);

                        currentClass.Rows.Add(currentRow);
                        state = ParserState.InClassFooter; // After a DATA line, subsequent lines are footers until next DATA/CLASS
                        break;
                }
            }
            return doc;
        }

        #region Helper Methods
        private static List<string> SplitPipeKeepEmpties(string line)
        {
            return line.Trim('|').Split('|').Select(s => s.Trim()).ToList();
        }

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
                (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_');
        }

        private static void MapToFields(DatClass cls, DatRow row)
        {
            for (int i = 0; i < cls.FormatFields.Count; i++)
            {
                var key = cls.FormatFields[i];
                var val = (i < row.Values.Count) ? row.Values[i] : string.Empty;
                row.Map[key] = val;
            }
        }
        #endregion
    }
}

