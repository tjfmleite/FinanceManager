using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using FinanceManager.Data;
using FinanceManager.Models;
using System.Linq;
using FinanceManager.Services;

namespace FinanceManager.Helpers
{
    public static class DatabaseHelper
    {
        /// <summary>
        /// Inicializa a base de dados
        /// </summary>
        public static async Task InitializeDatabaseAsync()
        {
            try
            {
                using var context = new FinanceContext();

                // Criar base de dados se não existir
                await context.Database.EnsureCreatedAsync();

                // SEMPRE executar migração para despesas recorrentes
                await MigrateToRecurringExpensesAsync();

                // Verificar se já existem utilizadores
                var hasUsers = await context.Users.AnyAsync();

                // APENAS criar utilizador demo se não existir nenhum utilizador
                if (!hasUsers)
                {
                    await CreateDemoUserAsync(context);
                }

                LoggingService.LogInfo("Base de dados inicializada com sucesso");
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao inicializar base de dados", ex);
                throw;
            }
        }

        /// <summary>
        /// MIGRAÇÃO COMPLETA para adicionar a tabela RecurringExpenses
        /// </summary>
        public static async Task<bool> MigrateToRecurringExpensesAsync()
        {
            try
            {
                LoggingService.LogInfo("Verificando migração para despesas recorrentes...");

                using var context = new FinanceContext();

                // Verificar se a tabela já existe usando uma query mais robusta
                var tableExists = false;
                try
                {
                    await context.RecurringExpenses.CountAsync();
                    tableExists = true;
                    LoggingService.LogInfo("Tabela RecurringExpenses já existe");
                }
                catch (Exception)
                {
                    LoggingService.LogInfo("Tabela RecurringExpenses não existe - criando...");
                }

                if (!tableExists)
                {
                    // Criar tabela com estrutura completa
                    LoggingService.LogInfo("Criando tabela RecurringExpenses com estrutura completa...");

                    var createTableSql = @"
                        CREATE TABLE IF NOT EXISTS RecurringExpenses (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            Description NVARCHAR(200) NOT NULL,
                            Amount DECIMAL(18,2) NOT NULL,
                            Category NVARCHAR(100) NOT NULL,
                            Frequency NVARCHAR(50) NOT NULL DEFAULT 'Mensal',
                            StartDate DATETIME NOT NULL,
                            EndDate DATETIME NULL,
                            LastProcessedDate DATETIME NULL,
                            Notes NVARCHAR(500) NULL,
                            IsActive BOOLEAN NOT NULL DEFAULT 1,
                            UserId INTEGER NOT NULL,
                            CreatedDate DATETIME NOT NULL DEFAULT (datetime('now')),
                            UpdatedDate DATETIME NULL,
                            FOREIGN KEY (UserId) REFERENCES Users (Id) ON DELETE CASCADE
                        )";

                    await context.Database.ExecuteSqlRawAsync(createTableSql);

                    // Criar todos os índices necessários
                    var indexes = new[]
                    {
                        "CREATE INDEX IF NOT EXISTS IX_RecurringExpenses_UserId ON RecurringExpenses (UserId)",
                        "CREATE INDEX IF NOT EXISTS IX_RecurringExpenses_IsActive ON RecurringExpenses (IsActive)",
                        "CREATE INDEX IF NOT EXISTS IX_RecurringExpenses_Frequency ON RecurringExpenses (Frequency)",
                        "CREATE INDEX IF NOT EXISTS IX_RecurringExpenses_StartDate ON RecurringExpenses (StartDate)",
                        "CREATE INDEX IF NOT EXISTS IX_RecurringExpenses_EndDate ON RecurringExpenses (EndDate)",
                        "CREATE INDEX IF NOT EXISTS IX_RecurringExpenses_LastProcessedDate ON RecurringExpenses (LastProcessedDate)",
                        "CREATE INDEX IF NOT EXISTS IX_RecurringExpenses_Category ON RecurringExpenses (Category)",
                        "CREATE INDEX IF NOT EXISTS IX_RecurringExpenses_CreatedDate ON RecurringExpenses (CreatedDate)",
                        "CREATE INDEX IF NOT EXISTS IX_RecurringExpenses_UserId_IsActive ON RecurringExpenses (UserId, IsActive)",
                        "CREATE INDEX IF NOT EXISTS IX_RecurringExpenses_IsActive_StartDate ON RecurringExpenses (IsActive, StartDate)"
                    };

                    foreach (var indexSql in indexes)
                    {
                        try
                        {
                            await context.Database.ExecuteSqlRawAsync(indexSql);
                        }
                        catch (Exception ex)
                        {
                            LoggingService.LogWarning($"Erro ao criar índice: {ex.Message}");
                        }
                    }

                    LoggingService.LogInfo("✅ Tabela RecurringExpenses criada com sucesso!");
                }

                // Verificar se a tabela foi criada corretamente
                try
                {
                    var testCount = await context.RecurringExpenses.CountAsync();
                    LoggingService.LogInfo($"✅ Verificação: Tabela RecurringExpenses funcional ({testCount} registos)");
                    return true;
                }
                catch (Exception ex)
                {
                    LoggingService.LogError("❌ Erro na verificação da tabela RecurringExpenses", ex);
                    return false;
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro na migração para despesas recorrentes", ex);
                return false;
            }
        }

        /// <summary>
        /// Cria apenas o utilizador demo
        /// </summary>
        public static async Task CreateDemoUserAsync(FinanceContext context)
        {
            try
            {
                var demoUser = new User
                {
                    Username = "demo",
                    Email = "demo@financemanager.com",
                    PasswordHash = HashPassword("123456"),
                    CreatedDate = DateTime.Now
                };

                context.Users.Add(demoUser);
                await context.SaveChangesAsync();

                LoggingService.LogInfo("Utilizador demo criado com sucesso");
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao criar utilizador demo", ex);
                throw;
            }
        }

        /// <summary>
        /// Hash de password (igual ao UserService)
        /// </summary>
        private static string HashPassword(string password)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashedBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashedBytes);
        }

