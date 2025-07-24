using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FinanceManager.Models;
using FinanceManager.Services;

namespace FinanceManager.Views
{
    public partial class EditIncomeWindow : Window
    {
        private readonly User _currentUser;
        private readonly IncomeService _incomeService;
        private readonly Income _income;

        public EditIncomeWindow(User currentUser, Income income)
        {
            InitializeComponent();
            _currentUser = currentUser;
            _incomeService = new IncomeService();
            _income = income;

            InitializeWindow();
            LoadIncomeData();
        }

        private void InitializeWindow()
        {
            // Configurar validação de números
            AmountTextBox.PreviewTextInput += AmountTextBox_PreviewTextInput;

            // Focar no primeiro campo quando a janela carregar
            this.Loaded += (s, e) => DescriptionTextBox.Focus();
        }

        private void LoadIncomeData()
        {
            // Carregar dados da receita nos controlos
            DescriptionTextBox.Text = _income.Description;
            AmountTextBox.Text = _income.Amount.ToString("F2");
            DatePicker.SelectedDate = _income.Date;
            NotesTextBox.Text = _income.Notes ?? "";

            // Selecionar categoria correspondente
            SelectCategoryByName(_income.Category);

            // Mostrar informações da receita
            UpdateIncomeInfo();
        }

        private void SelectCategoryByName(string categoryName)
        {
            foreach (ComboBoxItem item in CategoryComboBox.Items)
            {
                var content = item.Content.ToString();
                var extractedName = ExtractCategoryName(content);

                if (string.Equals(extractedName, categoryName, StringComparison.OrdinalIgnoreCase))
                {
                    CategoryComboBox.SelectedItem = item;
                    break;
                }
            }

            // Se não encontrou, selecionar "Outros" como padrão
            if (CategoryComboBox.SelectedItem == null)
            {
                CategoryComboBox.SelectedIndex = CategoryComboBox.Items.Count - 1; // Último item (Outros)
            }
        }

        private void UpdateIncomeInfo()
        {
            IncomeInfoText.Text = $"💡 Receita criada em {_income.Date:dd/MM/yyyy}\n" +
                                 $"📊 ID: {_income.Id} | 👤 Utilizador: {_currentUser.Username}";
        }

        private void AmountTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            var newText = textBox.Text.Insert(textBox.CaretIndex, e.Text);

            if (!IsValidDecimalInput(newText))
            {
                e.Handled = true;
            }
        }

        private bool IsValidDecimalInput(string text)
        {
            if (string.IsNullOrEmpty(text)) return true;

            var normalizedText = text.Replace(',', '.');
            return decimal.TryParse(normalizedText, NumberStyles.AllowDecimalPoint,
                                  CultureInfo.InvariantCulture, out _);
        }

        private async void Update_Click(object sender, RoutedEventArgs e)
        {
            // Validações
            if (string.IsNullOrWhiteSpace(DescriptionTextBox.Text))
            {
                ShowValidationError("Por favor, insira uma descrição para a receita.", DescriptionTextBox);
                return;
            }

            if (string.IsNullOrWhiteSpace(AmountTextBox.Text))
            {
                ShowValidationError("Por favor, insira o valor da receita.", AmountTextBox);
                return;
            }

            var amountText = AmountTextBox.Text.Replace(',', '.');
            if (!decimal.TryParse(amountText, NumberStyles.AllowDecimalPoint,
                                CultureInfo.InvariantCulture, out decimal amount) || amount <= 0)
            {
                ShowValidationError("Por favor, insira um valor válido maior que zero.", AmountTextBox);
                return;
            }

            if (CategoryComboBox.SelectedItem == null)
            {
                ShowValidationError("Por favor, selecione uma categoria.", CategoryComboBox);
                return;
            }

            if (!DatePicker.SelectedDate.HasValue)
            {
                ShowValidationError("Por favor, selecione uma data.", DatePicker);
                return;
            }

            if (DatePicker.SelectedDate.Value.Date > DateTime.Today)
            {
                ShowValidationError("A data não pode ser no futuro.", DatePicker);
                return;
            }

            try
            {
                SetControlsEnabled(false);

                // Atualizar dados da receita
                _income.Description = DescriptionTextBox.Text.Trim();
                _income.Amount = amount;
                _income.Category = ExtractCategoryName(((ComboBoxItem)CategoryComboBox.SelectedItem).Content.ToString());
                _income.Date = DatePicker.SelectedDate.Value;
                _income.Notes = string.IsNullOrWhiteSpace(NotesTextBox.Text) ? null : NotesTextBox.Text.Trim();

                var success = await _incomeService.UpdateIncomeAsync(_income);

                if (success)
                {
                    ShowSuccessMessage();
                    this.DialogResult = true;
                    this.Close();
                }
                else
                {
                    ShowErrorMessage("Erro ao atualizar a receita. Tente novamente.");
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Erro inesperado: {ex.Message}");
                LoggingService.LogError("Erro ao atualizar receita", ex);
            }
            finally
            {
                SetControlsEnabled(true);
            }
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                $"🗑️ Eliminar Receita\n\n" +
                $"Tem a certeza que quer eliminar esta receita?\n\n" +
                $"📝 {_income.Description}\n" +
                $"💶 {_income.Amount:C}\n" +
                $"📅 {_income.Date:dd/MM/yyyy}\n\n" +
                $"⚠️ Esta ação não pode ser desfeita!",
                "Confirmar Eliminação",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    SetControlsEnabled(false);

                    var success = await _incomeService.DeleteIncomeAsync(_income.Id);

                    if (success)
                    {
                        MessageBox.Show(
                            "✅ Receita eliminada com sucesso!",
                            "Receita Eliminada",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);

                        this.DialogResult = true;
                        this.Close();
                    }
                    else
                    {
                        ShowErrorMessage("Erro ao eliminar a receita. Tente novamente.");
                    }
                }
                catch (Exception ex)
                {
                    ShowErrorMessage($"Erro ao eliminar receita: {ex.Message}");
                }
                finally
                {
                    SetControlsEnabled(true);
                }
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            // Verificar se houve alterações
            if (HasChanges())
            {
                var result = MessageBox.Show(
                    "Tem alterações não guardadas.\n\nTem a certeza que quer cancelar?",
                    "Alterações Não Guardadas",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.No)
                    return;
            }

            this.DialogResult = false;
            this.Close();
        }

