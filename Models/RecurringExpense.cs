using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceManager.Models
{
    [Table("RecurringExpenses")]
    public class RecurringExpense
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Description { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Required]
        [StringLength(100)]
        public string Category { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        [Required]
        [StringLength(20)]
        public string Frequency { get; set; } // "Monthly", "Weekly", "Yearly", etc.

        [StringLength(500)]
        public string Notes { get; set; }

        [Required]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual User User { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        public bool IsActive { get; set; } = true;

        // Propriedade calculada para próxima ocorrência
        [NotMapped]
        public DateTime? NextOccurrence
        {
            get
            {
                if (!IsActive || (EndDate.HasValue && EndDate.Value < DateTime.Today))
                    return null;

                var current = StartDate;
                var today = DateTime.Today;

                while (current < today)
                {
                    current = GetNextDate(current);
                }

                return current;
            }
        }

        private DateTime GetNextDate(DateTime currentDate)
        {
            return Frequency.ToLower() switch
            {
                "daily" => currentDate.AddDays(1),
                "weekly" => currentDate.AddDays(7),
                "monthly" => currentDate.AddMonths(1),
                "quarterly" => currentDate.AddMonths(3),
                "yearly" => currentDate.AddYears(1),
                _ => currentDate.AddMonths(1) // Default to monthly
            };
        }
    }
}