        /// <summary>
        /// Verifica a saúde da base de dados
        /// </summary>
        public static async Task<bool> CheckDatabaseHealthAsync()
        {
            try
            {
                using var context = new FinanceContext();

                // Verificar conectividade
                var canConnect = await context.Database.CanConnectAsync();
                if (!canConnect) return false;

                // Verificar tabelas essenciais
                var userCount = await context.Users.CountAsync();

                // Verificar tabela RecurringExpenses
                try
                {
                    var recurringCount = await context.RecurringExpenses.CountAsync();
                    LoggingService.LogInfo($"Base de dados saudável: {userCount} utilizadores, {recurringCount} despesas recorrentes");
                }
                catch (Exception)
                {
                    LoggingService.LogWarning("Tabela RecurringExpenses com problemas - executando migração");
                    await MigrateToRecurringExpensesAsync();
                }

                return true;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro na verificação da base de dados", ex);
                return false;
            }
        }

        /// <summary>
        /// Limpa todos os dados de um utilizador específico
        /// </summary>
        public static async Task<bool> ClearUserDataAsync(int userId)
        {
            try
            {
                using var context = new FinanceContext();

                LoggingService.LogInfo($"Limpando dados do utilizador {userId}");

                // Eliminar despesas do utilizador
                try
                {
                    var userExpenses = await context.Expenses.Where(e => e.UserId == userId).ToListAsync();
                    context.Expenses.RemoveRange(userExpenses);
                }
                catch (Exception ex)
                {
                    LoggingService.LogWarning($"Erro ao eliminar despesas: {ex.Message}");
                }

                // Eliminar despesas recorrentes do utilizador
                try
                {
                    var userRecurringExpenses = await context.RecurringExpenses.Where(r => r.UserId == userId).ToListAsync();
                    context.RecurringExpenses.RemoveRange(userRecurringExpenses);
                    LoggingService.LogInfo($"Eliminadas {userRecurringExpenses.Count} despesas recorrentes");
                }
                catch (Exception ex)
                {
                    LoggingService.LogWarning($"Erro ao eliminar despesas recorrentes: {ex.Message}");
                }

                // Eliminar receitas do utilizador
                try
                {
                    var userIncomes = await context.Incomes.Where(i => i.UserId == userId).ToListAsync();
                    context.Incomes.RemoveRange(userIncomes);
                }
                catch (Exception ex)
                {
                    LoggingService.LogWarning($"Erro ao eliminar receitas: {ex.Message}");
                }

                // Eliminar investimentos do utilizador
                try
                {
                    var userInvestments = await context.Investments.Where(i => i.UserId == userId).ToListAsync();
                    context.Investments.RemoveRange(userInvestments);
                }
                catch (Exception ex)
                {
                    LoggingService.LogWarning($"Erro ao eliminar investimentos: {ex.Message}");
                }

                // Eliminar objetivos de poupança
                try
                {
                    var userSavings = await context.SavingsTargets.Where(s => s.UserId == userId).ToListAsync();
                    context.SavingsTargets.RemoveRange(userSavings);
                }
                catch (Exception ex)
                {
                    LoggingService.LogWarning($"Erro ao eliminar objetivos de poupança: {ex.Message}");
                }

                // Eliminar notas
                try
                {
                    var userNotes = await context.Notes.Where(n => n.UserId == userId).ToListAsync();
                    context.Notes.RemoveRange(userNotes);
                }
                catch (Exception ex)
                {
                    LoggingService.LogWarning($"Erro ao eliminar notas: {ex.Message}");
                }

                await context.SaveChangesAsync();
                LoggingService.LogInfo($"Dados do utilizador {userId} limpos com sucesso");

                return true;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Erro ao limpar dados do utilizador {userId}", ex);
                return false;
            }
        }