        private bool HasChanges()
        {
            var currentCategory = CategoryComboBox.SelectedItem != null
                ? ExtractCategoryName(((ComboBoxItem)CategoryComboBox.SelectedItem).Content.ToString())
                : "";

            var amountText = AmountTextBox.Text.Replace(',', '.');
            decimal.TryParse(amountText, NumberStyles.AllowDecimalPoint,
                           CultureInfo.InvariantCulture, out decimal currentAmount);

            var currentNotes = string.IsNullOrWhiteSpace(NotesTextBox.Text) ? null : NotesTextBox.Text.Trim();

            return _income.Description != DescriptionTextBox.Text.Trim() ||
                   _income.Amount != currentAmount ||
                   _income.Category != currentCategory ||
                   _income.Date != DatePicker.SelectedDate ||
                   _income.Notes != currentNotes;
        }

        private string ExtractCategoryName(string categoryWithEmoji)
        {
            if (string.IsNullOrEmpty(categoryWithEmoji)) return "Outros";

            var parts = categoryWithEmoji.Split(' ');
            if (parts.Length > 1)
            {
                return string.Join(" ", parts, 1, parts.Length - 1).Trim();
            }

            return categoryWithEmoji.Trim();
        }

        private void SetControlsEnabled(bool enabled)
        {
            DescriptionTextBox.IsEnabled = enabled;
            AmountTextBox.IsEnabled = enabled;
            CategoryComboBox.IsEnabled = enabled;
            DatePicker.IsEnabled = enabled;
            NotesTextBox.IsEnabled = enabled;
            UpdateButton.IsEnabled = enabled;
            DeleteButton.IsEnabled = enabled;
            CancelButton.IsEnabled = enabled;

            if (!enabled)
            {
                UpdateButton.Content = "⏳ A atualizar...";
                this.Cursor = Cursors.Wait;
            }
            else
            {
                UpdateButton.Content = "✏️ Atualizar";
                this.Cursor = Cursors.Arrow;
            }
        }

        private void ShowValidationError(string message, Control controlToFocus)
        {
            MessageBox.Show(message, "Campo Obrigatório",
                          MessageBoxButton.OK, MessageBoxImage.Warning);
            controlToFocus.Focus();

            if (controlToFocus is TextBox textBox)
            {
                textBox.SelectAll();
            }
        }

        private void ShowErrorMessage(string message)
        {
            MessageBox.Show(message, "Erro",
                          MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void ShowSuccessMessage()
        {
            var culture = new CultureInfo("pt-PT");
            MessageBox.Show(
                $"✅ Receita atualizada com sucesso!\n\n" +
                $"📝 Descrição: {_income.Description}\n" +
                $"💶 Valor: {_income.Amount.ToString("C", culture)}\n" +
                $"🏷️ Categoria: {_income.Category}\n" +
                $"📅 Data: {_income.Date:dd/MM/yyyy}",
                "Receita Atualizada",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        // Navegação por Enter entre campos
        private void DescriptionTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                AmountTextBox.Focus();
                e.Handled = true;
            }
        }

        private void AmountTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CategoryComboBox.Focus();
                e.Handled = true;
            }
        }

        private void CategoryComboBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                DatePicker.Focus();
                e.Handled = true;
            }
        }

        private void DatePicker_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                NotesTextBox.Focus();
                e.Handled = true;
            }
        }

        private void NotesTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control && UpdateButton.IsEnabled)
            {
                Update_Click(sender, e);
                e.Handled = true;
            }
        }

        // Atalhos de teclado
        protected override void OnKeyDown(KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.S when Keyboard.Modifiers == ModifierKeys.Control && UpdateButton.IsEnabled:
                    Update_Click(this, e);
                    e.Handled = true;
                    break;

                case Key.Delete when Keyboard.Modifiers == ModifierKeys.Control && DeleteButton.IsEnabled:
                    Delete_Click(this, e);
                    e.Handled = true;
                    break;

                case Key.Escape:
                    Cancel_Click(this, e);
                    e.Handled = true;
                    break;

                default:
                    base.OnKeyDown(e);
                    break;
            }
        }
    }
}