using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using FinanceManager.Models;
using FinanceManager.Services;

namespace FinanceManager.Views
{
    public partial class RecurringExpensesWindow : Window
    {
        private readonly User _currentUser;
        private readonly RecurringExpenseService _recurringExpenseService;
        private List<RecurringExpenseViewModel> _allExpenses;
        private List<RecurringExpenseViewModel> _filteredExpenses;

        public RecurringExpensesWindow(User currentUser)
        {
            InitializeComponent();
            _currentUser = currentUser;
            _recurringExpenseService = new RecurringExpenseService();
            _allExpenses = new List<RecurringExpenseViewModel>();
            _filteredExpenses = new List<RecurringExpenseViewModel>();

            InitializeWindow();
        }

        private async void InitializeWindow()
        {
            this.Title = $"Despesas Recorrentes - {_currentUser.Name}";

            // Configurar placeholder do search
            SearchTextBox.GotFocus += SearchTextBox_GotFocus;
            SearchTextBox.LostFocus += SearchTextBox_LostFocus;

            // Carregar dados
            await LoadRecurringExpensesAsync();

            // Popular filtro de categorias
            PopulateCategoryFilter();
        }

        private async System.Threading.Tasks.Task LoadRecurringExpensesAsync()
        {
            try
            {
                var expenses = await _recurringExpenseService.GetRecurringExpensesByUserAsync(_currentUser.Id);

                _allExpenses = expenses.Select(e => new RecurringExpenseViewModel(e)).ToList();
                _filteredExpenses = new List<RecurringExpenseViewModel>(_allExpenses);

                UpdateDataGrid();
                UpdateStatistics();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao carregar despesas recorrentes: {ex.Message}",
                              "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"Erro: {ex}");
            }
        }

        private void PopulateCategoryFilter()
        {
            var categories = _allExpenses.Select(e => e.Category).Distinct().OrderBy(c => c).ToList();

            CategoryFilterComboBox.Items.Clear();
            CategoryFilterComboBox.Items.Add(new ComboBoxItem { Content = "Todas as Categorias", Tag = "All", IsSelected = true });

            foreach (var category in categories)
            {
                CategoryFilterComboBox.Items.Add(new ComboBoxItem { Content = category, Tag = category });
            }
        }

        private void UpdateDataGrid()
        {
            RecurringExpensesDataGrid.ItemsSource = null;
            RecurringExpensesDataGrid.ItemsSource = _filteredExpenses;
        }

        private void UpdateStatistics()
        {
            var activeExpenses = _filteredExpenses.Where(e => e.IsActive).ToList();
            var totalCount = _filteredExpenses.Count;
            var activeCount = activeExpenses.Count;

            // Calcular total mensal aproximado
            var monthlyTotal = activeExpenses.Sum(e => e.MonthlyEquivalent);

            var culture = new CultureInfo("pt-PT");

            StatsText.Text = $"📊 {totalCount} despesas • {activeCount} ativas • {monthlyTotal.ToString("C", culture)}/mês";

            if (totalCount > 0)
            {
                TotalText.Text = $"💰 Total Mensal Estimado: {monthlyTotal.ToString("C", culture)}";
            }
            else
            {
                TotalText.Text = "Nenhuma despesa recorrente encontrada";
            }
        }

        private void ApplyFilters()
        {
            _filteredExpenses = new List<RecurringExpenseViewModel>(_allExpenses);

            // Filtro por status
            var statusFilter = ((ComboBoxItem)StatusFilterComboBox.SelectedItem)?.Tag?.ToString();
            if (statusFilter != "All")
            {
                var isActive = statusFilter == "Active";
                _filteredExpenses = _filteredExpenses.Where(e => e.IsActive == isActive).ToList();
            }

            // Filtro por categoria
            var categoryFilter = ((ComboBoxItem)CategoryFilterComboBox.SelectedItem)?.Tag?.ToString();
            if (categoryFilter != "All")
            {
                _filteredExpenses = _filteredExpenses.Where(e => e.Category == categoryFilter).ToList();
            }

            // Filtro por pesquisa
            var searchText = SearchTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(searchText) && searchText != "Pesquisar...")
            {
                _filteredExpenses = _filteredExpenses.Where(e =>
                    e.Description.ToLower().Contains(searchText.ToLower()) ||
                    e.Category.ToLower().Contains(searchText.ToLower()) ||
                    (e.Notes != null && e.Notes.ToLower().Contains(searchText.ToLower()))
                ).ToList();
            }

            UpdateDataGrid();
            UpdateStatistics();
        }