        /// <summary>
        /// Reset COMPLETO da base de dados
        /// </summary>
        public static async Task<bool> ResetDatabaseAsync()
        {
            try
            {
                using var context = new FinanceContext();

                // Eliminar base de dados
                await context.Database.EnsureDeletedAsync();

                // Recriar base de dados
                await context.Database.EnsureCreatedAsync();

                // Executar migração para despesas recorrentes
                await MigrateToRecurringExpensesAsync();

                // Criar utilizador demo
                await CreateDemoUserAsync(context);

                LoggingService.LogInfo("Base de dados resetada com sucesso");
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao resetar base de dados", ex);
                return false;
            }
        }

        /// <summary>
        /// Testa a conectividade da base de dados
        /// </summary>
        public static async Task<bool> TestDatabaseConnectionAsync()
        {
            try
            {
                using var context = new FinanceContext();

                // Tentar conectar
                var canConnect = await context.Database.CanConnectAsync();

                if (canConnect)
                {
                    // Testar acesso às tabelas principais
                    await context.Users.CountAsync();
                    await context.Expenses.CountAsync();

                    // Testar tabela RecurringExpenses
                    try
                    {
                        await context.RecurringExpenses.CountAsync();
                        LoggingService.LogInfo("✅ Conexão à base de dados testada com sucesso (incluindo RecurringExpenses)");
                    }
                    catch (Exception)
                    {
                        LoggingService.LogWarning("Tabela RecurringExpenses com problemas - executando migração");
                        await MigrateToRecurringExpensesAsync();
                    }

                    return true;
                }
                else
                {
                    LoggingService.LogError("Falha na conexão à base de dados", null);
                    return false;
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao testar conexão à base de dados", ex);
                return false;
            }
        }

        /// <summary>
        /// Obtém informações da base de dados
        /// </summary>
        public static async Task<string> GetDatabaseInfoAsync()
        {
            try
            {
                using var context = new FinanceContext();

                var userCount = await context.Users.CountAsync();
                var expenseCount = await context.Expenses.CountAsync();
                var incomeCount = await context.Incomes.CountAsync();
                var investmentCount = await context.Investments.CountAsync();
                var savingsCount = await context.SavingsTargets.CountAsync();
                var noteCount = await context.Notes.CountAsync();

                int recurringCount = 0;
                try
                {
                    recurringCount = await context.RecurringExpenses.CountAsync();
                }
                catch (Exception ex)
                {
                    LoggingService.LogWarning($"Erro ao contar despesas recorrentes: {ex.Message}");
                    // Tentar migração
                    await MigrateToRecurringExpensesAsync();
                    try
                    {
                        recurringCount = await context.RecurringExpenses.CountAsync();
                    }
                    catch
                    {
                        // Se ainda falhar, deixar como 0
                    }
                }

                return $"📊 Informações da Base de Dados:\n" +
                       $"👤 Utilizadores: {userCount}\n" +
                       $"💸 Despesas: {expenseCount}\n" +
                       $"🔄 Despesas Recorrentes: {recurringCount}\n" +
                       $"💰 Receitas: {incomeCount}\n" +
                       $"📈 Investimentos: {investmentCount}\n" +
                       $"🎯 Objetivos de Poupança: {savingsCount}\n" +
                       $"📝 Notas: {noteCount}";
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao obter informações da base de dados", ex);
                return "❌ Erro ao obter informações da base de dados";
            }
        }

        /// <summary>
        /// Obtém estatísticas da base de dados (para MainWindow)
        /// </summary>
        public static async Task<DatabaseStats> GetDatabaseStatsAsync()
        {
            try
            {
                using var context = new FinanceContext();

                var dbPath = GetDatabasePath();
                var dbSize = File.Exists(dbPath) ? new FileInfo(dbPath).Length / (1024.0 * 1024.0) : 0;

                int totalInvestments = 0;
                int totalIncomes = 0;
                int totalRecurring = 0;

                try
                {
                    totalInvestments = await context.Investments.CountAsync();
                }
                catch (Exception ex)
                {
                    LoggingService.LogWarning($"Erro ao contar investimentos: {ex.Message}");
                }

                try
                {
                    totalIncomes = await context.Incomes.CountAsync();
                }
                catch (Exception ex)
                {
                    LoggingService.LogWarning($"Erro ao contar receitas: {ex.Message}");
                }

                try
                {
                    totalRecurring = await context.RecurringExpenses.CountAsync();
                }
                catch (Exception ex)
                {
                    LoggingService.LogWarning($"Erro ao contar despesas recorrentes: {ex.Message}");
                    // Tentar migração automática
                    await MigrateToRecurringExpensesAsync();
                    try
                    {
                        totalRecurring = await context.RecurringExpenses.CountAsync();
                    }
                    catch
                    {
                        // Se ainda falhar, deixar como 0
                    }
                }

                return new DatabaseStats
                {
                    TotalUsers = await context.Users.CountAsync(),
                    TotalExpenses = await context.Expenses.CountAsync(),
                    TotalIncomes = totalIncomes,
                    TotalSavingsTargets = await context.SavingsTargets.CountAsync(),
                    TotalNotes = await context.Notes.CountAsync(),
                    TotalInvestments = totalInvestments,
                    TotalRecurringExpenses = totalRecurring,
                    DatabaseSize = Math.Round(dbSize, 2)
                };
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao obter estatísticas da base de dados", ex);
                return new DatabaseStats();
            }
        }

        /// <summary>
        /// Verifica e corrige problemas da base de dados
        /// </summary>
        public static async Task<bool> ValidateAndFixDatabaseAsync()
        {
            try
            {
                LoggingService.LogInfo("Validando estrutura da base de dados...");

                using var context = new FinanceContext();

                // Verificar se é possível conectar
                var canConnect = await context.Database.CanConnectAsync();
                if (!canConnect)
                {
                    LoggingService.LogError("Não é possível conectar à base de dados");

                    // Tentar recriar
                    await context.Database.EnsureCreatedAsync();
                    await MigrateToRecurringExpensesAsync();
                    LoggingService.LogInfo("Base de dados recriada");
                    return true;
                }

                // Testar todas as tabelas essenciais
                try
                {
                    await context.Users.CountAsync();
                    await context.Expenses.CountAsync();
                    await context.SavingsTargets.CountAsync();
                    await context.Notes.CountAsync();

                    LoggingService.LogInfo("Tabelas essenciais validadas");
                }
                catch (Exception ex)
                {
                    LoggingService.LogError("Erro nas tabelas essenciais", ex);

                    // Tentar recriar estrutura
                    await context.Database.EnsureCreatedAsync();
                    await MigrateToRecurringExpensesAsync();
                    LoggingService.LogInfo("Estrutura da base de dados recriada");
                }

                // Verificar tabelas opcionais
                try
                {
                    await context.Incomes.CountAsync();
                }
                catch (Exception ex)
                {
                    LoggingService.LogWarning($"Problema com tabela Incomes: {ex.Message}");
                }

                try
                {
                    await context.Investments.CountAsync();
                }
                catch (Exception ex)
                {
                    LoggingService.LogWarning($"Problema com tabela Investments: {ex.Message}");
                }

                // Verificar e corrigir tabela RecurringExpenses
                try
                {
                    await context.RecurringExpenses.CountAsync();
                    LoggingService.LogInfo("Tabela RecurringExpenses validada");
                }
                catch (Exception ex)
                {
                    LoggingService.LogWarning($"Problema com tabela RecurringExpenses: {ex.Message}");

                    // Executar migração
                    var migrationSuccess = await MigrateToRecurringExpensesAsync();
                    if (migrationSuccess)
                    {
                        LoggingService.LogInfo("✅ Migração para RecurringExpenses executada com sucesso");
                    }
                    else
                    {
                        LoggingService.LogError("❌ Falha na migração para RecurringExpenses");
                        return false;
                    }
                }

                LoggingService.LogInfo("✅ Validação da base de dados concluída com sucesso");
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro crítico na validação da base de dados", ex);
                return false;
            }
        }

        /// <summary>
        /// Obtém o caminho da base de dados
        /// </summary>
        public static string GetDatabasePath()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var financeManagerPath = Path.Combine(appDataPath, "FinanceManager");
            return Path.Combine(financeManagerPath, "finance.db");
        }

