using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using FinanceManager.Data;
using FinanceManager.Models;

namespace FinanceManager.Services
{
    public class ExpenseService
    {
        /// <summary>
        /// Adiciona uma nova despesa
        /// </summary>
        public async Task<bool> AddExpenseAsync(Expense expense)
        {
            try
            {
                using var context = new FinanceContext();
                expense.CreatedDate = DateTime.Now;
                context.Expenses.Add(expense);
                await context.SaveChangesAsync();

                LoggingService.LogInfo($"Despesa adicionada: {expense.Description} - {expense.FormattedAmount}");
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao adicionar despesa", ex);
                return false;
            }
        }

        /// <summary>
        /// Obtém todas as despesas de um utilizador
        /// </summary>
        public async Task<List<Expense>> GetExpensesByUserIdAsync(int userId)
        {
            try
            {
                using var context = new FinanceContext();
                return await context.Expenses
                    .Where(e => e.UserId == userId)
                    .OrderByDescending(e => e.Date)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao obter despesas", ex);
                return new List<Expense>();
            }
        }

        /// <summary>
        /// Obtém despesas por período
        /// </summary>
        public async Task<List<Expense>> GetExpensesByPeriodAsync(int userId, DateTime startDate, DateTime endDate)
        {
            try
            {
                using var context = new FinanceContext();
                return await context.Expenses
                    .Where(e => e.UserId == userId && e.Date >= startDate && e.Date <= endDate)
                    .OrderByDescending(e => e.Date)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao obter despesas por período", ex);
                return new List<Expense>();
            }
        }

        /// <summary>
        /// Obtém total de despesas por categoria
        /// </summary>
        public async Task<Dictionary<string, decimal>> GetExpensesByCategoryAsync(int userId, DateTime startDate, DateTime endDate)
        {
            try
            {
                using var context = new FinanceContext();

                // **CORREÇÃO SQLITE**: Buscar dados primeiro, depois agrupar em memória
                var expenses = await context.Expenses
                    .Where(e => e.UserId == userId && e.Date >= startDate && e.Date <= endDate)
                    .ToListAsync();

                return expenses
                    .GroupBy(e => e.Category)
                    .ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao obter despesas por categoria", ex);
                return new Dictionary<string, decimal>();
            }
        }

        /// <summary>
        /// Calcula total de despesas por período
        /// </summary>
        public async Task<decimal> GetTotalExpensesAsync(int userId, DateTime startDate, DateTime endDate)
        {
            try
            {
                using var context = new FinanceContext();

                LoggingService.LogInfo($"Calculando total de despesas para utilizador {userId}, período {startDate:dd/MM/yyyy} a {endDate:dd/MM/yyyy}");

                // **CORREÇÃO SQLITE**: Buscar dados primeiro, depois somar em memória
                var expenses = await context.Expenses
                    .Where(e => e.UserId == userId && e.Date >= startDate && e.Date <= endDate)
                    .ToListAsync();

                var total = expenses.Sum(e => e.Amount);

                LoggingService.LogInfo($"Total de despesas calculado: {total} (de {expenses.Count} registos)");

                return total;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao calcular total de despesas", ex);
                return 0;
            }
        }

        /// <summary>
        /// Obtém estatísticas mensais de despesas
        /// </summary>
        public async Task<MonthlyExpenseStats> GetMonthlyStatsAsync(int userId, int year, int month)
        {
            try
            {
                var startDate = new DateTime(year, month, 1);
                var endDate = startDate.AddMonths(1).AddDays(-1);

                using var context = new FinanceContext();

                var expenses = await context.Expenses
                    .Where(e => e.UserId == userId && e.Date >= startDate && e.Date <= endDate)
                    .ToListAsync();

                var totalAmount = expenses.Sum(e => e.Amount);
                var expensesByCategory = expenses
                    .GroupBy(e => e.Category)
                    .ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));

                var averagePerDay = expenses.Count > 0 ? totalAmount / DateTime.DaysInMonth(year, month) : 0;
                var transactionCount = expenses.Count;

