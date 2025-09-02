using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace NX_TOOL_MANAGER.Services
{
    public class FieldDefinition
    {
        public string Description { get; set; }
        public string Type { get; set; }
        public bool Nullable { get; set; } // ADDED: Property to hold nullability
    }

    public class FieldDefinitionsFile
    {
        public Dictionary<string, FieldDefinition> Fields { get; set; }
    }

    public static class FieldManager
    {
        private static readonly Dictionary<string, FieldDefinition> _fields = new Dictionary<string, FieldDefinition>();

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
                    if (stream == null) return;
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        string json = reader.ReadToEnd();
                        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        var definitionsFile = JsonSerializer.Deserialize<FieldDefinitionsFile>(json, options);

                        if (definitionsFile?.Fields != null)
                        {
                            _fields.Clear();
                            foreach (var field in definitionsFile.Fields)
                            {
                                _fields[field.Key] = field.Value;
                            }
                        }
                    }
                }
            }
            catch (System.Exception) { /* Handle exceptions */ }
        }

        public static FieldDefinition GetDefinition(string key)
        {
            _fields.TryGetValue(key, out var definition);
            return definition ?? new FieldDefinition { Description = key, Type = "string", Nullable = true };
        }
    }
}

