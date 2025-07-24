using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using FinanceManager.Services;

namespace FinanceManager.Views
{
    public partial class LiveQuoteWindow : Window
    {
        private readonly string _symbol;
        private readonly InvestmentPriceService _priceService;
        private readonly YahooFinanceService _yahooService;
        private readonly CultureInfo _culture = new CultureInfo("pt-PT");
        private DispatcherTimer _refreshTimer;
        private bool _isAutoRefreshEnabled = true;
        private bool _isRefreshing = false;
        private QuoteData? _lastQuote;

        public LiveQuoteWindow(string symbol, InvestmentPriceService priceService)
        {
            InitializeComponent();
            _symbol = symbol.ToUpper();
            _priceService = priceService;
            _yahooService = new YahooFinanceService();

            InitializeWindow();
            InitializeTimer();
            _ = LoadQuoteData(); // Fire-and-forget pattern
        }

        private void InitializeWindow()
        {
            this.Title = $"📊 {_symbol} - Cotação em Tempo Real";

            // Verificar se os elementos existem antes de usá-los
            if (SymbolText != null)
                SymbolText.Text = _symbol;

            if (CompanyNameText != null)
                CompanyNameText.Text = "Carregando informações...";

            if (StatusText != null)
                StatusText.Text = "🔄 Inicializando...";

            LoggingService.LogInfo($"Janela de cotação aberta para: {_symbol}");
        }

