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
    public partial class EditExpenseWindow : Window
    {
        private readonly User _currentUser;
        private readonly ExpenseService _expenseService;
        private readonly Expense _expense;

        public EditExpenseWindow(User currentUser, Expense expense)
        {
            InitializeComponent();
            _currentUser = currentUser;
            _expenseService = new ExpenseService();
            _expense = expense;

            InitializeWindow();
            LoadExpenseData();
        }

        private void InitializeWindow()
        {
            // Configurar validação de números
            AmountTextBox.PreviewTextInput += AmountTextBox_PreviewTextInput;

            // Focar no primeiro campo quando a janela carregar
            this.Loaded += (s, e) => DescriptionTextBox.Focus();
        }

        private void LoadExpenseData()
        {
            // Carregar dados da despesa nos controlos
            DescriptionTextBox.Text = _expense.Description;
            AmountTextBox.Text = _expense.Amount.ToString("F2");
            DatePicker.SelectedDate = _expense.Date;

            // Selecionar categoria correspondente
            SelectCategoryByName(_expense.Category);

            // Mostrar informações da despesa
            UpdateExpenseInfo();
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

        private void UpdateExpenseInfo()
        {
            ExpenseInfoText.Text = $"💡 Despesa criada em {_expense.Date:dd/MM/yyyy}\n" +
                                  $"📊 ID: {_expense.Id} | 👤 Utilizador: {_currentUser.Username}";
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
                ShowValidationError("Por favor, insira uma descrição para a despesa.", DescriptionTextBox);
                return;
            }

            if (string.IsNullOrWhiteSpace(AmountTextBox.Text))
            {
                ShowValidationError("Por favor, insira o valor da despesa.", AmountTextBox);
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

                // Atualizar dados da despesa
                _expense.Description = DescriptionTextBox.Text.Trim();
                _expense.Amount = amount;
                _expense.Category = ExtractCategoryName(((ComboBoxItem)CategoryComboBox.SelectedItem).Content.ToString());
                _expense.Date = DatePicker.SelectedDate.Value;

                var success = await _expenseService.UpdateExpenseAsync(_expense);

                if (success)
                {
                    ShowSuccessMessage();
                    this.DialogResult = true;
                    this.Close();
                }
                else
                {
                    ShowErrorMessage("Erro ao atualizar a despesa. Tente novamente.");
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Erro inesperado: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Erro ao atualizar despesa: {ex}");
            }
            finally
            {
                SetControlsEnabled(true);
            }
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                $"🗑️ Eliminar Despesa\n\n" +
                $"Tem a certeza que quer eliminar esta despesa?\n\n" +
                $"📝 {_expense.Description}\n" +
                $"💶 {_expense.Amount:C}\n" +
                $"📅 {_expense.Date:dd/MM/yyyy}\n\n" +
                $"⚠️ Esta ação não pode ser desfeita!",
                "Confirmar Eliminação",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    SetControlsEnabled(false);

                    var success = await _expenseService.DeleteExpenseAsync(_expense.Id);

                    if (success)
                    {
                        MessageBox.Show(
                            "✅ Despesa eliminada com sucesso!",
                            "Despesa Eliminada",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);

                        this.DialogResult = true;
                        this.Close();
                    }
                    else
                    {
                        ShowErrorMessage("Erro ao eliminar a despesa. Tente novamente.");
                    }
                }
                catch (Exception ex)
                {
                    ShowErrorMessage($"Erro ao eliminar despesa: {ex.Message}");
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

            return _expense.Description != DescriptionTextBox.Text.Trim() ||
                   _expense.Amount != currentAmount ||
                   _expense.Category != currentCategory ||
                   _expense.Date != DatePicker.SelectedDate;
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
            MessageBox.Show(
                $"✅ Despesa atualizada com sucesso!\n\n" +
                $"📝 Descrição: {_expense.Description}\n" +
                $"💶 Valor: {_expense.Amount:C}\n" +
                $"🏷️ Categoria: {_expense.Category}\n" +
                $"📅 Data: {_expense.Date:dd/MM/yyyy}",
                "Despesa Atualizada",
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
            if (e.Key == Key.Enter && UpdateButton.IsEnabled)
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