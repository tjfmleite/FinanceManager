using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using FinanceManager.Models;
using FinanceManager.Services;

namespace FinanceManager.Views
{
    public partial class AddInvestmentWindow : Window
    {
        private readonly User _currentUser;
        private readonly InvestmentService _investmentService;
        private readonly YahooFinanceService _yahooService;
        private readonly CultureInfo _culture = new CultureInfo("pt-PT");
        private ObservableCollection<SearchResult> _searchResults;
        private SearchResult? _selectedSymbol;
        private QuoteData? _currentQuote;
        private DispatcherTimer _searchTimer;
        private DispatcherTimer _priceUpdateTimer;
        private bool _isSearching = false;
        private bool _hasValidSelection = false;
        private bool _isQuantityMode = true; // true = quantidade+preço, false = valor total

        public AddInvestmentWindow(User currentUser)
        {
            InitializeComponent();
            _currentUser = currentUser;
            _investmentService = new InvestmentService();
            _yahooService = new YahooFinanceService();
            _searchResults = new ObservableCollection<SearchResult>();

            InitializeWindow();
        }

        private void InitializeWindow()
        {
            SearchResultsListBox.ItemsSource = _searchResults;

            // Timer para pesquisa com delay (evitar muitas chamadas à API)
            _searchTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1000) // 1 segundo de delay
            };
            _searchTimer.Tick += async (s, e) => await PerformSearch();

            // Timer para atualização automática de preços (a cada 30 segundos)
            _priceUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            _priceUpdateTimer.Tick += async (s, e) => await UpdateCurrentPrice();

            // Configurar validações numéricas
            QuantityTextBox.PreviewTextInput += NumericTextBox_PreviewTextInput;
            PurchasePriceTextBox.PreviewTextInput += NumericTextBox_PreviewTextInput;
            TotalInvestedTextBox.PreviewTextInput += NumericTextBox_PreviewTextInput;

            // Configurar eventos para cálculo automático
            QuantityTextBox.TextChanged += CalculateTotalValue;
            PurchasePriceTextBox.TextChanged += CalculateTotalValue;

            // Configurar data padrão
            PurchaseDatePicker.SelectedDate = DateTime.Today;

