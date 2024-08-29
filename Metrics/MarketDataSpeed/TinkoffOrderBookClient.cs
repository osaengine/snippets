using Google.Api;
using Grpc.Core;
using Grpc.Net.Client;
using Tinkoff.InvestApi;
using Tinkoff.InvestApi.V1;

class TinkoffOrderBookClient : IDisposable
{
	private readonly GrpcChannel _channel;
	private readonly MarketDataStreamService.MarketDataStreamServiceClient _client;

	public event Action<OrderBook> OrderBookReceived;

	public TinkoffOrderBookClient(string token)
	{
		_channel = GrpcChannel.ForAddress("https://invest-public-api.tinkoff.ru:443", new GrpcChannelOptions
		{
			Credentials = ChannelCredentials.Create(new SslCredentials(), CallCredentials.FromInterceptor(
				(context, metadata) =>
				{
					metadata.Add("Authorization", $"Bearer {token}");
					return Task.CompletedTask;
				}))
		});
		_client = new(_channel);
	}

	public async Task SubscribeToOrderBook(string uid, int depth, CancellationToken cancellationToken)
	{
		var subscribeRequest = new MarketDataRequest
		{
			SubscribeOrderBookRequest = new SubscribeOrderBookRequest
			{
				Instruments = { new OrderBookInstrument { InstrumentId = uid, Depth = depth } },
				SubscriptionAction = SubscriptionAction.Subscribe,
			}
		};

		using var call = _client.MarketDataStream(cancellationToken: cancellationToken);

		var responseReaderTask = Task.Run(async () =>
		{
			await foreach (var response in call.ResponseStream.ReadAllAsync(cancellationToken))
			{
				if (response.Orderbook != null)
				{
					OrderBookReceived?.Invoke(response.Orderbook);
				}
			}
		}, cancellationToken);

		await call.RequestStream.WriteAsync(subscribeRequest, cancellationToken);

		await responseReaderTask;

		await call.RequestStream.CompleteAsync();
	}

	public void Dispose()
	{
		_channel.Dispose();
	}
}