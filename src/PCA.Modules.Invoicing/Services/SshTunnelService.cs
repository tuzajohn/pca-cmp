using Microsoft.Extensions.Logging;
using MySqlConnector;
using Renci.SshNet;

namespace PCA.Modules.Invoicing.Services;

public class ExternalDbSettings
{
    public string SshHost { get; set; } = string.Empty;
    public int SshPort { get; set; } = 22;
    public string SshUsername { get; set; } = string.Empty;
    public string SshKeyPath { get; set; } = string.Empty;
    public string DbHost { get; set; } = "127.0.0.1";
    public int DbPort { get; set; } = 3306;
    public string Database { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class SshTunneledConnection : IDisposable
{
    private readonly SshClient _ssh;
    private readonly ForwardedPortLocal _port;
    public MySqlConnection Connection { get; }

    public SshTunneledConnection(SshClient ssh, ForwardedPortLocal port, MySqlConnection conn)
    {
        _ssh = ssh;
        _port = port;
        Connection = conn;
    }

    public void Dispose()
    {
        Connection.Dispose();
        _port.Stop();
        _ssh.Disconnect();
        _ssh.Dispose();
    }
}

public static class SshTunnelService
{
    private static readonly ILogger _logger =
        LoggerFactory.Create(b => b.AddConsole()).CreateLogger(nameof(SshTunnelService));

    public static async Task<SshTunneledConnection> OpenAsync(ExternalDbSettings cfg)
    {
        _logger.LogInformation("SSH tunnel: connecting to {SshHost}:{SshPort} as {SshUser}",
            cfg.SshHost, cfg.SshPort, cfg.SshUsername);

        var keyFile = new PrivateKeyFile(cfg.SshKeyPath);
        var authMethod = new PrivateKeyAuthenticationMethod(cfg.SshUsername, keyFile);
        var sshConn = new ConnectionInfo(cfg.SshHost, cfg.SshPort, cfg.SshUsername, authMethod);

        var ssh = new SshClient(sshConn);
        ssh.Connect();
        _logger.LogInformation("SSH tunnel: connected to {SshHost}", cfg.SshHost);

        var fwd = new ForwardedPortLocal("127.0.0.1", cfg.DbHost, (uint)cfg.DbPort);
        ssh.AddForwardedPort(fwd);
        fwd.Start();

        var localPort = fwd.BoundPort;
        _logger.LogInformation("SSH tunnel: forwarding 127.0.0.1:{LocalPort} → {DbHost}:{DbPort} (db: {Database})",
            localPort, cfg.DbHost, cfg.DbPort, cfg.Database);

        var cs = new MySqlConnectionStringBuilder
        {
            Server = "127.0.0.1",
            Port = localPort,
            Database = cfg.Database,
            UserID = cfg.Username,
            Password = cfg.Password,
            SslMode = MySqlSslMode.Disabled,
            AllowPublicKeyRetrieval = true
        }.ConnectionString;

        var conn = new MySqlConnection(cs);
        await conn.OpenAsync();
        _logger.LogInformation("SSH tunnel: MySQL connection open on port {LocalPort}", localPort);

        return new SshTunneledConnection(ssh, fwd, conn);
    }
}
