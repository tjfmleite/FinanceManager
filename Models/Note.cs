using System;
using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Models
{
    public class Note
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Content { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Tags { get; set; }

        [Required]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public DateTime? ModifiedDate { get; set; }

        [Required]
        public int UserId { get; set; }

        public virtual User User { get; set; } = null!;

        // Propriedades calculadas
        public string FormattedCreatedDate => CreatedDate.ToString("dd/MM/yyyy HH:mm", new System.Globalization.CultureInfo("pt-PT"));
        public string FormattedModifiedDate => ModifiedDate?.ToString("dd/MM/yyyy HH:mm", new System.Globalization.CultureInfo("pt-PT")) ?? "";
        public string ShortContent => Content.Length > 100 ? Content.Substring(0, 100) + "..." : Content;
        public string[] TagList => !string.IsNullOrEmpty(Tags) ? Tags.Split(',', StringSplitOptions.RemoveEmptyEntries) : new string[0];
        public int WordCount => Content.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        public string LastModified => ModifiedDate?.ToString("dd/MM/yyyy", new System.Globalization.CultureInfo("pt-PT")) ?? CreatedDate.ToString("dd/MM/yyyy", new System.Globalization.CultureInfo("pt-PT"));
    }
}
