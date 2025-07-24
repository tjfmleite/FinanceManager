using System;
using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Models
{
    public class Investment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Type { get; set; } = string.Empty;

        [Required]
        [Range(0.00000001, double.MaxValue, ErrorMessage = "A quantidade deve ser maior que zero")]
        public decimal Quantity { get; set; }

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "O preço de compra deve ser maior que zero")]
        public decimal PurchasePrice { get; set; }

        public decimal? CurrentPrice { get; set; }

        [Required]
        public decimal Amount { get; set; }

        [Required]
        public DateTime PurchaseDate { get; set; } = DateTime.Now;

        [StringLength(100)]
        public string? Broker { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        [StringLength(10)]
        public string Currency { get; set; } = "EUR";

        public bool IsActive { get; set; } = true;

        [Required]
        public int UserId { get; set; }

        public virtual User User { get; set; } = null!;

        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime? UpdatedDate { get; set; }

        // Propriedades calculadas
        public decimal CurrentValue => CurrentPrice.HasValue ? Quantity * CurrentPrice.Value : Amount;
        public decimal TotalValue => CurrentValue; // Alias para compatibilidade
        public decimal ProfitLoss => CurrentValue - Amount;
        public decimal ProfitLossPercentage => Amount > 0 ? (ProfitLoss / Amount) * 100 : 0;

        // Formatação em Euros
        public string FormattedAmount => Amount.ToString("C", new System.Globalization.CultureInfo("pt-PT"));
        public string FormattedCurrentValue => CurrentValue.ToString("C", new System.Globalization.CultureInfo("pt-PT"));
        public string FormattedProfitLoss => ProfitLoss.ToString("C", new System.Globalization.CultureInfo("pt-PT"));
        public string FormattedPurchaseDate => PurchaseDate.ToString("dd/MM/yyyy", new System.Globalization.CultureInfo("pt-PT"));
        public string FormattedQuantity => Quantity.ToString("N8").TrimEnd('0').TrimEnd(',');
        public string FormattedPurchasePrice => PurchasePrice.ToString("C", new System.Globalization.CultureInfo("pt-PT"));
        public string FormattedCurrentPrice => CurrentPrice?.ToString("C", new System.Globalization.CultureInfo("pt-PT")) ?? "N/A";
        public string FormattedProfitLossPercentage => ProfitLossPercentage.ToString("F2") + "%";

        public string StatusIcon => IsActive ? "🟢" : "🔴";
        public string ProfitLossIcon => ProfitLoss >= 0 ? "📈" : "📉";
    }
}