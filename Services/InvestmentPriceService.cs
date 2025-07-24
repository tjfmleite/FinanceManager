using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using FinanceManager.Data;
using FinanceManager.Models;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Services
{
    public class InvestmentPriceService : IDisposable
    {
        private readonly YahooFinanceService _yahooFinance;
        private readonly InvestmentService _investmentService;
        private readonly CultureInfo _culture = new CultureInfo("pt-PT");
        private bool _disposed = false;

        public InvestmentPriceService()
        {
            _yahooFinance = new YahooFinanceService();
            _investmentService = new InvestmentService();
        }

        /// <summary>
        /// Atualiza preços de todos os investimentos ativos de um utilizador
        /// </summary>
        public async Task<PriceUpdateResult> UpdateAllPricesAsync(int userId)
        {
            var result = new PriceUpdateResult();

            try
            {
                // Obter investimentos ativos
                var investments = await _investmentService.GetInvestmentsByUserIdAsync(userId);
                var activeInvestments = investments.Where(i => i.IsActive && !string.IsNullOrEmpty(i.Name)).ToList();

                if (!activeInvestments.Any())
                {
                    result.Message = "Nenhum investimento ativo encontrado.";
                    return result;
                }

                // Extrair símbolos únicos
                var symbols = activeInvestments
                    .Select(i => ExtractSymbolFromName(i.Name))
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Distinct()
                    .ToList();

                if (!symbols.Any())
                {
                    result.Message = "Nenhum símbolo válido encontrado nos investimentos.";
                    return result;
                }

                // Obter preços da API
                var prices = await _yahooFinance.GetMultiplePricesAsync(symbols);

                // Atualizar investimentos
                foreach (var investment in activeInvestments)
                {
                    var symbol = ExtractSymbolFromName(investment.Name);
                    if (string.IsNullOrEmpty(symbol) || !prices.ContainsKey(symbol))
                        continue;

                    var newPrice = prices[symbol];
                    if (!newPrice.HasValue)
                        continue;

                    // Converter de USD para EUR se necessário
                    var priceInEur = await ConvertToEurIfNeeded(newPrice.Value, symbol);

                    var oldPrice = investment.CurrentPrice;
                    investment.CurrentPrice = priceInEur;
                    investment.UpdatedDate = DateTime.Now;

                    // Guardar na base de dados
                    await _investmentService.UpdateInvestmentAsync(investment);

                    result.UpdatedInvestments.Add(new InvestmentPriceUpdate
                    {
                        InvestmentId = investment.Id,
                        Name = investment.Name,
                        Symbol = symbol,
                        OldPrice = oldPrice,
                        NewPrice = priceInEur,
                        Change = oldPrice.HasValue ? priceInEur - oldPrice.Value : 0,
                        ChangePercent = oldPrice.HasValue && oldPrice.Value > 0 ?
                            ((priceInEur - oldPrice.Value) / oldPrice.Value) * 100 : 0
                    });

                    result.SuccessCount++;
                }

                result.IsSuccess = true;
                result.Message = $"✅ {result.SuccessCount} investimentos atualizados com sucesso!";

                LoggingService.LogInfo($"Atualização de preços concluída: {result.SuccessCount} investimentos");
                return result;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao atualizar preços", ex);
                result.IsSuccess = false;
                result.Message = $"❌ Erro ao atualizar preços: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// Atualiza o preço de um único investimento
        /// </summary>
        public async Task<bool> UpdateSingleInvestmentPriceAsync(int investmentId)
        {
            try
            {
                // Obter o investimento
                var investment = await _investmentService.GetInvestmentByIdAsync(investmentId);
                if (investment == null || !investment.IsActive)
                    return false;

                // Extrair símbolo
                var symbol = ExtractSymbolFromName(investment.Name);
                if (string.IsNullOrEmpty(symbol))
                    return false;

                // Obter preço atual
                var currentPrice = await _yahooFinance.GetCurrentPriceAsync(symbol);
                if (!currentPrice.HasValue)
                    return false;

                // Converter para EUR se necessário
                var priceInEur = await ConvertToEurIfNeeded(currentPrice.Value, symbol);

                // Atualizar investimento
                investment.CurrentPrice = priceInEur;
                investment.UpdatedDate = DateTime.Now;

                return await _investmentService.UpdateInvestmentAsync(investment);
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Erro ao atualizar preço do investimento {investmentId}", ex);
                return false;
            }
        }

        /// <summary>
        /// Pesquisa símbolos financeiros
        /// </summary>
        public async Task<List<SearchResult>> SearchSymbolsAsync(string query)
        {
            try
            {
                return await _yahooFinance.SearchSymbolsAsync(query);
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Erro ao pesquisar símbolos para '{query}'", ex);
                return new List<SearchResult>();
            }
        }

        /// <summary>
        /// Obtém cotação em tempo real
        /// </summary>
        public async Task<QuoteData?> GetQuoteAsync(string symbol)
        {
            return await _yahooFinance.GetQuoteDataAsync(symbol);
        }

        /// <summary>
        /// Verifica se os mercados estão abertos
        /// </summary>
        public async Task<MarketStatus> GetMarketStatusAsync()
        {
            try
            {
                var isUsOpen = await _yahooFinance.IsMarketOpenAsync("SPY"); // S&P 500
                var isEuropeanOpen = await _yahooFinance.IsMarketOpenAsync("^STOXX50E"); // Euro Stoxx 50

                return new MarketStatus
                {
                    IsUSMarketOpen = isUsOpen,
                    IsEuropeanMarketOpen = isEuropeanOpen,
                    LastChecked = DateTime.Now
                };
            }
            catch
            {
                return new MarketStatus
                {
                    IsUSMarketOpen = false,
                    IsEuropeanMarketOpen = false,
                    LastChecked = DateTime.Now
                };
            }
        }

        private string ExtractSymbolFromName(string investmentName)
        {
            // Tentar extrair símbolo do nome do investimento
            // Exemplos: "Apple Inc. (AAPL)", "Microsoft (MSFT)", "AAPL", etc.

            if (string.IsNullOrEmpty(investmentName))
                return "";

            // Procurar por símbolo entre parênteses
            var start = investmentName.LastIndexOf('(');
            var end = investmentName.LastIndexOf(')');

            if (start >= 0 && end > start)
            {
                return investmentName.Substring(start + 1, end - start - 1).Trim().ToUpper();
            }

            // Se não há parênteses, assumir que o nome é o símbolo
            var cleanName = investmentName.Trim().ToUpper();

            // Lista de símbolos conhecidos (pode ser expandida)
            var knownSymbols = new[] { "AAPL", "MSFT", "GOOGL", "AMZN", "TSLA", "META", "NVDA", "BTC-USD", "ETH-USD" };

            if (knownSymbols.Contains(cleanName))
                return cleanName;

            // Retornar o nome limpo se for curto (provavelmente um símbolo)
            if (cleanName.Length <= 5 && cleanName.All(char.IsLetter))
                return cleanName;

            return cleanName; // Retornar como está e deixar a API tentar
        }

        private async Task<decimal> ConvertToEurIfNeeded(decimal usdPrice, string symbol)
        {
            // Se já for EUR ou uma moeda europeia, não converter
            if (symbol.Contains("EUR") || symbol.EndsWith(".L") || symbol.EndsWith(".PA"))
                return usdPrice;

            // Se for criptomoeda ou ação americana, converter
            if (symbol.Contains("-USD") || IsUsStock(symbol))
            {
                return await _yahooFinance.ConvertUsdToEurAsync(usdPrice);
            }

            return usdPrice;
        }

        private bool IsUsStock(string symbol)
        {
            // Lista básica de indicadores de ações americanas
            var usIndicators = new[] { "AAPL", "MSFT", "GOOGL", "AMZN", "TSLA", "META", "NVDA" };
            return usIndicators.Any(indicator => symbol.StartsWith(indicator));
        }

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
                    _yahooFinance?.Dispose();
                }
                _disposed = true;
            }
        }

        ~InvestmentPriceService()
        {
            Dispose(false);
        }
    }

    // Classes de modelo para resultados
    public class PriceUpdateResult
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = "";
        public int SuccessCount { get; set; }
        public List<InvestmentPriceUpdate> UpdatedInvestments { get; set; } = new();

        public string FormattedSummary
        {
            get
            {
                if (!IsSuccess)
                    return Message;

                var totalChange = UpdatedInvestments.Sum(u => u.Change);
                var culture = new CultureInfo("pt-PT");

                return $"✅ {SuccessCount} investimentos atualizados\n" +
                       $"💰 Variação total: {totalChange.ToString("C", culture)}\n" +
                       $"🕐 Última atualização: {DateTime.Now:HH:mm:ss}";
            }
        }
    }

    public class InvestmentPriceUpdate
    {
        public int InvestmentId { get; set; }
        public string Name { get; set; } = "";
        public string Symbol { get; set; } = "";
        public decimal? OldPrice { get; set; }
        public decimal NewPrice { get; set; }
        public decimal Change { get; set; }
        public decimal ChangePercent { get; set; }

        public string FormattedOldPrice => OldPrice?.ToString("C", new CultureInfo("pt-PT")) ?? "N/A";
        public string FormattedNewPrice => NewPrice.ToString("C", new CultureInfo("pt-PT"));
        public string FormattedChange => Change.ToString("+#,##0.00;-#,##0.00;0.00", new CultureInfo("pt-PT"));
        public string FormattedChangePercent => ChangePercent.ToString("+#,##0.00;-#,##0.00;0.00") + "%";
        public bool IsPositive => Change >= 0;
        public string ChangeIcon => IsPositive ? "📈" : "📉";
    }

    public class MarketStatus
    {
        public bool IsUSMarketOpen { get; set; }
        public bool IsEuropeanMarketOpen { get; set; }
        public DateTime LastChecked { get; set; }

        public string StatusText
        {
            get
            {
                if (IsUSMarketOpen && IsEuropeanMarketOpen)
                    return "🟢 Mercados Abertos";
                else if (IsUSMarketOpen || IsEuropeanMarketOpen)
                    return "🟡 Alguns Mercados Abertos";
                else
                    return "🔴 Mercados Fechados";
            }
        }

        public string DetailedStatus
        {
            get
            {
                var status = $"🇺🇸 EUA: {(IsUSMarketOpen ? "Aberto" : "Fechado")}\n";
                status += $"🇪🇺 Europa: {(IsEuropeanMarketOpen ? "Aberto" : "Fechado")}\n";
                status += $"🕐 Verificado: {LastChecked:HH:mm:ss}";
                return status;
            }
        }
    }

    /// <summary>
    /// Representa um resultado de pesquisa de símbolos financeiros
    /// </summary>
    public class SearchResult
    {
        public string Symbol { get; set; } = "";
        public string? ShortName { get; set; }
        public string? LongName { get; set; }
        public string? Type { get; set; }
        public string? Exchange { get; set; }

        /// <summary>
        /// Nome para exibição na interface
        /// </summary>
        public string DisplayName => !string.IsNullOrEmpty(ShortName) ?
            $"{Symbol} - {ShortName}" :
            $"{Symbol} - {LongName ?? "N/A"}";

        /// <summary>
        /// Descrição completa do ativo
        /// </summary>
        public string FullDescription => !string.IsNullOrEmpty(LongName) && !string.IsNullOrEmpty(ShortName) ?
            $"{Symbol} - {ShortName} ({LongName})" :
            DisplayName;

        /// <summary>
        /// Descrição com bolsa
        /// </summary>
        public string DescriptionWithExchange => !string.IsNullOrEmpty(Exchange) ?
            $"{DisplayName} [{Exchange}]" : DisplayName;

        /// <summary>
        /// Tipo formatado para exibição
        /// </summary>
        public string FormattedType => Type switch
        {
            "EQUITY" => "Ação",
            "ETF" => "ETF",
            "CRYPTOCURRENCY" => "Cripto",
            "CURRENCY" => "Moeda",
            "INDEX" => "Índice",
            "MUTUALFUND" => "Fundo",
            "FUTURE" => "Futuro",
            "OPTION" => "Opção",
            _ => Type ?? "Ativo"
        };

        /// <summary>
        /// Ícone baseado no tipo
        /// </summary>
        public string TypeIcon => Type switch
        {
            "EQUITY" => "📈",
            "ETF" => "📊",
            "CRYPTOCURRENCY" => "₿",
            "CURRENCY" => "💱",
            "INDEX" => "📉",
            "MUTUALFUND" => "🏦",
            "FUTURE" => "⚡",
            "OPTION" => "🎯",
            _ => "💼"
        };

        /// <summary>
        /// Verifica se é um resultado válido para investimento
        /// </summary>
        public bool IsValidForInvestment => !string.IsNullOrEmpty(Symbol) &&
                                           Symbol != "DICA" &&
                                           Symbol != "SUGESTÃO" &&
                                           Type != "DICA" &&
                                           Type != "INFO";
    }

    /// <summary>
    /// Dados de cotação em tempo real de um ativo
    /// </summary>
    public class QuoteData
    {
        public string Symbol { get; set; } = "";
        public decimal CurrentPrice { get; set; }
        public decimal PreviousClose { get; set; }
        public decimal Change { get; set; }
        public decimal ChangePercent { get; set; }
        public string Currency { get; set; } = "USD";
        public string MarketState { get; set; } = "";
        public string? LongName { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.Now;

        /// <summary>
        /// Preço formatado com moeda
        /// </summary>
        public string FormattedPrice => CurrentPrice.ToString("C2", GetCultureForCurrency());

        /// <summary>
        /// Variação formatada
        /// </summary>
        public string FormattedChange => Change.ToString("+#,##0.00;-#,##0.00;0.00", GetCultureForCurrency());

        /// <summary>
        /// Variação percentual formatada
        /// </summary>
        public string FormattedChangePercent => ChangePercent.ToString("+#,##0.00;-#,##0.00;0.00") + "%";

        /// <summary>
        /// Indica se a variação é positiva
        /// </summary>
        public bool IsPositiveChange => Change >= 0;

        /// <summary>
        /// Cor para exibição baseada na variação
        /// </summary>
        public string ChangeColor => IsPositiveChange ? "Green" : "Red";

        /// <summary>
        /// Ícone da tendência
        /// </summary>
        public string ChangeIcon => IsPositiveChange ? "📈" : "📉";

        /// <summary>
        /// Estado do mercado formatado
        /// </summary>
        public string FormattedMarketState => MarketState.ToUpper() switch
        {
            "REGULAR" => "🟢 Aberto",
            "CLOSED" => "🔴 Fechado",
            "PRE" => "🟡 Pré-Abertura",
            "POST" => "🟡 Pós-Fechamento",
            _ => "❓ Desconhecido"
        };

        /// <summary>
        /// Obtém a cultura apropriada para a moeda
        /// </summary>
        private System.Globalization.CultureInfo GetCultureForCurrency()
        {
            return Currency.ToUpper() switch
            {
                "EUR" => new System.Globalization.CultureInfo("pt-PT"),
                "USD" => new System.Globalization.CultureInfo("en-US"),
                "GBP" => new System.Globalization.CultureInfo("en-GB"),
                _ => new System.Globalization.CultureInfo("en-US")
            };
        }
    }
}