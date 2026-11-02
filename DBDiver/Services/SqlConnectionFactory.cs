using System.Data.Common;
using DBDiver.Models;
using Microsoft.Data.SqlClient;

namespace DBDiver.Services;

public static class SqlConnectionFactory
{
    public static DbConnection Create(DirectDatabaseConnection connection)
    {
        if (!connection.IsValid)
            throw new InvalidOperationException("Enter a server and database, plus a username and password or Windows authentication.");

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = connection.Server,
            InitialCatalog = connection.Database,
            IntegratedSecurity = connection.UseWindowsAuthentication,
            Encrypt = true,
            TrustServerCertificate = connection.TrustServerCertificate
        };

        if (!connection.UseWindowsAuthentication)
        {
            builder.UserID = connection.Username;
            builder.Password = connection.Password;
        }

        return new SqlConnection(builder.ConnectionString);
    }
}
