namespace DBDiver.Models;

public class DirectDatabaseConnection
{
    public string Server { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    public bool UseWindowsAuthentication { get; set; }

    public bool TrustServerCertificate { get; set; }

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Server) &&
        !string.IsNullOrWhiteSpace(Database) &&
        (UseWindowsAuthentication || (!string.IsNullOrWhiteSpace(Username) && !string.IsNullOrEmpty(Password)));
}
