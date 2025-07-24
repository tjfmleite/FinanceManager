using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FinanceManager.Models;
using FinanceManager.Services;

namespace FinanceManager.Views
{
    public partial class IncomesWindow : Window
    {
        private readonly User _currentUser;
        private readonly IncomeService _incomeService;
        private ObservableCollection<Income> _incomes;

        public IncomesWindow(User currentUser)
        {
            InitializeComponent();
            _currentUser = currentUser;
            _incomeService = new IncomeService();
            _incomes = new ObservableCollection<Income>();

            IncomesDataGrid.ItemsSource = _incomes;

            // Configurar double-click para edição
            IncomesDataGrid.MouseDoubleClick += IncomesDataGrid_MouseDoubleClick;

            LoadIncomes();
        }

        private async void LoadIncomes()
        {
            try
            {
                var incomes = await _incomeService.GetIncomesByUserIdAsync(_currentUser.Id);

                _incomes.Clear();
                foreach (var income in incomes.OrderByDescending(i => i.Date))
                {
                    _incomes.Add(income);
                }

                // Atualizar título da janela com contagem
                this.Title = $"💰 Receitas ({_incomes.Count} registos)";

                // Atualizar estatísticas no footer
                UpdateFooterStatistics();

                // Mostrar/esconder empty state
                if (_incomes.Count == 0)
                {
                    EmptyStatePanel.Visibility = Visibility.Visible;
                }
                else
                {
                    EmptyStatePanel.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao carregar receitas: {ex.Message}", "Erro",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateFooterStatistics()
        {
            if (_incomes.Count == 0)
            {
                TotalCountText.Text = "0";
                TotalAmountText.Text = "€0,00";
                MonthlyAverageText.Text = "€0,00";
                ThisMonthText.Text = "€0,00";
                return;
            }

            var culture = new CultureInfo("pt-PT");

            // Total de receitas
            TotalCountText.Text = _incomes.Count.ToString();

            // Valor total
            var totalAmount = _incomes.Sum(i => i.Amount);
            TotalAmountText.Text = totalAmount.ToString("C", culture);

            // Média mensal (últimos 12 meses)
            var twelveMonthsAgo = DateTime.Now.AddMonths(-12);
            var recentIncomes = _incomes.Where(i => i.Date >= twelveMonthsAgo);
            var monthsCount = recentIncomes.Any() ?
                recentIncomes.GroupBy(i => new { i.Date.Year, i.Date.Month }).Count() : 1;
            var monthlyAverage = recentIncomes.Sum(i => i.Amount) / monthsCount;
            MonthlyAverageText.Text = monthlyAverage.ToString("C", culture);

            // Este mês
            var startOfMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var thisMonthAmount = _incomes.Where(i => i.Date >= startOfMonth).Sum(i => i.Amount);
            ThisMonthText.Text = thisMonthAmount.ToString("C", culture);
        }

        private void AddIncome_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var addIncomeWindow = new AddIncomeWindow(_currentUser);
                addIncomeWindow.Owner = this;

                if (addIncomeWindow.ShowDialog() == true)
                {
                    LoadIncomes();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao abrir janela de adicionar receita: {ex.Message}", "Erro",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EditIncome_Click(object sender, RoutedEventArgs e)
        {
            EditSelectedIncome();
        }

        private void IncomesDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            EditSelectedIncome();
        }

        private void EditSelectedIncome()
        {
            if (IncomesDataGrid.SelectedItem is Income selectedIncome)
            {
                try
                {
                    var editIncomeWindow = new EditIncomeWindow(_currentUser, selectedIncome);
                    editIncomeWindow.Owner = this;

                    if (editIncomeWindow.ShowDialog() == true)
                    {
                        LoadIncomes();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Erro ao abrir janela de edição: {ex.Message}", "Erro",
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Por favor, selecione uma receita para editar.", "Nenhuma Seleção",
                              MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void DeleteIncome_Click(object sender, RoutedEventArgs e)
        {
            if (IncomesDataGrid.SelectedItem is Income selectedIncome)
            {
                var culture = new CultureInfo("pt-PT");
                var result = MessageBox.Show(
                    $"🗑️ Eliminar Receita\n\n" +
                    $"Tem a certeza que quer eliminar esta receita?\n\n" +
                    $"📝 {selectedIncome.Description}\n" +
                    $"💶 {selectedIncome.Amount.ToString("C", culture)}\n" +
                    $"🏷️ {selectedIncome.Category}\n" +
                    $"📅 {selectedIncome.Date:dd/MM/yyyy}\n\n" +
                    $"⚠️ Esta ação não pode ser desfeita!",
                    "Confirmar Eliminação",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        var success = await _incomeService.DeleteIncomeAsync(selectedIncome.Id);

                        if (success)
                        {
                            MessageBox.Show("✅ Receita eliminada com sucesso!", "Sucesso",
                                          MessageBoxButton.OK, MessageBoxImage.Information);
                            LoadIncomes();
                        }
                        else
                        {
                            MessageBox.Show("❌ Erro ao eliminar receita.", "Erro",
                                          MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Erro ao eliminar receita: {ex.Message}", "Erro",
                                      MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("Por favor, selecione uma receita para eliminar.", "Nenhuma Seleção",
                              MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void FilterIncomes_Click(object sender, RoutedEventArgs e)
        {
            ShowFilterOptions();
        }

        private void ShowFilterOptions()
        {
            var filterWindow = new Window
            {
                Title = "🔍 Filtrar Receitas",
                Width = 400,
                Height = 300,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var stackPanel = new StackPanel { Margin = new Thickness(20) };

            // Filtro por categoria
            stackPanel.Children.Add(new Label { Content = "🏷️ Categoria:", FontWeight = FontWeights.SemiBold });
            var categoryCombo = new ComboBox { Margin = new Thickness(0, 0, 0, 15) };
            categoryCombo.Items.Add(new ComboBoxItem { Content = "Todas as categorias" });
            categoryCombo.Items.Add(new ComboBoxItem { Content = "Salário" });
            categoryCombo.Items.Add(new ComboBoxItem { Content = "Investimentos" });
            categoryCombo.Items.Add(new ComboBoxItem { Content = "Rendas" });
            categoryCombo.Items.Add(new ComboBoxItem { Content = "Freelancing" });
            categoryCombo.Items.Add(new ComboBoxItem { Content = "Presentes" });
            categoryCombo.Items.Add(new ComboBoxItem { Content = "Reembolsos" });
            categoryCombo.Items.Add(new ComboBoxItem { Content = "Prémios" });
            categoryCombo.Items.Add(new ComboBoxItem { Content = "Dividendos" });
            categoryCombo.Items.Add(new ComboBoxItem { Content = "Outros" });
            categoryCombo.SelectedIndex = 0;
            stackPanel.Children.Add(categoryCombo);

            // Filtro por período
            stackPanel.Children.Add(new Label { Content = "📅 Período:", FontWeight = FontWeights.SemiBold });
            var periodCombo = new ComboBox { Margin = new Thickness(0, 0, 0, 15) };
            periodCombo.Items.Add(new ComboBoxItem { Content = "Todas as datas" });
            periodCombo.Items.Add(new ComboBoxItem { Content = "Última semana" });
            periodCombo.Items.Add(new ComboBoxItem { Content = "Último mês" });
            periodCombo.Items.Add(new ComboBoxItem { Content = "Últimos 3 meses" });
            periodCombo.Items.Add(new ComboBoxItem { Content = "Este ano" });
            periodCombo.SelectedIndex = 0;
            stackPanel.Children.Add(periodCombo);

            // Botões
            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };

            var applyButton = new Button { Content = "✅ Aplicar", Margin = new Thickness(5), Padding = new Thickness(15, 5, 15, 5) };
            applyButton.Click += (s, e) =>
            {
                ApplyFilters(categoryCombo, periodCombo);
                filterWindow.Close();
            };

            var clearButton = new Button { Content = "🔄 Limpar", Margin = new Thickness(5), Padding = new Thickness(15, 5, 15, 5) };
            clearButton.Click += (s, e) =>
            {
                LoadIncomes();
                filterWindow.Close();
            };

            buttonPanel.Children.Add(applyButton);
            buttonPanel.Children.Add(clearButton);
            stackPanel.Children.Add(buttonPanel);

            filterWindow.Content = stackPanel;
            filterWindow.ShowDialog();
        }

        private async void ApplyFilters(ComboBox categoryCombo, ComboBox periodCombo)
        {
            try
            {
                var allIncomes = await _incomeService.GetIncomesByUserIdAsync(_currentUser.Id);
                var filteredIncomes = allIncomes.AsEnumerable();

                // Filtro por categoria
                if (categoryCombo.SelectedIndex > 0)
                {
                    var selectedCategory = ((ComboBoxItem)categoryCombo.SelectedItem).Content.ToString();
                    filteredIncomes = filteredIncomes.Where(i => i.Category == selectedCategory);
                }

                // Filtro por período
                var today = DateTime.Today;
                switch (periodCombo.SelectedIndex)
                {
                    case 1: // Última semana
                        filteredIncomes = filteredIncomes.Where(i => i.Date >= today.AddDays(-7));
                        break;
                    case 2: // Último mês
                        filteredIncomes = filteredIncomes.Where(i => i.Date >= today.AddDays(-30));
                        break;
                    case 3: // Últimos 3 meses
                        filteredIncomes = filteredIncomes.Where(i => i.Date >= today.AddDays(-90));
                        break;
                    case 4: // Este ano
                        filteredIncomes = filteredIncomes.Where(i => i.Date.Year == today.Year);
                        break;
                }

                // Atualizar lista
                _incomes.Clear();
                foreach (var income in filteredIncomes.OrderByDescending(i => i.Date))
                {
                    _incomes.Add(income);
                }

                this.Title = $"💰 Receitas ({_incomes.Count} registos filtrados)";
                UpdateFooterStatistics();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao aplicar filtros: {ex.Message}", "Erro",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportIncomes_Click(object sender, RoutedEventArgs e)
        {
            if (_incomes.Count == 0)
            {
                MessageBox.Show("Não há receitas para exportar.", "Sem Dados",
                              MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Exportar Receitas",
                    Filter = "Ficheiro CSV (*.csv)|*.csv|Ficheiro de texto (*.txt)|*.txt",
                    DefaultExt = "csv",
                    FileName = $"receitas_{DateTime.Now:yyyy-MM-dd}.csv"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    ExportToCsv(saveDialog.FileName);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao exportar: {ex.Message}", "Erro",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportToCsv(string fileName)
        {
            try
            {
                using (var writer = new System.IO.StreamWriter(fileName, false, System.Text.Encoding.UTF8))
                {
                    // Header
                    writer.WriteLine("Data;Descrição;Categoria;Valor;Notas");

                    // Dados
                    foreach (var income in _incomes.OrderBy(i => i.Date))
                    {
                        writer.WriteLine($"{income.Date:dd/MM/yyyy};{income.Description};{income.Category};{income.Amount:F2};{income.Notes ?? ""}");
                    }
                }

                var result = MessageBox.Show(
                    $"✅ Receitas exportadas com sucesso!\n\n" +
                    $"📁 Ficheiro: {System.IO.Path.GetFileName(fileName)}\n" +
                    $"📊 {_incomes.Count} receitas exportadas\n\n" +
                    $"Quer abrir a pasta do ficheiro?",
                    "Exportação Concluída",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{fileName}\"");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao criar ficheiro: {ex.Message}", "Erro de Exportação",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshIncomes_Click(object sender, RoutedEventArgs e)
        {
            LoadIncomes();
        }

        private void ShowIncomeStats_Click(object sender, RoutedEventArgs e)
        {
            if (_incomes.Count == 0)
            {
                MessageBox.Show("Não há receitas para mostrar estatísticas.", "Sem Dados",
                              MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var stats = CalculateStatistics();
            var culture = new CultureInfo("pt-PT");

            MessageBox.Show(
                $"📊 Estatísticas das Receitas\n\n" +
                $"📈 Total de receitas: {_incomes.Count}\n" +
                $"💶 Valor total: {stats.Total.ToString("C", culture)}\n" +
                $"📊 Valor médio: {stats.Average.ToString("C", culture)}\n" +
                $"📉 Menor receita: {stats.Min.ToString("C", culture)}\n" +
                $"📈 Maior receita: {stats.Max.ToString("C", culture)}\n\n" +
                $"🏷️ Categoria mais usada: {stats.TopCategory}\n" +
                $"📅 Período: {stats.DateRange}\n\n" +
                $"💰 Receita média mensal: {stats.MonthlyAverage.ToString("C", culture)}\n" +
                $"📈 Receitas este mês: {stats.ThisMonth.ToString("C", culture)}",
                "Estatísticas",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private IncomeStatistics CalculateStatistics()
        {
            var culture = new CultureInfo("pt-PT");
            var stats = new IncomeStatistics
            {
                Total = _incomes.Sum(i => i.Amount),
                Average = _incomes.Average(i => i.Amount),
                Min = _incomes.Min(i => i.Amount),
                Max = _incomes.Max(i => i.Amount),
                TopCategory = _incomes.GroupBy(i => i.Category)
                                     .OrderByDescending(g => g.Count())
                                     .First().Key
            };

            if (_incomes.Any())
            {
                var minDate = _incomes.Min(i => i.Date);
                var maxDate = _incomes.Max(i => i.Date);
                stats.DateRange = $"{minDate:dd/MM/yyyy} a {maxDate:dd/MM/yyyy}";

                // Calcular média mensal
                var monthsSpan = ((maxDate.Year - minDate.Year) * 12) + maxDate.Month - minDate.Month + 1;
                stats.MonthlyAverage = stats.Total / Math.Max(monthsSpan, 1);

                // Receitas deste mês
                var startOfMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                stats.ThisMonth = _incomes.Where(i => i.Date >= startOfMonth).Sum(i => i.Amount);
            }

            return stats;
        }

        // Atalhos de teclado
        protected override void OnKeyDown(KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.F2:
                    EditSelectedIncome();
                    e.Handled = true;
                    break;

                case Key.Delete:
                    DeleteIncome_Click(this, e);
                    e.Handled = true;
                    break;

                case Key.F5:
                    RefreshIncomes_Click(this, e);
                    e.Handled = true;
                    break;

                case Key.N when Keyboard.Modifiers == ModifierKeys.Control:
                    AddIncome_Click(this, e);
                    e.Handled = true;
                    break;

                case Key.F when Keyboard.Modifiers == ModifierKeys.Control:
                    FilterIncomes_Click(this, e);
                    e.Handled = true;
                    break;

                case Key.E when Keyboard.Modifiers == ModifierKeys.Control:
                    ExportIncomes_Click(this, e);
                    e.Handled = true;
                    break;

                case Key.F1:
                    ShowHelp();
                    e.Handled = true;
                    break;

                default:
                    base.OnKeyDown(e);
                    break;
            }
        }

        private void ShowHelp()
        {
            MessageBox.Show(
                "💡 Ajuda - Gestão de Receitas\n\n" +
                "🔧 Funcionalidades:\n" +
                "• Adicionar nova receita\n" +
                "• Editar receita existente (duplo-clique)\n" +
                "• Eliminar receita selecionada\n" +
                "• Filtrar por categoria/período\n" +
                "• Exportar para CSV\n" +
                "• Ver estatísticas detalhadas\n\n" +
                "⌨️ Atalhos:\n" +
                "• Ctrl+N: Nova receita\n" +
                "• F2: Editar selecionada\n" +
                "• Delete: Eliminar selecionada\n" +
                "• Ctrl+F: Filtrar\n" +
                "• Ctrl+E: Exportar\n" +
                "• F5: Atualizar\n" +
                "• F1: Esta ajuda\n\n" +
                "💡 Dica: Duplo-clique numa receita para editá-la rapidamente!",
                "Ajuda",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Cleanup se necessário
        }
    }

    // Classe auxiliar para estatísticas
    public class IncomeStatistics
    {
        public decimal Total { get; set; }
        public decimal Average { get; set; }
        public decimal Min { get; set; }
        public decimal Max { get; set; }
        public string TopCategory { get; set; } = "";
        public string DateRange { get; set; } = "";
        public decimal MonthlyAverage { get; set; }
        public decimal ThisMonth { get; set; }
    }
}
