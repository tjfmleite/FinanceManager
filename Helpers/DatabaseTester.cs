using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using FinanceManager.Data;
using FinanceManager.Services;

namespace FinanceManager.Helpers
{
    public static class DatabaseTester
    {
        /// <summary>
        /// Testa a conectividade da base de dados
        /// </summary>
        public static async Task<bool> TestDatabaseConnectionAsync()
        {
            try
            {
                using var context = new FinanceContext();

                // Tentar conectar à base de dados
                await context.Database.CanConnectAsync();

                // Verificar se as tabelas existem
                var usersCount = await context.Users.CountAsync();
                var expensesCount = await context.Expenses.CountAsync();

                System.Diagnostics.Debug.WriteLine($"BD Conectada: {usersCount} utilizadores, {expensesCount} despesas");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro na BD: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Testa o fluxo completo de registo e login
        /// </summary>
        public static async Task<bool> TestUserRegistrationFlowAsync()
        {
            try
            {
                var userService = new UserService();
                var testUsername = $"test_user_{DateTime.Now.Ticks}";
                var testEmail = $"test_{DateTime.Now.Ticks}@test.com";
                var testPassword = "test123";

                // Teste 1: Registar utilizador
                var registerSuccess = await userService.RegisterAsync(testUsername, testEmail, testPassword);
                if (!registerSuccess)
                {
                    System.Diagnostics.Debug.WriteLine("Falha no registo");
                    return false;
                }

                // Teste 2: Login com credenciais corretas
                var loginUser = await userService.LoginAsync(testUsername, testPassword);
                if (loginUser == null)
                {
                    System.Diagnostics.Debug.WriteLine("Falha no login");
                    return false;
                }

                // Teste 3: Login com credenciais incorretas
                var wrongLogin = await userService.LoginAsync(testUsername, "wrong_password");
                if (wrongLogin != null)
                {
                    System.Diagnostics.Debug.WriteLine("Falha na validação de password");
                    return false;
                }

                // Teste 4: Tentar registar username duplicado
                var duplicateRegister = await userService.RegisterAsync(testUsername, "another@email.com", "password");
                if (duplicateRegister)
                {
                    System.Diagnostics.Debug.WriteLine("Falha na validação de username único");
                    return false;
                }

                System.Diagnostics.Debug.WriteLine("✅ Todos os testes de utilizador passaram!");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro no teste: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Testa o sistema de despesas
        /// </summary>
        public static async Task<bool> TestExpenseSystemAsync()
        {
            try
            {
                var userService = new UserService();
                var expenseService = new ExpenseService();

                // Criar utilizador de teste
                var testUsername = $"expense_test_{DateTime.Now.Ticks}";
                await userService.RegisterAsync(testUsername, $"{testUsername}@test.com", "test123");
                var user = await userService.LoginAsync(testUsername, "test123");

                if (user == null) return false;

                // Teste 1: Adicionar despesa
                var expense = new Models.Expense
                {
                    Description = "Teste de despesa",
                    Amount = 25.50m,
                    Category = "Teste",
                    Date = DateTime.Now,
                    UserId = user.Id
                };

                var addSuccess = await expenseService.AddExpenseAsync(expense);
                if (!addSuccess) return false;

                // Teste 2: Listar despesas
                var expenses = await expenseService.GetExpensesByUserIdAsync(user.Id);
                if (expenses.Count == 0) return false;

                // Teste 3: Estatísticas por categoria
                var categoryStats = await expenseService.GetExpensesByCategoryAsync(
                    user.Id, DateTime.Now.AddDays(-30), DateTime.Now);

                if (!categoryStats.ContainsKey("Teste")) return false;

                System.Diagnostics.Debug.WriteLine("✅ Todos os testes de despesas passaram!");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro no teste de despesas: {ex.Message}");
                return false;
            }
        }
    }
}
