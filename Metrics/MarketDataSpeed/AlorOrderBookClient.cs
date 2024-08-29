using System;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

class AlorOrderBookClient(string refreshToken) : IDisposable
{
	private readonly string _refreshToken = refreshToken;
	private readonly ClientWebSocket _webSocket = new();
	private readonly HttpClient _httpClient = new HttpClient();

	public event Action<object> OrderBookReceived;

	private async Task<string> GetJwtTokenAsync(CancellationToken cancellationToken)
	{
		var response = await _httpClient.PostAsync($"https://oauth.alor.ru/refresh?token={_refreshToken}", new StringContent(string.Empty), cancellationToken);
		response.EnsureSuccessStatusCode();
		dynamic content = JObject.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
		return (string)content.AccessToken;
	}

	public async Task SubscribeToOrderBook(string symbol, int depth, CancellationToken cancellationToken)
	{
		var jwt = await GetJwtTokenAsync(cancellationToken);

		await _webSocket.ConnectAsync(new Uri("wss://api.alor.ru/ws"), cancellationToken);

		var subscriptionMessage = new
		{
			opcode = "OrderBookGetAndSubscribe",
			code = symbol,
			exchange = "MOEX",
			depth,
			format = "Simple",
			guid = Guid.NewGuid().ToString(),
			token = jwt
		};

		var json = JsonConvert.SerializeObject(subscriptionMessage);
		var buffer = Encoding.UTF8.GetBytes(json);
		await _webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, cancellationToken);

		_ = ReceiveMessagesAsync(cancellationToken);
	}

	private async Task ReceiveMessagesAsync(CancellationToken cancellationToken)
	{
		var buffer = new byte[8192];
		while (!cancellationToken.IsCancellationRequested)
		{
			var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
			if (result.MessageType == WebSocketMessageType.Text)
			{
				var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
				var json = JObject.Parse(message);
				OrderBookReceived?.Invoke(json);
			}
		}
	}

	public void Dispose()
	{
		_webSocket.Dispose();
	}
}
