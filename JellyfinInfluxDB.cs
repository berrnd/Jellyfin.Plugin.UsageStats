using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.InfluxDB
{
	public class JellyfinInfluxDB : IServerEntryPoint
	{
		public JellyfinInfluxDB(ISessionManager sessionManager, ILogger<JellyfinInfluxDB> logger, IFileSystem fileSystem, ILibraryManager libraryManager)
		{
			this.Logger = logger;
			this.SessionManager = sessionManager;
			this.FileSystem = fileSystem;
			this.LibraryManager = libraryManager;
		}

		private readonly ISessionManager SessionManager;
		private readonly ILogger<JellyfinInfluxDB> Logger;
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

				this.Logger.LogInformation(String.Format("Jellyfin.Plugin.InfluxDB: Started with this configuration: {0}", JsonSerializer.Serialize(Plugin.Instance.PluginConfiguration)));
			}
			catch (Exception ex)
			{
				this.Logger.LogError(ex, $"Jellyfin.Plugin.InfluxDB: {ex.Message}");
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
				this.Logger.LogError(ex, $"Jellyfin.Plugin.InfluxDB: {ex.Message}");
			}
		}

		private void ItemDownloaded(object sender, PlaybackProgressEventArgs e)
		{
			try
			{
				this.Logger.LogInformation("Jellyfin.Plugin.InfluxDB: Item downloaded event");

				String seriesName = e.Item.Name;
				if (e.Item is Episode)
				{
					seriesName = ((Episode)e.Item).SeriesName;
				}

				String data = String.Format("webaccess,client={0},device={1},media_type={2},item_name={3},user={4},series_name={5} item_size={6},action=\"downloaded\"", this.InfluxDbStringValue(e.ClientName), this.InfluxDbStringValue(e.DeviceName), this.InfluxDbStringValue(e.Item.GetType().Name), this.InfluxDbStringValue(e.Item.Name), this.InfluxDbStringValue(e.Users.First().Username), this.InfluxDbStringValue(seriesName), this.FileSystem.GetFileSystemInfo(e.Item.Path).Length);
				this.InfluxDbWrite(data);
			}
			catch (Exception ex)
			{
				this.Logger.LogError(ex, $"Jellyfin.Plugin.InfluxDB: {ex.Message}");
			}
		}

		private void SessionEnded(object sender, SessionEventArgs e)
		{
			try
			{
				this.Logger.LogInformation("Jellyfin.Plugin.InfluxDB: Session ended event");

				String data = String.Format("session,client={0},device={1},user={2} action=\"ended\"", this.InfluxDbStringValue(e.SessionInfo.Client), this.InfluxDbStringValue(e.SessionInfo.DeviceName), this.InfluxDbStringValue(e.SessionInfo.UserName));
				this.InfluxDbWrite(data);
			}
			catch (Exception ex)
			{
				this.Logger.LogError(ex, $"Jellyfin.Plugin.InfluxDB: {ex.Message}");
			}
		}

		private void SessionStarted(object sender, SessionEventArgs e)
		{
			try
			{
				this.Logger.LogInformation("Jellyfin.Plugin.InfluxDB: Session started event");

				String data = String.Format("session,client={0},device={1},user={2} action=\"started\"", this.InfluxDbStringValue(e.SessionInfo.Client), this.InfluxDbStringValue(e.SessionInfo.DeviceName), this.InfluxDbStringValue(e.SessionInfo.UserName));
				this.InfluxDbWrite(data);
			}
			catch (Exception ex)
			{
				this.Logger.LogError(ex, $"Jellyfin.Plugin.InfluxDB: {ex.Message}");
			}
		}

		private void PlaybackProgress(object sender, PlaybackProgressEventArgs e)
		{
			try
			{
				if (e.IsPaused & !this.PausedDevices.Contains(e.DeviceId))
				{
					//Paused

					this.Logger.LogInformation("Jellyfin.Plugin.InfluxDB: Playback paused event");

					this.PausedDevices.Add(e.DeviceId);

					String seriesName = e.Item.Name;
					if (e.Item is Episode)
					{
						seriesName = ((Episode)e.Item).SeriesName;
					}

					String data = String.Format("playback,client={0},device={1},media_type={2},item_name={3},user={4},series_name={5} item_size={6},action=\"paused\"", this.InfluxDbStringValue(e.ClientName), this.InfluxDbStringValue(e.DeviceName), this.InfluxDbStringValue(e.Item.GetType().Name), this.InfluxDbStringValue(e.Item.Name), this.InfluxDbStringValue(e.Users.First().Username), this.InfluxDbStringValue(seriesName), this.FileSystem.GetFileSystemInfo(e.Item.Path).Length);
					this.InfluxDbWrite(data);
				}
				else if (e.IsPaused == false & this.PausedDevices.Contains(e.DeviceId))
				{
					//Resumed

					this.Logger.LogInformation("Jellyfin.Plugin.InfluxDB: Playback resume event");

					this.PausedDevices.Remove(e.DeviceId);

					String seriesName = e.Item.Name;
					if (e.Item is Episode)
					{
						seriesName = ((Episode)e.Item).SeriesName;
					}

					String data = String.Format("playback,client={0},device={1},media_type={2},item_name={3},user={4},series_name={5} item_size={6},action=\"resumed\"", this.InfluxDbStringValue(e.ClientName), this.InfluxDbStringValue(e.DeviceName), this.InfluxDbStringValue(e.Item.GetType().Name), this.InfluxDbStringValue(e.Item.Name), this.InfluxDbStringValue(e.Users.First().Username), this.InfluxDbStringValue(seriesName), this.FileSystem.GetFileSystemInfo(e.Item.Path).Length);
					this.InfluxDbWrite(data);
				}
			}
			catch (Exception ex)
			{
				this.Logger.LogError(ex, $"Jellyfin.Plugin.InfluxDB: {ex.Message}");
			}
		}

		private void PlaybackStart(object sender, PlaybackProgressEventArgs e)
		{
			try
			{
				this.Logger.LogInformation("Jellyfin.Plugin.InfluxDB: Playback start event");

				if (this.PausedDevices.Contains(e.DeviceId))
				{
					this.PausedDevices.Remove(e.DeviceId);
				}

				String seriesName = e.Item.Name;
				if (e.Item is Episode)
				{
					seriesName = ((Episode)e.Item).SeriesName;
				}

				String data = String.Format("playback,client={0},device={1},media_type={2},item_name={3},user={4},series_name={5} item_size={6},action=\"started\"", this.InfluxDbStringValue(e.ClientName), this.InfluxDbStringValue(e.DeviceName), this.InfluxDbStringValue(e.Item.GetType().Name), this.InfluxDbStringValue(e.Item.Name), this.InfluxDbStringValue(e.Users.First().Username), this.InfluxDbStringValue(seriesName), this.FileSystem.GetFileSystemInfo(e.Item.Path).Length);
				this.InfluxDbWrite(data);
			}
			catch (Exception ex)
			{
				this.Logger.LogError(ex, $"Jellyfin.Plugin.InfluxDB: {ex.Message}");
			}
		}

		private void PlaybackStopped(object sender, PlaybackProgressEventArgs e)
		{
			try
			{
				this.Logger.LogInformation("Jellyfin.Plugin.InfluxDB: Playback stop event");

				if (this.PausedDevices.Contains(e.DeviceId))
				{
					this.PausedDevices.Remove(e.DeviceId);
				}

				String seriesName = e.Item.Name;
				if (e.Item is Episode)
				{
					seriesName = ((Episode)e.Item).SeriesName;
				}

				String data = String.Format("playback,client={0},device={1},media_type={2},item_name={3},user={4},series_name={5} item_size={6},action=\"stopped\"", this.InfluxDbStringValue(e.ClientName), this.InfluxDbStringValue(e.DeviceName), this.InfluxDbStringValue(e.Item.GetType().Name), this.InfluxDbStringValue(e.Item.Name), this.InfluxDbStringValue(e.Users.First().Username), this.InfluxDbStringValue(seriesName), this.FileSystem.GetFileSystemInfo(e.Item.Path).Length);
				this.InfluxDbWrite(data);
			}
			catch (Exception ex)
			{
				this.Logger.LogError(ex, $"Jellyfin.Plugin.InfluxDB: {ex.Message}");
			}
		}

		private async void InfluxDbWrite(String payload)
		{
			try
			{
				using (HttpClient client = new HttpClient())
				{
					StringContent httpContent = new StringContent(payload, Encoding.UTF8, "application/x-www-form-urlencoded");
					HttpResponseMessage response = await client.PostAsync(Plugin.Instance.PluginConfiguration.InfluxDbWriteBaseUri, httpContent);

					if (!response.IsSuccessStatusCode)
					{
						this.Logger.LogError(String.Format("Jellyfin.Plugin.InfluxDB: {0} {1} {2} {3} {4}", response.StatusCode, response.ReasonPhrase, response.Content, response.RequestMessage, payload));
					}
				}
			}
			catch (Exception ex)
			{
				this.Logger.LogError(ex, $"Jellyfin.Plugin.InfluxDB: {ex.Message}");
			}
		}

		private String InfluxDbStringValue(String value)
		{
			return value.Replace(" ", "\\ ").Replace("=", "\\=").Replace(",", "\\,");
		}
	}
}
