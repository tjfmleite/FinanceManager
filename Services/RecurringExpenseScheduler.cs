using System;
using System.Threading.Tasks;
using FinanceManager.Services;

namespace FinanceManager.Services
{
    /// <summary>
    /// Agendador para processar despesas recorrentes automaticamente
    /// </summary>
    public class RecurringExpenseScheduler
    {
        private readonly RecurringExpenseService _recurringService;

        public RecurringExpenseScheduler()
        {
            _recurringService = new RecurringExpenseService();
        }

        /// <summary>
        /// Executa o processamento automático de despesas recorrentes
        /// </summary>
        public async Task<RecurringProcessResult> ExecuteScheduledProcessingAsync(int userId)
        {
            try
            {
                LoggingService.LogInfo($"Executando processamento agendado para utilizador {userId}");

                var result = await _recurringService.ProcessRecurringExpensesAsync(userId);

                LoggingService.LogInfo($"Processamento agendado concluído: {result.ProcessedCount} despesas processadas");

                return result;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro no processamento agendado", ex);

                return new RecurringProcessResult
                {
                    IsSuccess = false,
                    Message = $"Erro no processamento: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Verifica se há despesas vencidas para um utilizador
        /// </summary>
        public async Task<bool> HasOverdueExpensesAsync(int userId)
        {
            try
            {
                var dueExpenses = await _recurringService.GetDueRecurringExpensesAsync(userId);
                return dueExpenses.Count > 0;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao verificar despesas vencidas", ex);
                return false;
            }
        }

        /// <summary>
        /// Obtém resumo de despesas pendentes
        /// </summary>
        public async Task<string> GetPendingSummaryAsync(int userId)
        {
            try
            {
                var stats = await _recurringService.GetRecurringExpenseStatsAsync(userId);

                if (stats.DueToday + stats.Overdue == 0)
                    return "✅ Todas as despesas recorrentes estão em dia.";

                return $"📋 Despesas Recorrentes Pendentes:\n" +
                       $"• Para hoje: {stats.DueToday}\n" +
                       $"• Em atraso: {stats.Overdue}\n" +
                       $"• Próximas: {stats.DueSoon}";
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao obter resumo pendente", ex);
                return "❌ Erro ao verificar despesas pendentes.";
            }
        }
    }
}