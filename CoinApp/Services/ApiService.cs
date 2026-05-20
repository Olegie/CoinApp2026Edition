using CoinApp.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace CoinApp.Services
{
    public class ApiService
    {
        private const string CoinCapBaseUrl = "https://api.coincap.io/";
        private const string CoinGeckoBaseUrl = "https://api.coingecko.com/api/v3/";
        private const string CoinLoreBaseUrl = "https://api.coinlore.net/";
        private const string CryptoCompareBaseUrl = "https://min-api.cryptocompare.com/";

        private static readonly Dictionary<string, (DateTimeOffset ExpiresAt, string Json)> ResponseCache = new();
        private static readonly SemaphoreSlim RequestGate = new(1, 1);
        private static readonly TimeSpan MarketDataTtl = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan HistoricalDataTtl = TimeSpan.FromMinutes(30);

        private static readonly Dictionary<string, string> KnownSymbols = new(StringComparer.OrdinalIgnoreCase)
        {
            ["bitcoin"] = "BTC",
            ["ethereum"] = "ETH",
            ["tether"] = "USDT",
            ["binancecoin"] = "BNB",
            ["bnb"] = "BNB",
            ["solana"] = "SOL",
            ["usd-coin"] = "USDC",
            ["xrp"] = "XRP",
            ["ripple"] = "XRP",
            ["dogecoin"] = "DOGE",
            ["cardano"] = "ADA",
            ["tron"] = "TRX",
            ["wrapped-bitcoin"] = "WBTC",
            ["avalanche-2"] = "AVAX",
            ["avalanche"] = "AVAX",
            ["chainlink"] = "LINK",
            ["polkadot"] = "DOT",
            ["litecoin"] = "LTC",
            ["bitcoin-cash"] = "BCH"
        };

        private readonly HttpClient _coinCapClient;
        private readonly HttpClient _coinGeckoClient;
        private readonly HttpClient _coinLoreClient;
        private readonly HttpClient _cryptoCompareClient;

        public ApiService()
        {
            _coinCapClient = CreateClient(CoinCapBaseUrl);
            _coinGeckoClient = CreateClient(CoinGeckoBaseUrl);
            _coinLoreClient = CreateClient(CoinLoreBaseUrl);
            _cryptoCompareClient = CreateClient(CryptoCompareBaseUrl);
        }

        public async Task<Currency> GetCurrencyDetailsAsync(string id)
        {
            try
            {
                return await GetCoinCapItemAsync<Currency>($"v2/assets/{Uri.EscapeDataString(id)}");
            }
            catch
            {
                // CoinCap is currently unreliable without API credentials/DNS availability.
            }

            try
            {
                return await GetCoinLoreCurrencyAsync(id);
            }
            catch
            {
                // CoinLore is the no-key fallback for app startup; CoinGecko is the broader last resort.
            }

            var currencies = await GetCoinGeckoCurrenciesAsync(limit: 1, ids: id);
            return currencies.FirstOrDefault()
                ?? throw new InvalidOperationException("No currency data was returned.");
        }

        public async Task<Currency[]> GetCurrenciesAsync()
        {
            try
            {
                return await GetCoinCapListAsync<Currency>("v2/assets");
            }
            catch
            {
                // Fall through to no-key providers.
            }

            try
            {
                return await GetCoinLoreCurrenciesAsync(limit: 250);
            }
            catch
            {
                return await GetCoinGeckoCurrenciesAsync(limit: 250);
            }
        }

        public async Task<Currency[]> GetTopCurrenciesAsync(int topN = 10)
        {
            try
            {
                return await GetCoinCapListAsync<Currency>($"v2/assets?limit={topN}");
            }
            catch
            {
                // Fall through to no-key providers.
            }

            try
            {
                return await GetCoinLoreCurrenciesAsync(limit: topN);
            }
            catch
            {
                return await GetCoinGeckoCurrenciesAsync(limit: topN);
            }
        }

        public async Task<Market[]> GetAllMarketsAsync()
        {
            try
            {
                return await GetCoinCapListAsync<Market>("v2/markets");
            }
            catch
            {
                return await GetCryptoCompareMarketsAsync("BTC");
            }
        }

        public async Task<CurrencyMarketData> GetMarketsForCurrencyAsync(string id)
        {
            try
            {
                var markets = await GetCoinCapListAsync<Market>($"v2/markets?baseId={Uri.EscapeDataString(id)}");
                return new CurrencyMarketData
                {
                    Markets = new List<Market>(markets)
                };
            }
            catch
            {
                // Fall through to no-key providers.
            }

            try
            {
                var symbol = await GetSymbolForCurrencyAsync(id);
                return new CurrencyMarketData
                {
                    Markets = new List<Market>(await GetCryptoCompareMarketsAsync(symbol))
                };
            }
            catch
            {
                return new CurrencyMarketData
                {
                    Markets = new List<Market>(await GetCoinGeckoTickersAsync(id))
                };
            }
        }

        public async Task<List<HistoricalPriceModel>> GetMonthlyHistoricalPricesAsync(string id, DateTime startTime, DateTime endTime)
        {
            try
            {
                long startUnixTime = new DateTimeOffset(startTime).ToUnixTimeMilliseconds();
                long endUnixTime = new DateTimeOffset(endTime).ToUnixTimeMilliseconds();

                return (await GetCoinCapListAsync<HistoricalPriceModel>(
                    $"v2/assets/{Uri.EscapeDataString(id)}/history?interval=d1&start={startUnixTime}&end={endUnixTime}")).ToList();
            }
            catch
            {
                // Fall through to no-key providers.
            }

            try
            {
                return await GetCryptoCompareHistoricalPricesAsync(id, startTime, endTime);
            }
            catch
            {
                return await GetCoinGeckoHistoricalPricesAsync(id, startTime, endTime);
            }
        }

        private static HttpClient CreateClient(string baseUrl)
        {
            var client = new HttpClient
            {
                BaseAddress = new Uri(baseUrl),
                Timeout = TimeSpan.FromSeconds(15)
            };

            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.UserAgent.ParseAdd("CoinApp/1.0");
            return client;
        }

        private async Task<T> GetCoinCapItemAsync<T>(string path)
        {
            var json = await GetJsonAsync(_coinCapClient, path, MarketDataTtl);
            var result = JsonConvert.DeserializeObject<ApiResponse<T>>(json);
            if (result == null || result.Data == null)
            {
                throw new InvalidOperationException("CoinCap returned an empty response.");
            }

            return result.Data;
        }

        private async Task<T[]> GetCoinCapListAsync<T>(string path)
        {
            var json = await GetJsonAsync(_coinCapClient, path, MarketDataTtl);
            var result = JsonConvert.DeserializeObject<ApiResponseList<T>>(json);
            if (result == null || result.Data == null)
            {
                throw new InvalidOperationException("CoinCap returned an empty response.");
            }

            return result.Data;
        }

        private async Task<Currency[]> GetCoinLoreCurrenciesAsync(int limit)
        {
            var safeLimit = Math.Max(1, Math.Min(limit, 100));
            var json = await GetJsonAsync(_coinLoreClient, $"api/tickers/?start=0&limit={safeLimit}", MarketDataTtl);
            var response = JObject.Parse(json);
            var assets = response["data"] as JArray ?? new JArray();

            return assets
                .Select(MapCoinLoreCurrency)
                .Where(asset => !string.IsNullOrWhiteSpace(asset.Id))
                .ToArray();
        }

        private async Task<Currency> GetCoinLoreCurrencyAsync(string id)
        {
            var normalizedId = NormalizeId(id);
            var currencies = await GetCoinLoreCurrenciesAsync(limit: 100);
            var currency = currencies.FirstOrDefault(asset =>
                NormalizeId(asset.Id) == normalizedId ||
                string.Equals(asset.Symbol, id, StringComparison.OrdinalIgnoreCase));

            return currency ?? throw new InvalidOperationException($"CoinLore did not return data for '{id}'.");
        }

        private async Task<Currency[]> GetCoinGeckoCurrenciesAsync(int limit, string? ids = null)
        {
            var queryParts = new List<string>
            {
                "vs_currency=usd",
                "order=market_cap_desc",
                $"per_page={limit}",
                "page=1",
                "sparkline=false",
                "price_change_percentage=24h"
            };

            if (!string.IsNullOrWhiteSpace(ids))
            {
                queryParts.Add($"ids={Uri.EscapeDataString(ids)}");
            }

            var json = await GetJsonAsync(_coinGeckoClient, $"coins/markets?{string.Join("&", queryParts)}", MarketDataTtl);
            var assets = JArray.Parse(json);

            return assets
                .Select((asset, index) => new Currency
                {
                    Id = ReadString(asset["id"]),
                    Rank = ReadInt(asset["market_cap_rank"], index + 1),
                    Symbol = ReadString(asset["symbol"]).ToUpperInvariant(),
                    Name = ReadString(asset["name"]),
                    Supply = ReadDecimal(asset["circulating_supply"]),
                    MaxSupply = ReadNullableDecimal(asset["max_supply"]),
                    MarketCapUsd = ReadDecimal(asset["market_cap"]),
                    VolumeUsd24Hr = ReadDecimal(asset["total_volume"]),
                    PriceUsd = ReadDecimal(asset["current_price"]),
                    ChangePercent24Hr = ReadDecimal(asset["price_change_percentage_24h"]),
                    Vwap24Hr = 0m
                })
                .Where(asset => !string.IsNullOrWhiteSpace(asset.Id))
                .ToArray();
        }

        private async Task<Market[]> GetCryptoCompareMarketsAsync(string symbol)
        {
            var normalizedSymbol = symbol.ToUpperInvariant();
            var json = await GetJsonAsync(
                _cryptoCompareClient,
                $"data/top/exchanges/full?fsym={Uri.EscapeDataString(normalizedSymbol)}&tsym=USD&limit=50",
                MarketDataTtl);
            var response = JObject.Parse(json);
            var exchanges = response["Data"]?["Exchanges"] as JArray ?? new JArray();

            return exchanges
                .Select((exchange, index) => new Market
                {
                    RowNumber = index + 1,
                    ExchangeId = ReadString(exchange["MARKET"]),
                    Rank = index + 1,
                    BaseSymbol = ReadString(exchange["FROMSYMBOL"]),
                    BaseId = normalizedSymbol.ToLowerInvariant(),
                    QuoteSymbol = ReadString(exchange["TOSYMBOL"]),
                    QuoteId = "usd",
                    PriceQuote = ReadNullableDecimal(exchange["PRICE"]),
                    PriceUsd = ReadNullableDecimal(exchange["PRICE"]),
                    VolumeUsd24Hr = ReadNullableDecimal(exchange["VOLUME24HOURTO"]),
                    TradesCount24Hr = null
                })
                .Where(market => !string.IsNullOrWhiteSpace(market.ExchangeId))
                .ToArray();
        }

        private async Task<Market[]> GetCoinGeckoTickersAsync(string id)
        {
            var json = await GetJsonAsync(
                _coinGeckoClient,
                $"coins/{Uri.EscapeDataString(id)}/tickers?include_exchange_logo=false&page=1&depth=false&order=volume_desc",
                MarketDataTtl);
            var response = JObject.Parse(json);
            var tickers = response["tickers"] as JArray ?? new JArray();

            return tickers
                .Select(ticker =>
                {
                    var market = ticker["market"];
                    var marketId = ReadString(market?["identifier"]);
                    return new Market
                    {
                        ExchangeId = string.IsNullOrWhiteSpace(marketId) ? ReadString(market?["name"]) : marketId,
                        BaseSymbol = ReadString(ticker["base"]),
                        BaseId = ReadString(ticker["coin_id"]),
                        QuoteSymbol = ReadString(ticker["target"]),
                        QuoteId = ReadString(ticker["target_coin_id"]),
                        PriceQuote = ReadNullableDecimal(ticker["last"]),
                        PriceUsd = ReadNullableDecimal(ticker["converted_last"]?["usd"]),
                        VolumeUsd24Hr = ReadNullableDecimal(ticker["converted_volume"]?["usd"])
                    };
                })
                .Where(market => !string.IsNullOrWhiteSpace(market.ExchangeId))
                .ToArray();
        }

        private async Task<List<HistoricalPriceModel>> GetCryptoCompareHistoricalPricesAsync(string id, DateTime startTime, DateTime endTime)
        {
            var symbol = await GetSymbolForCurrencyAsync(id);
            var days = Math.Max(1, Math.Min(90, (int)Math.Ceiling((endTime - startTime).TotalDays)));
            var json = await GetJsonAsync(
                _cryptoCompareClient,
                $"data/v2/histoday?fsym={Uri.EscapeDataString(symbol)}&tsym=USD&limit={days}",
                HistoricalDataTtl);
            var response = JObject.Parse(json);
            var prices = response["Data"]?["Data"] as JArray ?? new JArray();

            return prices
                .Select(price => new HistoricalPriceModel
                {
                    Time = ReadLong(price["time"]) * 1000L,
                    PriceUsd = ReadDecimal(price["close"])
                })
                .Where(price => price.Time > 0)
                .ToList();
        }

        private async Task<List<HistoricalPriceModel>> GetCoinGeckoHistoricalPricesAsync(string id, DateTime startTime, DateTime endTime)
        {
            long startUnixTime = new DateTimeOffset(startTime).ToUnixTimeSeconds();
            long endUnixTime = new DateTimeOffset(endTime).ToUnixTimeSeconds();

            var json = await GetJsonAsync(
                _coinGeckoClient,
                $"coins/{Uri.EscapeDataString(id)}/market_chart/range?vs_currency=usd&from={startUnixTime}&to={endUnixTime}",
                HistoricalDataTtl);
            var response = JObject.Parse(json);
            var prices = response["prices"] as JArray ?? new JArray();

            return prices
                .OfType<JArray>()
                .Where(price => price.Count >= 2)
                .Select(price => new HistoricalPriceModel
                {
                    Time = ReadLong(price[0]),
                    PriceUsd = ReadDecimal(price[1])
                })
                .ToList();
        }

        private async Task<string> GetJsonAsync(HttpClient client, string path, TimeSpan ttl)
        {
            var cacheKey = $"{client.BaseAddress}{path}";
            var now = DateTimeOffset.UtcNow;

            lock (ResponseCache)
            {
                if (ResponseCache.TryGetValue(cacheKey, out var cached) && cached.ExpiresAt > now)
                {
                    return cached.Json;
                }
            }

            await RequestGate.WaitAsync();
            try
            {
                now = DateTimeOffset.UtcNow;
                lock (ResponseCache)
                {
                    if (ResponseCache.TryGetValue(cacheKey, out var cached) && cached.ExpiresAt > now)
                    {
                        return cached.Json;
                    }
                }

                var response = await client.GetAsync(path);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();

                lock (ResponseCache)
                {
                    ResponseCache[cacheKey] = (DateTimeOffset.UtcNow.Add(ttl), json);
                }

                await Task.Delay(200);
                return json;
            }
            finally
            {
                RequestGate.Release();
            }
        }

        private async Task<string> GetSymbolForCurrencyAsync(string id)
        {
            if (KnownSymbols.TryGetValue(id, out var knownSymbol))
            {
                return knownSymbol;
            }

            try
            {
                var currency = await GetCoinLoreCurrencyAsync(id);
                if (!string.IsNullOrWhiteSpace(currency.Symbol))
                {
                    return currency.Symbol.ToUpperInvariant();
                }
            }
            catch
            {
                // Fall through to a best-effort symbol derived from the id.
            }

            return id.Replace("-", string.Empty).ToUpperInvariant();
        }

        private static Currency MapCoinLoreCurrency(JToken asset)
        {
            return new Currency
            {
                Id = ReadString(asset["nameid"]),
                Rank = ReadInt(asset["rank"]),
                Symbol = ReadString(asset["symbol"]).ToUpperInvariant(),
                Name = ReadString(asset["name"]),
                Supply = ReadDecimal(asset["csupply"]),
                MaxSupply = ReadNullableDecimal(asset["msupply"]),
                MarketCapUsd = ReadDecimal(asset["market_cap_usd"]),
                VolumeUsd24Hr = ReadDecimal(asset["volume24"]),
                PriceUsd = ReadDecimal(asset["price_usd"]),
                ChangePercent24Hr = ReadDecimal(asset["percent_change_24h"]),
                Vwap24Hr = 0m
            };
        }

        private static string NormalizeId(string id)
        {
            return id.Trim().ToLowerInvariant();
        }

        private static string ReadString(JToken? token)
        {
            return token == null || token.Type == JTokenType.Null ? string.Empty : token.ToString();
        }

        private static int ReadInt(JToken? token, int fallback = 0)
        {
            return int.TryParse(ReadString(token), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : fallback;
        }

        private static long ReadLong(JToken? token)
        {
            return long.TryParse(ReadString(token), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : 0L;
        }

        private static decimal ReadDecimal(JToken? token)
        {
            return ReadNullableDecimal(token) ?? 0m;
        }

        private static decimal? ReadNullableDecimal(JToken? token)
        {
            var text = ReadString(token);
            return decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? value
                : null;
        }
    }
}
