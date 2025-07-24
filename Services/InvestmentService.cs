using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using FinanceManager.Data;
using FinanceManager.Models;

namespace FinanceManager.Services
{
    public class InvestmentService
    {
        /// <summary>
        /// Adiciona um novo investimento
        /// </summary>
        public async Task<bool> AddInvestmentAsync(Investment investment)
        {
            try
            {
                using var context = new FinanceContext();

                // Validações
                if (investment == null) return false;
                if (string.IsNullOrWhiteSpace(investment.Name)) return false;
                if (investment.Amount <= 0) return false;
                if (investment.Quantity <= 0) return false;
                if (investment.PurchasePrice <= 0) return false;
                if (investment.UserId <= 0) return false;

                investment.CreatedDate = DateTime.Now;
                context.Investments.Add(investment);
                await context.SaveChangesAsync();

                LoggingService.LogInfo($"Investimento adicionado: {investment.Name} - {investment.Amount:C}");
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao adicionar investimento", ex);
                return false;
            }
        }

        /// <summary>
        /// Obtém todos os investimentos de um utilizador
        /// </summary>
        public async Task<List<Investment>> GetInvestmentsByUserIdAsync(int userId)
        {
            try
            {
                using var context = new FinanceContext();

                return await context.Investments
                    .Where(i => i.UserId == userId && i.IsActive)
                    .OrderByDescending(i => i.PurchaseDate)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao obter investimentos", ex);
                return new List<Investment>();
            }
        }

        /// <summary>
        /// Atualiza um investimento existente
        /// </summary>
        public async Task<bool> UpdateInvestmentAsync(Investment investment)
        {
            try
            {
                using var context = new FinanceContext();

                var existingInvestment = await context.Investments
                    .FirstOrDefaultAsync(i => i.Id == investment.Id);

                if (existingInvestment == null) return false;

                // Atualizar propriedades
                existingInvestment.Name = investment.Name;
                existingInvestment.Type = investment.Type;
                existingInvestment.Quantity = investment.Quantity;
                existingInvestment.PurchasePrice = investment.PurchasePrice;
                existingInvestment.CurrentPrice = investment.CurrentPrice;
                existingInvestment.Amount = investment.Amount;
                existingInvestment.Description = investment.Description;
                existingInvestment.Broker = investment.Broker;
                existingInvestment.UpdatedDate = DateTime.Now;

                await context.SaveChangesAsync();

                LoggingService.LogInfo($"Investimento atualizado: {investment.Name}");
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao atualizar investimento", ex);
                return false;
            }
        }

        /// <summary>
        /// Atualiza o preço atual de um investimento
        /// </summary>
        public async Task<bool> UpdateCurrentPriceAsync(int investmentId, decimal currentPrice)
        {
            try
            {
                using var context = new FinanceContext();

                var investment = await context.Investments
                    .FirstOrDefaultAsync(i => i.Id == investmentId);

                if (investment == null) return false;

                investment.CurrentPrice = currentPrice;
                investment.UpdatedDate = DateTime.Now;
                await context.SaveChangesAsync();

                LoggingService.LogInfo($"Preço atualizado para {investment.Name}: {currentPrice:C}");
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao atualizar preço", ex);
                return false;
            }
        }

        /// <summary>
        /// Elimina um investimento (marca como inativo)
        /// </summary>
        public async Task<bool> DeleteInvestmentAsync(int investmentId)
        {
            try
            {
                using var context = new FinanceContext();

                var investment = await context.Investments
                    .FirstOrDefaultAsync(i => i.Id == investmentId);

                if (investment == null) return false;

                // Marcar como inativo em vez de eliminar
                investment.IsActive = false;
                investment.UpdatedDate = DateTime.Now;
                await context.SaveChangesAsync();

                LoggingService.LogInfo($"Investimento eliminado: {investment.Name}");
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao eliminar investimento", ex);
                return false;
            }
        }

