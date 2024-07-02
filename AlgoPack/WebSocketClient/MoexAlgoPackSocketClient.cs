namespace OsaEngine.MoexAlgoPack;

using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;
using Newtonsoft.Json.Linq;

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

	public async ValueTask ReceiveAsync(Action<JObject> received, CancellationToken cancellationToken = default)
	{
		var buffer = new byte[1024 * 4];
		var messageBuffer = new byte[buffer.Length];
		var messageLength = 0;

		while (_clientWebSocket.State == WebSocketState.Open)
		{
			var result = await _clientWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

			if (messageLength + result.Count > messageBuffer.Length)
			{
				Array.Resize(ref messageBuffer, Math.Max(messageBuffer.Length * 2, messageLength + result.Count));
			}

			Buffer.BlockCopy(buffer, 0, messageBuffer, messageLength, result.Count);
			messageLength += result.Count;

			if (result.EndOfMessage)
			{
				if (result.MessageType == WebSocketMessageType.Close)
				{
					await _clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, cancellationToken);
				}
				else
				{
					var message = Encoding.UTF8.GetString(messageBuffer, 0, messageLength);
					received(ParseStompMessage(message));
				}

				messageLength = 0;
			}
		}
	}

	private static JObject ParseStompMessage(string message)
	{
		var lines = message.Split('\n');
		var headers = new JObject();
		var bodyStart = 0;

		for (int i = 0; i < lines.Length; i++)
		{
			if (string.IsNullOrWhiteSpace(lines[i]))
			{
				bodyStart = i + 1;
				break;
			}

			var headerParts = lines[i].Split(':', 2);
			if (headerParts.Length == 2)
			{
				headers[headerParts[0].Trim()] = headerParts[1].Trim();
			}
		}

		var body = string.Join("\n", lines.Skip(bodyStart));
		var bodyJson = JToken.Parse(body);

		var result = new JObject
		{
			["headers"] = headers,
			["body"] = bodyJson
		};

		return result;
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