        private void InitializeTimer()
        {
            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30) // Atualizar a cada 30 segundos
            };
            _refreshTimer.Tick += async (s, e) => await RefreshQuoteData();
            _refreshTimer.Start();
        }

        private async Task LoadQuoteData()
        {
            await RefreshQuoteData();
        }

        private async Task RefreshQuoteData()
        {
            if (_isRefreshing)
                return;

            try
            {
                _isRefreshing = true;

                if (StatusText != null)
                    StatusText.Text = "🔄 Atualizando cotação...";

                if (RefreshButton != null)
                    RefreshButton.IsEnabled = false;

                LoggingService.LogInfo($"Atualizando cotação para: {_symbol}");

                var quoteData = await _yahooService.GetQuoteDataAsync(_symbol);

                if (quoteData != null)
                {
                    _lastQuote = quoteData;
                    await UpdateUI(quoteData);

                    if (StatusText != null)
                        StatusText.Text = _isAutoRefreshEnabled ?
                            "🔄 Atualização automática ativa" :
                            "⏸️ Atualização automática pausada";

                    LoggingService.LogInfo($"Cotação atualizada para {_symbol}: {quoteData.CurrentPrice}");
                }
                else
                {
                    if (StatusText != null)
                        StatusText.Text = "❌ Erro ao obter cotação";
                    ShowErrorState();
                    LoggingService.LogWarning($"Falha ao obter cotação para: {_symbol}");
                }
            }
            catch (Exception ex)
            {
                if (StatusText != null)
                    StatusText.Text = $"❌ Erro: {ex.Message}";
                ShowErrorState();
                LoggingService.LogError($"Erro ao obter cotação para {_symbol}", ex);
            }
            finally
            {
                _isRefreshing = false;
                if (RefreshButton != null)
                    RefreshButton.IsEnabled = true;
            }
        }

        private async Task UpdateUI(QuoteData quote)
        {
            try
            {
                // Informações básicas
                if (CompanyNameText != null)
                    CompanyNameText.Text = quote.LongName ?? quote.Symbol;

                if (CurrentPriceText != null)
                    CurrentPriceText.Text = quote.FormattedPrice;

                if (CurrencyText != null)
                    CurrencyText.Text = quote.Currency;

                if (CurrencyDetailText != null)
                    CurrencyDetailText.Text = quote.Currency;

                // Variação
                if (ChangeText != null)
                    ChangeText.Text = quote.FormattedChange;

                if (ChangePercentText != null)
                    ChangePercentText.Text = quote.FormattedChangePercent;

                // Cores baseadas na variação
                var changeColor = quote.IsPositiveChange ? Brushes.Green : Brushes.Red;

                if (ChangeText != null)
                    ChangeText.Foreground = changeColor;

                if (ChangePercentText != null)
                    ChangePercentText.Foreground = changeColor;

                // Detalhes
                if (PreviousCloseText != null)
                    PreviousCloseText.Text = quote.PreviousClose.ToString("C2", GetCultureForCurrency(quote.Currency));

                // Estado do mercado
                if (MarketStateText != null)
                {
                    MarketStateText.Text = GetMarketStateText(quote.MarketState);
                    MarketStateText.Foreground = GetMarketStateColor(quote.MarketState);
                }

                // Conversão para EUR
                if (quote.Currency == "USD")
                {
                    try
                    {
                        var priceEur = await _yahooService.ConvertUsdToEurAsync(quote.CurrentPrice);
                        var changeEur = await _yahooService.ConvertUsdToEurAsync(quote.Change);

                        if (PriceEurText != null)
                            PriceEurText.Text = priceEur.ToString("C", _culture);

                        if (ChangeEurText != null)
                        {
                            ChangeEurText.Text = changeEur.ToString("+#,##0.00;-#,##0.00;0.00", _culture) + " €";
                            ChangeEurText.Foreground = changeColor;
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggingService.LogWarning($"Erro na conversão USD->EUR: {ex.Message}");

                        if (PriceEurText != null)
                            PriceEurText.Text = "N/A";

                        if (ChangeEurText != null)
                            ChangeEurText.Text = "N/A";
                    }
                }
                else
                {
                    // Se já for EUR ou outra moeda
                    if (PriceEurText != null)
                        PriceEurText.Text = quote.CurrentPrice.ToString("C", _culture);

                    if (ChangeEurText != null)
                    {
                        ChangeEurText.Text = quote.Change.ToString("+#,##0.00;-#,##0.00;0.00", _culture) + " €";
                        ChangeEurText.Foreground = changeColor;
                    }
                }

                if (LastUpdateText != null)
                    LastUpdateText.Text = quote.LastUpdated.ToString("HH:mm:ss");
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Erro ao atualizar UI para {_symbol}", ex);
            }
        }

        private CultureInfo GetCultureForCurrency(string currency)
        {
            return currency.ToUpper() switch
            {
                "EUR" => new CultureInfo("pt-PT"),
                "USD" => new CultureInfo("en-US"),
                "GBP" => new CultureInfo("en-GB"),
                _ => new CultureInfo("en-US")
            };
        }

        private string GetMarketStateText(string marketState)
        {
            return marketState.ToUpper() switch
            {
                "REGULAR" => "🟢 ABERTO",
                "CLOSED" => "🔴 FECHADO",
                "PRE" => "🟡 PRÉ-ABERTURA",
                "POST" => "🟡 PÓS-FECHAMENTO",
                _ => "❓ DESCONHECIDO"
            };
        }

        private Brush GetMarketStateColor(string marketState)
        {
            return marketState.ToUpper() switch
            {
                "REGULAR" => Brushes.Green,
                "CLOSED" => Brushes.Red,
                "PRE" or "POST" => Brushes.Orange,
                _ => Brushes.Gray
            };
        }

        private void ShowErrorState()
        {
            if (CurrentPriceText != null)
                CurrentPriceText.Text = "N/A";

            if (ChangeText != null)
                ChangeText.Text = "N/A";

            if (ChangePercentText != null)
                ChangePercentText.Text = "N/A";

            if (PreviousCloseText != null)
                PreviousCloseText.Text = "N/A";

            if (MarketStateText != null)
            {
                MarketStateText.Text = "❌ ERRO";
                MarketStateText.Foreground = Brushes.Red;
            }

            if (PriceEurText != null)
                PriceEurText.Text = "N/A";

            if (ChangeEurText != null)
                ChangeEurText.Text = "N/A";
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            await RefreshQuoteData();
        }

        private void AutoRefreshToggle_Click(object sender, RoutedEventArgs e)
        {
            _isAutoRefreshEnabled = !_isAutoRefreshEnabled;

            if (_isAutoRefreshEnabled)
            {
                _refreshTimer.Start();
                if (AutoRefreshToggle != null)
                {
                    AutoRefreshToggle.Content = "⏸️ Pausar Auto";
                    AutoRefreshToggle.Background = new SolidColorBrush(Colors.Orange);
                }
                if (StatusText != null)
                    StatusText.Text = "🔄 Atualização automática ativa";
            }
            else
            {
                _refreshTimer.Stop();
                if (AutoRefreshToggle != null)
                {
                    AutoRefreshToggle.Content = "▶️ Retomar Auto";
                    AutoRefreshToggle.Background = new SolidColorBrush(Colors.Green);
                }
                if (StatusText != null)
                    StatusText.Text = "⏸️ Atualização automática pausada";
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                _refreshTimer?.Stop();
                _yahooService?.Dispose();
                LoggingService.LogInfo($"Janela de cotação fechada para: {_symbol}");
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Erro ao fechar janela de cotação para {_symbol}", ex);
            }
            finally
            {
                base.OnClosed(e);
            }
        }

        // Atalhos de teclado
        protected override void OnKeyDown(KeyEventArgs e)
        {
            try
            {
                switch (e.Key)
                {
                    case Key.F5:
                        Refresh_Click(this, new RoutedEventArgs());
                        e.Handled = true;
                        break;

                    case Key.Space:
                        AutoRefreshToggle_Click(this, new RoutedEventArgs());
                        e.Handled = true;
                        break;

                    case Key.Escape:
                        this.Close();
                        e.Handled = true;
                        break;

                    default:
                        base.OnKeyDown(e);
                        break;
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Erro no atalho de teclado: {ex.Message}", ex);
                base.OnKeyDown(e);
            }
        }
    }
}
