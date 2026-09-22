# MarketCap Calculator Pre-Release

Applicazione desktop in via di sviluppo per il calcolo del potenziale profitto di investimenti in criptovalute basato sulla crescita del Market Cap.

## Funzionalità

- Calcolo profitto potenziale basato su Market Cap
- Ricerca token universale (Binance, DexScreener, CoinGecko + CoinMarketCap API)
- Grafico dei prezzi in tempo reale con timeframe multipli
- Grafico predizioni investimento
- Gestione wallet Solana con saldi reali
- Auto-refresh dei prezzi
- Esportazione CSV e PDF
- Modalità "Investi solo con wallet"
- Protezione con password (crittografia AES-256)
- Recupero password via Telegram
- Auto-refresh dei saldi wallet

## Tecnologie

- C# .NET 8
- WPF
- MVVM (CommunityToolkit.Mvvm)
- LiveCharts2
- SkiaSharp
- API CoinMarketCap (opzionale)
- API DexScreener
- API CoinGecko
- API Binance

## Installazione

### Requisiti

- Windows 10/11
- .NET 8 SDK
- Visual Studio 2022 (opzionale)

### Build

```bash
git clone https://github.com/lore6e7a6f/MarketCapCalculator.git
cd MarketCapCalculator

1. start build.bat

2. dotnet restore
   dotnet build
   dotnet run --project MarketCapCalculator

Se dovessero crearsi conflitti con SDK obsolete, consiglio di aggiornarle.
Se non possibile si può modificare il global.json con la propria versione installata
```

## Configurazione

1. Al primo avvio crea una password per proteggere i dati.
2. Inserisci la tua API key di CoinMarketCap per dati più completi (facoltativo).
3. Configura il bot Telegram per il recupero password (opzionale ma consigliato).

## Licenza

MIT License

## Autore

- [lore6e7a6f](https://github.com/lore6e7a6f)
