
using OsaEngine.MoexAlgoPack;

var client = new MoexAlgoPackSocketClient("wss://iss.moex.com/infocx/v3/websocket");
await client.ConnectAsync("passport", "<login>", "<password>");

_ = Task.Run(async () =>
{
	await Task.Yield();
	await client.ReceiveAsync(Console.WriteLine);
});

await Task.Delay(2000);
await client.SubscribeAsync(Guid.NewGuid(), "MXSE.orderbooks", "TICKER=\"MXSE.TQBR.SBER\"");

Console.ReadLine();

await client.CloseAsync();