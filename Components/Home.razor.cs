using H.Tools.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.IdentityModel.Tokens;
using MudBlazor;
using SqlServerBackup.Services;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Windows;

namespace SqlServerBackup.Components;

public partial class Home
{
    [Inject]
    private ConfigService ConfigService { get; set; } = null!;
    [Inject]
    private DatabaseService DatabaseService { get; set; } = null!;
    [Inject]
    private IDialogService DialogService { get; set; } = null!;
    [Inject]
    private ISnackbar Snackbar { get; set; } = null!;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (firstRender && !string.IsNullOrEmpty(ConfigService.DbPassword))
        {
            await OpenDatabase(ConfigService.DbAddress, ConfigService.DbUserName, ConfigService.DbPassword);
        }
    }

    public bool Logined { get; set; } = false;
    public bool Logining { get; set; } = false;
    public object? SelectedDatabase { get; set; }
    public List<string> Databases { get; } = [];
    public List<string> BackupFiles { get; } = [];

    private async Task OpenDatabase(string dbAddress, string userName, string password)
    {
        Logining = true;
        StateHasChanged();

        try
        {
            await DatabaseService.OpenAsync(dbAddress, userName, password);
            Logined = DatabaseService.Connected;
        }
        catch (Exception ex)
        {
            await DialogService.ShowMessageBox("登录失败", ex.Message, "确认");
            Logined = false;
        }
        StateHasChanged();

        if (Logined)
        {
            await LoadDatabases();
            await LoadFiles();
        }

        Logining = false;
        StateHasChanged();
    }

    private async Task LoadDatabases()
    {
        var sql = "select name from sysdatabases";
        var dt = await DatabaseService.ExecuteQueryAsync(sql);
        if (dt == null) return;

        Databases.Clear();
        dt.ToList()
            .Select(item => item.name as string)
            .Where(item => !string.IsNullOrEmpty(item))
            .OrderBy(item => item)
            .ToList()
            .ForEach(database =>
            {
                Databases.Add(database!);
            });

        StateHasChanged();
    }

    private async Task LoadFiles()
    {
        var sql = $"EXEC xp_dirtree '{ConfigService.DbBackupPath}',1,1";
        var dt = await DatabaseService.ExecuteQueryAsync(sql);
        if (dt == null) return;

        BackupFiles.Clear();
        dt.ToList()
            .Select(item => item.subdirectory as string)
            .Where(item => !string.IsNullOrEmpty(item))
            .Reverse()
            .ToList()
            .ForEach(file =>
            {
                BackupFiles.Add(file!);
            });

        StateHasChanged();
    }

    private string? DeleteLoading = null;
    private async Task Delete(string file)
    {
        if (!string.IsNullOrEmpty(DeleteLoading)) return;

        var ask = await DialogService.ShowMessageBox("确认删除", $"即将删除备份 {file}，\n该操作不可恢复，确认继续？", "确认", "取消");
        if (ask != true) return;

        DeleteLoading = file;
        var filePath = Path.Combine(ConfigService.DbBackupPath, file);
        var sql = $"EXEC xp_delete_file 0, '{filePath}'";
        try
        {
            await DatabaseService.ExecuteNonQueryAsync(sql);
        }
        catch (Exception ex)
        {
            await DialogService.ShowMessageBox("删除失败", ex.Message, "确认");
            return;
        }
        finally
        {
            DeleteLoading = null;
        }

        BackupFiles.Remove(file);
        Snackbar.Add("已删除", Severity.Success, config =>
        {
            config.ShowCloseIcon = false;
            config.VisibleStateDuration = 1000;
        });
    }


    #region 备份
    public string Remark { get; set; } = string.Empty;
    private string CreateBackupSql(string database)
    {
        var datetime = DateTime.Now.ToString("yyyy-MM-dd=HH_mm_ss");
        var fileName = $"[{datetime}]_[{database}]_";
        if (!string.IsNullOrEmpty(Remark))
        {
            fileName += $"[{Remark}]";
        }
        fileName += ".bak";
        var filePath = Path.Combine(ConfigService.DbBackupPath, fileName);

        return $"BACKUP DATABASE [{database}] TO DISK = N'{filePath}' WITH NOFORMAT, NOINIT, NAME = N'{database}-FullBackup, SKIP, NOREWIND, NOUNLOAD, STATS = 10'";

    }

    public bool IsRemarkDialogOpened { get; set; } = false;
    public void OpenBackupDialog(string database)
    {
        SelectedDatabase = database;
        IsRemarkDialogOpened = true;
        StateHasChanged();
    }

    public bool BackupLoading { get; set; } = false;
    public async Task HandleBackup()
    {
        if (BackupLoading) return;

        var database = SelectedDatabase as string;
        if (string.IsNullOrEmpty(database)) return;

        BackupLoading = true;
        StateHasChanged();

        try
        {
            await DatabaseService.ExecuteNonQueryAsync(CreateBackupSql(database));
        }
        catch (Exception ex)
        {
            await DialogService.ShowMessageBox("备份失败", ex.Message);
            return;
        }
        finally
        {
            BackupLoading = false;
        }

        IsRemarkDialogOpened = false;
        Remark = string.Empty;
        StateHasChanged();

        Snackbar.Add("备份完成", Severity.Success, config =>
        {
            config.ShowCloseIcon = false;
            config.VisibleStateDuration = 1000;
        });
        await LoadFiles();
    }
    #endregion

    #region Restore
    private string? RestoreLoading = null;
    public async Task Restore(string file)
    {
        if (!string.IsNullOrEmpty(RestoreLoading)) return;
        if (string.IsNullOrEmpty(SelectedDatabase as string))
        {
            Snackbar.Add("未选择数据库", Severity.Warning, config =>
            {
                config.ShowCloseIcon = false;
                config.VisibleStateDuration = 1000;
            });
            return;
        }

        var ask = await DialogService.ShowMessageBox("确认还原", $"即将使用文件 {file} 还原数据库 {SelectedDatabase}，确认继续？", "确认", "取消");
        if (ask != true) return;

        RestoreLoading = file;
        try
        {
            if (!await CloseAllSessions()) return;
            await SetOffline(true);

            var filePath = Path.Combine(ConfigService.DbBackupPath, file);
            var sql = $"RESTORE DATABASE {SelectedDatabase} FROM DISK = '{filePath}' WITH  REPLACE";
            await DatabaseService.ExecuteNonQueryAsync(sql);
        }
        catch (Exception ex)
        {
            await DialogService.ShowMessageBox("还原失败", ex.Message);
            return;
        }
        finally
        {
            try
            {
                await SetOffline(false);
            }
            catch { }
            RestoreLoading = null;
        }
        Snackbar.Add("还原完成", Severity.Success, config =>
        {
            config.ShowCloseIcon = false;
            config.VisibleStateDuration = 1000;
        });
    }

    private async Task<bool> CloseAllSessions()
    {
        string sql = $"SELECT A.spid FROM sysprocesses AS A,sysdatabases AS B WHERE A.dbid = B.dbid AND B.Name='{SelectedDatabase}'";
        var dt = await DatabaseService.ExecuteQueryAsync(sql);
        if (dt == null) return false;
        if (dt.Rows.Count == 0) return true;

        foreach (var item in dt.ToList())
        {
            sql = $"KILL {item.spid}";
            await DatabaseService.ExecuteNonQueryAsync(sql);
        }

        return true;
    }

    private async Task SetOffline(bool isOffline)
    {
        string sql = $"ALTER DATABASE [{SelectedDatabase}] SET {(isOffline ? "OFFLINE" : "ONLINE")} WITH ROLLBACK IMMEDIATE";
        await DatabaseService.ExecuteNonQueryAsync(sql);
    }
    #endregion

    #region 登录
    public bool IsLoginDialogOpened { get; set; } = false;
    MudForm loginForm = null!;
    readonly LoginModel loginModel = new();

    public async Task Login()
    {
        await loginForm.Validate();
        if (!loginForm.IsValid) return;

        await OpenDatabase(loginModel.Address, loginModel.UserName, loginModel.Password);

        if (Logined)
        {
            ConfigService.DbAddress = loginModel.Address;
            ConfigService.DbUserName = loginModel.UserName;
            ConfigService.DbPassword = loginModel.Password;

            IsLoginDialogOpened = false;
            StateHasChanged();
        }
    }

    public void OpenLogin()
    {
        loginModel.Address = ConfigService.DbAddress;
        loginModel.Password = ConfigService.DbPassword;
        loginModel.UserName = ConfigService.DbUserName;

        IsLoginDialogOpened = true;
        StateHasChanged();
    }

    private async Task Logout()
    {
        var ask = await DialogService.ShowMessageBox("退出登录", $"即将退出登录，确认继续？", "确认", "取消");
        if (ask != true) return;

        DatabaseService.Close();

        Logined = false;
        Databases.Clear();
        BackupFiles.Clear();
        SelectedDatabase = null;
        StateHasChanged();
    }
    #endregion
}

class LoginModel
{
    [Required]
    public string Address { get; set; } = null!;
    [Required]
    public string UserName { get; set; } = null!;
    [Required]
    public string Password { get; set; } = null!;
}