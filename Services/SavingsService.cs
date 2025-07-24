using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using FinanceManager.Data;
using FinanceManager.Models;

namespace FinanceManager.Services
{
    public class SavingsService
    {
        /// <summary>
        /// Adiciona um novo objetivo de poupança
        /// </summary>
        public async Task<bool> AddSavingsTargetAsync(SavingsTarget target)
        {
            try
            {
                using var context = new FinanceContext();
                target.CreatedDate = DateTime.Now;
                context.SavingsTargets.Add(target);
                await context.SaveChangesAsync();

                LoggingService.LogInfo($"Objetivo de poupança criado: {target.Name} - {target.FormattedTargetAmount}");
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao adicionar objetivo de poupança", ex);
                return false;
            }
        }

        /// <summary>
        /// Obtém todos os objetivos de poupança de um utilizador
        /// </summary>
        public async Task<List<SavingsTarget>> GetSavingsTargetsByUserIdAsync(int userId)
        {
            try
            {
                using var context = new FinanceContext();
                return await context.SavingsTargets
                    .Include(s => s.UpdateHistory)
                    .Where(s => s.UserId == userId)
                    .OrderByDescending(s => s.CreatedDate)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao obter objetivos de poupança", ex);
                return new List<SavingsTarget>();
            }
        }

        /// <summary>
        /// Atualiza o valor atual de um objetivo (adiciona montante)
        /// </summary>
        public async Task<bool> UpdateSavingsAmountAsync(int targetId, decimal amountToAdd, string? notes = null)
        {
            try
            {
                using var context = new FinanceContext();
                var target = await context.SavingsTargets.FindAsync(targetId);

                if (target == null) return false;

                var previousAmount = target.CurrentAmount;
                target.CurrentAmount += amountToAdd;
                target.UpdatedDate = DateTime.Now;

                // Verificar se atingiu o objetivo
                if (target.CurrentAmount >= target.TargetAmount && !target.IsCompleted)
                {
                    target.IsCompleted = true;
                    target.CompletedDate = DateTime.Now;
                }

                // Criar histórico da atualização
                var historyEntry = new SavingsUpdateHistory
                {
                    SavingsTargetId = targetId,
                    AmountAdded = amountToAdd,
                    PreviousAmount = previousAmount,
                    NewAmount = target.CurrentAmount,
                    UpdateDate = DateTime.Now,
                    Notes = notes
                };

                context.SavingsUpdateHistories.Add(historyEntry);
                context.SavingsTargets.Update(target);
                await context.SaveChangesAsync();

                LoggingService.LogInfo($"Poupança atualizada: {target.Name} - Adicionado {amountToAdd.ToString("C", new System.Globalization.CultureInfo("pt-PT"))}");

                if (target.IsCompleted)
                {
                    LoggingService.LogInfo($"🎉 Objetivo atingido: {target.Name}!");
                }

                return true;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao atualizar valor de poupança", ex);
                return false;
            }
        }

        /// <summary>
        /// Obtém o histórico de atualizações de um objetivo
        /// </summary>
        public async Task<List<SavingsUpdateHistory>> GetUpdateHistoryAsync(int targetId)
        {
            try
            {
                using var context = new FinanceContext();
                return await context.SavingsUpdateHistories
                    .Where(h => h.SavingsTargetId == targetId)
                    .OrderByDescending(h => h.UpdateDate)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao obter histórico de atualizações", ex);
                return new List<SavingsUpdateHistory>();
            }
        }

        /// <summary>
        /// Atualiza um objetivo de poupança
        /// </summary>
        public async Task<bool> UpdateSavingsTargetAsync(SavingsTarget target)
        {
            try
            {
                using var context = new FinanceContext();
                target.UpdatedDate = DateTime.Now;

                // Verificar se atingiu o objetivo
                if (target.CurrentAmount >= target.TargetAmount && !target.IsCompleted)
                {
                    target.IsCompleted = true;
                    target.CompletedDate = DateTime.Now;
                }
                else if (target.CurrentAmount < target.TargetAmount && target.IsCompleted)
                {
                    target.IsCompleted = false;
                    target.CompletedDate = null;
                }

                context.SavingsTargets.Update(target);
                await context.SaveChangesAsync();

                LoggingService.LogInfo($"Objetivo de poupança atualizado: {target.Name}");
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao atualizar objetivo de poupança", ex);
                return false;
            }
        }

