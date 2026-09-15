public class CandlestickData
{
    public DateTime OpenTime { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public decimal Volume { get; set; }
    public decimal MarketCap { get; set; } // Market Cap del token
    public decimal Supply { get; set; } // Supply circolante
    public bool IsBullish => Close >= Open;
}

public class TokenChartInfo
{
    public string Symbol { get; set; } = string.Empty;
    public decimal AthPrice { get; set; }
    public DateTime AthDate { get; set; }
    public decimal CurrentPrice { get; set; }
}