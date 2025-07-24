using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using FinanceManager.Models;

namespace FinanceManager.Services
{
    public class YahooFinanceService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _quoteUrl = "https://query1.finance.yahoo.com/v8/finance/chart/";
        private readonly string _searchUrl = "https://query1.finance.yahoo.com/v1/finance/search";
        private readonly string _backupQuoteUrl = "https://query2.finance.yahoo.com/v8/finance/chart/";
        private bool _disposed = false;

        public YahooFinanceService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        /// <summary>
        /// Pesquisa símbolos financeiros na Yahoo Finance
        /// </summary>
        public async Task<List<SearchResult>> SearchSymbolsAsync(string query)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query) || query.Length < 1)
                    return new List<SearchResult>();

                LoggingService.LogInfo($"Pesquisando símbolos para: {query}");

                var searchUrl = $"{_searchUrl}?q={Uri.EscapeDataString(query)}&lang=en-US&region=US&quotesCount=25&newsCount=0";

                var response = await _httpClient.GetStringAsync(searchUrl);
                var jsonDoc = JsonDocument.Parse(response);

                var results = new List<SearchResult>();

                if (!jsonDoc.RootElement.TryGetProperty("quotes", out var quotesElement))
                {
                    LoggingService.LogWarning($"Nenhuma propriedade 'quotes' encontrada para: {query}");
                    return results;
                }

                foreach (var quote in quotesElement.EnumerateArray())
                {
                    try
                    {
                        var symbol = quote.TryGetProperty("symbol", out var symbolProp) ?
                            symbolProp.GetString() : "";

                        if (string.IsNullOrEmpty(symbol))
                            continue;

                        var shortName = quote.TryGetProperty("shortname", out var shortProp) ?
                            shortProp.GetString() : "";

                        var longName = quote.TryGetProperty("longname", out var longProp) ?
                            longProp.GetString() : "";

                        var quoteType = quote.TryGetProperty("quoteType", out var typeProp) ?
                            typeProp.GetString() : "";

                        var exchange = quote.TryGetProperty("exchange", out var exchangeProp) ?
                            exchangeProp.GetString() : "";

                        // Filtrar resultados irrelevantes
                        if (symbol.Contains("^") && !symbol.StartsWith("^")) // Índices estranhos
                            continue;

                        results.Add(new SearchResult
                        {
                            Symbol = symbol,
                            ShortName = shortName,
                            LongName = longName,
                            Type = MapQuoteType(quoteType),
                            Exchange = exchange
                        });
                    }
                    catch (Exception ex)
                    {
                        LoggingService.LogWarning($"Erro ao processar resultado de pesquisa: {ex.Message}");
                        continue;
                    }
                }

                LoggingService.LogInfo($"Encontrados {results.Count} resultados para: {query}");
                return results.Take(20).ToList(); // Limitar a 20 resultados
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Erro ao pesquisar símbolos para '{query}'", ex);
                return new List<SearchResult>();
            }
        }

        /// <summary>
        /// Obtém dados de cotação de um símbolo
        /// </summary>
        public async Task<QuoteData?> GetQuoteDataAsync(string symbol)
        {
            try
            {
                LoggingService.LogInfo($"Obtendo cotação para: {symbol}");

                // Tentar URL principal primeiro
                var quote = await TryGetQuoteFromUrl(_quoteUrl + symbol);

                // Se falhar, tentar URL backup
                if (quote == null)
                {
                    quote = await TryGetQuoteFromUrl(_backupQuoteUrl + symbol);
                }

                if (quote != null)
                {
                    LoggingService.LogInfo($"Cotação obtida para {symbol}: {quote.CurrentPrice:C}");
                }
                else
                {
                    LoggingService.LogWarning($"Não foi possível obter cotação para: {symbol}");
                }

                return quote;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Erro ao obter cotação para {symbol}", ex);
                return null;
            }
        }

        /// <summary>
        /// Obtém o preço atual de um símbolo
        /// </summary>
        public async Task<decimal?> GetCurrentPriceAsync(string symbol)
        {
            try
            {
                var data = await GetQuoteDataAsync(symbol);
                return data?.CurrentPrice;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Erro ao obter preço para {symbol}", ex);
                return null;
            }
        }

        /// <summary>
        /// Obtém múltiplos preços de uma só vez
        /// </summary>
        public async Task<Dictionary<string, decimal?>> GetMultiplePricesAsync(List<string> symbols)
        {
            var result = new Dictionary<string, decimal?>();

            foreach (var symbol in symbols)
            {
                try
                {
                    var price = await GetCurrentPriceAsync(symbol);
                    result[symbol] = price;

                    // Pequeno delay para evitar rate limiting
                    await Task.Delay(200);
                }
                catch (Exception ex)
                {
                    LoggingService.LogError($"Erro ao obter preço para {symbol}", ex);
                    result[symbol] = null;
                }
            }

            return result;
        }

        /// <summary>
        /// Verifica se um mercado específico está aberto
        /// </summary>
        public async Task<bool> IsMarketOpenAsync(string symbol)
        {
            try
            {
                var quote = await GetQuoteDataAsync(symbol);
                if (quote != null)
                {
                    return quote.MarketState.ToUpper() == "REGULAR";
                }

                // Fallback baseado no horário
                return symbol.Contains("^STOXX") ? IsEuropeanMarketOpen(DateTime.Now) : IsUSMarketOpen(DateTime.Now);
            }
            catch
            {
                // Fallback baseado no horário se a API falhar
                return symbol.Contains("^STOXX") ? IsEuropeanMarketOpen(DateTime.Now) : IsUSMarketOpen(DateTime.Now);
            }
        }

        /// <summary>
        /// Converte USD para EUR
        /// </summary>
        public async Task<decimal> ConvertUsdToEurAsync(decimal usdAmount)
        {
            try
            {
                var eurUsdQuote = await GetQuoteDataAsync("EURUSD=X");
                if (eurUsdQuote != null && eurUsdQuote.CurrentPrice > 0)
                {
                    return usdAmount / eurUsdQuote.CurrentPrice;
                }

                // Taxa de fallback se a API falhar
                return usdAmount * 0.85m; // Aproximação
            }
            catch
            {
                return usdAmount * 0.85m; // Taxa de fallback
            }
        }

        private async Task<QuoteData?> TryGetQuoteFromUrl(string url)
        {
            try
            {
                var response = await _httpClient.GetStringAsync(url);
                using var document = JsonDocument.Parse(response);
                var root = document.RootElement;

                if (!root.TryGetProperty("chart", out var chart) ||
                    !chart.TryGetProperty("result", out var result) ||
                    result.GetArrayLength() == 0)
                {
                    return null;
                }

                var quoteData = result[0];
                var meta = quoteData.GetProperty("meta");

                var currentPrice = meta.GetProperty("regularMarketPrice").GetDecimal();
                var previousClose = meta.GetProperty("previousClose").GetDecimal();
                var currency = meta.TryGetProperty("currency", out var currencyProp) ?
                    currencyProp.GetString() ?? "USD" : "USD";
                var symbol = meta.TryGetProperty("symbol", out var symbolProp) ?
                    symbolProp.GetString() ?? "" : "";
                var marketState = meta.TryGetProperty("marketState", out var stateProp) ?
                    stateProp.GetString() ?? "" : "";
                var longName = meta.TryGetProperty("longName", out var nameProp) ?
                    nameProp.GetString() : null;

                var change = currentPrice - previousClose;
                var changePercent = previousClose > 0 ? (change / previousClose) * 100 : 0;

                return new QuoteData
                {
                    Symbol = symbol,
                    CurrentPrice = currentPrice,
                    PreviousClose = previousClose,
                    Change = change,
                    ChangePercent = changePercent,
                    Currency = currency,
                    MarketState = marketState,
                    LongName = longName,
                    LastUpdated = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Erro ao fazer request para URL: {url}", ex);
                return null;
            }
        }

        private string MapQuoteType(string quoteType)
        {
            return quoteType?.ToUpper() switch
            {
                "EQUITY" => "EQUITY",
                "ETF" => "ETF",
                "MUTUALFUND" => "MUTUALFUND",
                "INDEX" => "INDEX",
                "CRYPTOCURRENCY" => "CRYPTOCURRENCY",
                "CURRENCY" => "CURRENCY",
                "FUTURE" => "FUTURE",
                "OPTION" => "OPTION",
                _ => "EQUITY" // Default para ações
            };
        }

        private bool IsUSMarketOpen(DateTime time)
        {
            try
            {
                var estTime = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(time, "Eastern Standard Time");

                if (estTime.DayOfWeek == DayOfWeek.Saturday || estTime.DayOfWeek == DayOfWeek.Sunday)
                    return false;

                var marketOpen = new TimeSpan(9, 30, 0);
                var marketClose = new TimeSpan(16, 0, 0);

                return estTime.TimeOfDay >= marketOpen && estTime.TimeOfDay <= marketClose;
            }
            catch
            {
                return false;
            }
        }

        private bool IsEuropeanMarketOpen(DateTime time)
        {
            try
            {
                var cetTime = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(time, "Central European Standard Time");

                if (cetTime.DayOfWeek == DayOfWeek.Saturday || cetTime.DayOfWeek == DayOfWeek.Sunday)
                    return false;

                var marketOpen = new TimeSpan(9, 0, 0);
                var marketClose = new TimeSpan(17, 30, 0);

                return cetTime.TimeOfDay >= marketOpen && cetTime.TimeOfDay <= marketClose;
            }
            catch
            {
                return false;
            }
        }

        // Implementação do IDisposable
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _httpClient?.Dispose();
                }
                _disposed = true;
            }
        }

        ~YahooFinanceService()
        {
            Dispose(false);
        }
    }
}