        private async void AddRecurringExpense_Click(object sender, RoutedEventArgs e)
        {
            var addWindow = new AddRecurringExpenseWindow(_currentUser);
            var result = addWindow.ShowDialog();

            if (result == true)
            {
                await LoadRecurringExpensesAsync();
                PopulateCategoryFilter();
            }
        }

        private async void Edit_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var expense = button?.Tag as RecurringExpenseViewModel;

            if (expense != null)
            {
                var editWindow = new EditRecurringExpenseWindow(_currentUser, expense.OriginalExpense);
                var result = editWindow.ShowDialog();

                if (result == true)
                {
                    await LoadRecurringExpensesAsync();
                    PopulateCategoryFilter();
                }
            }
        }

        private async void Toggle_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var expense = button?.Tag as RecurringExpenseViewModel;

            if (expense != null)
            {
                var action = expense.IsActive ? "desativar" : "ativar";
                var result = MessageBox.Show(
                    $"Tem a certeza que quer {action} a despesa '{expense.Description}'?",
                    "Confirmar Ação",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    var success = await _recurringExpenseService.ToggleActiveStatusAsync(expense.Id, _currentUser.Id);
                    if (success)
                    {
                        await LoadRecurringExpensesAsync();
                        MessageBox.Show($"Despesa {action}da com sucesso!", "Sucesso",
                                      MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show($"Erro ao {action} a despesa.", "Erro",
                                      MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var expense = button?.Tag as RecurringExpenseViewModel;

            if (expense != null)
            {
                var result = MessageBox.Show(
                    $"Tem a certeza que quer eliminar permanentemente a despesa '{expense.Description}'?\n\n" +
                    "Esta ação não pode ser desfeita!",
                    "Confirmar Eliminação",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    var success = await _recurringExpenseService.DeleteRecurringExpenseAsync(expense.Id, _currentUser.Id);
                    if (success)
                    {
                        await LoadRecurringExpensesAsync();
                        PopulateCategoryFilter();
                        MessageBox.Show("Despesa eliminada com sucesso!", "Sucesso",
                                      MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Erro ao eliminar a despesa.", "Erro",
                                      MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadRecurringExpensesAsync();
            PopulateCategoryFilter();
        }

        private void StatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void CategoryFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void SearchTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (SearchTextBox.Text == "Pesquisar...")
            {
                SearchTextBox.Text = "";
                SearchTextBox.Foreground = System.Windows.Media.Brushes.Black;
            }
        }

        private void SearchTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SearchTextBox.Text))
            {
                SearchTextBox.Text = "Pesquisar...";
                SearchTextBox.Foreground = System.Windows.Media.Brushes.Gray;
            }
        }
    }

    // ViewModel para apresentar dados na DataGrid
    public class RecurringExpenseViewModel
    {
        public RecurringExpense OriginalExpense { get; private set; }

        public RecurringExpenseViewModel(RecurringExpense expense)
        {
            OriginalExpense = expense;
        }

        public int Id => OriginalExpense.Id;
        public string Description => OriginalExpense.Description;
        public decimal Amount => OriginalExpense.Amount;
        public string Category => OriginalExpense.Category;
        public DateTime StartDate => OriginalExpense.StartDate;
        public DateTime? EndDate => OriginalExpense.EndDate;
        public string Frequency => OriginalExpense.Frequency;
        public string Notes => OriginalExpense.Notes;
        public bool IsActive => OriginalExpense.IsActive;
        public DateTime? NextOccurrence => OriginalExpense.NextOccurrence;

        public string StatusDisplay => IsActive ? "✅ Ativa" : "⏸️ Inativa";

        public string AmountFormatted
        {
            get
            {
                var culture = new CultureInfo("pt-PT");
                return Amount.ToString("C", culture);
            }
        }

        public string FrequencyDisplay => Frequency switch
        {
            "Daily" => "Diária",
            "Weekly" => "Semanal",
            "Monthly" => "Mensal",
            "Quarterly" => "Trimestral",
            "Yearly" => "Anual",
            _ => Frequency
        };

        public string StartDateFormatted => StartDate.ToString("dd/MM/yyyy");

        public string NextOccurrenceFormatted => NextOccurrence?.ToString("dd/MM/yyyy") ?? "—";

        // Calcular equivalente mensal para estatísticas
        public decimal MonthlyEquivalent
        {
            get
            {
                if (!IsActive) return 0;

                return Frequency switch
                {
                    "Daily" => Amount * 30, // Aproximação
                    "Weekly" => Amount * 4.33m, // 52 semanas / 12 meses
                    "Monthly" => Amount,
                    "Quarterly" => Amount / 3,
                    "Yearly" => Amount / 12,
                    _ => Amount // Default to monthly
                };
            }
        }
    }
}