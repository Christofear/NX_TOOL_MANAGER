namespace NX_TOOL_MANAGER.Services
{
    public static class SettingsManager
    {
        /// <summary>
        /// Gets the path to the tool definition file directly from the application's settings,
        /// which are managed by the DefinitionFilesDialog.
        /// </summary>
        public static string AsciiDefFilePath => Properties.Settings.Default.ToolsDefPath;
    }
}

