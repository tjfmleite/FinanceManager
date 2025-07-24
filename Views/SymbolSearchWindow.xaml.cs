using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FinanceManager.Services;

namespace FinanceManager.Views
{
    public partial class SymbolSearchWindow : Window
    {
        private readonly InvestmentPriceService _priceService;
        private ObservableCollection<SearchResult> _searchResults;
        private bool _isSearching = false;

        public SymbolSearchWindow(InvestmentPriceService priceService)
        {
            InitializeComponent();
            _priceService = priceService;
            _searchResults = new ObservableCollection<SearchResult>();

            ResultsListBox.ItemsSource = _searchResults;
            ResultsListBox.SelectionChanged += ResultsListBox_SelectionChanged;

            SearchTextBox.Focus();
        }

        private async void Search_Click(object sender, RoutedEventArgs e)
        {
            await PerformSearch();
        }

        private async void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await PerformSearch();
            }
        }

        private async Task PerformSearch()
        {
            if (_isSearching || string.IsNullOrWhiteSpace(SearchTextBox.Text))
                return;

            try
            {
                _isSearching = true;
                ShowLoading(true);

                SearchButton.IsEnabled = false;
                SearchButton.Content = "⏳ Pesquisando...";

                _searchResults.Clear();
                ViewQuoteButton.IsEnabled = false;

                var query = SearchTextBox.Text.Trim();
                var results = await _priceService.SearchSymbolsAsync(query);

                if (results.Any())
                {
                    foreach (var result in results)
                    {
                        _searchResults.Add(result);
                    }

                    ResultsHeader.Text = $"✅ {results.Count} resultados encontrados para '{query}'";
                }
                else
                {
                    ResultsHeader.Text = $"❌ Nenhum resultado encontrado para '{query}'";

                    // Adicionar sugestões
                    _searchResults.Add(new SearchResult
                    {
                        Symbol = "SUGESTÃO",
                        ShortName = "Tente pesquisar por:",
                        LongName = "• Nome da empresa (ex: Apple, Microsoft)\n• Símbolo (ex: AAPL, MSFT)\n• Criptomoeda (ex: Bitcoin, BTC-USD)",
                        Type = "DICA"
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro na pesquisa: {ex.Message}", "Erro",
                              MessageBoxButton.OK, MessageBoxImage.Error);
                ResultsHeader.Text = "❌ Erro na pesquisa";
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
            ResultsListBox.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
        }

        private void ResultsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedResult = ResultsListBox.SelectedItem as SearchResult;
            ViewQuoteButton.IsEnabled = selectedResult != null && selectedResult.Symbol != "SUGESTÃO";
        }

        private async void ResultsListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ViewQuoteButton.IsEnabled)
            {
                await ViewSelectedQuote();
            }
        }

        private async void ViewQuote_Click(object sender, RoutedEventArgs e)
        {
            await ViewSelectedQuote();
        }

        private async Task ViewSelectedQuote()
        {
            var selectedResult = ResultsListBox.SelectedItem as SearchResult;
            if (selectedResult == null || selectedResult.Symbol == "SUGESTÃO")
                return;

            try
            {
                LoggingService.LogInfo($"Abrindo cotação para: {selectedResult.Symbol}");

                var quoteWindow = new LiveQuoteWindow(selectedResult.Symbol, _priceService);
                quoteWindow.Owner = this.Owner; // Manter a referência da janela pai original
                quoteWindow.Show();

                LoggingService.LogInfo($"Janela de cotação aberta com sucesso para: {selectedResult.Symbol}");
            }
            catch (Exception ex)
            {
                var errorMessage = $"Erro ao abrir janela de cotação para {selectedResult.Symbol}:\n\n{ex.Message}";
                LoggingService.LogError(errorMessage, ex);
                MessageBox.Show(errorMessage, "Erro ao Abrir Cotação",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // Atalhos de teclado
        protected override void OnKeyDown(KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    this.Close();
                    e.Handled = true;
                    break;

                case Key.F3:
                    SearchTextBox.Focus();
                    SearchTextBox.SelectAll();
                    e.Handled = true;
                    break;

                case Key.Enter:
                    if (ViewQuoteButton.IsEnabled && ResultsListBox.SelectedItem != null)
                    {
                        // Correção do erro CS1503: usar async Task em vez de void
                        _ = ViewSelectedQuote(); // Fire-and-forget pattern
                        e.Handled = true;
                    }
                    break;

                default:
                    base.OnKeyDown(e);
                    break;
            }
        }
    }
}