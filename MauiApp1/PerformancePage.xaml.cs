using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace MauiApp1
{
    public partial class PerformancePage : ContentPage
    {
        public enum SerializerType { Protobuf, JSON }

        public readonly record struct PerfResult(SerializerType Type, long Bytes, long Microseconds);

        // Ergebnisse
        public static readonly List<PerfResult> Results = new();

        public List<string> ProgressList { get; set; } = new();

        public PerformancePage()
        {
            InitializeComponent();
            BindingContext = this;
            Results.Clear();
            PerfChart.Drawable = new PerfChartDrawable(Snapshot(Results));
            PerfChart.Invalidate();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            StartBenchmark();
        }

        private async void StartBenchmark()
        {
            ProgressList.Clear();
            OnPropertyChanged(nameof(ProgressList));
            Results.Clear();
            RefreshChart();

            string protoUrl = "http://10.0.2.2:5233/tasks/protobuf";
            string jsonUrl = "http://10.0.2.2:5233/tasks/json";

            using var client = new HttpClient(new HttpClientHandler { UseCookies = false });
            client.DefaultRequestHeaders.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue
            {
                NoCache = true,
                NoStore = true,
                MustRevalidate = true
            };
            client.DefaultRequestHeaders.ConnectionClose = false;

            // ---- Größe 1x messen ----
            var jsonSizeResult = await LoadTasksOnce(client, jsonUrl, false);
            var protoSizeResult = await LoadTasksOnce(client, protoUrl, true);
            double jsonKb = Math.Round(jsonSizeResult.Bytes / 1024.0, 2);
            double protoKb = Math.Round(protoSizeResult.Bytes / 1024.0, 2);
            DownloadInfoLabel.Text = $"downloadsize: JSON = {jsonKb} KB , Protobuf = {protoKb} KB";

            Results.Clear();
            ProgressList.Clear();
            OnPropertyChanged(nameof(ProgressList));
            RefreshChart();

            int runs = 10000;

            // ---- Benchmark: abwechselnd JSON/Protobuf ----
            for (int i = 1; i <= runs; i++)
            {
                // JSON
                var jsonResult = await LoadTasksOnce(client, jsonUrl, false);
                Results.Add(jsonResult);
                ProgressList.Add($"JSON {i}/{runs}: {jsonResult.Microseconds} µs");
                OnPropertyChanged(nameof(ProgressList));
                RefreshChart();

                // Protobuf
                var protoResult = await LoadTasksOnce(client, protoUrl, true);
                Results.Add(protoResult);
                ProgressList.Add($"Protobuf {i}/{runs}: {protoResult.Microseconds} µs");
                OnPropertyChanged(nameof(ProgressList));
                RefreshChart();
            }
        }

        private async Task<PerfResult> LoadTasksOnce(HttpClient client, string url, bool isProtobuf)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var cacheBuster = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var fullUrl = url.Contains("?") ? url + "&cb=" + cacheBuster : url + "?cb=" + cacheBuster;

                var response = await client.GetAsync(fullUrl, HttpCompletionOption.ResponseContentRead);
                response.EnsureSuccessStatusCode();

                var bytes = await response.Content.ReadAsByteArrayAsync();
                sw.Stop();

                // Deserialisierung
                if (isProtobuf)
                {
                    var taskList = TaskList.Parser.ParseFrom(bytes);
                }
                else
                {
                    var taskList = System.Text.Json.JsonSerializer.Deserialize<TaskList>(bytes);
                }

                long micros = (long)(sw.ElapsedTicks * 1_000_000.0 / System.Diagnostics.Stopwatch.Frequency);

                return new PerfResult(
                    isProtobuf ? SerializerType.Protobuf : SerializerType.JSON,
                    bytes.Length,
                    micros
                );
            }
            catch (Exception ex)
            {
                sw.Stop();
                ProgressList.Add($"Fehler: {ex.Message}");
                OnPropertyChanged(nameof(ProgressList));
                return new PerfResult(
                    isProtobuf ? SerializerType.Protobuf : SerializerType.JSON,
                    0,
                    0
                );
            }
        }

        private static List<PerfResult> Snapshot(List<PerfResult> source)
        {
            lock (source) return source.ToList();
        }

        public void RefreshChart()
        {
            var snap = Snapshot(Results);
            PerfChart.Drawable = new PerfChartDrawable(snap);
            PerfChart.Invalidate();

            // Update average duration label
            if (Results.Count > 0)
            {
                var jsonResults = Results.Where(r => r.Type == SerializerType.JSON).ToList();
                var protoResults = Results.Where(r => r.Type == SerializerType.Protobuf).ToList();
                double avgJsonMs = jsonResults.Count > 0 ? Math.Round(jsonResults.Average(r => r.Microseconds) / 1000.0, 2) : 0;
                double avgProtoMs = protoResults.Count > 0 ? Math.Round(protoResults.Average(r => r.Microseconds) / 1000.0, 2) : 0;
                AvgDurationLabel.Text = $"Average download duration: JSON = {avgJsonMs} ms, Protobuf = {avgProtoMs} ms";
            }
            else
            {
                AvgDurationLabel.Text = "";
            }
        }

        public sealed class PerfChartDrawable : IDrawable
        {
            private readonly List<PerformancePage.PerfResult> results;
            private const int BucketCount = 7;
            private readonly (double maxMs, string label)[] Buckets;

            private static readonly Color ProtoColor = Colors.Blue;
            private static readonly Color JsonColor = Colors.Orange;

            public PerfChartDrawable(List<PerformancePage.PerfResult> results)
            {
                this.results = results ?? new();

                // Dynamische Buckets nach Download-Dauer (ms)
                double minMs = results.Count > 0 ? results.Min(r => r.Microseconds) / 1000.0 : 0;
                double maxMs = results.Count > 0 ? results.Max(r => r.Microseconds) / 1000.0 : 1;
                if (minMs == maxMs) maxMs += 1;
                string unit;
                double factor;
                if (maxMs < 2000)
                {
                    unit = "ms";
                    factor = 1.0;
                }
                else
                {
                    unit = "s";
                    factor = 0.001;
                    minMs *= factor;
                    maxMs *= factor;
                }
                // Exakte gleichmäßige Verteilung zwischen min und max
                double[] edges = new double[BucketCount + 1];
                for (int i = 0; i <= BucketCount; i++)
                    edges[i] = minMs + (maxMs - minMs) * i / BucketCount;
                Buckets = Enumerable.Range(0, BucketCount)
                    .Select(i =>
                    {
                        double from = edges[i];
                        double to = edges[i + 1];
                        string fromLabel = Math.Round(from, 2).ToString();
                        string toLabel = Math.Round(to, 2).ToString();
                        return (to, $"{fromLabel}–{toLabel} {unit}");
                    })
                    .ToArray();
            }

            public void Draw(ICanvas canvas, RectF dirtyRect)
            {
                canvas.SaveState();

                // Hintergrund
                canvas.FillColor = Colors.White;
                canvas.FillRectangle(dirtyRect);

                // Layout
                float padding = 16;
                float legendHeight = 30;
                float chartTop = dirtyRect.Top + legendHeight + padding;
                float chartBot = dirtyRect.Bottom - padding;
                float chartLeft = dirtyRect.Left + padding;
                float chartRight = dirtyRect.Right - padding;

                if (chartRight <= chartLeft || chartBot <= chartTop)
                {
                    canvas.RestoreState();
                    return;
                }

                // ---------- Legende oben ----------
                float legendX = chartLeft;
                float legendY = dirtyRect.Top + padding;
                DrawLegendSwatch(canvas, legendX, legendY, ProtoColor, "Protobuf");
                DrawLegendSwatch(canvas, legendX + 110, legendY, JsonColor, "JSON");

                // ---------- Buckets ----------
                int bucketCount = Buckets.Length;
                var protoBuckets = new int[bucketCount];
                var jsonBuckets = new int[bucketCount];

                foreach (var r in results)
                {
                    int idx = BucketIndex(r.Microseconds / 1000.0);
                    if (r.Type == PerformancePage.SerializerType.Protobuf) protoBuckets[idx]++;
                    else if (r.Type == PerformancePage.SerializerType.JSON) jsonBuckets[idx]++;
                }

                int maxCount = Math.Max(protoBuckets.DefaultIfEmpty(0).Max(),
                                        jsonBuckets.DefaultIfEmpty(0).Max());
                if (maxCount <= 0) maxCount = 1;

                // ---------- Chartbereich ----------
                float chartHeight = chartBot - chartTop;
                float chartWidth = chartRight - chartLeft;

                float groupGap = 12; // Abstand zwischen Gruppen
                float barGap = 4;    // Abstand zwischen Protobuf/JSON Balken in einer Gruppe

                // → Wichtige Änderung: barWidth korrekt berechnen
                float totalGroupsWidth = chartWidth - (bucketCount - 1) * groupGap;
                float barWidth = (totalGroupsWidth / bucketCount - barGap) / 2f;
                barWidth = MathF.Max(6, barWidth);

                float scaleY = chartHeight / maxCount;

                // Y-Achse & Gitter
                canvas.StrokeColor = Colors.LightGray;
                canvas.StrokeSize = 1;
                canvas.FontColor = Colors.DarkGray;

                int yTicks = Math.Clamp(maxCount, 3, 8);
                for (int i = 0; i <= yTicks; i++)
                {
                    float value = (maxCount / (float)yTicks) * i;
                    float y = chartBot - value * scaleY;
                    canvas.DrawLine(chartLeft, y, chartRight, y);
                    canvas.FontSize = 10;
                    canvas.DrawString(MathF.Round(value).ToString(),
                        chartLeft - 24, y - 6, 20, 12,
                        HorizontalAlignment.Right, VerticalAlignment.Center);
                }

                // Balken-Gruppen
                for (int i = 0; i < bucketCount; i++)
                {
                    float groupX = chartLeft + i * (2 * barWidth + barGap + groupGap);

                    // Protobuf
                    float protoH = protoBuckets[i] * scaleY;
                    canvas.FillColor = ProtoColor;
                    canvas.FillRectangle(groupX, chartBot - protoH, barWidth, protoH);

                    if (protoBuckets[i] > 0)
                    {
                        canvas.FontColor = Colors.White;
                        canvas.FontSize = 11;
                        canvas.DrawString(protoBuckets[i].ToString(),
                            groupX, chartBot - protoH + 2, barWidth, 14,
                            HorizontalAlignment.Center, VerticalAlignment.Top);
                    }

                    // JSON
                    float jsonH = jsonBuckets[i] * scaleY;
                    float jsonX = groupX + barWidth + barGap;
                    canvas.FillColor = JsonColor;
                    canvas.FillRectangle(jsonX, chartBot - jsonH, barWidth, jsonH);

                    if (jsonBuckets[i] > 0)
                    {
                        canvas.FontColor = Colors.White;
                        canvas.FontSize = 11;
                        canvas.DrawString(jsonBuckets[i].ToString(),
                            jsonX, chartBot - jsonH + 2, barWidth, 14,
                            HorizontalAlignment.Center, VerticalAlignment.Top);
                    }

                    // X-Labels
                    canvas.FontColor = Colors.Black;
                    canvas.FontSize = 12;
                    canvas.DrawString(Buckets[i].label, groupX, chartBot + 4,
                        2 * barWidth + barGap, 16,
                        HorizontalAlignment.Center, VerticalAlignment.Top);
                }

                canvas.RestoreState();
            }

            private int BucketIndex(double kb)
            {
                // Der schlechteste Wert (max) kommt immer in den letzten Bucket
                if (Buckets.Length >= 7 && kb >= Buckets[6].maxMs)
                    return 6;
                for (int i = 0; i < Buckets.Length; i++)
                    if (kb <= Buckets[i].maxMs) return i;
                return Buckets.Length - 1;
            }

            private static void DrawLegendSwatch(ICanvas canvas, float x, float y, Color color, string text)
            {
                canvas.FillColor = color;
                canvas.FillRectangle(x, y - 8, 14, 14);
                canvas.FontColor = Colors.Black;
                canvas.FontSize = 12;
                canvas.DrawString(text, x + 20, y - 8, 80, 16,
                    HorizontalAlignment.Left, VerticalAlignment.Center);
            }
}}    

}