using NX_TOOL_MANAGER.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace NX_TOOL_MANAGER.Services
{
    public class CategoryNode
    {
        public string Name { get; }
        public List<DatClass> Classes { get; } = new();

        public CategoryNode(string name)
        {
            Name = string.IsNullOrWhiteSpace(name) ? "(Unnamed Category)" : name;
        }
    }

    public static class CategoryService
    {
        private static Dictionary<string, string> _classToCategoryMap;
        private static Dictionary<string, string> _classToUINameMap;
        private static Dictionary<string, (int Type, int Subtype)> _classToTypeMap;
        private static List<string> _categoryOrder;
        private static List<string> _toolClassOrder;
        private static string _lastLoadedFilePath;

        /// <summary>
        /// Checks if a given tool class name corresponds to a special type that requires the dual-view layout.
        /// </summary>
        public static bool IsSpecialToolType(string className)
        {
            LoadMappingFromDefFile(); // Ensure the latest def file is parsed.
            if (_classToTypeMap != null && _classToTypeMap.TryGetValue(className, out var types))
            {
                // This is where we define the special types based on your rules.
                bool isType3Subtype4 = (types.Type == 3 && types.Subtype == 4);
                bool isType2Subtype51 = (types.Type == 2 && types.Subtype == 51);

                return isType3Subtype4 || isType2Subtype51;
            }
            return false;
        }

        public static List<CategoryNode> GroupClassesIntoCategories(List<DatClass> datClasses)
        {
            LoadMappingFromDefFile();

            if (_classToCategoryMap == null || !_classToCategoryMap.Any())
            {
                var flatNode = new CategoryNode("All Tools");
                flatNode.Classes.AddRange(datClasses);
                return new List<CategoryNode> { flatNode };
            }

            var categoryNodes = new Dictionary<string, CategoryNode>();
            var unclassifiedClasses = new List<DatClass>();

            foreach (var datClass in datClasses)
            {
                if (_classToCategoryMap.TryGetValue(datClass.Name, out var categoryName))
                {
                    if (!categoryNodes.TryGetValue(categoryName, out var node))
                    {
                        node = new CategoryNode(categoryName);
                        categoryNodes[categoryName] = node;
                    }
                    node.Classes.Add(datClass);
                }
                else
                {
                    unclassifiedClasses.Add(datClass);
                }
            }

            var result = _categoryOrder
                .Where(categoryNodes.ContainsKey)
                .Select(catName => categoryNodes[catName])
                .ToList();

            foreach (var node in result)
            {
                node.Classes.Sort((a, b) =>
                    _toolClassOrder.IndexOf(a.Name).CompareTo(_toolClassOrder.IndexOf(b.Name)));
            }

            if (unclassifiedClasses.Any())
            {
                var otherCategory = new CategoryNode("Other");
                otherCategory.Classes.AddRange(unclassifiedClasses);
                result.Add(otherCategory);
            }

            return result;
        }

        public static string GetUINameForClass(string className)
        {
            LoadMappingFromDefFile();
            _classToUINameMap.TryGetValue(className, out var uiName);
            return uiName;
        }

        public static void LoadMappingFromDefFile()
        {
            string defFilePath = Properties.Settings.Default.ToolsDefPath;

            if (string.Equals(defFilePath, _lastLoadedFilePath) && _classToCategoryMap != null)
            {
                return;
            }

            _classToCategoryMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _classToUINameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _classToTypeMap = new Dictionary<string, (int Type, int Subtype)>(StringComparer.OrdinalIgnoreCase);
            _categoryOrder = new List<string>();
            _toolClassOrder = new List<string>();
            _lastLoadedFilePath = defFilePath;

            if (string.IsNullOrEmpty(defFilePath) || !File.Exists(defFilePath)) return;

            try
            {
                var lines = File.ReadAllLines(defFilePath);
                int braceLevel = 0;
                string currentCategoryUIName = null;
                int? currentType = null;

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    string trimmedLine = line.Trim();

                    if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("#")) continue;

                    // Track brace level to understand the hierarchy
                    if (line.Contains("{")) braceLevel++;
                    if (line.Contains("}")) braceLevel--;

                    // Level 1: A main tool category (e.g., MILLING)
                    if (braceLevel == 1 && trimmedLine.StartsWith("CLASS", StringComparison.OrdinalIgnoreCase))
                    {
                        currentCategoryUIName = FindNextUIName(lines, i + 1, 1);
                        if (currentCategoryUIName != null && !_categoryOrder.Contains(currentCategoryUIName))
                        {
                            _categoryOrder.Add(currentCategoryUIName);
                        }
                        // Find the Type for this entire category
                        currentType = FindQueryValue(lines, i + 1, 1, "Type");
                    }
                    // Level 2: A specific tool class (e.g., END_MILL_NON_INDEXABLE)
                    else if (braceLevel == 2 && trimmedLine.StartsWith("CLASS", StringComparison.OrdinalIgnoreCase))
                    {
                        string currentToolClassName = GetValueFromLine(trimmedLine);
                        if (currentToolClassName != null && currentCategoryUIName != null && currentType.HasValue)
                        {
                            _classToCategoryMap[currentToolClassName] = currentCategoryUIName;
                            _toolClassOrder.Add(currentToolClassName);

                            string toolUIName = FindNextUIName(lines, i + 1, 2);
                            if (toolUIName != null)
                            {
                                _classToUINameMap[currentToolClassName] = toolUIName;
                            }

                            // Find the SubType for this specific class
                            int? currentSubType = FindQueryValue(lines, i + 1, 2, "SubType");
                            if (currentSubType.HasValue)
                            {
                                // Store the combination of the parent Type and this class's SubType
                                _classToTypeMap[currentToolClassName] = (currentType.Value, currentSubType.Value);
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                _classToCategoryMap.Clear(); _classToUINameMap.Clear(); _classToTypeMap.Clear();
                _categoryOrder.Clear(); _toolClassOrder.Clear();
            }
        }

        /// <summary>
        /// Searches within a class definition for a QUERY line and extracts the value for a given key (Type or SubType).
        /// </summary>
        private static int? FindQueryValue(string[] lines, int startIndex, int expectedBraceLevel, string keyToFind)
        {
            int braceLevel = expectedBraceLevel;
            // Regex to capture the key (e.g., SubType) and the value (e.g., 01) from a QUERY line
            var regex = new Regex(@"\[DB\((\w+)\)\]\s*==\s*\[(\d+)\]", RegexOptions.IgnoreCase);

            for (int i = startIndex; i < lines.Length; i++)
            {
                string line = lines[i].Trim();

                if (line.StartsWith("QUERY", StringComparison.OrdinalIgnoreCase))
                {
                    var match = regex.Match(line);
                    if (match.Success)
                    {
                        string key = match.Groups[1].Value;
                        string value = match.Groups[2].Value;

                        if (key.Equals(keyToFind, StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out int parsedValue))
                        {
                            return parsedValue; // Found it
                        }
                    }
                }

                if (line.Contains("{")) braceLevel++;
                if (line.Contains("}")) braceLevel--;
                if (braceLevel < expectedBraceLevel) break; // We've exited the scope of the class definition
            }
            return null; // Not found
        }

        private static string FindNextUIName(string[] lines, int startIndex, int expectedBraceLevel)
        {
            int braceLevel = expectedBraceLevel;
            for (int i = startIndex; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.StartsWith("UI_NAME", StringComparison.OrdinalIgnoreCase))
                {
                    return CleanUIName(GetValueFromLine(line));
                }
                if (line.Contains("{")) braceLevel++;
                if (line.Contains("}")) braceLevel--;
                if (braceLevel < expectedBraceLevel) return null;
            }
            return null;
        }

        private static string GetValueFromLine(string line)
        {
            if (line.Contains("\""))
            {
                var match = Regex.Match(line, "\"(.*?)\"");
                return match.Success ? match.Groups[1].Value : null;
            }
            var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 1 ? parts[1].Trim(';') : null;
        }

        private static string CleanUIName(string name)
        {
            if (name == null) return string.Empty;
            return Regex.Replace(name, @"^##\d{2}", "").Trim();
        }
    }
}

