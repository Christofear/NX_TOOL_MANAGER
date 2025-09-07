using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using NX_TOOL_MANAGER.Models;

namespace NX_TOOL_MANAGER.Services
{
    public class CategoryNode
    {
        public string Name { get; }
        public List<DatClass> Classes { get; } = new();
        public CategoryNode(string name) { Name = name; }
    }

    public static class CategoryService
    {
        private static readonly Dictionary<string, string> _classToCategoryMap = new(StringComparer.OrdinalIgnoreCase);
        // ADDED: A list to store the category order from the JSON file
        private static readonly List<string> _categoryOrder = new();

        static CategoryService()
        {
            LoadCategories();
        }

        private static void LoadCategories()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = "NX_TOOL_MANAGER.Resources.ClassCategories.json";

                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null) { /* Error logging is already handled in your MainWindow */ return; }
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        string json = reader.ReadToEnd();
                        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, AllowTrailingCommas = true };
                        var categoryFile = JsonSerializer.Deserialize<JsonElement>(json, options);

                        var categories = categoryFile.GetProperty("categories");
                        foreach (var categoryProperty in categories.EnumerateObject())
                        {
                            string categoryName = categoryProperty.Name;
                            // Store the category name to preserve the JSON order
                            _categoryOrder.Add(categoryName);

                            if (categoryProperty.Value.ValueKind == JsonValueKind.String)
                            {
                                _classToCategoryMap[categoryProperty.Value.GetString()] = categoryName;
                            }
                            else if (categoryProperty.Value.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var classElement in categoryProperty.Value.EnumerateArray())
                                {
                                    string className = classElement.GetString();
                                    if (!string.IsNullOrEmpty(className))
                                    {
                                        _classToCategoryMap[className] = categoryName;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { /* Error logging is already handled */ }
        }

        public static List<CategoryNode> GroupClassesIntoCategories(List<DatClass> classes)
        {
            var categoryMap = new Dictionary<string, CategoryNode>();
            var otherCategory = new CategoryNode("Other");

            foreach (var cls in classes)
            {
                if (_classToCategoryMap.TryGetValue(cls.Name, out var categoryName))
                {
                    if (!categoryMap.ContainsKey(categoryName))
                    {
                        categoryMap[categoryName] = new CategoryNode(categoryName);
                    }
                    categoryMap[categoryName].Classes.Add(cls);
                }
                else
                {
                    otherCategory.Classes.Add(cls);
                }
            }

            // UPDATED: Build the result list based on the stored order, not alphabetical.
            var result = new List<CategoryNode>();
            foreach (var categoryName in _categoryOrder)
            {
                if (categoryMap.TryGetValue(categoryName, out var node))
                {
                    result.Add(node);
                }
            }

            if (otherCategory.Classes.Any())
            {
                result.Add(otherCategory);
            }

            return result;
        }
    }
}

