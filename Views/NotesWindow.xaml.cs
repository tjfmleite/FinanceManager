using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using FinanceManager.Models;
using FinanceManager.Services;

namespace FinanceManager.Views
{
    public partial class NotesWindow : Window
    {
        private readonly User _currentUser;
        private readonly NoteService _noteService;
        private ObservableCollection<Note> _notes;
        private Note? _selectedNote;

        public NotesWindow(User currentUser)
        {
            InitializeComponent();
            _currentUser = currentUser;
            _noteService = new NoteService();
            _notes = new ObservableCollection<Note>();

            NotesListBox.ItemsSource = _notes;
            LoadNotes();
        }

        private async void LoadNotes()
        {
            try
            {
                var notes = await _noteService.GetNotesByUserIdAsync(_currentUser.Id);

                _notes.Clear();
                foreach (var note in notes.OrderByDescending(n => n.ModifiedDate ?? n.CreatedDate))
                {
                    _notes.Add(note);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao carregar notas: {ex.Message}", "Erro",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void NewNote_Click(object sender, RoutedEventArgs e)
        {
            _selectedNote = new Note
            {
                Title = "Nova Nota",
                Content = "",
                CreatedDate = DateTime.Now,
                UserId = _currentUser.Id
            };

            TitleTextBox.Text = _selectedNote.Title;
            ContentTextBox.Text = _selectedNote.Content;
            TagsTextBox.Text = _selectedNote.Tags;

            TitleTextBox.IsEnabled = true;
            ContentTextBox.IsEnabled = true;
            TagsTextBox.IsEnabled = true;
            SaveButton.IsEnabled = true;
        }

        private void NotesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (NotesListBox.SelectedItem is Note selectedNote)
            {
                _selectedNote = selectedNote;
                TitleTextBox.Text = selectedNote.Title;
                ContentTextBox.Text = selectedNote.Content;
                TagsTextBox.Text = selectedNote.Tags;

                TitleTextBox.IsEnabled = true;
                ContentTextBox.IsEnabled = true;
                TagsTextBox.IsEnabled = true;
                SaveButton.IsEnabled = true;
                DeleteButton.IsEnabled = true;
            }
        }

        private async void SaveNote_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedNote == null) return;

            try
            {
                _selectedNote.Title = TitleTextBox.Text.Trim();
                _selectedNote.Content = ContentTextBox.Text;
                _selectedNote.Tags = TagsTextBox.Text.Trim();
                _selectedNote.ModifiedDate = DateTime.Now;

                bool success;
                if (_selectedNote.Id == 0)
                {
                    success = await _noteService.AddNoteAsync(_selectedNote);
                }
                else
                {
                    success = await _noteService.UpdateNoteAsync(_selectedNote);
                }

                if (success)
                {
                    LoadNotes();
                    MessageBox.Show("Nota guardada com sucesso!", "Sucesso",
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Erro ao guardar nota.", "Erro",
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro: {ex.Message}", "Erro",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void DeleteNote_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedNote == null || _selectedNote.Id == 0) return;

            var result = MessageBox.Show("Tem a certeza que quer eliminar esta nota?",
                                       "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await _noteService.DeleteNoteAsync(_selectedNote.Id);
                    LoadNotes();

                    // Limpar campos
                    TitleTextBox.Text = "";
                    ContentTextBox.Text = "";
                    TagsTextBox.Text = "";
                    TitleTextBox.IsEnabled = false;
                    ContentTextBox.IsEnabled = false;
                    TagsTextBox.IsEnabled = false;
                    SaveButton.IsEnabled = false;
                    DeleteButton.IsEnabled = false;

                    _selectedNote = null;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Erro ao eliminar nota: {ex.Message}", "Erro",
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void AddBulletPoint_Click(object sender, RoutedEventArgs e)
        {
            var currentText = ContentTextBox.Text;
            var caretIndex = ContentTextBox.CaretIndex;

            var bulletPoint = "\n• ";
            var newText = currentText.Insert(caretIndex, bulletPoint);

            ContentTextBox.Text = newText;
            ContentTextBox.CaretIndex = caretIndex + bulletPoint.Length;
            ContentTextBox.Focus();
        }
    }
}
