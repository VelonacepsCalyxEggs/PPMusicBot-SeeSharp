using Npgsql;
using NpgsqlTypes;
using System.Collections.ObjectModel;
using System.Data.Common;
using static PPMusicBot.Models.KenobiAPIModels;

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
        private const int BatchInsertCount = 10;
        private List<VoiceChannelData> voiceChannelDatas = new List<VoiceChannelData>();
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
        public async Task RecordVoiceChannelData(ulong userId, ulong? oldChannelId, ulong? newChannelId, ulong guildId, DateTime eventTimestamp)
        {
            VoiceChannelData voiceChannelData = new VoiceChannelData()
            {
                UserId = userId,
                OldChannelId = oldChannelId,
                NewChannelId = newChannelId,
                GuildId = guildId,
                EventTimeStamp = eventTimestamp
            };
            voiceChannelDatas.Add(voiceChannelData);
            if (voiceChannelDatas.Count >= BatchInsertCount)
            {
                await WriteVoiceChannelData();
            }
        }
        public async Task WriteVoiceChannelData()
        {
            try
            {
                await using var conn = DataSource.CreateConnection();
                await conn.OpenAsync();
                await using var batch = new NpgsqlBatch(conn);
                foreach (var voiceChannelData in voiceChannelDatas)
                {
                    var cmd = new NpgsqlBatchCommand(
                    "INSERT INTO discord_data_join (user_id, old_channel, new_channel, server_id, timestamp) VALUES ($1, $2, $3, $4, $5)");
                    cmd.Parameters.AddWithValue(voiceChannelData.UserId.ToString());
                    cmd.Parameters.AddWithValue(voiceChannelData.OldChannelId?.ToString() ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue(voiceChannelData.NewChannelId?.ToString() ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue(voiceChannelData.GuildId.ToString());
                    cmd.Parameters.AddWithValue(NpgsqlDbType.Timestamp, voiceChannelData.EventTimeStamp);
                    batch.BatchCommands.Add(cmd);
                }

                await batch.ExecuteNonQueryAsync();
                voiceChannelDatas.Clear();
                _logger.LogInformation($"Successfully wrote {batch.BatchCommands.Count} VoiceChannelData to the database.");
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
public readonly record struct VoiceChannelData
{
    public ulong UserId { get; init; }
    public ulong? OldChannelId { get; init; }
    public ulong? NewChannelId { get; init; }
    public ulong GuildId { get; init; }
    public DateTime EventTimeStamp { get; init; }
}
