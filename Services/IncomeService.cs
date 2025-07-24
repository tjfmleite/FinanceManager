using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using FinanceManager.Data;
using FinanceManager.Models;

namespace FinanceManager.Services
{
    public class IncomeService
    {
        /// <summary>
        /// Adiciona uma nova receita
        /// </summary>
        public async Task<bool> AddIncomeAsync(Income income)
        {
            try
            {
                using var context = new FinanceContext();
                context.Incomes.Add(income);
                await context.SaveChangesAsync();

                LoggingService.LogInfo($"Receita adicionada: {income.Description} - {income.FormattedAmount}");
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao adicionar receita", ex);
                return false;
            }
        }

        /// <summary>
        /// Obtém todas as receitas de um utilizador
        /// </summary>
        public async Task<List<Income>> GetIncomesByUserIdAsync(int userId)
        {
            try
            {
                using var context = new FinanceContext();
                return await context.Incomes
                    .Where(i => i.UserId == userId)
                    .OrderByDescending(i => i.Date)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao obter receitas", ex);
                return new List<Income>();
            }
        }

        /// <summary>
        /// Obtém receitas por período
        /// </summary>
        public async Task<List<Income>> GetIncomesByPeriodAsync(int userId, DateTime startDate, DateTime endDate)
        {
            try
            {
                using var context = new FinanceContext();
                return await context.Incomes
                    .Where(i => i.UserId == userId && i.Date >= startDate && i.Date <= endDate)
                    .OrderByDescending(i => i.Date)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao obter receitas por período", ex);
                return new List<Income>();
            }
        }

        /// <summary>
        /// Obtém total de receitas por categoria
        /// </summary>
        public async Task<Dictionary<string, decimal>> GetIncomesByCategoryAsync(int userId, DateTime startDate, DateTime endDate)
        {
            try
            {
                using var context = new FinanceContext();

                // **CORREÇÃO SQLITE**: Buscar dados primeiro, depois agrupar em memória
                var incomes = await context.Incomes
                    .Where(i => i.UserId == userId && i.Date >= startDate && i.Date <= endDate)
                    .ToListAsync();

                return incomes
                    .GroupBy(i => i.Category)
                    .ToDictionary(g => g.Key, g => g.Sum(i => i.Amount));
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao obter receitas por categoria", ex);
                return new Dictionary<string, decimal>();
            }
        }

        /// <summary>
        /// Calcula total de receitas por período - CORRIGIDO PARA SQLITE
        /// </summary>
        public async Task<decimal> GetTotalIncomeAsync(int userId, DateTime startDate, DateTime endDate)
        {
            try
            {
                using var context = new FinanceContext();

                LoggingService.LogInfo($"Calculando total de receitas para utilizador {userId}, período {startDate:dd/MM/yyyy} a {endDate:dd/MM/yyyy}");

                // **CORREÇÃO SQLITE**: Buscar dados primeiro, depois somar em memória
                var incomes = await context.Incomes
                    .Where(i => i.UserId == userId && i.Date >= startDate && i.Date <= endDate)
                    .ToListAsync();

                var total = incomes.Sum(i => i.Amount);

                LoggingService.LogInfo($"Total de receitas calculado: {total} (de {incomes.Count} registos)");

                return total;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao calcular total de receitas", ex);
                return 0;
            }
        }

        /// <summary>
        /// Calcula saldo (Receitas - Despesas) por período
        /// </summary>
        public async Task<decimal> GetBalanceAsync(int userId, DateTime startDate, DateTime endDate)
        {
            try
            {
                using var context = new FinanceContext();

                // **CORREÇÃO SQLITE**: Buscar dados primeiro, depois calcular em memória
                var incomes = await context.Incomes
                    .Where(i => i.UserId == userId && i.Date >= startDate && i.Date <= endDate)
                    .ToListAsync();

                var expenses = await context.Expenses
                    .Where(e => e.UserId == userId && e.Date >= startDate && e.Date <= endDate)
                    .ToListAsync();

                var totalIncome = incomes.Sum(i => i.Amount);
                var totalExpenses = expenses.Sum(e => e.Amount);

                return totalIncome - totalExpenses;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao calcular saldo", ex);
                return 0;
            }
        }

        /// <summary>
        /// Obtém dados para análise mensal
        /// </summary>
        public async Task<MonthlyAnalysis> GetMonthlyAnalysisAsync(int userId, int year, int month)
        {
            try
            {
                var startDate = new DateTime(year, month, 1);
                var endDate = startDate.AddMonths(1).AddDays(-1);

                var totalIncome = await GetTotalIncomeAsync(userId, startDate, endDate);
                var expenseService = new ExpenseService();
                var totalExpenses = await expenseService.GetTotalExpensesAsync(userId, startDate, endDate);
                var balance = totalIncome - totalExpenses;

                var incomesByCategory = await GetIncomesByCategoryAsync(userId, startDate, endDate);
                var expensesByCategory = await expenseService.GetExpensesByCategoryAsync(userId, startDate, endDate);

                return new MonthlyAnalysis
                {
                    Year = year,
                    Month = month,
                    TotalIncome = totalIncome,
                    TotalExpenses = totalExpenses,
                    Balance = balance,
                    IncomesByCategory = incomesByCategory,
                    ExpensesByCategory = expensesByCategory,
                    SavingsRate = totalIncome > 0 ? (balance / totalIncome) * 100 : 0
                };
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao obter análise mensal", ex);
                return new MonthlyAnalysis();
            }
        }

        /// <summary>
        /// Atualiza uma receita
        /// </summary>
        public async Task<bool> UpdateIncomeAsync(Income income)
        {
            try
            {
                using var context = new FinanceContext();
                income.UpdatedDate = DateTime.Now;
                context.Incomes.Update(income);
                await context.SaveChangesAsync();

                LoggingService.LogInfo($"Receita atualizada: {income.Description}");
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao atualizar receita", ex);
                return false;
            }
        }

        /// <summary>
        /// Remove uma receita
        /// </summary>
        public async Task<bool> DeleteIncomeAsync(int incomeId)
        {
            try
            {
                using var context = new FinanceContext();
                var income = await context.Incomes.FindAsync(incomeId);
                if (income != null)
                {
                    context.Incomes.Remove(income);
                    await context.SaveChangesAsync();

                    LoggingService.LogInfo($"Receita removida: {income.Description}");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao remover receita", ex);
                return false;
            }
        }
    }
}
