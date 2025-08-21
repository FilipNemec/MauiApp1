using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Google.Protobuf;

namespace MauiApp1
{
	public partial class MainPage : ContentPage
	{
		public List<string> ProgressList { get; set; } = new();
		public List<Task> Tasks { get; set; } = new();

		public MainPage()
		{
			InitializeComponent();
			BindingContext = this;
			ProgressList = new List<string>();
		}

		private async Task<PerformancePage.PerfResult> LoadTasksOnce(string url, bool isProtobuf)
		{
			var stopwatch = System.Diagnostics.Stopwatch.StartNew();
			try
			{
				using var client = new HttpClient(new HttpClientHandler { UseCookies = false });
				client.DefaultRequestHeaders.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true, NoStore = true, MustRevalidate = true };
				client.DefaultRequestHeaders.ConnectionClose = true;
				var cacheBuster = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
				if (url.Contains("?"))
					url += "&cb=" + cacheBuster;
				else
					url += "?cb=" + cacheBuster;
				var response = await client.GetAsync(url);
				response.EnsureSuccessStatusCode();
				var bytes = await response.Content.ReadAsByteArrayAsync();
				stopwatch.Stop();
				if (isProtobuf)
				{
					var taskList = TaskList.Parser.ParseFrom(bytes);
					Tasks = new List<Task>(taskList.Tasks);
				}
				else
				{
					var json = System.Text.Encoding.UTF8.GetString(bytes);
					var taskList = System.Text.Json.JsonSerializer.Deserialize<TaskList>(json);
					Tasks = taskList != null ? new List<Task>(taskList.Tasks) : new List<Task>();
				}
				OnPropertyChanged(nameof(Tasks));
				return new PerformancePage.PerfResult(
					isProtobuf ? PerformancePage.SerializerType.Protobuf : PerformancePage.SerializerType.JSON,
					bytes.Length,
					stopwatch.ElapsedMilliseconds
				);
			}
			catch (Exception ex)
			{
				stopwatch.Stop();
				ProgressList.Add($"Fehler: {ex.Message}");
				OnPropertyChanged(nameof(ProgressList));
				return new PerformancePage.PerfResult(
					isProtobuf ? PerformancePage.SerializerType.Protobuf : PerformancePage.SerializerType.JSON,
					0,
					0
				);
			}
		}

		public async void RunBenchmark()
		{
			// ...existing code...
		}

		private async void OnShowPerformanceClicked(object sender, EventArgs e)
		{
			await Navigation.PushAsync(new PerformancePage());
		}

		private void OnRunBenchmarkClicked(object sender, EventArgs e)
		{
			RunBenchmark();
		}
	}
}
