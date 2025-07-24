using System;
using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace FinanceManager.Models
{
    public class Expense
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Description { get; set; } = string.Empty;

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "O valor deve ser maior que zero")]
        public decimal Amount { get; set; }

        [Required]
        [StringLength(100)]
        public string Category { get; set; } = string.Empty;

        [Required]
        public DateTime Date { get; set; } = DateTime.Now;

        [StringLength(500)]
        public string? Notes { get; set; }

        [Required]
        public int UserId { get; set; }

        public virtual User User { get; set; } = null!;

        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime? UpdatedDate { get; set; }

        // Cultura portuguesa para formatação em Euros
        private static readonly CultureInfo PortugueseCulture = new CultureInfo("pt-PT");

        // Propriedades calculadas para exibição em Euros
        public string FormattedAmount => Amount.ToString("C", PortugueseCulture);
        public string FormattedDate => Date.ToString("dd/MM/yyyy", PortugueseCulture);
        public string FormattedDateTime => Date.ToString("dd/MM/yyyy HH:mm", PortugueseCulture);
        public string ShortDescription => Description.Length > 30 ? Description.Substring(0, 30) + "..." : Description;

        // Categorias padrão para despesas
        public static readonly string[] DefaultCategories = new[]
        {
            "Alimentação",
            "Transporte",
            "Casa",
            "Saúde",
            "Entretenimento",
            "Compras",
            "Educação",
            "Viagens",
            "Serviços",
            "Outros"
        };
    }
}