        /// <summary>
        /// Remove um objetivo de poupança
        /// </summary>
        public async Task<bool> DeleteSavingsTargetAsync(int targetId)
        {
            try
            {
                using var context = new FinanceContext();
                var target = await context.SavingsTargets
                    .Include(s => s.UpdateHistory)
                    .FirstOrDefaultAsync(s => s.Id == targetId);

                if (target != null)
                {
                    // Remover histórico primeiro (devido à foreign key)
                    context.SavingsUpdateHistories.RemoveRange(target.UpdateHistory);
                    context.SavingsTargets.Remove(target);
                    await context.SaveChangesAsync();

                    LoggingService.LogInfo($"Objetivo de poupança removido: {target.Name}");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao remover objetivo de poupança", ex);
                return false;
            }
        }

        /// <summary>
        /// Obtém estatísticas de poupança de um utilizador
        /// </summary>
        public async Task<SavingsStatistics> GetSavingsStatisticsAsync(int userId)
        {
            try
            {
                using var context = new FinanceContext();
                var targets = await context.SavingsTargets
                    .Where(s => s.UserId == userId)
                    .ToListAsync();

                var completedTargets = targets.Where(t => t.IsCompleted).ToList();
                var activeTargets = targets.Where(t => !t.IsCompleted).ToList();

                var totalTargetAmount = targets.Sum(t => t.TargetAmount);
                var totalCurrentAmount = targets.Sum(t => t.CurrentAmount);
                var totalRemainingAmount = targets.Sum(t => t.RemainingAmount);

                var overallProgress = totalTargetAmount > 0 ? (totalCurrentAmount / totalTargetAmount) * 100 : 0;

                return new SavingsStatistics
                {
                    TotalTargets = targets.Count,
                    CompletedTargets = completedTargets.Count,
                    ActiveTargets = activeTargets.Count,
                    TotalTargetAmount = totalTargetAmount,
                    TotalCurrentAmount = totalCurrentAmount,
                    TotalRemainingAmount = totalRemainingAmount,
                    OverallProgress = overallProgress,
                    AverageProgress = targets.Any() ? targets.Average(t => t.ProgressPercentage) : 0
                };
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao obter estatísticas de poupança", ex);
                return new SavingsStatistics();
            }
        }

        /// <summary>
        /// Marca um objetivo como concluído manualmente
        /// </summary>
        public async Task<bool> CompleteSavingsTargetAsync(int targetId)
        {
            try
            {
                using var context = new FinanceContext();
                var target = await context.SavingsTargets.FindAsync(targetId);

                if (target != null && !target.IsCompleted)
                {
                    target.IsCompleted = true;
                    target.CompletedDate = DateTime.Now;
                    target.UpdatedDate = DateTime.Now;

                    await context.SaveChangesAsync();

                    LoggingService.LogInfo($"Objetivo marcado como concluído: {target.Name}");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao completar objetivo", ex);
                return false;
            }
        }
    }

    public class SavingsStatistics
    {
        public int TotalTargets { get; set; }
        public int CompletedTargets { get; set; }
        public int ActiveTargets { get; set; }
        public decimal TotalTargetAmount { get; set; }
        public decimal TotalCurrentAmount { get; set; }
        public decimal TotalRemainingAmount { get; set; }
        public decimal OverallProgress { get; set; }
        public decimal AverageProgress { get; set; }

        // Formatação em Euros
        public string FormattedTotalTargetAmount => TotalTargetAmount.ToString("C", new System.Globalization.CultureInfo("pt-PT"));
        public string FormattedTotalCurrentAmount => TotalCurrentAmount.ToString("C", new System.Globalization.CultureInfo("pt-PT"));
        public string FormattedTotalRemainingAmount => TotalRemainingAmount.ToString("C", new System.Globalization.CultureInfo("pt-PT"));
        public string FormattedOverallProgress => OverallProgress.ToString("F1") + "%";
        public string FormattedAverageProgress => AverageProgress.ToString("F1") + "%";

        public decimal CompletionRate => TotalTargets > 0 ? (decimal)CompletedTargets / TotalTargets * 100 : 0;
        public string FormattedCompletionRate => CompletionRate.ToString("F1") + "%";
    }
}