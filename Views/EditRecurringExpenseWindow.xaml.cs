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
    public partial class EditRecurringExpenseWindow : Window
    {
        private readonly User _currentUser;
        private readonly RecurringExpense _originalExpense;
        private readonly RecurringExpenseService _recurringExpenseService;

        public EditRecurringExpenseWindow(User currentUser, RecurringExpense expense)
        {
            InitializeComponent();
            _currentUser = currentUser;
            _originalExpense = expense;
            _recurringExpenseService = new RecurringExpenseService();

            InitializeWindow();
            LoadExpenseData();
        }

        private void InitializeWindow()
        {
            // Configurar validação de números
            AmountTextBox.PreviewTextInput += AmountTextBox_PreviewTextInput;

            // Focar no primeiro campo quando a janela carregar
            this.Loaded += (s, e) => DescriptionTextBox.Focus();

            // Configurar atalhos de teclado
            this.KeyDown += EditRecurringExpenseWindow_KeyDown;
        }

        private void LoadExpenseData()
        {
            // Preencher campos com dados existentes
            DescriptionTextBox.Text = _originalExpense.Description;
            AmountTextBox.Text = _originalExpense.Amount.ToString("F2", CultureInfo.InvariantCulture).Replace('.', ',');
            StartDatePicker.SelectedDate = _originalExpense.StartDate;
            EndDatePicker.SelectedDate = _originalExpense.EndDate;
            NotesTextBox.Text = _originalExpense.Notes ?? "";
            IsActiveCheckBox.IsChecked = _originalExpense.IsActive;

            // Selecionar categoria
            var categoryToFind = _originalExpense.Category;
            foreach (ComboBoxItem item in CategoryComboBox.Items)
            {
                var itemText = item.Content.ToString();
                var categoryName = ExtractCategoryName(itemText);
                if (categoryName.Equals(categoryToFind, StringComparison.OrdinalIgnoreCase))
                {
                    CategoryComboBox.SelectedItem = item;
                    break;
                }
            }

            // Selecionar frequência
            var frequencyToFind = _originalExpense.Frequency;
            foreach (ComboBoxItem item in FrequencyComboBox.Items)
            {
                if (item.Tag.ToString().Equals(frequencyToFind, StringComparison.OrdinalIgnoreCase))
                {
                    FrequencyComboBox.SelectedItem = item;
                    break;
                }
            }

            // Atualizar informações adicionais
            UpdateAdditionalInfo();

            // Atualizar título da janela e cabeçalho
            this.Title = $"Editar: {_originalExpense.Description}";
            ExpenseInfoText.Text = $"ID: {_originalExpense.Id} • Criada em {_originalExpense.CreatedAt:dd/MM/yyyy}";
        }

        private void UpdateAdditionalInfo()
        {
            var culture = new CultureInfo("pt-PT");

            CreatedDateText.Text = $"Criada em: {_originalExpense.CreatedAt:dd/MM/yyyy 'às' HH:mm}";

            if (_originalExpense.UpdatedAt.HasValue)
            {
                LastModifiedText.Text = $"Última modificação: {_originalExpense.UpdatedAt.Value:dd/MM/yyyy 'às' HH:mm}";
            }
            else
            {
                LastModifiedText.Text = "Última modificação: Nunca modificada";
            }

            if (_originalExpense.NextOccurrence.HasValue)
            {
                NextOccurrenceText.Text = $"Próxima ocorrência: {_originalExpense.NextOccurrence.Value:dd/MM/yyyy}";
            }
            else
            {
                NextOccurrenceText.Text = "Próxima ocorrência: Não aplicável (inativa ou expirada)";
            }
        }

        private void AmountTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            var newText = textBox.Text.Insert(textBox.CaretIndex, e.Text);

            // Permitir apenas números, vírgula e ponto
            if (!IsValidDecimalInput(newText))
            {
                e.Handled = true;
            }
        }

        private bool IsValidDecimalInput(string text)
        {
            if (string.IsNullOrEmpty(text)) return true;

            // Substituir vírgula por ponto para validação uniforme
            var normalizedText = text.Replace(',', '.');

            // Verificar se é um número decimal válido
            return decimal.TryParse(normalizedText, NumberStyles.AllowDecimalPoint,
                                  CultureInfo.InvariantCulture, out _);
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            // Validação de descrição
            if (string.IsNullOrWhiteSpace(DescriptionTextBox.Text))
            {
                ShowValidationError("Por favor, insira uma descrição para a despesa recorrente.", DescriptionTextBox);
                return;
            }

            // Validação de valor
            if (string.IsNullOrWhiteSpace(AmountTextBox.Text))
            {
                ShowValidationError("Por favor, insira o valor da despesa recorrente.", AmountTextBox);
                return;
            }

            // Converter valor (aceitar vírgula ou ponto)
            var amountText = AmountTextBox.Text.Replace(',', '.');
            if (!decimal.TryParse(amountText, NumberStyles.AllowDecimalPoint,
                                CultureInfo.InvariantCulture, out decimal amount) || amount <= 0)
            {
                ShowValidationError("Por favor, insira um valor válido maior que zero.\nExemplo: 450,50 ou 450.50", AmountTextBox);
                return;
            }

            // Validação de categoria
            if (CategoryComboBox.SelectedItem == null)
            {
                ShowValidationError("Por favor, selecione uma categoria.", CategoryComboBox);
                return;
            }

            // Validação de frequência
            if (FrequencyComboBox.SelectedItem == null)
            {
                ShowValidationError("Por favor, selecione uma frequência.", FrequencyComboBox);
                return;
            }

            // Validação de data de início
            if (!StartDatePicker.SelectedDate.HasValue)
            {
                ShowValidationError("Por favor, selecione uma data de início.", StartDatePicker);
                return;
            }

            // Validação de data de fim (se preenchida)
            if (EndDatePicker.SelectedDate.HasValue &&
                EndDatePicker.SelectedDate.Value < StartDatePicker.SelectedDate.Value)
            {
                ShowValidationError("A data de fim deve ser posterior à data de início.", EndDatePicker);
                return;
            }

            try
            {
                // Desabilitar controlos durante o processo
                SetControlsEnabled(false);

                // Extrair categoria (remover emoji se existir)
                var selectedCategory = (ComboBoxItem)CategoryComboBox.SelectedItem;
                var categoryText = selectedCategory.Content.ToString();
                var category = ExtractCategoryName(categoryText);

                // Extrair frequência
                var selectedFrequency = (ComboBoxItem)FrequencyComboBox.SelectedItem;
                var frequency = selectedFrequency.Tag.ToString();

                // Verificar se houve alterações
                var hasChanges = HasChanges(amount, category, frequency);

                if (!hasChanges)
                {
                    var result = MessageBox.Show(
                        "Nenhuma alteração foi detectada. Quer mesmo guardar?",
                        "Nenhuma Alteração",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.No)
                    {
                        SetControlsEnabled(true);
                        return;
                    }
                }

                // Atualizar despesa recorrente
                var updatedExpense = new RecurringExpense
                {
                    Id = _originalExpense.Id,
                    Description = DescriptionTextBox.Text.Trim(),
                    Amount = amount,
                    Category = category,
                    StartDate = StartDatePicker.SelectedDate.Value,
                    EndDate = EndDatePicker.SelectedDate,
                    Frequency = frequency,
                    Notes = NotesTextBox.Text.Trim(),
                    UserId = _currentUser.Id,
                    IsActive = IsActiveCheckBox.IsChecked ?? true,
                    CreatedAt = _originalExpense.CreatedAt
                };

                // Guardar na base de dados
                var success = await _recurringExpenseService.UpdateRecurringExpenseAsync(updatedExpense);

                if (success)
                {
                    ShowSuccessMessage(updatedExpense, hasChanges);
                    this.DialogResult = true;
                    this.Close();
                }
                else
                {
                    ShowErrorMessage("Erro ao guardar as alterações. Tente novamente.");
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Erro inesperado: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Erro ao atualizar despesa recorrente: {ex}");
            }
            finally
            {
                SetControlsEnabled(true);
            }
        }

        private bool HasChanges(decimal amount, string category, string frequency)
        {
            return _originalExpense.Description != DescriptionTextBox.Text.Trim() ||
                   _originalExpense.Amount != amount ||
                   _originalExpense.Category != category ||
                   _originalExpense.StartDate != StartDatePicker.SelectedDate.Value ||
                   _originalExpense.EndDate != EndDatePicker.SelectedDate ||
                   _originalExpense.Frequency != frequency ||
                   _originalExpense.Notes != (NotesTextBox.Text.Trim() ?? "") ||
                   _originalExpense.IsActive != (IsActiveCheckBox.IsChecked ?? true);
        }

        private string ExtractCategoryName(string categoryWithEmoji)
        {
            if (string.IsNullOrEmpty(categoryWithEmoji)) return "Outros";

            // Remover emoji (assumindo que está no início) e espaços extra
            var parts = categoryWithEmoji.Split(' ');
            if (parts.Length > 1)
            {
                // Juntar todas as partes exceto a primeira (que é o emoji)
                return string.Join(" ", parts, 1, parts.Length - 1).Trim();
            }

            // Se não há espaços, retornar como está
            return categoryWithEmoji.Trim();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Tem a certeza que quer cancelar?\nTodas as alterações serão perdidas.",
                "Confirmar Cancelamento",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                this.DialogResult = false;
                this.Close();
            }
        }

        private void SetControlsEnabled(bool enabled)
        {
            DescriptionTextBox.IsEnabled = enabled;
            AmountTextBox.IsEnabled = enabled;
            CategoryComboBox.IsEnabled = enabled;
            FrequencyComboBox.IsEnabled = enabled;
            StartDatePicker.IsEnabled = enabled;
            EndDatePicker.IsEnabled = enabled;
            NotesTextBox.IsEnabled = enabled;
            IsActiveCheckBox.IsEnabled = enabled;

            if (!enabled)
            {
                StatusMessage.Text = "⏳ A guardar alterações...";
                StatusMessage.Foreground = System.Windows.Media.Brushes.Blue;
                this.Cursor = Cursors.Wait;
            }
            else
            {
                StatusMessage.Text = "";
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

        private void ShowSuccessMessage(RecurringExpense expense, bool hasChanges)
        {
            var culture = new CultureInfo("pt-PT");

            var frequencyText = expense.Frequency switch
            {
                "Daily" => "Diária",
                "Weekly" => "Semanal",
                "Monthly" => "Mensal",
                "Quarterly" => "Trimestral",
                "Yearly" => "Anual",
                _ => expense.Frequency
            };

            var statusText = expense.IsActive ? "Ativa" : "Inativa";
            var changeText = hasChanges ? "atualizada" : "verificada";

            MessageBox.Show(
                $"✅ Despesa recorrente {changeText} com sucesso!\n\n" +
                $"📝 Descrição: {expense.Description}\n" +
                $"💶 Valor: {expense.Amount.ToString("C", culture)}\n" +
                $"🏷️ Categoria: {expense.Category}\n" +
                $"🔄 Frequência: {frequencyText}\n" +
                $"📊 Status: {statusText}",
                "Alterações Guardadas",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        // Atalhos de teclado
        private void EditRecurringExpenseWindow_KeyDown(object sender, KeyEventArgs e)
        {
            // Ctrl+S para guardar
            if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
            {
                Save_Click(this, e);
                e.Handled = true;
                return;
            }

            // Escape para cancelar
            if (e.Key == Key.Escape)
            {
                Cancel_Click(this, e);
                e.Handled = true;
                return;
            }
        }
    }
}