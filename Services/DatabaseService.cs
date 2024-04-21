using H.Tools.Task;
using System.Data;
using System.Data.SqlClient;

namespace SqlServerBackup.Services;

public class DatabaseService
{
    public SqlConnection? DbConnection { get; private set; }
    public bool Connected => DbConnection?.State == ConnectionState.Open;

    public async Task OpenAsync(string dbAddress, string userName, string password)
    {
        var connectionString = $"Data Source={dbAddress};Initial Catalog=master;Persist Security Info=True;User ID={userName};Password={password}";

        DbConnection?.Dispose();
        DbConnection = new SqlConnection(connectionString);
        await DbConnection.OpenAsync().TimeoutAfter(TimeSpan.FromSeconds(5));
    }

    public void Close()
    {
        DbConnection?.Dispose();
    }

    public async Task ExecuteNonQueryAsync(string sqlStr)
    {
        using var command = new SqlCommand(sqlStr, DbConnection);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<DataTable> ExecuteQueryAsync(string sqlStr)
    {
        using var adapter = new SqlDataAdapter(sqlStr, DbConnection);
        var dt = new DataTable();
        await TaskExtend.Run(() =>
        {
            adapter.Fill(dt);
        });
        return dt;
    }
}
