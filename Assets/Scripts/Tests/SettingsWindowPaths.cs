namespace KitchenDesigner.Tests
{
    public static class SettingsWindowPaths
    {
        public const string Panel = "SettingsPanel";
        public const string Body = "SettingsPanel/SettingsPanelBody/SettingsPanelBodyContent/";
        public const string Nav = "SettingsPanel/SettingsNav";
        public const string Footer = "SettingsPanel/SettingsPanelFooter";

        public static string Page(string id) => Body + "Page_" + id;

        public static string Row(string key) => "Row_" + key;

        public static string Switch(string key) => "Row_" + key + "/Sw_" + key;

        public static string Slider(string key) => "Row_" + key + "/Sld_" + key;

        public static string Field(string key) => "Row_" + key + "/F_" + key;

        public static string Dropdown(string key) => "Row_" + key + "/Dd_" + key;

        public static string NavItem(string id) => Nav + "/NavItem_" + id;
    }
}
