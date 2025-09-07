using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace NX_TOOL_MANAGER.Services
{
    // The classes that model the JSON structure remain the same
    public class CategoryDefinition
    {
        public int DisplayOrder { get; set; } = 999;
        public bool DefaultExpanded { get; set; } = false;
        public bool Visible { get; set; } = true;
    }

    public class FieldDefinition
    {
        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = "string";
        public bool Nullable { get; set; }
        public string Category { get; set; } = "General";
        public bool Required { get; set; }
        public bool Visible { get; set; } = true;
    }

    // THE FIX: This class now correctly expects "categories" and "fields"
    // to be two separate, top-level properties in the JSON file.
    public class FieldDefinitionsFile
    {
        public Dictionary<string, CategoryDefinition> Categories { get; set; }
        public Dictionary<string, FieldDefinition> Fields { get; set; }
    }


    public static class FieldManager
    {
        private static readonly Dictionary<string, FieldDefinition> _fields = new Dictionary<string, FieldDefinition>(System.StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, CategoryDefinition> _categorySettings = new Dictionary<string, CategoryDefinition>(System.StringComparer.OrdinalIgnoreCase);
        private static readonly CategoryDefinition _defaultCategorySettings = new CategoryDefinition();

        static FieldManager()
        {
            LoadDefinitions();
        }

        private static void LoadDefinitions()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = "NX_TOOL_MANAGER.Resources.ToolFields.json";

                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error: Embedded resource '{resourceName}' not found.");
                        return;
                    }
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        string json = reader.ReadToEnd();
                        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                        // The parsing logic is now much simpler because the JSON is properly structured.
                        var definitionsFile = JsonSerializer.Deserialize<FieldDefinitionsFile>(json, options);

                        if (definitionsFile?.Fields != null)
                        {
                            _fields.Clear();
                            foreach (var field in definitionsFile.Fields)
                            {
                                _fields[field.Key] = field.Value;
                            }
                        }

                        if (definitionsFile?.Categories != null)
                        {
                            _categorySettings.Clear();
                            foreach (var category in definitionsFile.Categories)
                            {
                                _categorySettings[category.Key] = category.Value;
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fatal error loading field definitions: {ex.Message}");
            }
        }

        public static FieldDefinition GetDefinition(string key)
        {
            _fields.TryGetValue(key ?? string.Empty, out var definition);
            return definition;
        }

        public static CategoryDefinition GetCategorySettings(string categoryName)
        {
            _categorySettings.TryGetValue(categoryName ?? string.Empty, out var settings);
            return settings ?? _defaultCategorySettings;
        }
    }
}

