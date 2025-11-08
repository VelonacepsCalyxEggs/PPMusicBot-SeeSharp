using Npgsql;
using NpgsqlTypes;
using System.Data.Common;

namespace PPMusicBot.Services
{
    public class DatabaseService(IConfiguration configuration, ILogger<DatabaseService> logger)
    {
        private readonly IConfiguration _configuration = configuration;
        private readonly ILogger<DatabaseService> _logger = logger;
        private NpgsqlDataSource DataSource;
        private TimeSpan _timeout = new(0,0,0,0,5000); // In Ms
        private double _retryCount = 0;
        private const int MaxRetries = 5;

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Database service is starting!");
            }
            try
            {
                await CreateConnection(isInitial: true);
                return;
            }
            catch
            {
                _logger.LogCritical("Could not open initial connection to the Database.");
                throw;
            }
        }

        public async Task StopAsync()
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Initiating Bot Service graceful shutdown...");
            }
            await DataSource.DisposeAsync();
            return;
        }
        private async Task CreateConnection(bool isInitial = false)
        {
            if (_retryCount > MaxRetries)
            {
                _logger.LogCritical($"Could not connect to the database after {MaxRetries} retries...");
                throw new Exception($"Could not connect to the database after {MaxRetries} retries...");
            }
            try
            {
                if (_retryCount > 0)
                {
                    _timeout = TimeSpan.FromMilliseconds(_timeout.TotalMilliseconds * Math.Pow(2, _retryCount));
                    await Task.Delay(_timeout);
                }
                DataSource?.Dispose();
                DataSource = NpgsqlDataSource.Create(_configuration["Database:ConnectionString"]);
                using var conn = DataSource.OpenConnection();
                if (_retryCount > 0)
                {
                    _retryCount = 0;
                    _timeout = new(0, 0, 0, 0, 5000);
                }
            }
            catch
            {
                if (isInitial)
                {
                    throw;
                }
                _retryCount++;
                _logger.LogError($"Could not create a datasource for the database, retry count: {_retryCount}, timeout: {_timeout}");
                await CreateConnection();
            }
        }
        // Possibly make it so the data about discord data joins is stored in a list, and when it reaches a certain threshhold we batch insert and clear it.
        // This would optimize requests a bit, but also probably help with connection management as well.
        public async Task WriteVoiceChannelData(ulong userId, ulong? oldChannelId, ulong? newChannelId, ulong guildId, DateTime eventTimestamp)
        {
            try
            {
                await using var conn = DataSource.CreateConnection();
                await conn.OpenAsync();
                await using var cmd = new NpgsqlCommand(
                    "INSERT INTO discord_data_join (user_id, old_channel, new_channel, server_id, timestamp) VALUES ($1, $2, $3, $4, $5)",
                    conn);

                cmd.Parameters.AddWithValue(userId.ToString());
                cmd.Parameters.AddWithValue(oldChannelId?.ToString() ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue(newChannelId?.ToString() ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue(guildId.ToString());
                cmd.Parameters.AddWithValue(NpgsqlDbType.Timestamp, eventTimestamp);

                await cmd.ExecuteNonQueryAsync();
                await conn.CloseAsync();
                await conn.DisposeAsync();
            }
            catch (DbException ex)
            {
                _logger.LogError(ex, ex.Message);
                await CreateConnection();
            }
        }
    }
}
