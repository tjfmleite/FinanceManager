using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FinanceManager.Models;
using FinanceManager.Services;

namespace FinanceManager.Views
{
    public partial class AddRecurringExpenseWindow : Window
    {
        private readonly User _currentUser;
        private readonly RecurringExpenseService _recurringExpenseService;

        public AddRecurringExpenseWindow(User currentUser)
        {
            InitializeComponent();
            _currentUser = currentUser;
            _recurringExpenseService = new RecurringExpenseService();

            InitializeWindow();
        }

        private void InitializeWindow()
        {
            // Definir data padrão como hoje
            StartDatePicker.SelectedDate = DateTime.Today;

            // Definir categoria padrão
            CategoryComboBox.SelectedIndex = 0;

            // Definir frequência padrão como mensal
            FrequencyComboBox.SelectedIndex = 2; // Monthly

            // Configurar validação de números
            AmountTextBox.PreviewTextInput += AmountTextBox_PreviewTextInput;

            // Focar no primeiro campo quando a janela carregar
            this.Loaded += (s, e) => DescriptionTextBox.Focus();

            // Configurar atalhos de teclado
            this.KeyDown += AddRecurringExpenseWindow_KeyDown;
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

        private async void Add_Click(object sender, RoutedEventArgs e)
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

                // Criar despesa recorrente
                var recurringExpense = new RecurringExpense
                {
                    Description = DescriptionTextBox.Text.Trim(),
                    Amount = amount,
                    Category = category,
                    StartDate = StartDatePicker.SelectedDate.Value,
                    EndDate = EndDatePicker.SelectedDate,
                    Frequency = frequency,
                    Notes = NotesTextBox.Text.Trim(),
                    UserId = _currentUser.Id,
                    IsActive = true
                };

                // Guardar na base de dados
                var success = await _recurringExpenseService.AddRecurringExpenseAsync(recurringExpense);

                if (success)
                {
                    ShowSuccessMessage(recurringExpense);
                    this.DialogResult = true;
                    this.Close();
                }
                else
                {
                    ShowErrorMessage("Erro ao guardar a despesa recorrente. Tente novamente.");
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Erro inesperado: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Erro ao guardar despesa recorrente: {ex}");
            }
            finally
            {
                SetControlsEnabled(true);
            }
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
                "Tem a certeza que quer cancelar?\nTodos os dados inseridos serão perdidos.",
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

            if (!enabled)
            {
                StatusMessage.Text = "⏳ A guardar despesa recorrente...";
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

        private void ShowSuccessMessage(RecurringExpense expense)
        {
            // Configurar cultura portuguesa para euros
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

            var endDateText = expense.EndDate.HasValue
                ? $"📅 Data de Fim: {expense.EndDate.Value:dd/MM/yyyy}\n"
                : "";

            MessageBox.Show(
                $"✅ Despesa recorrente criada com sucesso!\n\n" +
                $"📝 Descrição: {expense.Description}\n" +
                $"💶 Valor: {expense.Amount.ToString("C", culture)}\n" +
                $"🏷️ Categoria: {expense.Category}\n" +
                $"🔄 Frequência: {frequencyText}\n" +
                $"📅 Data de Início: {expense.StartDate:dd/MM/yyyy}\n" +
                endDateText +
                $"📊 Status: Ativa",
                "Despesa Recorrente Adicionada",
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
                FrequencyComboBox.Focus();
                e.Handled = true;
            }
        }

        private void FrequencyComboBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                StartDatePicker.Focus();
                e.Handled = true;
            }
        }

        private void StartDatePicker_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                EndDatePicker.Focus();
                e.Handled = true;
            }
        }

        private void EndDatePicker_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                NotesTextBox.Focus();
                e.Handled = true;
            }
        }

        // Atalhos de teclado
        private void AddRecurringExpenseWindow_KeyDown(object sender, KeyEventArgs e)
        {
            // Ctrl+S para guardar
            if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
            {
                Add_Click(this, e);
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