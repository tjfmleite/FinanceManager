using System;
using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Models
{
    public class SavingsUpdateHistory
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int SavingsTargetId { get; set; }
        public SavingsTarget SavingsTarget { get; set; } = null!;

        [Required]
        public decimal AmountAdded { get; set; }

        [Required]
        public decimal PreviousAmount { get; set; }

        [Required]
        public decimal NewAmount { get; set; }

        [Required]
        public DateTime UpdateDate { get; set; } = DateTime.Now;

        [StringLength(500)]
        public string? Notes { get; set; }

        // Propriedades calculadas para exibição
        public string FormattedAmountAdded => AmountAdded.ToString("C", new System.Globalization.CultureInfo("pt-PT"));
        public string FormattedDate => UpdateDate.ToString("dd/MM/yyyy HH:mm", new System.Globalization.CultureInfo("pt-PT"));
        public string FormattedPreviousAmount => PreviousAmount.ToString("C", new System.Globalization.CultureInfo("pt-PT"));
        public string FormattedNewAmount => NewAmount.ToString("C", new System.Globalization.CultureInfo("pt-PT"));
    }
}
