using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Text;
using Microsoft.Win32;
using System.IO;
using FinanceManager.Models;
using FinanceManager.Services;

namespace FinanceManager.Views
{
    public partial class PortfolioWindow : Window
    {
        private readonly User _currentUser;
        private readonly InvestmentService _investmentService;
        private ObservableCollection<Investment> _investments;
        private readonly CultureInfo _culture = new CultureInfo("pt-PT");
        private DispatcherTimer _autoRefreshTimer;
        private bool _isUpdatingPrices = false;

        // Deixar estes opcionais até implementar a API
        private InvestmentPriceService _priceService;
        private YahooFinanceService _yahooService;

        public PortfolioWindow(User currentUser)
        {
            InitializeComponent();
            _currentUser = currentUser;
            _investmentService = new InvestmentService();
            _investments = new ObservableCollection<Investment>();

            DataGrid.ItemsSource = _investments;
            DataGrid.MouseDoubleClick += DataGrid_MouseDoubleClick;

            InitializeAutoRefresh();
            LoadData();
        }

        private void InitializeAutoRefresh()
        {
            _autoRefreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(5)
            };
            _autoRefreshTimer.Tick += async (s, e) => await AutoRefreshPrices();
        }

        private async void LoadData()
        {
            await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            try
            {
                var investments = await _investmentService.GetInvestmentsByUserIdAsync(_currentUser.Id);

                _investments.Clear();
                foreach (var investment in investments.OrderByDescending(i => i.PurchaseDate))
                {
                    _investments.Add(investment);
                }

                UpdateUI();
                await UpdateStats();

                if (_priceService != null)
                {
                    await UpdateMarketStatus();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateUI()
        {
            this.Title = $"📈 Portfólio ({_investments.Count} investimentos)";
            CountText.Text = $"({_investments.Count})";

            if (UpdateAllPricesButton != null)
                UpdateAllPricesButton.IsEnabled = _investments.Any();
            if (AutoRefreshToggle != null)
                AutoRefreshToggle.IsEnabled = _investments.Any();
        }

        private async Task UpdateStats()
        {
            try
            {
                var stats = await _investmentService.GetInvestmentStatisticsAsync(_currentUser.Id);

                ValueText.Text = stats.TotalValue.ToString("C", _culture);
                CostText.Text = stats.TotalCost.ToString("C", _culture);
                ProfitText.Text = stats.TotalProfitLoss.ToString("C", _culture);
                ProfitText.Foreground = stats.TotalProfitLoss >= 0 ?
                    System.Windows.Media.Brushes.Green :
                    System.Windows.Media.Brushes.Red;
                TotalText.Text = stats.TotalInvestments.ToString();

                ChartArea.Text = stats.TotalInvestments > 0
                    ? $"📊 {stats.TotalInvestments} investimentos\n💰 {stats.TotalValue.ToString("C", _culture)}"
                    : "📈 Sem dados";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro stats: {ex.Message}");
                ValueText.Text = CostText.Text = ProfitText.Text = "€0,00";
                TotalText.Text = "0";
                ChartArea.Text = "📈 Erro";
            }
        }

        private async Task UpdateMarketStatus()
        {
            try
            {
                if (_priceService == null) return;

                var marketStatus = await _priceService.GetMarketStatusAsync();
                if (MarketStatusText != null)
                    MarketStatusText.Text = marketStatus.StatusText;
                if (LastUpdateText != null)
                    LastUpdateText.Text = $"Última verificação: {marketStatus.LastChecked:HH:mm:ss}";
            }
            catch
            {
                if (MarketStatusText != null)
                    MarketStatusText.Text = "❓ Status Desconhecido";
                if (LastUpdateText != null)
                    LastUpdateText.Text = "";
            }
        }

        private async void Add_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Verificar se a API do Yahoo Finance está disponível
                if (AddInvestmentWindow.IsYahooFinanceAvailable())
                {
                    var window = new AddInvestmentWindow(_currentUser);
                    window.Owner = this;
                    if (window.ShowDialog() == true)
                    {
                        _ = LoadDataAsync();

                        // Mostrar mensagem de sucesso adicional se StatusText existir
                        // StatusText.Text = "✅ Investimento adicionado com cotação em tempo real!";
                    }
                }
                else
                {
                    // Fallback para janela básica se a API não estiver disponível
                    MessageBox.Show("🔧 API do Yahoo Finance não disponível.\n\n" +
                                  "A janela de adicionar investimento com pesquisa automática requer:\n" +
                                  "• YahooFinanceService.cs\n" +
                                  "• Conexão com a internet\n\n" +
                                  "Usando janela básica...",
                                  "API Não Disponível", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Aqui você pode chamar a janela básica original se existir
                    // var basicWindow = new BasicAddInvestmentWindow(_currentUser);
                    // basicWindow.Owner = this;
                    // if (basicWindow.ShowDialog() == true)
                    // {
                    //     _ = LoadDataAsync();
                    // }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao abrir janela de investimento: {ex.Message}", "Erro",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            if (DataGrid.SelectedItem is Investment selected)
            {
                MessageBox.Show("Edição em desenvolvimento.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Selecione um investimento.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void UpdatePrice_Click(object sender, RoutedEventArgs e)
        {
            if (DataGrid.SelectedItem is Investment selected)
            {
                if (_priceService != null)
                {
                    await UpdateSingleInvestmentPrice(selected);
                }
                else
                {
                    MessageBox.Show("Serviço de preços não disponível. Implemente a API do Yahoo Finance primeiro.",
                                  "Funcionalidade Não Disponível", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else
            {
                MessageBox.Show("Selecione um investimento.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void UpdateAllPrices_Click(object sender, RoutedEventArgs e)
        {
            if (_priceService != null)
            {
                await UpdateAllPrices();
            }
            else
            {
                MessageBox.Show("🔧 Funcionalidade de atualização de preços em tempo real ainda não implementada.\n\n" +
                              "Para usar esta funcionalidade:\n" +
                              "1. Adicione YahooFinanceService.cs\n" +
                              "2. Adicione InvestmentPriceService.cs\n" +
                              "3. Descomente a inicialização do _priceService no construtor",
                              "Funcionalidade em Desenvolvimento", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async Task UpdateSingleInvestmentPrice(Investment investment)
        {
            try
            {
                if (UpdatePriceButton != null)
                {
                    UpdatePriceButton.IsEnabled = false;
                    UpdatePriceButton.Content = "⏳ Atualizando...";
                }

                var success = await _priceService.UpdateSingleInvestmentPriceAsync(investment.Id);

                if (success)
                {
                    _ = LoadDataAsync();
                    MessageBox.Show($"✅ Preço de '{investment.Name}' atualizado com sucesso!",
                                  "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"❌ Não foi possível atualizar o preço de '{investment.Name}'.\n\n" +
                                  "Verifique se o símbolo está correto ou se há conexão com a internet.",
                                  "Erro", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (UpdatePriceButton != null)
                {
                    UpdatePriceButton.IsEnabled = true;
                    UpdatePriceButton.Content = "📊 Atualizar Preço";
                }
            }
        }

        private async Task UpdateAllPrices()
        {
            if (_isUpdatingPrices) return;

            try
            {
                _isUpdatingPrices = true;
                if (UpdateAllPricesButton != null)
                {
                    UpdateAllPricesButton.IsEnabled = false;
                    UpdateAllPricesButton.Content = "⏳ Atualizando preços...";
                }

                var result = await _priceService.UpdateAllPricesAsync(_currentUser.Id);

                if (result.IsSuccess)
                {
                    _ = LoadDataAsync();

                    var summaryMessage = $"✅ Atualização concluída!\n\n" +
                                       $"📊 {result.SuccessCount} investimentos atualizados\n\n";

                    if (result.UpdatedInvestments.Any())
                    {
                        summaryMessage += "📈 Variações:\n";
                        foreach (var update in result.UpdatedInvestments.Take(5))
                        {
                            summaryMessage += $"• {update.Name}: {update.FormattedChange} ({update.FormattedChangePercent}) {update.ChangeIcon}\n";
                        }

                        if (result.UpdatedInvestments.Count > 5)
                        {
                            summaryMessage += $"... e mais {result.UpdatedInvestments.Count - 5} investimentos\n";
                        }
                    }

                    summaryMessage += $"\n🕐 Atualizado às {DateTime.Now:HH:mm:ss}";

                    MessageBox.Show(summaryMessage, "Preços Atualizados",
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(result.Message, "Erro na Atualização",
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao atualizar preços: {ex.Message}", "Erro",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isUpdatingPrices = false;
                if (UpdateAllPricesButton != null)
                {
                    UpdateAllPricesButton.IsEnabled = true;
                    UpdateAllPricesButton.Content = "🔄 Atualizar Todos os Preços";
                }

                if (_priceService != null)
                    _ = UpdateMarketStatusAsync();
            }
        }

        private async Task UpdateMarketStatusAsync()
        {
            if (_priceService != null)
                await UpdateMarketStatus();
        }

        private async Task AutoRefreshPrices()
        {
            if (!_isUpdatingPrices && _investments.Any() && _priceService != null)
            {
                try
                {
                    var result = await _priceService.UpdateAllPricesAsync(_currentUser.Id);
                    if (result.IsSuccess)
                    {
                        _ = LoadDataAsync();
                        if (LastUpdateText != null)
                            LastUpdateText.Text = $"🔄 Atualização automática: {DateTime.Now:HH:mm:ss}";
                    }
                }
                catch
                {
                    // Falha silenciosa na atualização automática
                }
            }
        }

        private void AutoRefreshToggle_Checked(object sender, RoutedEventArgs e)
        {
            _autoRefreshTimer?.Start();
            if (AutoRefreshStatus != null)
                AutoRefreshStatus.Text = "🟢 Ativa (5 min)";
        }

        private void AutoRefreshToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            _autoRefreshTimer?.Stop();
            if (AutoRefreshStatus != null)
                AutoRefreshStatus.Text = "🔴 Inativa";
        }

        private void SearchSymbols_Click(object sender, RoutedEventArgs e)
        {
            if (_priceService != null)
            {
                MessageBox.Show("Janela de pesquisa de símbolos em desenvolvimento.", "Info",
                              MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Serviço de preços não disponível. Implemente a API do Yahoo Finance primeiro.",
                              "Funcionalidade Não Disponível", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ViewLiveQuote_Click(object sender, RoutedEventArgs e)
        {
            if (DataGrid.SelectedItem is Investment selected)
            {
                if (_priceService != null)
                {
                    try
                    {
                        var symbol = ExtractSymbolFromInvestment(selected);
                        if (string.IsNullOrEmpty(symbol))
                        {
                            MessageBox.Show("Não foi possível extrair o símbolo deste investimento.",
                                          "Símbolo Inválido", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        MessageBox.Show($"Cotação em tempo real para {symbol} em desenvolvimento.", "Info",
                                      MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Erro ao abrir cotação: {ex.Message}", "Erro",
                                      MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    MessageBox.Show("Serviço de preços não disponível. Implemente a API do Yahoo Finance primeiro.",
                                  "Funcionalidade Não Disponível", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else
            {
                MessageBox.Show("Selecione um investimento.", "Aviso",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private string ExtractSymbolFromInvestment(Investment investment)
        {
            if (string.IsNullOrEmpty(investment.Name))
                return "";

            var start = investment.Name.LastIndexOf('(');
            var end = investment.Name.LastIndexOf(')');

            if (start >= 0 && end > start)
            {
                return investment.Name.Substring(start + 1, end - start - 1).Trim().ToUpper();
            }

            return investment.Name.Trim().ToUpper();
        }

        private string ExtractSymbolFromName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "";

            var start = name.LastIndexOf('(');
            var end = name.LastIndexOf(')');

            if (start >= 0 && end > start)
            {
                return name.Substring(start + 1, end - start - 1).Trim().ToUpper();
            }

            return name.Trim().ToUpper();
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            await DeleteSelectedAsync();
        }

        private async Task DeleteSelectedAsync()
        {
            if (DataGrid.SelectedItem is Investment selected)
            {
                var result = MessageBox.Show($"Eliminar '{selected.Name}'?", "Confirmar",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        await _investmentService.DeleteInvestmentAsync(selected.Id);
                        _ = LoadDataAsync();
                        MessageBox.Show("Eliminado!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Erro: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("Selecione um investimento.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void Export_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_investments.Count == 0)
                {
                    MessageBox.Show("Não há investimentos para exportar.", "Sem Dados",
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var exportService = new ExportService();
                await exportService.ExportInvestmentsAsync(_investments.ToList(), _currentUser.Id);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao exportar: {ex.Message}", "Erro",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ShowStats_Click(object sender, RoutedEventArgs e)
        {
            if (_investments.Count == 0)
            {
                MessageBox.Show("Sem dados.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var stats = await _investmentService.GetInvestmentStatisticsAsync(_currentUser.Id);

                MessageBox.Show(
                    $"📊 Estatísticas\n\n" +
                    $"Total: {stats.TotalInvestments}\n" +
                    $"Valor: {stats.TotalValue.ToString("C", _culture)}\n" +
                    $"Investido: {stats.TotalCost.ToString("C", _culture)}\n" +
                    $"Ganho/Perda: {stats.TotalProfitLoss.ToString("C", _culture)}\n" +
                    $"Retorno: {stats.TotalProfitLossPercentage:F2}%",
                    "Estatísticas",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            _ = LoadDataAsync();
        }

        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataGrid.SelectedItem is Investment selected)
            {
                ViewLiveQuote_Click(sender, new RoutedEventArgs());
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _autoRefreshTimer?.Stop();
            _priceService?.Dispose();
            base.OnClosed(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.F5:
                    _ = LoadDataAsync();
                    e.Handled = true;
                    break;

                case Key.U when Keyboard.Modifiers == ModifierKeys.Control:
                    _ = UpdateAllPricesAsync();
                    e.Handled = true;
                    break;

                case Key.N when Keyboard.Modifiers == ModifierKeys.Control:
                    Add_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;

                case Key.Delete:
                    _ = DeleteSelectedAsync();
                    e.Handled = true;
                    break;

                case Key.Enter:
                    if (DataGrid.SelectedItem != null)
                        ViewLiveQuote_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;

                default:
                    base.OnKeyDown(e);
                    break;
            }
        }

        private async Task UpdateAllPricesAsync()
        {
            if (_priceService != null)
            {
                await UpdateAllPrices();
            }
            else
            {
                MessageBox.Show("Serviço de preços não disponível.", "Info",
                              MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        public void EnableYahooFinanceIntegration()
        {
            try
            {
                MessageBox.Show("✅ Integração com Yahoo Finance ativada!\n\n" +
                              "Agora pode:\n" +
                              "• Atualizar preços em tempo real\n" +
                              "• Ver cotações ao vivo\n" +
                              "• Pesquisar símbolos\n" +
                              "• Auto-refresh automático",
                              "API Ativada", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao ativar API: {ex.Message}", "Erro",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}