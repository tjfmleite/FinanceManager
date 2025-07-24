using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Models
{
    public class SavingsTarget
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "O valor objetivo deve ser maior que zero")]
        public decimal TargetAmount { get; set; }

        [Required]
        [Range(0, double.MaxValue, ErrorMessage = "O valor atual não pode ser negativo")]
        public decimal CurrentAmount { get; set; }

        [Required]
        public DateTime StartDate { get; set; } = DateTime.Now;

        public DateTime? EndDate { get; set; }

        public bool IsCompleted { get; set; }

        public DateTime? CompletedDate { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        [Required]
        public int UserId { get; set; }

        public virtual User User { get; set; } = null!;

        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime? UpdatedDate { get; set; }

        // Relacionamento com histórico de atualizações
        public List<SavingsUpdateHistory> UpdateHistory { get; set; } = new();

        // Propriedades calculadas
        public decimal ProgressPercentage
        {
            get
            {
                if (TargetAmount > 0)
                {
                    return Math.Min((CurrentAmount / TargetAmount) * 100, 100);
                }
                return 0;
            }
        }

        public decimal RemainingAmount => Math.Max(TargetAmount - CurrentAmount, 0);
        public bool IsTargetReached => CurrentAmount >= TargetAmount;

        // Formatação em Euros (Portugal)
        public string FormattedTargetAmount => TargetAmount.ToString("C", new System.Globalization.CultureInfo("pt-PT"));
        public string FormattedCurrentAmount => CurrentAmount.ToString("C", new System.Globalization.CultureInfo("pt-PT"));
        public string FormattedRemainingAmount => RemainingAmount.ToString("C", new System.Globalization.CultureInfo("pt-PT"));
        public string FormattedStartDate => StartDate.ToString("dd/MM/yyyy", new System.Globalization.CultureInfo("pt-PT"));
        public string FormattedEndDate => EndDate?.ToString("dd/MM/yyyy", new System.Globalization.CultureInfo("pt-PT")) ?? "Sem limite";
        public string FormattedCompletedDate => CompletedDate?.ToString("dd/MM/yyyy", new System.Globalization.CultureInfo("pt-PT")) ?? "";
        public string FormattedProgressPercentage => ProgressPercentage.ToString("F1") + "%";

        // Status visual
        public string StatusText
        {
            get
            {
                if (IsCompleted && IsTargetReached)
                    return "✅ Objetivo Atingido!";
                else if (IsTargetReached)
                    return "🎯 Meta Alcançada!";
                else if (EndDate.HasValue && DateTime.Now > EndDate.Value)
                    return "⏰ Prazo Expirado";
                else if (EndDate.HasValue)
                {
                    var daysRemaining = (EndDate.Value - DateTime.Now).Days;
                    if (daysRemaining <= 7)
                        return $"⚠️ {daysRemaining} dias restantes";
                    else
                        return $"📅 {daysRemaining} dias restantes";
                }
                else
                    return "🔄 Em Progresso";
            }
        }

        public string ProgressBarColor
        {
            get
            {
                if (IsTargetReached) return "#4CAF50"; // Verde
                else if (ProgressPercentage >= 75) return "#FF9800"; // Laranja
                else if (ProgressPercentage >= 50) return "#2196F3"; // Azul
                else return "#9E9E9E"; // Cinza
            }
        }

        // String version para binding XAML
        public string ProgressBarColorString
        {
            get
            {
                if (IsTargetReached) return "Green";
                else if (ProgressPercentage >= 75) return "Orange";
                else if (ProgressPercentage >= 50) return "Blue";
                else return "Gray";
            }
        }

        // Método para recalcular progresso quando necessário
        public void RecalculateProgress()
        {
            // Força recálculo das propriedades derivadas
            var _ = ProgressPercentage;
            var __ = RemainingAmount;
            var ___ = IsTargetReached;
        }

        // Método para atualizar status de conclusão
        public void UpdateCompletionStatus()
        {
            if (CurrentAmount >= TargetAmount && !IsCompleted)
            {
                IsCompleted = true;
                CompletedDate = DateTime.Now;
            }
            else if (CurrentAmount < TargetAmount && IsCompleted)
            {
                IsCompleted = false;
                CompletedDate = null;
            }
        }

        // Método para adicionar valor ao objetivo
        public void AddAmount(decimal amount, string? notes = null)
        {
            var previousAmount = CurrentAmount;
            CurrentAmount += amount;

            // Verificar se atingiu o objetivo
            UpdateCompletionStatus();

            // Recalcular progresso
            RecalculateProgress();

            // Atualizar data de modificação
            UpdatedDate = DateTime.Now;
        }

        // Método para definir novo valor
        public void SetAmount(decimal newAmount, string? notes = null)
        {
            CurrentAmount = Math.Max(0, newAmount); // Não permitir valores negativos

            // Verificar se atingiu o objetivo
            UpdateCompletionStatus();

            // Recalcular progresso
            RecalculateProgress();

            // Atualizar data de modificação
            UpdatedDate = DateTime.Now;
        }

        // Propriedades de conveniência para relatórios
        public decimal PercentageToTarget => TargetAmount > 0 ? (CurrentAmount / TargetAmount) * 100 : 0;
        public decimal AmountToTarget => Math.Max(TargetAmount - CurrentAmount, 0);
        public bool IsOverTarget => CurrentAmount > TargetAmount;
        public decimal OverTargetAmount => IsOverTarget ? CurrentAmount - TargetAmount : 0;

        // Dias para atingir objetivo (se houver data limite)
        public int? DaysToDeadline => EndDate?.Subtract(DateTime.Now).Days;

        // Valor médio necessário por dia até à data limite
        public decimal? RequiredDailyAmount
        {
            get
            {
                if (!EndDate.HasValue || IsTargetReached) return null;

                var daysRemaining = DaysToDeadline;
                if (daysRemaining <= 0) return null;

                return RemainingAmount / daysRemaining.Value;
            }
        }

        public string FormattedRequiredDailyAmount => RequiredDailyAmount?.ToString("C", new System.Globalization.CultureInfo("pt-PT")) ?? "N/A";

        // Tempo decorrido desde o início
        public TimeSpan TimeElapsed => DateTime.Now - StartDate;
        public int DaysElapsed => TimeElapsed.Days;

        // Velocidade média de poupança (por dia)
        public decimal AverageDailySavings => DaysElapsed > 0 ? CurrentAmount / DaysElapsed : 0;
        public string FormattedAverageDailySavings => AverageDailySavings.ToString("C", new System.Globalization.CultureInfo("pt-PT"));

        // Previsão de conclusão baseada na velocidade atual
        public DateTime? EstimatedCompletionDate
        {
            get
            {
                if (IsTargetReached) return CompletedDate ?? DateTime.Now;
                if (AverageDailySavings <= 0) return null;

                var daysNeeded = (double)(RemainingAmount / AverageDailySavings);
                return DateTime.Now.AddDays(daysNeeded);
            }
        }

        public string FormattedEstimatedCompletionDate => EstimatedCompletionDate?.ToString("dd/MM/yyyy", new System.Globalization.CultureInfo("pt-PT")) ?? "Indeterminado";
    }
}