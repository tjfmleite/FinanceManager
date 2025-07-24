using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using FinanceManager.Models;
using FinanceManager.Services;
using FinanceManager.Helpers;

namespace FinanceManager.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        private readonly UserService _userService;
        private readonly ExpenseService _expenseService;
        private readonly IncomeService _incomeService;
        private readonly SavingsService _savingsService;
        private readonly InvestmentService _investmentService;
        private User? _currentUser;
        private string _selectedPeriod = "Este Mês";

        // Campos privados para propriedades financeiras
        private decimal _totalExpenses = 0;
        private decimal _totalExpensesThisMonth = 0;
        private decimal _totalIncome = 0;
        private decimal _totalIncomeThisMonth = 0;
        private decimal _balanceThisMonth = 0;
        private decimal _portfolioValue = 0;
        private bool _isLoading = false;

        // Cultura portuguesa para formatação CORRETA em euros
        private readonly CultureInfo _culture = new CultureInfo("pt-PT");

        public MainViewModel()
        {
            _userService = new UserService();
            _expenseService = new ExpenseService();
            _incomeService = new IncomeService();
            _savingsService = new SavingsService();
            _investmentService = new InvestmentService();

            LoadDataCommand = new RelayCommand(async () => await LoadData());
            AddExpenseCommand = new RelayCommand(() => AddExpense());
            RefreshDataCommand = new RelayCommand(async () => await RefreshAllData());

            Expenses = new ObservableCollection<Expense>();
            SavingsTargets = new ObservableCollection<SavingsTarget>();
            Categories = new ObservableCollection<string>
            {
                "Alimentação", "Transporte", "Casa", "Saúde",
                "Entretenimento", "Compras", "Educação", "Outros"
            };
        }

        #region Propriedades

        public User? CurrentUser
        {
            get => _currentUser;
            set => SetProperty(ref _currentUser, value);
        }

        public ObservableCollection<Expense> Expenses { get; }
        public ObservableCollection<SavingsTarget> SavingsTargets { get; }
        public ObservableCollection<string> Categories { get; }

        public string SelectedPeriod
        {
            get => _selectedPeriod;
            set => SetProperty(ref _selectedPeriod, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        // Propriedades financeiras principais
        public decimal TotalExpenses
        {
            get => _totalExpenses;
            set
            {
                SetProperty(ref _totalExpenses, value);
                OnPropertyChanged(nameof(FormattedTotalExpenses));
            }
        }

        public decimal TotalExpensesThisMonth
        {
            get => _totalExpensesThisMonth;
            set
            {
                SetProperty(ref _totalExpensesThisMonth, value);
                OnPropertyChanged(nameof(FormattedTotalExpensesThisMonth));
                UpdateBalance();
            }
        }

        public decimal TotalIncome
        {
            get => _totalIncome;
            set
            {
                SetProperty(ref _totalIncome, value);
                OnPropertyChanged(nameof(FormattedTotalIncome));
            }
        }

        public decimal TotalIncomeThisMonth
        {
            get => _totalIncomeThisMonth;
            set
            {
                SetProperty(ref _totalIncomeThisMonth, value);
                OnPropertyChanged(nameof(FormattedTotalIncomeThisMonth));
                UpdateBalance();
            }
        }

        public decimal BalanceThisMonth
        {
            get => _balanceThisMonth;
            set
            {
                SetProperty(ref _balanceThisMonth, value);
                OnPropertyChanged(nameof(FormattedBalanceThisMonth));
                OnPropertyChanged(nameof(IsPositiveBalance));
                OnPropertyChanged(nameof(FormattedSavingsRate)); // Atualizar taxa de poupança
            }
        }

        public decimal PortfolioValue
        {
            get => _portfolioValue;
            set
            {
                SetProperty(ref _portfolioValue, value);
                OnPropertyChanged(nameof(FormattedPortfolioValue));
            }
        }

        // Propriedades formatadas em EUROS (pt-PT)
        public string FormattedTotalExpenses => TotalExpenses.ToString("C", _culture);
        public string FormattedTotalExpensesThisMonth => TotalExpensesThisMonth.ToString("C", _culture);
        public string FormattedTotalIncome => TotalIncome.ToString("C", _culture);
        public string FormattedTotalIncomeThisMonth => TotalIncomeThisMonth.ToString("C", _culture);
        public string FormattedBalanceThisMonth => BalanceThisMonth.ToString("C", _culture);
        public string FormattedPortfolioValue => PortfolioValue.ToString("C", _culture);

        // Propriedades calculadas
        public int ActiveSavingsTargets => SavingsTargets.Count(s => !s.IsCompleted);
        public bool HasExpenses => Expenses.Any();
        public bool HasSavingsTargets => SavingsTargets.Any();
        public bool IsPositiveBalance => BalanceThisMonth >= 0;

        // Estatísticas do mês
        public string CurrentMonthName => DateTime.Now.ToString("MMMM yyyy", _culture);
        public decimal SavingsRate => TotalIncomeThisMonth > 0 ? (BalanceThisMonth / TotalIncomeThisMonth) * 100 : 0;
        public string FormattedSavingsRate => SavingsRate.ToString("F1", _culture) + "%";

        #endregion

        #region Comandos

        public ICommand LoadDataCommand { get; }
        public ICommand AddExpenseCommand { get; }
        public ICommand RefreshDataCommand { get; }

        #endregion

        #region Métodos Principais

        public async Task LoadData()
        {
            if (CurrentUser == null) return;

            try
            {
                IsLoading = true;

                LoggingService.LogInfo($"[MainViewModel] Carregando dados para {CurrentUser.Username}...");

                // Carregar despesas
                var expenses = await _expenseService.GetExpensesByUserIdAsync(CurrentUser.Id);
                Expenses.Clear();
                foreach (var expense in expenses.OrderByDescending(e => e.Date))
                {
                    Expenses.Add(expense);
                }

                // Carregar objetivos de poupança
                var savingsTargets = await _savingsService.GetSavingsTargetsByUserIdAsync(CurrentUser.Id);
                SavingsTargets.Clear();
                foreach (var target in savingsTargets)
                {
                    SavingsTargets.Add(target);
                }

                // Calcular totais financeiros REAIS
                await CalculateFinancialTotals();

                // Notificar TODAS as propriedades calculadas
                OnPropertyChanged(nameof(ActiveSavingsTargets));
                OnPropertyChanged(nameof(HasExpenses));
                OnPropertyChanged(nameof(HasSavingsTargets));
                OnPropertyChanged(nameof(CurrentMonthName));

                LoggingService.LogInfo($"[MainViewModel] Dados carregados: {Expenses.Count} despesas, {SavingsTargets.Count} objetivos");
                LoggingService.LogInfo($"[MainViewModel] VALORES REAIS - Receitas mês: {FormattedTotalIncomeThisMonth}, Despesas mês: {FormattedTotalExpensesThisMonth}, Saldo: {FormattedBalanceThisMonth}");
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao carregar dados no MainViewModel", ex);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task CalculateFinancialTotals()
        {
            if (CurrentUser == null) return;

            try
            {
                var now = DateTime.Now;
                var startOfMonth = new DateTime(now.Year, now.Month, 1);
                var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

                LoggingService.LogInfo($"[MainViewModel] Calculando totais para período: {startOfMonth:dd/MM/yyyy} a {endOfMonth:dd/MM/yyyy}");

                // Obter dados REAIS diretamente da base de dados
                var thisMonthIncome = await _incomeService.GetTotalIncomeAsync(CurrentUser.Id, startOfMonth, endOfMonth);
                var thisMonthExpenses = await _expenseService.GetTotalExpensesAsync(CurrentUser.Id, startOfMonth, endOfMonth);
                var totalHistoricIncome = await _incomeService.GetTotalIncomeAsync(CurrentUser.Id, DateTime.MinValue, DateTime.MaxValue);
                var totalHistoricExpenses = await _expenseService.GetTotalExpensesAsync(CurrentUser.Id, DateTime.MinValue, DateTime.MaxValue);
                var portfolioVal = await _investmentService.GetPortfolioValueAsync(CurrentUser.Id);

                // Atualizar propriedades com valores REAIS
                TotalIncomeThisMonth = thisMonthIncome;
                TotalExpensesThisMonth = thisMonthExpenses;
                TotalIncome = totalHistoricIncome;
                TotalExpenses = totalHistoricExpenses;
                PortfolioValue = portfolioVal;

                // O saldo é calculado automaticamente no setter de UpdateBalance()

                LoggingService.LogInfo($"[MainViewModel] TOTAIS CALCULADOS:");
                LoggingService.LogInfo($"  - Receitas mês atual: {TotalIncomeThisMonth:C}");
                LoggingService.LogInfo($"  - Despesas mês atual: {TotalExpensesThisMonth:C}");
                LoggingService.LogInfo($"  - Saldo mês atual: {BalanceThisMonth:C}");
                LoggingService.LogInfo($"  - Portfólio: {PortfolioValue:C}");
                LoggingService.LogInfo($"  - Taxa poupança: {SavingsRate:F1}%");
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao calcular totais financeiros", ex);

                // Valores padrão em caso de erro - MAS SEM dados fictícios
                TotalExpenses = 0;
                TotalIncome = 0;
                TotalExpensesThisMonth = 0;
                TotalIncomeThisMonth = 0;
                PortfolioValue = 0;
            }
        }

        private void UpdateBalance()
        {
            // Calcular saldo do mês automaticamente
            BalanceThisMonth = TotalIncomeThisMonth - TotalExpensesThisMonth;
        }

        public async Task RefreshAllData()
        {
            LoggingService.LogInfo("[MainViewModel] Refresh completo dos dados iniciado...");
            await LoadData();
        }

        private void AddExpense()
        {
            // Implementado no code-behind da MainWindow
        }

        #endregion
    }
}