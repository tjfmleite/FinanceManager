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
    public partial class ExpensesWindow : Window
    {
        private readonly User _currentUser;
        private readonly ExpenseService _expenseService;
        private ObservableCollection<Expense> _expenses;
        private readonly CultureInfo _culture = new CultureInfo("pt-PT");

        public ExpensesWindow(User currentUser)
        {
            InitializeComponent();
            _currentUser = currentUser;
            _expenseService = new ExpenseService();
            _expenses = new ObservableCollection<Expense>();

            ExpensesDataGrid.ItemsSource = _expenses;

            // Configurar double-click para edição
            ExpensesDataGrid.MouseDoubleClick += ExpensesDataGrid_MouseDoubleClick;

            LoadExpenses();
        }

        private async void LoadExpenses()
        {
            try
            {
                var expenses = await _expenseService.GetExpensesByUserIdAsync(_currentUser.Id);

                _expenses.Clear();
                foreach (var expense in expenses.OrderByDescending(e => e.Date))
                {
                    _expenses.Add(expense);
                }

                // Atualizar título da janela com contagem
                this.Title = $"💰 Despesas ({_expenses.Count} registos)";

                // Mostrar mensagem se não há despesas
                if (_expenses.Count == 0)
                {
                    MessageBox.Show(
                        "📊 Ainda não há despesas registadas.\n\n" +
                        "Clique em 'Adicionar Despesa' para começar a registar as suas despesas.",
                        "Sem Despesas",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao carregar despesas: {ex.Message}", "Erro",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddExpense_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var addExpenseWindow = new AddExpenseWindow(_currentUser);
                addExpenseWindow.Owner = this;

                if (addExpenseWindow.ShowDialog() == true)
                {
                    LoadExpenses();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao abrir janela de adicionar despesa: {ex.Message}", "Erro",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EditExpense_Click(object sender, RoutedEventArgs e)
        {
            EditSelectedExpense();
        }

        private void ExpensesDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            EditSelectedExpense();
        }

        private void EditSelectedExpense()
        {
            if (ExpensesDataGrid.SelectedItem is Expense selectedExpense)
            {
                try
                {
                    var editExpenseWindow = new EditExpenseWindow(_currentUser, selectedExpense);
                    editExpenseWindow.Owner = this;

                    if (editExpenseWindow.ShowDialog() == true)
                    {
                        LoadExpenses();
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
                MessageBox.Show("Por favor, selecione uma despesa para editar.", "Nenhuma Seleção",
                              MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void DeleteExpense_Click(object sender, RoutedEventArgs e)
        {
            if (ExpensesDataGrid.SelectedItem is Expense selectedExpense)
            {
                var result = MessageBox.Show(
                    $"Tem a certeza que deseja eliminar a despesa:\n\n" +
                    $"📝 {selectedExpense.Description}\n" +
                    $"💶 {selectedExpense.FormattedAmount}\n" +
                    $"📅 {selectedExpense.FormattedDate}\n\n" +
                    "Esta ação não pode ser desfeita.",
                    "Confirmar Eliminação",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        var success = await _expenseService.DeleteExpenseAsync(selectedExpense.Id);
                        if (success)
                        {
                            LoadExpenses();
                            MessageBox.Show("✅ Despesa eliminada com sucesso!", "Sucesso",
                                          MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            MessageBox.Show("❌ Erro ao eliminar despesa. Tente novamente.", "Erro",
                                          MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Erro ao eliminar despesa: {ex.Message}", "Erro",
                                      MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("Por favor, selecione uma despesa para eliminar.", "Nenhuma Seleção",
                              MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void FilterExpenses_Click(object sender, RoutedEventArgs e)
        {
            ShowFilterOptions();
        }

        private void ShowFilterOptions()
        {
            var filterWindow = new Window
            {
                Title = "🔍 Filtrar Despesas",
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
            categoryCombo.Items.Add(new ComboBoxItem { Content = "Alimentação" });
            categoryCombo.Items.Add(new ComboBoxItem { Content = "Transporte" });
            categoryCombo.Items.Add(new ComboBoxItem { Content = "Casa" });
            categoryCombo.Items.Add(new ComboBoxItem { Content = "Saúde" });
            categoryCombo.Items.Add(new ComboBoxItem { Content = "Entretenimento" });
            categoryCombo.Items.Add(new ComboBoxItem { Content = "Compras" });
            categoryCombo.Items.Add(new ComboBoxItem { Content = "Educação" });
            categoryCombo.Items.Add(new ComboBoxItem { Content = "Outros" });
            categoryCombo.SelectedIndex = 0;
            stackPanel.Children.Add(categoryCombo);

            // Filtro por período
            stackPanel.Children.Add(new Label { Content = "📅 Período:", FontWeight = FontWeights.SemiBold });
            var periodCombo = new ComboBox { Margin = new Thickness(0, 0, 0, 15) };
            periodCombo.Items.Add(new ComboBoxItem { Content = "Todos os períodos" });
            periodCombo.Items.Add(new ComboBoxItem { Content = "Este mês" });
            periodCombo.Items.Add(new ComboBoxItem { Content = "Últimos 30 dias" });
            periodCombo.Items.Add(new ComboBoxItem { Content = "Últimos 3 meses" });
            periodCombo.Items.Add(new ComboBoxItem { Content = "Este ano" });
            periodCombo.SelectedIndex = 0;
            stackPanel.Children.Add(periodCombo);

            // Botões
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 20, 0, 0)
            };

            var applyButton = new Button
            {
                Content = "✅ Aplicar",
                Width = 80,
                Height = 30,
                Margin = new Thickness(5, 0, 5, 0)
            };

            var clearButton = new Button
            {
                Content = "🔄 Limpar",
                Width = 80,
                Height = 30,
                Margin = new Thickness(5, 0, 5, 0)
            };

            applyButton.Click += (s, e) =>
            {
                ApplyFilters(categoryCombo.SelectedIndex, periodCombo.SelectedIndex);
                filterWindow.Close();
            };

            clearButton.Click += (s, e) =>
            {
                LoadExpenses();
                filterWindow.Close();
            };

            buttonPanel.Children.Add(applyButton);
            buttonPanel.Children.Add(clearButton);
            stackPanel.Children.Add(buttonPanel);

            filterWindow.Content = stackPanel;
            filterWindow.ShowDialog();
        }

        private async void ApplyFilters(int categoryIndex, int periodIndex)
        {
            try
            {
                var allExpenses = await _expenseService.GetExpensesByUserIdAsync(_currentUser.Id);
                var filteredExpenses = allExpenses.AsQueryable();

                // Filtro por categoria
                if (categoryIndex > 0)
                {
                    var categories = new[] { "", "Alimentação", "Transporte", "Casa", "Saúde", "Entretenimento", "Compras", "Educação", "Outros" };
                    var selectedCategory = categories[categoryIndex];
                    filteredExpenses = filteredExpenses.Where(e => e.Category == selectedCategory);
                }

                // Filtro por período
                if (periodIndex > 0)
                {
                    var now = DateTime.Now;
                    DateTime startDate = periodIndex switch
                    {
                        1 => new DateTime(now.Year, now.Month, 1), // Este mês
                        2 => now.AddDays(-30), // Últimos 30 dias
                        3 => now.AddMonths(-3), // Últimos 3 meses
                        4 => new DateTime(now.Year, 1, 1), // Este ano
                        _ => DateTime.MinValue
                    };

                    filteredExpenses = filteredExpenses.Where(e => e.Date >= startDate);
                }

                _expenses.Clear();
                foreach (var expense in filteredExpenses.OrderByDescending(e => e.Date))
                {
                    _expenses.Add(expense);
                }

                this.Title = $"💰 Despesas ({_expenses.Count} registos) - Filtradas";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao aplicar filtros: {ex.Message}", "Erro",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportExpenses_Click(object sender, RoutedEventArgs e)
        {
            if (_expenses.Count == 0)
            {
                MessageBox.Show("Não há despesas para exportar.", "Sem Dados",
                              MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            MessageBox.Show("Funcionalidade de exportação em desenvolvimento.", "Info",
                          MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void RefreshExpenses_Click(object sender, RoutedEventArgs e)
        {
            LoadExpenses();
        }

        private void ShowStats_Click(object sender, RoutedEventArgs e)
        {
            if (_expenses.Count == 0)
            {
                MessageBox.Show("Não há dados suficientes para mostrar estatísticas.", "Sem Dados",
                              MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var stats = CalculateStatistics();

            MessageBox.Show(
                $"📊 Estatísticas das Despesas\n\n" +
                $"📈 Total de despesas: {_expenses.Count}\n" +
                $"💶 Valor total: {stats.Total.ToString("C", _culture)}\n" +
                $"📊 Valor médio: {stats.Average.ToString("C", _culture)}\n" +
                $"📉 Menor despesa: {stats.Min.ToString("C", _culture)}\n" +
                $"📈 Maior despesa: {stats.Max.ToString("C", _culture)}\n\n" +
                $"🏷️ Categoria mais usada: {stats.TopCategory}\n" +
                $"📅 Período: {stats.DateRange}",
                "Estatísticas",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private ExpenseStatistics CalculateStatistics()
        {
            var stats = new ExpenseStatistics
            {
                Total = _expenses.Sum(e => e.Amount),
                Average = _expenses.Average(e => e.Amount),
                Min = _expenses.Min(e => e.Amount),
                Max = _expenses.Max(e => e.Amount),
                TopCategory = _expenses.GroupBy(e => e.Category)
                                     .OrderByDescending(g => g.Count())
                                     .First().Key
            };

            if (_expenses.Any())
            {
                var minDate = _expenses.Min(e => e.Date);
                var maxDate = _expenses.Max(e => e.Date);
                stats.DateRange = $"{minDate:dd/MM/yyyy} a {maxDate:dd/MM/yyyy}";
            }

            return stats;
        }

        // Atalhos de teclado
        protected override void OnKeyDown(KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.F2:
                    EditSelectedExpense();
                    e.Handled = true;
                    break;

                case Key.Delete:
                    DeleteExpense_Click(this, e);
                    e.Handled = true;
                    break;

                case Key.F5:
                    RefreshExpenses_Click(this, e);
                    e.Handled = true;
                    break;

                case Key.N when Keyboard.Modifiers == ModifierKeys.Control:
                    AddExpense_Click(this, e);
                    e.Handled = true;
                    break;

                case Key.F when Keyboard.Modifiers == ModifierKeys.Control:
                    FilterExpenses_Click(this, e);
                    e.Handled = true;
                    break;

                case Key.E when Keyboard.Modifiers == ModifierKeys.Control:
                    ExportExpenses_Click(this, e);
                    e.Handled = true;
                    break;

                default:
                    base.OnKeyDown(e);
                    break;
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Cleanup se necessário
        }
    }

    // Classe auxiliar para estatísticas
    public class ExpenseStatistics
    {
        public decimal Total { get; set; }
        public decimal Average { get; set; }
        public decimal Min { get; set; }
        public decimal Max { get; set; }
        public string TopCategory { get; set; } = "";
        public string DateRange { get; set; } = "";
    }
}