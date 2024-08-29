using System.Collections.Concurrent;

class PerformanceMonitor(TimeSpan reportInterval)
{
	private readonly ConcurrentDictionary<string, (long TotalUpdates, ConcurrentQueue<DateTime> UpdateTimes)> _apiStats = new();
	private readonly TimeSpan _reportInterval = reportInterval;
	private readonly DateTime _startTime = DateTime.UtcNow;

	public void RecordUpdate(string apiId)
	{
		_apiStats.AddOrUpdate(apiId,
			_ => (1, new ConcurrentQueue<DateTime>(new[] { DateTime.UtcNow })),
			(_, stats) =>
			{
				stats.UpdateTimes.Enqueue(DateTime.UtcNow);
				while (stats.UpdateTimes.Count > 10000 && stats.UpdateTimes.TryDequeue(out var __)) { }
				return (stats.TotalUpdates + 1, stats.UpdateTimes);
			});
	}

	public async Task StartReportingAsync(CancellationToken cancellationToken)
	{
		while (!cancellationToken.IsCancellationRequested)
		{
			await Task.Delay(_reportInterval, cancellationToken);
			PrintPerformanceReport();
		}
	}

	private void PrintPerformanceReport()
	{
		var now = DateTime.UtcNow;
		var duration = now - _startTime;

		Console.Clear();
		Console.WriteLine($"=== Отчет о производительности (Обновлено: {now:HH:mm:ss}) ===");
		Console.WriteLine($"Длительность мониторинга: {duration:hh\\:mm\\:ss}");

		var apiPerformance = _apiStats.Select(kvp =>
		{
			var (apiId, (totalUpdates, updateTimes)) = kvp;
			var updatesPerSecond = totalUpdates / duration.TotalSeconds;
			var recentUpdates = updateTimes.Count(t => t > now.AddSeconds(-1));
			return new { ApiId = apiId, TotalUpdates = totalUpdates, UpdatesPerSecond = updatesPerSecond, RecentUpdates = recentUpdates };
		}).OrderByDescending(x => x.UpdatesPerSecond).ToList();

		var maxSpeed = apiPerformance.Max(x => x.UpdatesPerSecond);

		foreach (var api in apiPerformance)
		{
			var relativeSpeed = api.UpdatesPerSecond / maxSpeed;
			Console.WriteLine($"\nAPI: {api.ApiId}");
			Console.WriteLine($"  Всего обновлений: {api.TotalUpdates:N0}");
			Console.WriteLine($"  Средняя скорость: {api.UpdatesPerSecond:F2} обновлений/сек");
			Console.WriteLine($"  Текущая скорость: {api.RecentUpdates} обновлений/сек");
			Console.WriteLine($"  Относительная скорость: {relativeSpeed:P2}");
		}

		if (apiPerformance.Count > 1)
		{
			Console.WriteLine("\nСравнение скоростей API:");
			for (int i = 0; i < apiPerformance.Count; i++)
			{
				for (int j = i + 1; j < apiPerformance.Count; j++)
				{
					var ratio = apiPerformance[i].UpdatesPerSecond / apiPerformance[j].UpdatesPerSecond;
					Console.WriteLine($"  {apiPerformance[i].ApiId} в {ratio:F2} раз быстрее чем {apiPerformance[j].ApiId}");
				}
			}
		}
	}
}