        /// <summary>
        /// Obtém estatísticas dos investimentos de um utilizador
        /// </summary>
        public async Task<InvestmentStatistics> GetInvestmentStatisticsAsync(int userId)
        {
            try
            {
                using var context = new FinanceContext();

                var investments = await context.Investments
                    .Where(i => i.UserId == userId && i.IsActive)
                    .ToListAsync();

                if (!investments.Any())
                {
                    return new InvestmentStatistics();
                }

                var totalCost = investments.Sum(i => i.Amount);
                var totalValue = investments.Sum(i => i.CurrentValue);
                var totalProfitLoss = totalValue - totalCost;
                var totalProfitLossPercentage = totalCost > 0 ? (totalProfitLoss / totalCost) * 100 : 0;

                return new InvestmentStatistics
                {
                    TotalInvestments = investments.Count,
                    TotalCost = totalCost,
                    TotalValue = totalValue,
                    TotalProfitLoss = totalProfitLoss,
                    TotalProfitLossPercentage = totalProfitLossPercentage,
                    ActiveInvestments = investments.Count(i => i.IsActive),
                    BestPerformer = investments.OrderByDescending(i => i.ProfitLossPercentage).FirstOrDefault()?.Name ?? "N/A",
                    WorstPerformer = investments.OrderBy(i => i.ProfitLossPercentage).FirstOrDefault()?.Name ?? "N/A"
                };
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao calcular estatísticas de investimentos", ex);
                return new InvestmentStatistics();
            }
        }

        /// <summary>
        /// Obtém investimentos por tipo
        /// </summary>
        public async Task<Dictionary<string, List<Investment>>> GetInvestmentsByTypeAsync(int userId)
        {
            try
            {
                using var context = new FinanceContext();

                var investments = await context.Investments
                    .Where(i => i.UserId == userId && i.IsActive)
                    .ToListAsync();

                return investments.GroupBy(i => i.Type)
                    .ToDictionary(g => g.Key, g => g.ToList());
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao obter investimentos por tipo", ex);
                return new Dictionary<string, List<Investment>>();
            }
        }

        /// <summary>
        /// Obtém investimentos com maior ganho/perda
        /// </summary>
        public async Task<List<Investment>> GetTopPerformingInvestmentsAsync(int userId, int count = 5)
        {
            try
            {
                using var context = new FinanceContext();

                return await context.Investments
                    .Where(i => i.UserId == userId && i.IsActive && i.CurrentPrice.HasValue)
                    .OrderByDescending(i => i.ProfitLossPercentage)
                    .Take(count)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao obter melhores investimentos", ex);
                return new List<Investment>();
            }
        }

        /// <summary>
        /// Obtém valor total do portfólio de um utilizador
        /// </summary>
        public async Task<decimal> GetPortfolioValueAsync(int userId)
        {
            try
            {
                using var context = new FinanceContext();

                var investments = await context.Investments
                    .Where(i => i.UserId == userId && i.IsActive)
                    .ToListAsync();

                return investments.Sum(i => i.CurrentValue);
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao calcular valor do portfólio", ex);
                return 0;
            }
        }

        /// <summary>
        /// Obtém diversificação do portfólio por tipo
        /// </summary>
        public async Task<Dictionary<string, decimal>> GetPortfolioDiversificationAsync(int userId)
        {
            try
            {
                using var context = new FinanceContext();

                var investments = await context.Investments
                    .Where(i => i.UserId == userId && i.IsActive)
                    .ToListAsync();

                if (!investments.Any()) return new Dictionary<string, decimal>();

                var totalValue = investments.Sum(i => i.CurrentValue);

                return investments
                    .GroupBy(i => i.Type)
                    .ToDictionary(
                        g => g.Key,
                        g => totalValue > 0 ? (g.Sum(i => i.CurrentValue) / totalValue) * 100 : 0
                    );
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao calcular diversificação", ex);
                return new Dictionary<string, decimal>();
            }
        }

