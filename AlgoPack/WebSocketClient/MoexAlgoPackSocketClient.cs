namespace OsaEngine.MoexAlgoPack;

using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;

public class MoexAlgoPackSocketClient(string url) : IAsyncDisposable
{
	private readonly Uri _uri = new(url);
	private readonly ClientWebSocket _clientWebSocket = new();

	public async ValueTask ConnectAsync(string domain = "DEMO", string login = "guest", string passcode = "guest", CancellationToken cancellationToken = default)
	{
		await _clientWebSocket.ConnectAsync(_uri, cancellationToken);

		await SendAsync($"CONNECT\ndomain:{domain}\nlogin:{login}\npasscode:{passcode}\n\n\0", cancellationToken);
	}

	public ValueTask SubscribeAsync(object id, string destination, string selector, CancellationToken cancellationToken = default)
	{
		return SendAsync($"SUBSCRIBE\nid:{id}\ndestination:{destination}\nselector:{selector}\n\n\0", cancellationToken);
	}

	public async ValueTask SendAsync(string message, CancellationToken cancellationToken = default)
	{
		var messageBytes = Encoding.UTF8.GetBytes(message);
		var segment = new ArraySegment<byte>(messageBytes);
		await _clientWebSocket.SendAsync(segment, WebSocketMessageType.Text, true, cancellationToken);
	}

	public async ValueTask ReceiveAsync(Action<string> received, CancellationToken cancellationToken = default)
	{
		var buffer = new byte[1024 * 4];

		while (_clientWebSocket.State == WebSocketState.Open)
		{
			var result = await _clientWebSocket.ReceiveAsync(new(buffer), cancellationToken);

			if (result.MessageType == WebSocketMessageType.Close)
			{
				await _clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, default);
			}
			else
			{
				var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
				received(message);
			}
		}
	}

	public async ValueTask CloseAsync()
	{
		await _clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", default);
		_clientWebSocket.Dispose();
	}

	ValueTask IAsyncDisposable.DisposeAsync()
	{
		GC.SuppressFinalize(this);
		return CloseAsync();
	}
}