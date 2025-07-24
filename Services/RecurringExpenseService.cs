using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Threading.Tasks;
using FinanceManager.Data;
using FinanceManager.Models;
using Microsoft.Data.Sqlite;

namespace FinanceManager.Services
{
    public class RecurringExpenseService
    {
        private readonly DatabaseManager _databaseManager;

        public RecurringExpenseService()
        {
            _databaseManager = new DatabaseManager();
        }

        public async Task<bool> AddRecurringExpenseAsync(RecurringExpense expense)
        {
            try
            {
                const string query = @"
                    INSERT INTO RecurringExpenses 
                    (Description, Amount, Category, StartDate, EndDate, Frequency, Notes, UserId, CreatedAt, IsActive)
                    VALUES 
                    (@Description, @Amount, @Category, @StartDate, @EndDate, @Frequency, @Notes, @UserId, @CreatedAt, @IsActive)";

                var parameters = new[]
                {
                    new SQLiteParameter("@Description", expense.Description),
                    new SQLiteParameter("@Amount", expense.Amount),
                    new SQLiteParameter("@Category", expense.Category),
                    new SQLiteParameter("@StartDate", expense.StartDate.ToString("yyyy-MM-dd")),
                    new SQLiteParameter("@EndDate", expense.EndDate?.ToString("yyyy-MM-dd") ?? (object)DBNull.Value),
                    new SQLiteParameter("@Frequency", expense.Frequency),
                    new SQLiteParameter("@Notes", expense.Notes ?? (object)DBNull.Value),
                    new SQLiteParameter("@UserId", expense.UserId),
                    new SQLiteParameter("@CreatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")),
                    new SQLiteParameter("@IsActive", expense.IsActive)
                };

                var result = await _databaseManager.ExecuteNonQueryAsync(query, parameters);
                return result > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao adicionar despesa recorrente: {ex.Message}");
                return false;
            }
        }

        public async Task<List<RecurringExpense>> GetRecurringExpensesByUserAsync(int userId)
        {
            try
            {
                const string query = @"
                    SELECT Id, Description, Amount, Category, StartDate, EndDate, 
                           Frequency, Notes, UserId, CreatedAt, UpdatedAt, IsActive
                    FROM RecurringExpenses 
                    WHERE UserId = @UserId 
                    ORDER BY CreatedAt DESC"
                ;

                var parameters = new[] { new SQLiteParameter("@UserId", userId) };
                var dataTable = await _databaseManager.ExecuteQueryAsync(query, parameters);

                var expenses = new List<RecurringExpense>();

                foreach (System.Data.DataRow row in dataTable.Rows)
                {
                    var expense = new RecurringExpense
                    {
                        Id = Convert.ToInt32(row["Id"]),
                        Description = row["Description"].ToString(),
                        Amount = Convert.ToDecimal(row["Amount"]),
                        Category = row["Category"].ToString(),
                        StartDate = DateTime.Parse(row["StartDate"].ToString()),
                        EndDate = row["EndDate"] != DBNull.Value ? DateTime.Parse(row["EndDate"].ToString()) : null,
                        Frequency = row["Frequency"].ToString(),
                        Notes = row["Notes"]?.ToString(),
                        UserId = Convert.ToInt32(row["UserId"]),
                        CreatedAt = DateTime.Parse(row["CreatedAt"].ToString()),
                        UpdatedAt = row["UpdatedAt"] != DBNull.Value ? DateTime.Parse(row["UpdatedAt"].ToString()) : null,
                        IsActive = Convert.ToBoolean(row["IsActive"])
                    };

                    expenses.Add(expense);
                }

                return expenses;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao obter despesas recorrentes: {ex.Message}");
                return new List<RecurringExpense>();
            }
        }

