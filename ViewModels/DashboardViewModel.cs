using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using FinanceManager.Models;
using FinanceManager.Services;

namespace FinanceManager.ViewModels
{
    public class DashboardViewModel : INotifyPropertyChanged
    {
        private readonly ExpenseService _expenseService;
        private readonly IncomeService _incomeService;
        private readonly User _currentUser;

        private decimal _totalIncome = 0;
        private decimal _totalExpenses = 0;
        private decimal _balance = 0;
        private decimal _savingsRate = 0;
        private bool _hasData = false;
        private string _lastUpdateText = string.Empty;

        public DashboardViewModel(User user)
        {
            _currentUser = user;
            _expenseService = new ExpenseService();
            _incomeService = new IncomeService();

            UpdateLastUpdateText();
        }

        #region Propriedades

        public decimal TotalIncome
        {
            get => _totalIncome;
            set
            {
                _totalIncome = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalIncomeFormatted));
            }
        }

        public decimal TotalExpenses
        {
            get => _totalExpenses;
            set
            {
                _totalExpenses = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalExpensesFormatted));
            }
        }

        public decimal Balance
        {
            get => _balance;
            set
            {
                _balance = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BalanceFormatted));
                OnPropertyChanged(nameof(IsPositiveBalance));
            }
        }

        public decimal SavingsRate
        {
            get => _savingsRate;
            set
            {
                _savingsRate = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SavingsRateFormatted));
            }
        }

        public bool HasData
        {
            get => _hasData;
            set
            {
                _hasData = value;
                OnPropertyChanged();
            }
        }

        public string LastUpdateText
        {
            get => _lastUpdateText;
            set
            {
                _lastUpdateText = value;
                OnPropertyChanged();
            }
        }

        // Propriedades formatadas
        public string TotalIncomeFormatted => TotalIncome.ToString("C", new System.Globalization.CultureInfo("pt-PT"));
        public string TotalExpensesFormatted => TotalExpenses.ToString("C", new System.Globalization.CultureInfo("pt-PT"));
        public string BalanceFormatted => Balance.ToString("C", new System.Globalization.CultureInfo("pt-PT"));
        public string SavingsRateFormatted => SavingsRate.ToString("F1", new System.Globalization.CultureInfo("pt-PT")) + "%";
        public bool IsPositiveBalance => Balance >= 0;

        #endregion

        #region Métodos

        public async Task RefreshDataAsync()
        {
            try
            {
                // Calcular período atual (mês atual)
                var now = DateTime.Now;
                var startOfMonth = new DateTime(now.Year, now.Month, 1);
                var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

                // Obter dados reais do mês atual
                TotalIncome = await _incomeService.GetTotalIncomeAsync(_currentUser.Id, startOfMonth, endOfMonth);
                TotalExpenses = await _expenseService.GetTotalExpensesAsync(_currentUser.Id, startOfMonth, endOfMonth);
                Balance = TotalIncome - TotalExpenses;
                SavingsRate = TotalIncome > 0 ? (Balance / TotalIncome) * 100 : 0;

                // Verificar se há dados
                HasData = TotalIncome > 0 || TotalExpenses > 0;

                // Atualizar timestamp
                UpdateLastUpdateText();

                LoggingService.LogInfo($"Dashboard atualizado - Receitas: {TotalIncomeFormatted}, Despesas: {TotalExpensesFormatted}, Saldo: {BalanceFormatted}");
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao atualizar dashboard", ex);

                // Valores padrão em caso de erro
                TotalIncome = 0;
                TotalExpenses = 0;
                Balance = 0;
                SavingsRate = 0;
                HasData = false;
            }
        }

        private void UpdateLastUpdateText()
        {
            LastUpdateText = $"Atualizado: {DateTime.Now.ToString("dd/MM/yyyy HH:mm", new System.Globalization.CultureInfo("pt-PT"))}";
        }

        public async Task<Dictionary<string, decimal>> GetExpensesByCategoryAsync()
        {
            try
            {
                var now = DateTime.Now;
                var startOfMonth = new DateTime(now.Year, now.Month, 1);
                var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

                return await _expenseService.GetExpensesByCategoryAsync(_currentUser.Id, startOfMonth, endOfMonth);
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao obter despesas por categoria", ex);
                return new Dictionary<string, decimal>();
            }
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