            // Focar no campo de pesquisa
            SearchTextBox.Focus();
        }

        #region Pesquisa de Símbolos

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchTimer.Stop();

            var query = SearchTextBox.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(query) && query.Length >= 2)
            {
                _searchTimer.Start();
                SearchStatusText.Text = "⏳ Preparando pesquisa...";
            }
            else
            {
                _searchResults.Clear();
                SearchStatusText.Text = "💡 Digite pelo menos 2 caracteres para pesquisar";
                ClearSelection();
            }
        }

        private async void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await PerformSearch();
            }
        }

        private async void Search_Click(object sender, RoutedEventArgs e)
        {
            await PerformSearch();
        }

        private async Task PerformSearch()
        {
            _searchTimer.Stop();

            var query = SearchTextBox.Text?.Trim();
            if (_isSearching || string.IsNullOrWhiteSpace(query) || query.Length < 2)
                return;

            try
            {
                _isSearching = true;
                ShowLoading(true);

                SearchButton.IsEnabled = false;
                SearchButton.Content = "⏳ Pesquisando...";
                SearchStatusText.Text = "🔍 Pesquisando na Yahoo Finance...";

                LoggingService.LogInfo($"Iniciando pesquisa para: {query}");

                var results = await _yahooService.SearchSymbolsAsync(query);

                _searchResults.Clear();

                if (results.Any())
                {
                    foreach (var result in results.Take(15)) // Limitar a 15 resultados
                    {
                        _searchResults.Add(result);
                    }

                    SearchStatusText.Text = $"✅ {results.Count} resultado(s) encontrado(s) para '{query}'";
                    LoggingService.LogInfo($"Encontrados {results.Count} resultados para: {query}");
                }
                else
                {
                    SearchStatusText.Text = $"❌ Nenhum resultado para '{query}'";

                    // Adicionar sugestões
                    _searchResults.Add(new SearchResult
                    {
                        Symbol = "DICA",
                        ShortName = "💡 Sugestões de pesquisa:",
                        LongName = "• Ações: Apple, Microsoft, Tesla, Amazon\n• Símbolos: AAPL, MSFT, TSLA, AMZN\n• ETFs: VTI, SPY, QQQ\n• Criptos: Bitcoin, BTC-USD, ETH-USD",
                        Type = "INFO"
                    });

                    LoggingService.LogWarning($"Nenhum resultado encontrado para: {query}");
                }
            }
            catch (Exception ex)
            {
                SearchStatusText.Text = "❌ Erro na pesquisa - Verifique sua conexão";
                MessageBox.Show($"Erro ao pesquisar símbolos:\n\n{ex.Message}\n\nVerifique sua conexão com a internet.",
                               "Erro de Conexão", MessageBoxButton.OK, MessageBoxImage.Warning);
                LoggingService.LogError($"Erro na pesquisa para '{query}'", ex);
            }
            finally
            {
                _isSearching = false;
                ShowLoading(false);
                SearchButton.IsEnabled = true;
                SearchButton.Content = "🔍 Pesquisar";
            }
        }

        private void ShowLoading(bool show)
        {
            LoadingPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion

        #region Modos de Entrada

        private void InputMode_Changed(object sender, RoutedEventArgs e)
        {
            // Verificar se os elementos da interface foram inicializados
            if (QuantityModeRadio == null || TotalValueModeRadio == null ||
                QuantityModePanel == null || TotalValueModePanel == null)
                return;

            _isQuantityMode = QuantityModeRadio.IsChecked == true;

            if (_isQuantityMode)
            {
                // Modo Quantidade + Preço
                QuantityModePanel.Visibility = Visibility.Visible;
                TotalValueModePanel.Visibility = Visibility.Collapsed;

                // Limpar campos do modo valor total
                if (TotalInvestedTextBox != null)
                    TotalInvestedTextBox.Clear();
            }
            else
            {
                // Modo Valor Total
                QuantityModePanel.Visibility = Visibility.Collapsed;
                TotalValueModePanel.Visibility = Visibility.Visible;

                // Limpar campos do modo quantidade
                if (QuantityTextBox != null)
                    QuantityTextBox.Clear();
                if (PurchasePriceTextBox != null)
                    PurchasePriceTextBox.Clear();
            }

            // Recalcular valores apenas se os elementos existirem
            if (TotalValueText != null)
                CalculateTotalValue(null, null);
        }

        private void CalculateFromTotalValue(object sender, TextChangedEventArgs e)
        {
            if (_isQuantityMode || _currentQuote == null || TotalInvestedTextBox == null ||
                TotalValueText == null || CurrentValueText == null || ProfitLossText == null ||
                StatusText == null)
                return;

            try
            {
                var totalText = TotalInvestedTextBox.Text.Replace(',', '.');

                if (decimal.TryParse(totalText, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal totalInvested))
                {
                    // Converter preço atual para EUR se necessário
                    var currentPriceEur = _currentQuote.Currency == "USD" ?
                        _currentQuote.CurrentPrice * 0.85m : _currentQuote.CurrentPrice;

                    if (currentPriceEur > 0)
                    {
                        // Calcular quantidade baseada no preço atual
                        var calculatedQuantity = totalInvested / currentPriceEur;

                        // Atualizar displays
                        TotalValueText.Text = totalInvested.ToString("C", _culture);

                        var currentValue = calculatedQuantity * currentPriceEur;
                        var profitLoss = currentValue - totalInvested;

                        CurrentValueText.Text = currentValue.ToString("C", _culture);
                        ProfitLossText.Text = profitLoss.ToString("C", _culture);
                        ProfitLossText.Foreground = profitLoss >= 0 ?
                            new SolidColorBrush(Colors.Green) : new SolidColorBrush(Colors.Red);

                        StatusText.Text = $"✅ Quantidade calculada: {calculatedQuantity:F6} unidades";
                    }
                    else
                    {
                        StatusText.Text = "❌ Aguardando cotação para calcular quantidade";
                    }
                }
                else
                {
                    TotalValueText.Text = "€0,00";
                    CurrentValueText.Text = "€0,00";
                    ProfitLossText.Text = "€0,00";
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao calcular do valor total", ex);
            }
        }

        #endregion

        #region Seleção de Símbolo

        private async void SearchResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SearchResultsListBox.SelectedItem is SearchResult selected && selected.IsValidForInvestment)
            {
                await SelectSymbol(selected);
            }
        }

        private async void SearchResults_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (SearchResultsListBox.SelectedItem is SearchResult selected && selected.IsValidForInvestment)
            {
                await SelectSymbol(selected);
                QuantityTextBox.Focus();
            }
        }

        private async Task SelectSymbol(SearchResult symbol)
        {
            try
            {
                _selectedSymbol = symbol;
                StatusText.Text = $"⏳ Obtendo cotação para {symbol.Symbol}...";

                LoggingService.LogInfo($"Selecionado símbolo: {symbol.Symbol}");

                // Buscar cotação atual
                _currentQuote = await _yahooService.GetQuoteDataAsync(symbol.Symbol);

                if (_currentQuote != null)
                {
                    // Preencher informações do ativo
                    var displayName = !string.IsNullOrEmpty(symbol.LongName) ? symbol.LongName : symbol.ShortName;
                    NameTextBox.Text = $"{displayName} ({symbol.Symbol})";
                    TypeTextBox.Text = symbol.FormattedType;
                    SymbolTextBox.Text = symbol.Symbol;

                    // Mostrar cotação atual
                    UpdatePriceDisplay();

                    // Calcular valores se já há quantidade e preço
                    CalculateTotalValue(null, null);

                    _hasValidSelection = true;
                    SaveButton.IsEnabled = true;
                    StatusText.Text = $"✅ {symbol.Symbol} selecionado - Cotação: {_currentQuote.FormattedPrice}";

                    // Iniciar atualização automática de preços
                    _priceUpdateTimer.Start();

                    LoggingService.LogInfo($"Cotação obtida para {symbol.Symbol}: {_currentQuote.CurrentPrice:C}");
                }
                else
                {
                    StatusText.Text = $"❌ Não foi possível obter cotação para {symbol.Symbol}";
                    MessageBox.Show($"Não foi possível obter a cotação atual para {symbol.Symbol}.\n\nO símbolo pode estar incorreto ou a API pode estar indisponível.",
                                   "Cotação Indisponível", MessageBoxButton.OK, MessageBoxImage.Warning);
                    LoggingService.LogWarning($"Falha ao obter cotação para: {symbol.Symbol}");
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"❌ Erro ao obter dados de {symbol.Symbol}";
                MessageBox.Show($"Erro ao obter dados do símbolo:\n\n{ex.Message}",
                               "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                LoggingService.LogError($"Erro ao selecionar símbolo {symbol.Symbol}", ex);
            }
        }

        private async Task UpdateCurrentPrice()
        {
            if (_selectedSymbol == null || string.IsNullOrEmpty(_selectedSymbol.Symbol))
                return;

            try
            {
                var updatedQuote = await _yahooService.GetQuoteDataAsync(_selectedSymbol.Symbol);
                if (updatedQuote != null)
                {
                    _currentQuote = updatedQuote;
                    UpdatePriceDisplay();
                    CalculateTotalValue(null, null);

                    // Atualizar status discretamente
                    if (StatusText.Text.Contains("✅"))
                    {
                        StatusText.Text = $"✅ {_selectedSymbol.Symbol} - Atualizado às {DateTime.Now:HH:mm:ss}";
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Erro ao atualizar preço de {_selectedSymbol.Symbol}", ex);
            }
        }

        private void UpdatePriceDisplay()
        {
            if (_currentQuote == null) return;

            CurrentPriceText.Text = _currentQuote.FormattedPrice;
            PriceChangeText.Text = _currentQuote.FormattedChange + " (" + _currentQuote.FormattedChangePercent + ")";
            PriceChangeText.Foreground = _currentQuote.IsPositiveChange ?
                new SolidColorBrush(Colors.Green) : new SolidColorBrush(Colors.Red);
        }

        private void ClearSelection()
        {
            _selectedSymbol = null;
            _currentQuote = null;
            _hasValidSelection = false;
            _priceUpdateTimer?.Stop();

            // Verificar se os elementos existem antes de tentar limpá-los
            if (NameTextBox != null) NameTextBox.Clear();
            if (TypeTextBox != null) TypeTextBox.Clear();
            if (SymbolTextBox != null) SymbolTextBox.Clear();
            if (QuantityTextBox != null) QuantityTextBox.Clear();
            if (PurchasePriceTextBox != null) PurchasePriceTextBox.Clear();
            if (TotalInvestedTextBox != null) TotalInvestedTextBox.Clear();

            if (CurrentPriceText != null)
            {
                CurrentPriceText.Text = "--";
            }

            if (PriceChangeText != null)
            {
                PriceChangeText.Text = "--";
                PriceChangeText.Foreground = new SolidColorBrush(Colors.Black);
            }

            if (TotalValueText != null) TotalValueText.Text = "€0,00";
            if (CurrentValueText != null) CurrentValueText.Text = "€0,00";

            if (ProfitLossText != null)
            {
                ProfitLossText.Text = "€0,00";
                ProfitLossText.Foreground = new SolidColorBrush(Colors.Black);
            }

            if (SaveButton != null) SaveButton.IsEnabled = false;
        }

        #endregion

        #region Validação e Cálculos

        private void NumericTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            var newText = textBox.Text.Insert(textBox.CaretIndex, e.Text);

            if (!IsValidDecimalInput(newText))
            {
                e.Handled = true;
            }
        }

        private bool IsValidDecimalInput(string text)
        {
            if (string.IsNullOrEmpty(text)) return true;

            var normalizedText = text.Replace(',', '.');
            return decimal.TryParse(normalizedText, NumberStyles.AllowDecimalPoint,
                                  CultureInfo.InvariantCulture, out _);
        }

        private void CalculateTotalValue(object sender, TextChangedEventArgs e)
        {
            // Verificar se os elementos necessários foram inicializados
            if (QuantityTextBox == null || PurchasePriceTextBox == null ||
                TotalValueText == null || CurrentValueText == null || ProfitLossText == null)
                return;

            try
            {
                var quantityText = QuantityTextBox.Text.Replace(',', '.');
                var priceText = PurchasePriceTextBox.Text.Replace(',', '.');

                if (decimal.TryParse(quantityText, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal quantity) &&
                    decimal.TryParse(priceText, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal price))
                {
                    var totalValue = quantity * price;
                    TotalValueText.Text = totalValue.ToString("C", _culture);

                    // Calcular valor atual e ganho/perda se há cotação
                    if (_currentQuote != null && _currentQuote.CurrentPrice > 0)
                    {
                        // Converter preço atual para EUR se necessário
                        var currentPriceEur = _currentQuote.Currency == "USD" ?
                            _currentQuote.CurrentPrice * 0.85m : _currentQuote.CurrentPrice; // Aproximação USD->EUR

                        var currentValue = quantity * currentPriceEur;
                        var profitLoss = currentValue - totalValue;

                        CurrentValueText.Text = currentValue.ToString("C", _culture);
                        ProfitLossText.Text = profitLoss.ToString("C", _culture);
                        ProfitLossText.Foreground = profitLoss >= 0 ?
                            new SolidColorBrush(Colors.Green) : new SolidColorBrush(Colors.Red);
                    }
                    else
                    {
                        CurrentValueText.Text = "Aguardando cotação...";
                        ProfitLossText.Text = "€0,00";
                        if (ProfitLossText != null)
                            ProfitLossText.Foreground = new SolidColorBrush(Colors.Black);
                    }
                }
                else
                {
                    TotalValueText.Text = "€0,00";
                    CurrentValueText.Text = "€0,00";
                    ProfitLossText.Text = "€0,00";
                    if (ProfitLossText != null)
                        ProfitLossText.Foreground = new SolidColorBrush(Colors.Black);
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao calcular valores", ex);
            }
        }

        #endregion

        #region Navegação por Teclado

        private void QuantityTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                PurchasePriceTextBox.Focus();
                e.Handled = true;
            }
        }

        private void PurchasePriceTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                PurchaseDatePicker.Focus();
                e.Handled = true;
            }
        }

        #endregion

        #region Botões

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateForm())
                return;

            try
            {
                SetControlsEnabled(false);

                decimal quantity, purchasePrice, totalAmount;

                if (_isQuantityMode)
                {
                    // Modo Quantidade + Preço
                    var quantityText = QuantityTextBox.Text.Replace(',', '.');
                    var priceText = PurchasePriceTextBox.Text.Replace(',', '.');

                    if (!decimal.TryParse(quantityText, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out quantity) ||
                        !decimal.TryParse(priceText, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out purchasePrice))
                    {
                        MessageBox.Show("Valores numéricos inválidos.", "Erro de Validação",
                                      MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    totalAmount = quantity * purchasePrice;
                }
                else
                {
                    // Modo Valor Total - calcular quantidade baseada no preço atual
                    var totalText = TotalInvestedTextBox.Text.Replace(',', '.');

                    if (!decimal.TryParse(totalText, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out totalAmount))
                    {
                        MessageBox.Show("Valor total inválido.", "Erro de Validação",
                                      MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    if (_currentQuote == null || _currentQuote.CurrentPrice <= 0)
                    {
                        MessageBox.Show("Não é possível calcular a quantidade sem cotação atual válida.", "Cotação Necessária",
                                      MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Converter preço atual para EUR se necessário
                    var marketPriceEur = _currentQuote.Currency == "USD" ?
                        await _yahooService.ConvertUsdToEurAsync(_currentQuote.CurrentPrice) :
                        _currentQuote.CurrentPrice;

                    quantity = totalAmount / marketPriceEur;
                    purchasePrice = marketPriceEur; // Usar preço atual como preço de compra
                }

                // Converter preço atual para EUR para armazenar no investimento
                decimal? currentPriceEur = null;
                if (_currentQuote != null)
                {
                    currentPriceEur = _currentQuote.Currency == "USD" ?
                        await _yahooService.ConvertUsdToEurAsync(_currentQuote.CurrentPrice) :
                        _currentQuote.CurrentPrice;
                }

                var investment = new Investment
                {
                    Name = NameTextBox.Text.Trim(),
                    Type = TypeTextBox.Text.Trim(),
                    Quantity = quantity,
                    PurchasePrice = purchasePrice,
                    CurrentPrice = currentPriceEur,
                    Amount = totalAmount,
                    PurchaseDate = PurchaseDatePicker.SelectedDate.Value,
                    Broker = BrokerTextBox.Text?.Trim(),
                    Description = GetInvestmentDescription(),
                    Currency = "EUR",
                    IsActive = true,
                    UserId = _currentUser.Id
                };

                var success = await _investmentService.AddInvestmentAsync(investment);

                if (success)
                {
                    ShowSuccessMessage(investment);
                    this.DialogResult = true;
                    this.Close();
                }
                else
                {
                    MessageBox.Show("Erro ao guardar o investimento. Tente novamente.", "Erro",
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro inesperado: {ex.Message}", "Erro",
                              MessageBoxButton.OK, MessageBoxImage.Error);
                LoggingService.LogError("Erro ao salvar investimento", ex);
            }
            finally
            {
                SetControlsEnabled(true);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        #endregion

        #region Métodos Auxiliares

        private bool ValidateForm()
        {
            if (!_hasValidSelection || _selectedSymbol == null)
            {
                MessageBox.Show("Por favor, selecione um ativo da lista de pesquisa.", "Ativo Não Selecionado",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                SearchTextBox.Focus();
                return false;
            }

            if (_isQuantityMode)
            {
                // Validação para modo Quantidade + Preço
                if (string.IsNullOrWhiteSpace(QuantityTextBox.Text))
                {
                    MessageBox.Show("Por favor, insira a quantidade.", "Campo Obrigatório",
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    QuantityTextBox.Focus();
                    return false;
                }

                if (string.IsNullOrWhiteSpace(PurchasePriceTextBox.Text))
                {
                    MessageBox.Show("Por favor, insira o preço de compra.", "Campo Obrigatório",
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    PurchasePriceTextBox.Focus();
                    return false;
                }
            }
            else
            {
                // Validação para modo Valor Total
                if (string.IsNullOrWhiteSpace(TotalInvestedTextBox.Text))
                {
                    MessageBox.Show("Por favor, insira o valor total investido.", "Campo Obrigatório",
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    TotalInvestedTextBox.Focus();
                    return false;
                }

                if (_currentQuote == null || _currentQuote.CurrentPrice <= 0)
                {
                    MessageBox.Show("É necessária uma cotação válida para calcular a quantidade a partir do valor total.", "Cotação Necessária",
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }

            if (!PurchaseDatePicker.SelectedDate.HasValue)
            {
                MessageBox.Show("Por favor, selecione a data de compra.", "Campo Obrigatório",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                PurchaseDatePicker.Focus();
                return false;
            }

            return true;
        }

        private void SetControlsEnabled(bool enabled)
        {
            // Verificar se os elementos existem antes de tentar acessá-los
            if (SearchTextBox != null) SearchTextBox.IsEnabled = enabled;
            if (SearchButton != null) SearchButton.IsEnabled = enabled;
            if (QuantityModeRadio != null) QuantityModeRadio.IsEnabled = enabled;
            if (TotalValueModeRadio != null) TotalValueModeRadio.IsEnabled = enabled;
            if (QuantityTextBox != null) QuantityTextBox.IsEnabled = enabled;
            if (PurchasePriceTextBox != null) PurchasePriceTextBox.IsEnabled = enabled;
            if (TotalInvestedTextBox != null) TotalInvestedTextBox.IsEnabled = enabled;
            if (PurchaseDatePicker != null) PurchaseDatePicker.IsEnabled = enabled;
            if (BrokerTextBox != null) BrokerTextBox.IsEnabled = enabled;
            if (NotesTextBox != null) NotesTextBox.IsEnabled = enabled;
            if (CancelButton != null) CancelButton.IsEnabled = enabled;

            if (SaveButton != null)
                SaveButton.IsEnabled = enabled && _hasValidSelection;

            if (!enabled)
            {
                if (StatusText != null)
                    StatusText.Text = "⏳ Guardando investimento...";
                this.Cursor = Cursors.Wait;
                if (SaveButton != null)
                    SaveButton.Content = "⏳ Guardando...";
            }
            else
            {
                this.Cursor = Cursors.Arrow;
                if (SaveButton != null)
                    SaveButton.Content = "💾 Adicionar Investimento";
            }
        }

        private string GetInvestmentDescription()
        {
            var description = NotesTextBox.Text?.Trim() ?? "";

            // Adicionar informação sobre o método de entrada
            var methodInfo = _isQuantityMode ?
                "Entrada por quantidade + preço" :
                $"Entrada por valor total (€{TotalInvestedTextBox.Text}) - Quantidade calculada automaticamente";

            return string.IsNullOrEmpty(description) ? methodInfo : $"{description}\n\n[{methodInfo}]";
        }

        private void TotalInvestedTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                PurchaseDatePicker.Focus();
                e.Handled = true;
            }
        }

        private void ShowSuccessMessage(Investment investment)
        {
            var message = $"✅ Investimento adicionado com sucesso!\n\n" +
                         $"📊 Ativo: {investment.Name}\n" +
                         $"🎯 Tipo: {investment.Type}\n" +
                         $"📈 Quantidade: {investment.FormattedQuantity}\n" +
                         $"💰 Preço: {investment.FormattedPurchasePrice}\n" +
                         $"💼 Valor Total: {investment.FormattedAmount}\n" +
                         $"📅 Data: {investment.FormattedPurchaseDate}";

            if (_currentQuote != null)
            {
                message += $"\n🔄 Cotação Atual: {_currentQuote.FormattedPrice}";
            }

            MessageBox.Show(message, "Investimento Adicionado",
                          MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Método estático para validar se a API está disponível
        public static bool IsYahooFinanceAvailable()
        {
            try
            {
                using var testService = new YahooFinanceService();
                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Cleanup

        protected override void OnClosed(EventArgs e)
        {
            _searchTimer?.Stop();
            _priceUpdateTimer?.Stop();
            _yahooService?.Dispose();
            base.OnClosed(e);
        }

        #endregion
    }
}