                var topCategory = expensesByCategory.Any()
                    ? expensesByCategory.OrderByDescending(x => x.Value).First()
                    : new KeyValuePair<string, decimal>("Sem dados", 0);

                return new MonthlyExpenseStats
                {
                    Year = year,
                    Month = month,
                    TotalAmount = totalAmount,
                    TransactionCount = transactionCount,
                    AveragePerDay = averagePerDay,
                    ExpensesByCategory = expensesByCategory,
                    TopCategory = topCategory.Key,
                    TopCategoryAmount = topCategory.Value
                };
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao obter estatísticas mensais", ex);
                return new MonthlyExpenseStats();
            }
        }

        /// <summary>
        /// Obtém tendências de gastos (comparação mensal)
        /// </summary>
        public async Task<List<MonthlyTrend>> GetExpenseTrendsAsync(int userId, int monthsBack = 6)
        {
            try
            {
                var trends = new List<MonthlyTrend>();
                var currentDate = DateTime.Now;

                for (int i = monthsBack - 1; i >= 0; i--)
                {
                    var targetDate = currentDate.AddMonths(-i);
                    var startDate = new DateTime(targetDate.Year, targetDate.Month, 1);
                    var endDate = startDate.AddMonths(1).AddDays(-1);

                    var totalExpenses = await GetTotalExpensesAsync(userId, startDate, endDate);

                    trends.Add(new MonthlyTrend
                    {
                        Year = targetDate.Year,
                        Month = targetDate.Month,
                        Amount = totalExpenses,
                        MonthName = targetDate.ToString("MMM yyyy", new System.Globalization.CultureInfo("pt-PT"))
                    });
                }

                return trends;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao obter tendências de despesas", ex);
                return new List<MonthlyTrend>();
            }
        }

        /// <summary>
        /// Atualiza uma despesa
        /// </summary>
        public async Task<bool> UpdateExpenseAsync(Expense expense)
        {
            try
            {
                using var context = new FinanceContext();
                expense.UpdatedDate = DateTime.Now;
                context.Expenses.Update(expense);
                await context.SaveChangesAsync();

                LoggingService.LogInfo($"Despesa atualizada: {expense.Description}");
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao atualizar despesa", ex);
                return false;
            }
        }

        /// <summary>
        /// Remove uma despesa
        /// </summary>
        public async Task<bool> DeleteExpenseAsync(int expenseId)
        {
            try
            {
                using var context = new FinanceContext();
                var expense = await context.Expenses.FindAsync(expenseId);
                if (expense != null)
                {
                    context.Expenses.Remove(expense);
                    await context.SaveChangesAsync();

                    LoggingService.LogInfo($"Despesa removida: {expense.Description}");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao remover despesa", ex);
                return false;
            }
        }
    }

    public class MonthlyExpenseStats
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal TotalAmount { get; set; }
        public int TransactionCount { get; set; }
        public decimal AveragePerDay { get; set; }
        public Dictionary<string, decimal> ExpensesByCategory { get; set; } = new();
        public string TopCategory { get; set; } = string.Empty;
        public decimal TopCategoryAmount { get; set; }

        public string MonthName => new DateTime(Year, Month, 1).ToString("MMMM yyyy", new System.Globalization.CultureInfo("pt-PT"));
        public string FormattedTotalAmount => TotalAmount.ToString("C", new System.Globalization.CultureInfo("pt-PT"));
        public string FormattedAveragePerDay => AveragePerDay.ToString("C", new System.Globalization.CultureInfo("pt-PT"));
        public string FormattedTopCategoryAmount => TopCategoryAmount.ToString("C", new System.Globalization.CultureInfo("pt-PT"));
    }

    public class MonthlyTrend
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal Amount { get; set; }
        public string MonthName { get; set; } = string.Empty;
        public string FormattedAmount => Amount.ToString("C", new System.Globalization.CultureInfo("pt-PT"));
    }
}