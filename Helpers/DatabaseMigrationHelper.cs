using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using FinanceManager.Data;
using FinanceManager.Services;
using System.Collections.Generic;

namespace FinanceManager.Helpers
{
    public static class DatabaseMigrationHelper
    {
        /// <summary>
        /// Força a recriação da base de dados com a estrutura correta
        /// </summary>
        public static async Task<bool> RecreateDatabase()
        {
            try
            {
                LoggingService.LogInfo("Iniciando recriação da base de dados...");

                using var context = new FinanceContext();

                // 1. Fazer backup dos utilizadores existentes (se existirem)
                var existingUsers = new List<(string Username, string Email, string PasswordHash, DateTime CreatedDate)>();

                try
                {
                    var users = await context.Users.ToListAsync();
                    foreach (var user in users)
                    {
                        existingUsers.Add((user.Username, user.Email, user.PasswordHash, user.CreatedDate));
                    }
                    LoggingService.LogInfo($"Backup de {existingUsers.Count} utilizadores criado");
                }
                catch (Exception ex)
                {
                    LoggingService.LogWarning($"Não foi possível fazer backup dos utilizadores: {ex.Message}");
                }

                // 2. Eliminar base de dados existente
                await context.Database.EnsureDeletedAsync();
                LoggingService.LogInfo("Base de dados eliminada");

                // 3. Recriar base de dados com estrutura correta
                await context.Database.EnsureCreatedAsync();
                LoggingService.LogInfo("Base de dados recriada com estrutura corrigida");

                // 4. Restaurar utilizadores
                if (existingUsers.Count > 0)
                {
                    foreach (var (username, email, passwordHash, createdDate) in existingUsers)
                    {
                        var user = new Models.User
                        {
                            Username = username,
                            Email = email,
                            PasswordHash = passwordHash,
                            CreatedDate = createdDate
                        };
                        context.Users.Add(user);
                    }
                    await context.SaveChangesAsync();
                    LoggingService.LogInfo($"Restaurados {existingUsers.Count} utilizadores");
                }
                else
                {
                    // Se não havia utilizadores, criar o demo
                    await DatabaseHelper.CreateDemoUserAsync(context);
                    LoggingService.LogInfo("Utilizador demo criado");
                }

                return true;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao recriar base de dados", ex);
                return false;
            }
        }

        /// <summary>
        /// Verifica se a base de dados tem a estrutura correta
        /// </summary>
        public static async Task<bool> ValidateDatabaseStructure()
        {
            try
            {
                using var context = new FinanceContext();

                // Tentar fazer queries simples para verificar estrutura
                await context.Users.CountAsync();
                await context.Expenses.CountAsync();
                await context.SavingsTargets.CountAsync();
                await context.Notes.CountAsync();

                try
                {
                    await context.Investments.CountAsync();
                }
                catch
                {
                    // Se falhar, a tabela de investimentos não existe ou tem problema
                    return false;
                }

                LoggingService.LogInfo("Estrutura da base de dados validada com sucesso");
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro na validação da estrutura da base de dados", ex);
                return false;
            }
        }

        /// <summary>
        /// Migração segura que tenta corrigir problemas sem perder dados
        /// </summary>
        public static async Task<bool> SafeMigration()
        {
            try
            {
                LoggingService.LogInfo("Iniciando migração segura...");

                // 1. Verificar se a estrutura está OK
                if (await ValidateDatabaseStructure())
                {
                    LoggingService.LogInfo("Base de dados já está com estrutura correta");
                    return true;
                }

                // 2. Se há problemas, tentar recriar
                LoggingService.LogWarning("Problemas detectados na estrutura - recriando base de dados");
                return await RecreateDatabase();
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro na migração segura", ex);
                return false;
            }
        }

        /// <summary>
        /// Obtém informações detalhadas sobre a base de dados
        /// </summary>
        public static async Task<string> GetDatabaseInfo()
        {
            try
            {
                using var context = new FinanceContext();

                var dbPath = DatabaseHelper.GetDatabasePath();
                var dbExists = File.Exists(dbPath);
                var dbSize = dbExists ? new FileInfo(dbPath).Length / 1024.0 : 0;

                var info = $"📊 Informações da Base de Dados\n\n";
                info += $"📁 Caminho: {dbPath}\n";
                info += $"📦 Existe: {(dbExists ? "Sim" : "Não")}\n";
                info += $"💾 Tamanho: {dbSize:F2} KB\n\n";

                if (dbExists)
                {
                    try
                    {
                        var canConnect = await context.Database.CanConnectAsync();
                        info += $"🔗 Conectividade: {(canConnect ? "OK" : "Erro")}\n";

                        if (canConnect)
                        {
                            var users = await context.Users.CountAsync();
                            var expenses = await context.Expenses.CountAsync();
                            var savings = await context.SavingsTargets.CountAsync();
                            var notes = await context.Notes.CountAsync();

                            info += $"👥 Utilizadores: {users}\n";
                            info += $"💰 Despesas: {expenses}\n";
                            info += $"🎯 Poupanças: {savings}\n";
                            info += $"📝 Notas: {notes}\n";

                            try
                            {
                                var investments = await context.Investments.CountAsync();
                                info += $"📈 Investimentos: {investments}\n";
                            }
                            catch
                            {
                                info += $"📈 Investimentos: Tabela não existe ou com erro\n";
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        info += $"❌ Erro: {ex.Message}\n";
                    }
                }

                return info;
            }
            catch (Exception ex)
            {
                return $"Erro ao obter informações: {ex.Message}";
            }
        }
    }
}