        /// <summary>
        /// Força uma migração completa (para debugging)
        /// </summary>
        public static async Task<bool> ForceMigrationAsync()
        {
            try
            {
                LoggingService.LogInfo("🔧 Forçando migração completa...");

                using var context = new FinanceContext();

                // Primeiro, tentar eliminar a tabela se existir
                try
                {
                    await context.Database.ExecuteSqlRawAsync("DROP TABLE IF EXISTS RecurringExpenses");
                    LoggingService.LogInfo("Tabela RecurringExpenses eliminada");
                }
                catch (Exception ex)
                {
                    LoggingService.LogWarning($"Erro ao eliminar tabela: {ex.Message}");
                }

                // Executar migração
                var success = await MigrateToRecurringExpensesAsync();

                if (success)
                {
                    LoggingService.LogInfo("✅ Migração forçada concluída com sucesso");
                }
                else
                {
                    LoggingService.LogError("❌ Falha na migração forçada");
                }

                return success;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro na migração forçada", ex);
                return false;
            }
        }
    }

    /// <summary>
    /// Classe para armazenar estatísticas da base de dados
    /// </summary>
    public class DatabaseStats
    {
        public int TotalUsers { get; set; }
        public int TotalExpenses { get; set; }
        public int TotalIncomes { get; set; }
        public int TotalSavingsTargets { get; set; }
        public int TotalNotes { get; set; }
        public int TotalInvestments { get; set; }
        public int TotalRecurringExpenses { get; set; }
        public double DatabaseSize { get; set; } // em MB
    }
}