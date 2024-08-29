using var tinkoffClient = new TinkoffOrderBookClient("<TINKOFF-TOKEN>");
using var alorClient = new AlorOrderBookClient("<ALOR-TOKEN>");

var monitor = new PerformanceMonitor(TimeSpan.FromSeconds(5));

tinkoffClient.OrderBookReceived += (orderBook) => monitor.RecordUpdate("Tinkoff");
alorClient.OrderBookReceived += (orderBook) => monitor.RecordUpdate("Alor");

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (s, e) => cts.Cancel();

_ = Task.Run(async () =>
{
	try
	{
		await tinkoffClient.SubscribeToOrderBook("FUTSBRF09240", 10, cts.Token);
	}
	catch (Exception error)
	{
		Console.WriteLine(error);
	}
}, cts.Token);

_ = Task.Run(async () =>
{
	try
	{
		await alorClient.SubscribeToOrderBook("SRU4", 10, cts.Token);
	}
	catch (Exception error)
	{
		Console.WriteLine(error);
	}
}, cts.Token);

await monitor.StartReportingAsync(cts.Token);
