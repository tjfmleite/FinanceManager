using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using FinanceManager.Data;
using FinanceManager.Models;

namespace FinanceManager.Services
{
    public class NoteService
    {
        public async Task<List<Note>> GetNotesByUserIdAsync(int userId)
        {
            using var context = new FinanceContext();
            return await context.Notes
                .Where(n => n.UserId == userId)
                // REMOVIDO: .Include(n => n.User) que estava a causar o erro
                .OrderByDescending(n => n.ModifiedDate ?? n.CreatedDate)
                .ToListAsync();
        }

        public async Task<bool> AddNoteAsync(Note note)
        {
            using var context = new FinanceContext();

            context.Notes.Add(note);
            await context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpdateNoteAsync(Note note)
        {
            using var context = new FinanceContext();

            context.Notes.Update(note);
            await context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteNoteAsync(int noteId)
        {
            using var context = new FinanceContext();

            var note = await context.Notes.FindAsync(noteId);
            if (note == null) return false;

            context.Notes.Remove(note);
            await context.SaveChangesAsync();
            return true;
        }

        public async Task<List<Note>> SearchNotesAsync(int userId, string searchTerm)
        {
            using var context = new FinanceContext();

            searchTerm = searchTerm.ToLower();

            return await context.Notes
                .Where(n => n.UserId == userId &&
                           (n.Title.ToLower().Contains(searchTerm) ||
                            n.Content.ToLower().Contains(searchTerm) ||
                            n.Tags.ToLower().Contains(searchTerm)))
                .OrderByDescending(n => n.ModifiedDate ?? n.CreatedDate)
                .ToListAsync();
        }
    }
}

