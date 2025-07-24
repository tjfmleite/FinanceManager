using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FinanceManager.Models;
using FinanceManager.Services;

namespace FinanceManager.Views
{
    public partial class AddIncomeWindow : Window
    {
        private readonly User _currentUser;
        private readonly IncomeService _incomeService;

        public AddIncomeWindow(User currentUser)
        {
            InitializeComponent();
            _currentUser = currentUser;
            _incomeService = new IncomeService();

            InitializeWindow();
        }

        private void InitializeWindow()
        {
            // Definir data padrão como hoje
            DatePicker.SelectedDate = DateTime.Today;

            // Definir categoria padrão
            CategoryComboBox.SelectedIndex = 0;

            // Configurar validação de números
            AmountTextBox.PreviewTextInput += AmountTextBox_PreviewTextInput;

            // Focar no primeiro campo quando a janela carregar
            this.Loaded += (s, e) => DescriptionTextBox.Focus();
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
                ShowValidationError("Por favor, insira uma descrição para a receita.", DescriptionTextBox);
                return;
            }

            // Validação de valor
            if (string.IsNullOrWhiteSpace(AmountTextBox.Text))
            {
                ShowValidationError("Por favor, insira o valor da receita.", AmountTextBox);
                return;
            }

            // Converter valor (aceitar vírgula ou ponto)
            var amountText = AmountTextBox.Text.Replace(',', '.');
            if (!decimal.TryParse(amountText, NumberStyles.AllowDecimalPoint,
                                CultureInfo.InvariantCulture, out decimal amount) || amount <= 0)
            {
                ShowValidationError("Por favor, insira um valor válido maior que zero.\nExemplo: 1250,50 ou 1250.50", AmountTextBox);
                return;
            }

            // Validação de categoria
            if (CategoryComboBox.SelectedItem == null)
            {
                ShowValidationError("Por favor, selecione uma categoria.", CategoryComboBox);
                return;
            }

            // Validação de data
            if (!DatePicker.SelectedDate.HasValue)
            {
                ShowValidationError("Por favor, selecione uma data.", DatePicker);
                return;
            }

            // Verificar se a data não é no futuro
            if (DatePicker.SelectedDate.Value.Date > DateTime.Today)
            {
                ShowValidationError("A data não pode ser no futuro.", DatePicker);
                return;
            }

            try
            {
                // Desabilitar controlos durante o processo
                SetControlsEnabled(false);

                // Extrair categoria (remover emoji se existir)
                var selectedItem = (ComboBoxItem)CategoryComboBox.SelectedItem;
                var categoryText = selectedItem.Content.ToString();
                var category = ExtractCategoryName(categoryText);

                // Criar receita
                var income = new Income
                {
                    Description = DescriptionTextBox.Text.Trim(),
                    Amount = amount,
                    Category = category,
                    Date = DatePicker.SelectedDate.Value,
                    Notes = NotesTextBox.Text.Trim(),
                    UserId = _currentUser.Id
                };

                // Guardar na base de dados
                var success = await _incomeService.AddIncomeAsync(income);

                if (success)
                {
                    ShowSuccessMessage(income);
                    this.DialogResult = true;
                    this.Close();
                }
                else
                {
                    ShowErrorMessage("Erro ao guardar a receita. Tente novamente.");
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Erro inesperado: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Erro ao guardar receita: {ex}");
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
            DatePicker.IsEnabled = enabled;
            NotesTextBox.IsEnabled = enabled;

            if (!enabled)
            {
                StatusMessage.Text = "⏳ A guardar receita...";
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

        private void ShowSuccessMessage(Income income)
        {
            // Configurar cultura portuguesa para euros
            var culture = new CultureInfo("pt-PT");

            MessageBox.Show(
                $"✅ Receita registada com sucesso!\n\n" +
                $"📝 Descrição: {income.Description}\n" +
                $"💶 Valor: {income.Amount.ToString("C", culture)}\n" +
                $"🏷️ Categoria: {income.Category}\n" +
                $"📅 Data: {income.Date:dd/MM/yyyy}",
                "Receita Adicionada",
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

        // Atalhos de teclado
        protected override void OnKeyDown(KeyEventArgs e)
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

            base.OnKeyDown(e);
        }
    }
}