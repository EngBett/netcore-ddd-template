namespace Template.Common.Options
{
    public class ApplicationOptions
    {
        public string[] AllowedOrigins { get; set; } = [];
        public string Authority { get; set; } = null!;
        public string[] DefaultApis { get; set; } = [];
        public string Audience { get; set; } = null!;
        public string ClientId { get; set; } = null!;
        public string LogUrl { get; set; } = null!;
        public string ClientSecret { get; set; } = null!;
        public string MetadataAddress { get; set; } = null!;

        public string SensitiveDataKeys { get; set; } = null!;
        public string SensitiveDataDefaultValues { get; set; } = null!;
        public bool EnableAutoMigration { get; set; }
        public bool UseLoggerMiddleWare { get; set; }
        public string DefaultRedirectUrl { get; set; } = null!;
        public bool ShowSwagger { get; set; }
    }
}