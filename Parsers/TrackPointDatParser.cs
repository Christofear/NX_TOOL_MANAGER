using System;
using System.Collections.Generic;
using System.Linq;
using NX_TOOL_MANAGER.Models;

namespace NX_TOOL_MANAGER.Services
{
    /// <summary>
    /// Parses NX ASCII trackpoint_database.dat files into a structured object model.
    /// This structure is very similar to the tool_database.dat file.
    /// </summary>
    public static class TrackpointDatParser
    {
        private enum ParserState { BeforeFirstClass, InClassHeader, InFormat, InData, InClassFooter }

        public static DatDocument Parse(IEnumerable<string> lines)
        {
            var doc = new DatDocument();
            var state = ParserState.BeforeFirstClass;
            DatClass currentClass = null;
            bool unitsFound = false; // Trackpoints files typically don't have units, but we check just in case.

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                var trimmedUpper = trimmed.ToUpperInvariant();

                if (trimmedUpper.StartsWith("#CLASS") || trimmedUpper.StartsWith("CLASS"))
                {
                    if (currentClass != null)
                    {
                        state = ParserState.InClassFooter;
                    }

                    currentClass = new DatClass { ParentDocument = doc };
                    doc.Classes.Add(currentClass);

                    currentClass.ClassLine = line;
                    currentClass.Name = line.Substring(trimmedUpper.StartsWith("#CLASS") ? 6 : 5).Trim();
                    state = ParserState.InClassHeader;
                    continue;
                }

                if (state == ParserState.InClassHeader && trimmedUpper.StartsWith("FORMAT"))
                {
                    state = ParserState.InFormat;
                }
                else if (state == ParserState.InFormat && trimmedUpper.StartsWith("DATA"))
                {
                    state = ParserState.InData;
                }

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
                        var formatContent = trimmed.Substring("FORMAT".Length).Trim();
                        foreach (var token in TokensThatLookLikeFields(formatContent))
                            currentClass.FormatFields.Add(token);
                        state = ParserState.InClassFooter;
                        break;

                    case ParserState.InClassFooter:
                        if (trimmedUpper.StartsWith("DATA"))
                        {
                            state = ParserState.InData;
                            currentClass.PreDataLines.AddRange(currentClass.PostDataLines);
                            currentClass.PostDataLines.Clear();
                            goto case ParserState.InData;
                        }
                        else
                        {
                            currentClass.PostDataLines.Add(line);
                        }
                        break;

                    case ParserState.InData:
                        var newRow = new DatRow { ParentClass = currentClass };
                        newRow.RawLines.Add(line);

                        var after = trimmed.Substring("DATA".Length).TrimStart();
                        newRow.Values.AddRange(SplitPipeKeepEmpties(after));
                        MapToFields(currentClass, newRow);

                        currentClass.Rows.Add(newRow);
                        state = ParserState.InClassFooter;
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
