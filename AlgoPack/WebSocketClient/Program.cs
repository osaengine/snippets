using Newtonsoft.Json.Linq;
using OsaEngine.MoexAlgoPack;

var client = new MoexAlgoPackSocketClient("wss://iss.moex.com/infocx/v3/websocket");
await client.ConnectAsync("passport", "<login>", "<password>");

var orderBooks = new Dictionary<string, OrderBook>();

void processMessage(JObject message)
{
	var headers = message["headers"];
	var body = message["body"];

	var subscription = headers["subscription"]?.ToString();

	if (subscription is null || !orderBooks.TryGetValue(subscription, out var orderBook))
		return;

	var data = body["data"];

	foreach (var entry in data)
	{
		var side = entry[0].ToString();
		var priceArray = entry[1].ToObject<decimal[]>();
		var price = priceArray[0];
		var quantity = entry[2].Value<int>();

		orderBook.UpdateEntry(side, price, quantity);
	}

	printOrderBook(orderBook);
}

void printOrderBook(OrderBook orderBook)
{
	Console.WriteLine("Asks:");

	foreach (var ask in orderBook.Asks.Take(5).OrderByDescending(e => e.Key))
	{
		Console.WriteLine($"{ask.Key}: {ask.Value.Quantity}");
	}

	Console.WriteLine("Bids:");
	foreach (var bid in orderBook.Bids.Take(5))
	{
		Console.WriteLine($"{bid.Key}: {bid.Value.Quantity}");
	}
}

_ = Task.Run(async () =>
{
	await Task.Yield();
	await client.ReceiveAsync(processMessage);
});

await Task.Delay(2000);

var subId = Guid.NewGuid().ToString();
var symbol = "MXSE.TQBR.SBER";
orderBooks.Add(subId, new(symbol));

await client.SubscribeAsync(subId, "MXSE.orderbooks", $"TICKER=\"{symbol}\"");

Console.ReadLine();

await client.CloseAsync();