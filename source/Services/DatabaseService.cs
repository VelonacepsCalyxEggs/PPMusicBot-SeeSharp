using Discord;
using Npgsql;
using NpgsqlTypes;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PPMusicBot.Services
{
    public class DatabaseService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<DatabaseService> _logger;
        private NpgsqlDataSource DataSource;
        public DatabaseService(IConfiguration configuration, ILogger<DatabaseService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Database service is starting!");
            }
            try
            {
                CreateConnection();
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
        private void CreateConnection()
        {
            DataSource?.Dispose();
            DataSource = NpgsqlDataSource.Create(_configuration["Database:ConnectionString"]);
            using var conn = DataSource.OpenConnection();
        }

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
            }
            catch (DbException ex)
            {
                _logger.LogError(ex, ex.Message);
                CreateConnection(); // This is scuffed but I am too sleepy to figure out the correct exception types for proper handling.
            }
        }
    }
}