        public async Task<bool> UpdateRecurringExpenseAsync(RecurringExpense expense)
        {
            try
            {
                const string query = @"
                    UPDATE RecurringExpenses 
                    SET Description = @Description, Amount = @Amount, Category = @Category,
                        StartDate = @StartDate, EndDate = @EndDate, Frequency = @Frequency,
                        Notes = @Notes, IsActive = @IsActive, UpdatedAt = @UpdatedAt
                    WHERE Id = @Id AND UserId = @UserId";

                var parameters = new[]
                {
                    new SQLiteParameter("@Description", expense.Description),
                    new SQLiteParameter("@Amount", expense.Amount),
                    new SQLiteParameter("@Category", expense.Category),
                    new SQLiteParameter("@StartDate", expense.StartDate.ToString("yyyy-MM-dd")),
                    new SQLiteParameter("@EndDate", expense.EndDate?.ToString("yyyy-MM-dd") ?? (object)DBNull.Value),
                    new SQLiteParameter("@Frequency", expense.Frequency),
                    new SQLiteParameter("@Notes", expense.Notes ?? (object)DBNull.Value),
                    new SQLiteParameter("@IsActive", expense.IsActive),
                    new SQLiteParameter("@UpdatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")),
                    new SQLiteParameter("@Id", expense.Id),
                    new SQLiteParameter("@UserId", expense.UserId)
                };

                var result = await _databaseManager.ExecuteNonQueryAsync(query, parameters);
                return result > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao atualizar despesa recorrente: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteRecurringExpenseAsync(int expenseId, int userId)
        {
            try
            {
                const string query = "DELETE FROM RecurringExpenses WHERE Id = @Id AND UserId = @UserId";

                var parameters = new[]
                {
                    new SQLiteParameter("@Id", expenseId),
                    new SQLiteParameter("@UserId", userId)
                };

                var result = await _databaseManager.ExecuteNonQueryAsync(query, parameters);
                return result > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao eliminar despesa recorrente: {ex.Message}");
                return false;
            }
        }

        public async Task<RecurringExpense> GetRecurringExpenseByIdAsync(int expenseId, int userId)
        {
            try
            {
                const string query = @"
                    SELECT Id, Description, Amount, Category, StartDate, EndDate, 
                           Frequency, Notes, UserId, CreatedAt, UpdatedAt, IsActive
                    FROM RecurringExpenses 
                    WHERE Id = @Id AND UserId = @UserId";

                var parameters = new[]
                {
                    new SQLiteParameter("@Id", expenseId),
                    new SQLiteParameter("@UserId", userId)
                };

                var dataTable = await _databaseManager.ExecuteQueryAsync(query, parameters);

                if (dataTable.Rows.Count > 0)
                {
                    var row = dataTable.Rows[0];
                    return new RecurringExpense
                    {
                        Id = Convert.ToInt32(row["Id"]),
                        Description = row["Description"].ToString(),
                        Amount = Convert.ToDecimal(row["Amount"]),
                        Category = row["Category"].ToString(),
                        StartDate = DateTime.Parse(row["StartDate"].ToString()),
                        EndDate = row["EndDate"] != DBNull.Value ? DateTime.Parse(row["EndDate"].ToString()) : null,
                        Frequency = row["Frequency"].ToString(),
                        Notes = row["Notes"]?.ToString(),
                        UserId = Convert.ToInt32(row["UserId"]),
                        CreatedAt = DateTime.Parse(row["CreatedAt"].ToString()),
                        UpdatedAt = row["UpdatedAt"] != DBNull.Value ? DateTime.Parse(row["UpdatedAt"].ToString()) : null,
                        IsActive = Convert.ToBoolean(row["IsActive"])
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao obter despesa recorrente por ID: {ex.Message}");
                return null;
            }
        }

        public async Task<List<RecurringExpense>> GetActiveRecurringExpensesAsync(int userId)
        {
            try
            {
                const string query = @"
                    SELECT Id, Description, Amount, Category, StartDate, EndDate, 
                           Frequency, Notes, UserId, CreatedAt, UpdatedAt, IsActive
                    FROM RecurringExpenses 
                    WHERE UserId = @UserId AND IsActive = 1
                    ORDER BY StartDate DESC"
                ;

                var parameters = new[] { new SQLiteParameter("@UserId", userId) };
                var dataTable = await _databaseManager.ExecuteQueryAsync(query, parameters);

                var expenses = new List<RecurringExpense>();

                foreach (System.Data.DataRow row in dataTable.Rows)
                {
                    var expense = new RecurringExpense
                    {
                        Id = Convert.ToInt32(row["Id"]),
                        Description = row["Description"].ToString(),
                        Amount = Convert.ToDecimal(row["Amount"]),
                        Category = row["Category"].ToString(),
                        StartDate = DateTime.Parse(row["StartDate"].ToString()),
                        EndDate = row["EndDate"] != DBNull.Value ? DateTime.Parse(row["EndDate"].ToString()) : null,
                        Frequency = row["Frequency"].ToString(),
                        Notes = row["Notes"]?.ToString(),
                        UserId = Convert.ToInt32(row["UserId"]),
                        CreatedAt = DateTime.Parse(row["CreatedAt"].ToString()),
                        UpdatedAt = row["UpdatedAt"] != DBNull.Value ? DateTime.Parse(row["UpdatedAt"].ToString()) : null,
                        IsActive = Convert.ToBoolean(row["IsActive"])
                    };

                    expenses.Add(expense);
                }

                return expenses;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao obter despesas recorrentes ativas: {ex.Message}");
                return new List<RecurringExpense>();
            }
        }

        public async Task<bool> ToggleActiveStatusAsync(int expenseId, int userId)
        {
            try
            {
                const string query = @"
                    UPDATE RecurringExpenses 
                    SET IsActive = NOT IsActive, UpdatedAt = @UpdatedAt
                    WHERE Id = @Id AND UserId = @UserId"
                ;

                var parameters = new[]
                {
                    new SQLiteParameter("@UpdatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")),
                    new SQLiteParameter("@Id", expenseId),
                    new SQLiteParameter("@UserId", userId)
                };

                var result = await _databaseManager.ExecuteNonQueryAsync(query, parameters);
                return result > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao alterar status ativo: {ex.Message}");
                return false;
            }
        }
    }
}