        /// <summary>
        /// Obtém análise de performance do portfólio
        /// </summary>
        public async Task<PortfolioPerformance> GetPortfolioPerformanceAsync(int userId)
        {
            try
            {
                using var context = new FinanceContext();

                var investments = await context.Investments
                    .Where(i => i.UserId == userId && i.IsActive)
                    .ToListAsync();

                if (!investments.Any())
                {
                    return new PortfolioPerformance();
                }

                var totalCost = investments.Sum(i => i.Amount);
                var totalValue = investments.Sum(i => i.CurrentValue);
                var totalReturn = totalValue - totalCost;
                var totalReturnPercentage = totalCost > 0 ? (totalReturn / totalCost) * 100 : 0;

                var profitableInvestments = investments.Count(i => i.ProfitLoss > 0);
                var lossMakingInvestments = investments.Count(i => i.ProfitLoss < 0);

                return new PortfolioPerformance
                {
                    TotalInvested = totalCost,
                    CurrentValue = totalValue,
                    TotalReturn = totalReturn,
                    TotalReturnPercentage = totalReturnPercentage,
                    ProfitableInvestments = profitableInvestments,
                    LossMakingInvestments = lossMakingInvestments,
                    TotalInvestments = investments.Count,
                    AverageReturn = investments.Any() ? investments.Average(i => i.ProfitLossPercentage) : 0
                };
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao analisar performance do portfólio", ex);
                return new PortfolioPerformance();
            }
        }

        public async Task<Investment?> GetInvestmentByIdAsync(int investmentId)
        {
            try
            {
                using var context = new FinanceContext();

                return await context.Investments
                    .FirstOrDefaultAsync(i => i.Id == investmentId);
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Erro ao obter investimento {investmentId}", ex);
                return null;
            }
        }

        /// <summary>
        /// Classe para estatísticas de investimentos
        /// </summary>
        public class InvestmentStatistics
    {
        public int TotalInvestments { get; set; }
        public decimal TotalCost { get; set; }
        public decimal TotalValue { get; set; }
        public decimal TotalProfitLoss { get; set; }
        public decimal TotalProfitLossPercentage { get; set; }
        public int ActiveInvestments { get; set; }
        public string BestPerformer { get; set; } = "";
        public string WorstPerformer { get; set; } = "";

        // Propriedades formatadas
        public string FormattedTotalCost => TotalCost.ToString("C", new System.Globalization.CultureInfo("pt-PT"));
        public string FormattedTotalValue => TotalValue.ToString("C", new System.Globalization.CultureInfo("pt-PT"));
        public string FormattedTotalProfitLoss => TotalProfitLoss.ToString("C", new System.Globalization.CultureInfo("pt-PT"));
        public string FormattedTotalProfitLossPercentage => TotalProfitLossPercentage.ToString("F2") + "%";
    }

    /// <summary>
    /// Classe para análise de performance do portfólio
    /// </summary>
    public class PortfolioPerformance
    {
        public decimal TotalInvested { get; set; }
        public decimal CurrentValue { get; set; }
        public decimal TotalReturn { get; set; }
        public decimal TotalReturnPercentage { get; set; }
        public int ProfitableInvestments { get; set; }
        public int LossMakingInvestments { get; set; }
        public int TotalInvestments { get; set; }
        public decimal AverageReturn { get; set; }

        // Propriedades formatadas
        public string FormattedTotalInvested => TotalInvested.ToString("C", new System.Globalization.CultureInfo("pt-PT"));
        public string FormattedCurrentValue => CurrentValue.ToString("C", new System.Globalization.CultureInfo("pt-PT"));
        public string FormattedTotalReturn => TotalReturn.ToString("C", new System.Globalization.CultureInfo("pt-PT"));
        public string FormattedTotalReturnPercentage => TotalReturnPercentage.ToString("F2") + "%";
        public string FormattedAverageReturn => AverageReturn.ToString("F2") + "%";

        // Indicadores visuais
        public string ReturnIcon => TotalReturn >= 0 ? "📈" : "📉";
        public string ReturnColor => TotalReturn >= 0 ? "Green" : "Red";
        public decimal SuccessRate => TotalInvestments > 0 ? (decimal)ProfitableInvestments / TotalInvestments * 100 : 0;
        public string FormattedSuccessRate => SuccessRate.ToString("F1") + "%";
    }
}
}

