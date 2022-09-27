using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.UsageStats
{
	public class JellyfinUsageStats : IServerEntryPoint
	{
		public JellyfinUsageStats(ISessionManager sessionManager, ILogger<JellyfinUsageStats> logger, IFileSystem fileSystem, ILibraryManager libraryManager)
		{
			this.Logger = logger;
			this.SessionManager = sessionManager;
			this.FileSystem = fileSystem;
			this.LibraryManager = libraryManager;
		}

		private readonly ISessionManager SessionManager;
		private readonly ILogger<JellyfinUsageStats> Logger;
		private readonly IFileSystem FileSystem;
		private readonly ILibraryManager LibraryManager;
		private readonly List<String> PausedDevices = new List<String>();
		
		public Task RunAsync()
		{
			try
			{
				this.SessionManager.PlaybackStart += this.PlaybackStart;
				this.SessionManager.PlaybackStopped += this.PlaybackStopped;
				this.SessionManager.PlaybackProgress += this.PlaybackProgress;
				this.SessionManager.SessionStarted += this.SessionStarted;
				this.SessionManager.SessionEnded += this.SessionEnded;
				this.LibraryManager.ItemDownloaded += this.ItemDownloaded;

				this.Logger.LogInformation(String.Format("Jellyfin.Plugin.UsageStats: Started with this configuration: {0}", JsonSerializer.Serialize(Plugin.Instance.PluginConfiguration)));
			}
			catch (Exception ex)
			{
				this.Logger.LogError(ex, $"Jellyfin.Plugin.UsageStats: {ex.Message}");
				return Task.CompletedTask;
			}

			return Task.CompletedTask;
		}
		public void Dispose()
		{
			try
			{
				this.SessionManager.PlaybackStart -= this.PlaybackStart;
				this.SessionManager.PlaybackStopped -= this.PlaybackStopped;
				this.SessionManager.PlaybackProgress -= this.PlaybackProgress;
				this.SessionManager.SessionStarted -= this.SessionStarted;
				this.SessionManager.SessionEnded -= this.SessionEnded;
				this.LibraryManager.ItemDownloaded -= this.ItemDownloaded;
			}
			catch (Exception ex)
			{
				this.Logger.LogError(ex, $"Jellyfin.Plugin.UsageStats: {ex.Message}");
			}
		}

		private void ItemDownloaded(object sender, PlaybackProgressEventArgs e)
		{
			try
			{
				this.Logger.LogInformation("Jellyfin.Plugin.UsageStats: Item downloaded event");

				String seriesName = e.Item.Name;
				if (e.Item is Episode)
				{
					seriesName = ((Episode)e.Item).SeriesName;
				}

				MySqlCommand cmd = new MySqlCommand("INSERT INTO webaccess (client, device, media_type, item_name, user, series_name, item_size, action) VALUES (@client, @device, @media_type, @item_name, @user, @series_name, @item_size, @action)");
				cmd.Parameters.AddWithValue("@client", e.ClientName);
				cmd.Parameters.AddWithValue("@device", e.DeviceName);
				cmd.Parameters.AddWithValue("@media_type", e.Item.GetType().Name);
                cmd.Parameters.AddWithValue("@item_name", e.Item.Name);
                cmd.Parameters.AddWithValue("@user", e.Users.First().Username);
                cmd.Parameters.AddWithValue("@series_name", seriesName);
                cmd.Parameters.AddWithValue("@item_size", this.FileSystem.GetFileSystemInfo(e.Item.Path).Length);
                cmd.Parameters.AddWithValue("@action", "downloaded");
				this.UsageStatsWrite(cmd);
			}
			catch (Exception ex)
			{
				this.Logger.LogError(ex, $"Jellyfin.Plugin.UsageStats: {ex.Message}");
			}
		}

		private void SessionEnded(object sender, SessionEventArgs e)
		{
			try
			{
				this.Logger.LogInformation("Jellyfin.Plugin.UsageStats: Session ended event");

                MySqlCommand cmd = new MySqlCommand("INSERT INTO session (client, device, user, action) VALUES (@client, @device, @user, @action)");
                cmd.Parameters.AddWithValue("@client", e.SessionInfo.Client);
                cmd.Parameters.AddWithValue("@device", e.SessionInfo.DeviceName);
                cmd.Parameters.AddWithValue("@user", e.SessionInfo.UserName);
                cmd.Parameters.AddWithValue("@action", "ended");
                this.UsageStatsWrite(cmd);
			}
			catch (Exception ex)
			{
				this.Logger.LogError(ex, $"Jellyfin.Plugin.UsageStats: {ex.Message}");
			}
		}

		private void SessionStarted(object sender, SessionEventArgs e)
		{
			try
			{
				this.Logger.LogInformation("Jellyfin.Plugin.UsageStats: Session started event");

                MySqlCommand cmd = new MySqlCommand("INSERT INTO session (client, device, user, action) VALUES (@client, @device, @user, @action)");
                cmd.Parameters.AddWithValue("@client", e.SessionInfo.Client);
                cmd.Parameters.AddWithValue("@device", e.SessionInfo.DeviceName);
                cmd.Parameters.AddWithValue("@user", e.SessionInfo.UserName);
                cmd.Parameters.AddWithValue("@action", "started");
                this.UsageStatsWrite(cmd);
			}
			catch (Exception ex)
			{
				this.Logger.LogError(ex, $"Jellyfin.Plugin.UsageStats: {ex.Message}");
			}
		}

		private void PlaybackProgress(object sender, PlaybackProgressEventArgs e)
		{
			try
			{
				if (e.IsPaused & !this.PausedDevices.Contains(e.DeviceId))
				{
					//Paused

					this.Logger.LogInformation("Jellyfin.Plugin.UsageStats: Playback paused event");

					this.PausedDevices.Add(e.DeviceId);

					String seriesName = e.Item.Name;
					if (e.Item is Episode)
					{
						seriesName = ((Episode)e.Item).SeriesName;
					}

                    MySqlCommand cmd = new MySqlCommand("INSERT INTO playback (client, device, media_type, item_name, user, series_name, item_size, action) VALUES (@client, @device, @media_type, @item_name, @user, @series_name, @item_size, @action)");
                    cmd.Parameters.AddWithValue("@client", e.ClientName);
                    cmd.Parameters.AddWithValue("@device", e.DeviceName);
                    cmd.Parameters.AddWithValue("@media_type", e.Item.GetType().Name);
                    cmd.Parameters.AddWithValue("@item_name", e.Item.Name);
                    cmd.Parameters.AddWithValue("@user", e.Users.First().Username);
                    cmd.Parameters.AddWithValue("@series_name", seriesName);
                    cmd.Parameters.AddWithValue("@item_size", this.FileSystem.GetFileSystemInfo(e.Item.Path).Length);
                    cmd.Parameters.AddWithValue("@action", "paused");
                    this.UsageStatsWrite(cmd);
				}
				else if (e.IsPaused == false & this.PausedDevices.Contains(e.DeviceId))
				{
					//Resumed

					this.Logger.LogInformation("Jellyfin.Plugin.UsageStats: Playback resume event");

					this.PausedDevices.Remove(e.DeviceId);

					String seriesName = e.Item.Name;
					if (e.Item is Episode)
					{
						seriesName = ((Episode)e.Item).SeriesName;
					}

                    MySqlCommand cmd = new MySqlCommand("INSERT INTO playback (client, device, media_type, item_name, user, series_name, item_size, action) VALUES (@client, @device, @media_type, @item_name, @user, @series_name, @item_size, @action)");
                    cmd.Parameters.AddWithValue("@client", e.ClientName);
                    cmd.Parameters.AddWithValue("@device", e.DeviceName);
                    cmd.Parameters.AddWithValue("@media_type", e.Item.GetType().Name);
                    cmd.Parameters.AddWithValue("@item_name", e.Item.Name);
                    cmd.Parameters.AddWithValue("@user", e.Users.First().Username);
                    cmd.Parameters.AddWithValue("@series_name", seriesName);
                    cmd.Parameters.AddWithValue("@item_size", this.FileSystem.GetFileSystemInfo(e.Item.Path).Length);
                    cmd.Parameters.AddWithValue("@action", "resumed");
                    this.UsageStatsWrite(cmd);
				}
			}
			catch (Exception ex)
			{
				this.Logger.LogError(ex, $"Jellyfin.Plugin.UsageStats: {ex.Message}");
			}
		}

		private void PlaybackStart(object sender, PlaybackProgressEventArgs e)
		{
			try
			{
				this.Logger.LogInformation("Jellyfin.Plugin.UsageStats: Playback start event");

				if (this.PausedDevices.Contains(e.DeviceId))
				{
					this.PausedDevices.Remove(e.DeviceId);
				}

				String seriesName = e.Item.Name;
				if (e.Item is Episode)
				{
					seriesName = ((Episode)e.Item).SeriesName;
				}

                MySqlCommand cmd = new MySqlCommand("INSERT INTO playback (client, device, media_type, item_name, user, series_name, item_size, action) VALUES (@client, @device, @media_type, @item_name, @user, @series_name, @item_size, @action)");
                cmd.Parameters.AddWithValue("@client", e.ClientName);
                cmd.Parameters.AddWithValue("@device", e.DeviceName);
                cmd.Parameters.AddWithValue("@media_type", e.Item.GetType().Name);
                cmd.Parameters.AddWithValue("@item_name", e.Item.Name);
                cmd.Parameters.AddWithValue("@user", e.Users.First().Username);
                cmd.Parameters.AddWithValue("@series_name", seriesName);
                cmd.Parameters.AddWithValue("@item_size", this.FileSystem.GetFileSystemInfo(e.Item.Path).Length);
                cmd.Parameters.AddWithValue("@action", "started");
                this.UsageStatsWrite(cmd);
			}
			catch (Exception ex)
			{
				this.Logger.LogError(ex, $"Jellyfin.Plugin.UsageStats: {ex.Message}");
			}
		}

		private void PlaybackStopped(object sender, PlaybackProgressEventArgs e)
		{
			try
			{
				this.Logger.LogInformation("Jellyfin.Plugin.UsageStats: Playback stop event");

				if (this.PausedDevices.Contains(e.DeviceId))
				{
					this.PausedDevices.Remove(e.DeviceId);
				}

				String seriesName = e.Item.Name;
				if (e.Item is Episode)
				{
					seriesName = ((Episode)e.Item).SeriesName;
				}

                MySqlCommand cmd = new MySqlCommand("INSERT INTO playback (client, device, media_type, item_name, user, series_name, item_size, action) VALUES (@client, @device, @media_type, @item_name, @user, @series_name, @item_size, @action)");
                cmd.Parameters.AddWithValue("@client", e.ClientName);
                cmd.Parameters.AddWithValue("@device", e.DeviceName);
                cmd.Parameters.AddWithValue("@media_type", e.Item.GetType().Name);
                cmd.Parameters.AddWithValue("@item_name", e.Item.Name);
                cmd.Parameters.AddWithValue("@user", e.Users.First().Username);
                cmd.Parameters.AddWithValue("@series_name", seriesName);
                cmd.Parameters.AddWithValue("@item_size", this.FileSystem.GetFileSystemInfo(e.Item.Path).Length);
                cmd.Parameters.AddWithValue("@action", "stopped");
                this.UsageStatsWrite(cmd);
			}
			catch (Exception ex)
			{
				this.Logger.LogError(ex, $"Jellyfin.Plugin.UsageStats: {ex.Message}");
			}
		}

		private async void UsageStatsWrite(MySqlCommand command)
		{
			try
			{
                await using MySqlConnection connection = new MySqlConnection(Plugin.Instance.PluginConfiguration.DBConnectionString);
                await connection.OpenAsync();

				command.Connection = connection;
                await command.ExecuteNonQueryAsync();
            }
			catch (Exception ex)
			{
				this.Logger.LogError(ex, $"Jellyfin.Plugin.UsageStats: {ex.Message}");
			}
		}
	}
}
