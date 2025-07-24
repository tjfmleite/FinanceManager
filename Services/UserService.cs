using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using FinanceManager.Data;
using FinanceManager.Models;

namespace FinanceManager.Services
{
    public class UserService
    {
        public async Task<User?> LoginAsync(string username, string password)
        {
            try
            {
                using var context = new FinanceContext();

                var passwordHash = HashPassword(password);
                var user = await context.Users
                    .FirstOrDefaultAsync(u => u.Username == username && u.PasswordHash == passwordHash);

                return user;
            }
            catch (Exception ex)
            {
                // Log do erro (em produção usarias um logger real)
                System.Diagnostics.Debug.WriteLine($"Erro no login: {ex.Message}");
                throw new Exception("Erro ao verificar credenciais. Tente novamente.");
            }
        }

        public async Task<bool> RegisterAsync(string username, string email, string password)
        {
            try
            {
                using var context = new FinanceContext();

                // Verificar se username já existe (case-insensitive)
                var existingUser = await context.Users
                    .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());

                if (existingUser != null)
                {
                    return false; // Username já existe
                }

                // Verificar se email já existe
                var existingEmail = await context.Users
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());

                if (existingEmail != null)
                {
                    throw new Exception("Este email já está registado.");
                }

                // Criar novo utilizador
                var user = new User
                {
                    Username = username.Trim(),
                    Email = email.Trim().ToLower(),
                    PasswordHash = HashPassword(password),
                    CreatedDate = DateTime.Now
                };

                context.Users.Add(user);
                await context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                // Log do erro
                System.Diagnostics.Debug.WriteLine($"Erro no registo: {ex.Message}");

                if (ex.Message.Contains("email"))
                {
                    throw; // Re-throw specific email error
                }

                throw new Exception("Erro ao criar conta. Tente novamente.");
            }
        }

        public async Task<bool> UsernameExistsAsync(string username)
        {
            try
            {
                using var context = new FinanceContext();
                return await context.Users
                    .AnyAsync(u => u.Username.ToLower() == username.ToLower());
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> EmailExistsAsync(string email)
        {
            try
            {
                using var context = new FinanceContext();
                return await context.Users
                    .AnyAsync(u => u.Email.ToLower() == email.ToLower());
            }
            catch
            {
                return false;
            }
        }

        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashedBytes);
        }

        public async Task<User?> GetUserByIdAsync(int userId)
        {
            try
            {
                using var context = new FinanceContext();
                return await context.Users.FindAsync(userId);
            }
            catch
            {
                return null;
            }
        }

        public async Task<int> GetTotalUsersCountAsync()
        {
            try
            {
                using var context = new FinanceContext();
                return await context.Users.CountAsync();
            }
            catch
            {
                return 0;
            }
        }
    }
}