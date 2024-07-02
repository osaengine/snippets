namespace OsaEngine.MoexAlgoPack;

public class OrderBookEntry
{
	public decimal Price { get; set; }
	public int Quantity { get; set; }
}

public class OrderBook(string symbol)
{
	public string Symbol { get; } = symbol;
	public SortedDictionary<decimal, OrderBookEntry> Bids { get; } = new(Comparer<decimal>.Create((a, b) => b.CompareTo(a)));
	public SortedDictionary<decimal, OrderBookEntry> Asks { get; } = [];

	public void UpdateEntry(string side, decimal price, int quantity)
	{
		var book = side == "B" ? Bids : Asks;

		if (quantity == 0)
		{
			book.Remove(price);
		}
		else
		{
			book[price] = new() { Price = price, Quantity = quantity };
		}
	}
}
