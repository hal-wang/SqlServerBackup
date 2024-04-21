using H.Tools.Config;

namespace SqlServerBackup.Services;

public class ConfigService : AppSettingsConfiguration
{
    public string DbAddress
    {
        get => Get("127.0.0.1");
        set => Set(value);
    }

    public string DbUserName
    {
        get => Get("sa");
        set => Set(value);
    }

    public string DbPassword
    {
        get => Get("");
        set => Set(value);
    }

    public string DbBackupPath
    {
        get => Get(@"C:\\DatabaseBackup");
        set => Set(value);
    }
}
