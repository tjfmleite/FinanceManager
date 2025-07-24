using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using FinanceManager.Helpers;
using FinanceManager.Views;
using FinanceManager.Services;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager
{
    public partial class App : Application
    {
        private async void Application_Startup(object sender, StartupEventArgs e)
        {
            try
            {
                // Configurar cultura para Euros (Português)
                var culture = new CultureInfo("pt-PT");
                Thread.CurrentThread.CurrentCulture = culture;
                Thread.CurrentThread.CurrentUICulture = culture;
                CultureInfo.DefaultThreadCurrentCulture = culture;
                CultureInfo.DefaultThreadCurrentUICulture = culture;

                LoggingService.LogInfo("=== Finance Manager Iniciado ===");

                // Inicializar base de dados com migração obrigatória
                await InitializeDatabaseSafely();

                // **CRÍTICO**: Só abrir LoginWindow - NUNCA MainWindow ou ChartsWindow
                var loginWindow = new LoginWindow();
                loginWindow.Show();

                // Verificar se é primeira execução e mostrar boas-vindas
                var isFirstRun = await CheckFirstRunAsync();
                if (isFirstRun)
                {
                    ShowFirstRunWelcome();
                }

                LoggingService.LogInfo("Aplicação inicializada - LoginWindow mostrada");
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro crítico na inicialização", ex);
                ShowCriticalErrorAndExit(ex);
            }
        }

        private async Task InitializeDatabaseSafely()
        {
            try
            {
                LoggingService.LogInfo("Inicializando base de dados...");

                // Verificar se existe e inicializar se necessário
                var dbExists = await DatabaseHelper.TestDatabaseConnectionAsync();

                if (!dbExists)
                {
                    LoggingService.LogInfo("Base de dados não encontrada - criando nova...");
                    await DatabaseHelper.InitializeDatabaseAsync();
                }
                else
                {
                    LoggingService.LogInfo("Base de dados encontrada - validando estrutura...");

                    // SEMPRE validar e executar migração se necessário
                    await DatabaseHelper.ValidateAndFixDatabaseAsync();
                }

                // Força verificação da tabela RecurringExpenses
                LoggingService.LogInfo("Verificando tabela RecurringExpenses...");
                var migrationSuccess = await DatabaseHelper.MigrateToRecurringExpensesAsync();

                if (!migrationSuccess)
                {
                    LoggingService.LogWarning("Problema na migração - tentando migração forçada...");
                    await DatabaseHelper.ForceMigrationAsync();
                }

                LoggingService.LogInfo("✅ Base de dados inicializada com sucesso");
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro na inicialização da base de dados", ex);

                var result = MessageBox.Show(
                    "⚠️ Problema na Base de Dados\n\n" +
                    "Houve um problema ao inicializar a base de dados.\n" +
                    "A aplicação tentará criar uma nova base de dados.\n\n" +
                    "Continuar?",
                    "Finance Manager",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        // Tentar criar base de dados nova usando método existente
                        await DatabaseHelper.ResetDatabaseAsync();
                        LoggingService.LogInfo("Nova base de dados criada com sucesso");
                    }
                    catch (Exception ex2)
                    {
                        LoggingService.LogError("Falha crítica ao criar base de dados", ex2);
                        throw; // Re-throw para mostrar erro crítico
                    }
                }
                else
                {
                    LoggingService.LogInfo("Utilizador cancelou inicialização");
                    Current.Shutdown();
                }
            }
        }

        private void ShowCriticalErrorAndExit(Exception ex)
        {
            var errorMessage =
                "💥 Erro Crítico no Finance Manager\n\n" +
                $"Detalhes: {ex.Message}\n\n" +
                "A aplicação será encerrada.\n" +
                "Contacte o suporte se o problema persistir.";

            MessageBox.Show(errorMessage, "Erro Crítico",
                          MessageBoxButton.OK, MessageBoxImage.Error);

            LoggingService.LogError("Aplicação encerrada devido a erro crítico", ex);
            Current.Shutdown(1); // Exit code 1 indica erro
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            try
            {
                LoggingService.LogInfo("=== Finance Manager Encerrado ===");

                // Limpeza de logs antigos
                LoggingService.CleanOldLogs();

                LoggingService.LogInfo("Limpeza concluída - aplicação encerrada");
            }
            catch (Exception ex)
            {
                // Não mostrar erro na saída - apenas log
                LoggingService.LogError("Erro durante encerramento", ex);
            }
        }

        private void Application_DispatcherUnhandledException(object sender,
            System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                LoggingService.LogError("Exceção não tratada", e.Exception);

                var errorMessage =
                    "❌ Erro Inesperado\n\n" +
                    $"Detalhes: {e.Exception.Message}\n\n" +
                    "A aplicação tentará continuar.\n" +
                    "Se o problema persistir, reinicie a aplicação.";

                MessageBox.Show(errorMessage, "Finance Manager - Erro",
                              MessageBoxButton.OK, MessageBoxImage.Warning);

                // Marcar como tratado para evitar crash
                e.Handled = true;
            }
            catch
            {
                // Se falhar a mostrar o erro, deixar WPF tratar
                e.Handled = false;
            }
        }

        /// <summary>
        /// Método estático para verificar se a aplicação já está em execução
        /// </summary>
        public static bool IsApplicationAlreadyRunning()
        {
            try
            {
                var current = System.Diagnostics.Process.GetCurrentProcess();
                var processes = System.Diagnostics.Process.GetProcessesByName(current.ProcessName);

                return processes.Length > 1;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// ATUALIZADO: Inicialização completa com despesas recorrentes
        /// </summary>
        public static async Task InitializeApplicationWithRecurringAsync()
        {
            try
            {
                LoggingService.LogInfo("Inicializando aplicação com suporte completo a despesas recorrentes...");

                // Inicializar base de dados normal
                await DatabaseHelper.InitializeDatabaseAsync();

                // Verificar e corrigir estrutura da base de dados
                await DatabaseHelper.ValidateAndFixDatabaseAsync();

                // Garantir que a migração para despesas recorrentes foi executada
                var migrationSuccess = await DatabaseHelper.MigrateToRecurringExpensesAsync();

                if (!migrationSuccess)
                {
                    LoggingService.LogWarning("Problema na migração - executando migração forçada...");
                    await DatabaseHelper.ForceMigrationAsync();
                }

                // Verificação final
                using var context = new FinanceManager.Data.FinanceContext();
                var recurringCount = await context.RecurringExpenses.CountAsync();

                LoggingService.LogInfo($"✅ Aplicação inicializada com suporte completo a despesas recorrentes ({recurringCount} registos existentes)");
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro na inicialização com despesas recorrentes", ex);
                throw;
            }
        }

        /// <summary>
        /// Verificar se é primeira execução da aplicação
        /// </summary>
        private async Task<bool> CheckFirstRunAsync()
        {
            try
            {
                // Verificar se existem utilizadores na base de dados
                var userService = new UserService();
                var userCount = await userService.GetTotalUsersCountAsync();

                return userCount == 0;
            }
            catch
            {
                return true; // Se houver erro, assumir primeira execução
            }
        }

        /// <summary>
        /// Mostrar mensagem de boas-vindas para primeira execução
        /// </summary>
        private void ShowFirstRunWelcome()
        {
            MessageBox.Show(
                "🎉 Bem-vindo ao Finance Manager!\n\n" +
                "Esta é a sua primeira execução.\n\n" +
                "💡 Dicas para começar:\n" +
                "• Crie uma conta de utilizador\n" +
                "• Comece por adicionar algumas despesas\n" +
                "• Explore as despesas recorrentes\n" +
                "• Explore todas as funcionalidades\n\n" +
                "Divirta-se a gerir as suas finanças!",
                "Finance Manager - Primeira Execução",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        /// <summary>
        /// NOVO: Método para testar a funcionalidade de despesas recorrentes
        /// </summary>
        public static async Task<bool> TestRecurringExpenseFunctionalityAsync()
        {
            try
            {
                LoggingService.LogInfo("🧪 Testando funcionalidade de despesas recorrentes...");

                using var context = new FinanceManager.Data.FinanceContext();

                // Testar se consegue contar registos
                var count = await context.RecurringExpenses.CountAsync();
                LoggingService.LogInfo($"✅ Contagem de despesas recorrentes: {count}");

                // Testar criação de um registo de teste
                var testExpense = new FinanceManager.Models.RecurringExpense
                {
                    Description = "Teste de Funcionalidade",
                    Amount = 10.00m,
                    Category = "Teste",
                    Frequency = "Mensal",
                    StartDate = DateTime.Now,
                    IsActive = true,
                    UserId = 1 // Assumindo que existe utilizador demo
                };

                context.RecurringExpenses.Add(testExpense);
                await context.SaveChangesAsync();

                LoggingService.LogInfo($"✅ Despesa de teste criada com ID: {testExpense.Id}");

                // Remover o registo de teste
                context.RecurringExpenses.Remove(testExpense);
                await context.SaveChangesAsync();

                LoggingService.LogInfo("✅ Despesa de teste removida");
                LoggingService.LogInfo("🎉 Teste de funcionalidade concluído com sucesso!");

                return true;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("❌ Erro no teste de funcionalidade", ex);
                return false;
            }
        }
    }
}