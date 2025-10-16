using ConfigurationManager = System.Configuration.ConfigurationManager;

namespace System_Monitor_MQTT
{
    public static class Settings
    {
        public static string? AdminName = ConfigurationManager.AppSettings["admin_name"];
        public static string? AdminPassword = ConfigurationManager.AppSettings["admin_password"];
        public static string? UserName = ConfigurationManager.AppSettings["user_name"];
        public static string? UserPassword = ConfigurationManager.AppSettings["user_password"];
        public static string ServerName
        {
            get
            {
                if (ConfigurationManager.AppSettings["server_name"] == null) { return "localhost-" + Guid.NewGuid().ToString(); }
                else { return ConfigurationManager.AppSettings["server_name"]!; }
            }
        }

        public static string UpdateInterval
        {
            get
            {
                if (ConfigurationManager.AppSettings["update_interval"] == null) { return "1000"; }
                else { return ConfigurationManager.AppSettings["update_interval"]!; }
            }
        }
        public static string Port
        {
            get
            {
                if (ConfigurationManager.AppSettings["port"] == null) { return "1883"; }
                else { return ConfigurationManager.AppSettings["port"]!; }
            }
        }      
    }
}
