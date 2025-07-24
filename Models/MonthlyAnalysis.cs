using System;
using System.Collections.Generic;
using System.Globalization;

namespace FinanceManager.Models
{
    public class MonthlyAnalysis
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal TotalIncome { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal Balance { get; set; }
        public Dictionary<string, decimal> IncomesByCategory { get; set; } = new Dictionary<string, decimal>();
        public Dictionary<string, decimal> ExpensesByCategory { get; set; } = new Dictionary<string, decimal>();
        public decimal SavingsRate { get; set; }

        // Propriedades formatadas para apresentação
        public string MonthName => new DateTime(Year, Month, 1).ToString("MMMM yyyy", new CultureInfo("pt-PT"));
        public string FormattedIncome => TotalIncome.ToString("C", new CultureInfo("pt-PT"));
        public string FormattedExpenses => TotalExpenses.ToString("C", new CultureInfo("pt-PT"));
        public string FormattedBalance => Balance.ToString("C", new CultureInfo("pt-PT"));
        public string FormattedSavingsRate => SavingsRate.ToString("F1") + "%";
        public bool IsPositiveBalance => Balance >= 0;
    }
}