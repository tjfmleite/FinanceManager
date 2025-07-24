using System;
using System.Globalization;
using System.Windows;
using FinanceManager.Models;
using FinanceManager.Services;

namespace FinanceManager.Views
{
    public partial class UpdateSavingsProgressWindow : Window
    {
        private readonly SavingsTarget _savingsTarget;
        private readonly SavingsService _savingsService;
        private readonly CultureInfo _culture = new CultureInfo("pt-PT");

        public UpdateSavingsProgressWindow(SavingsTarget savingsTarget)
        {
            InitializeComponent();
            _savingsTarget = savingsTarget;
            _savingsService = new SavingsService();

            LoadTargetInfo();
        }

        private void LoadTargetInfo()
        {
            TargetNameText.Text = _savingsTarget.Name;
            TargetAmountText.Text = _savingsTarget.TargetAmount.ToString("C", _culture);
            CurrentAmountText.Text = _savingsTarget.CurrentAmount.ToString("C", _culture);

            var progress = _savingsTarget.ProgressPercentage;
            ProgressText.Text = $"{progress:F1}%";
            ProgressBar.Value = (double)progress; 

            var remaining = _savingsTarget.RemainingAmount;
            RemainingAmountText.Text = remaining.ToString("C", _culture);

          
            StartDateText.Text = _savingsTarget.FormattedStartDate;
            EndDateText.Text = _savingsTarget.FormattedEndDate;
            AverageSpeedText.Text = _savingsTarget.FormattedAverageDailySavings + "/dia";
            EstimatedDateText.Text = _savingsTarget.FormattedEstimatedCompletionDate;

           
            AmountToAddTextBox.Text = "0,00";
            AmountToAddTextBox.Focus();
            AmountToAddTextBox.SelectAll();

            // Ajustar cor da barra de progresso baseada no progresso
            if (_savingsTarget.IsTargetReached)
            {
                ProgressBar.Foreground = System.Windows.Media.Brushes.Green;
            }
            else if (progress >= 75)
            {
                ProgressBar.Foreground = System.Windows.Media.Brushes.Orange;
            }
            else if (progress >= 50)
            {
                ProgressBar.Foreground = System.Windows.Media.Brushes.Blue;
            }
            else
            {
                ProgressBar.Foreground = System.Windows.Media.Brushes.Gray;
            }
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            // Validação do valor a adicionar
            if (string.IsNullOrWhiteSpace(AmountToAddTextBox.Text))
            {
                MessageBox.Show("Por favor, insira o valor a adicionar.", "Campo Obrigatório",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                AmountToAddTextBox.Focus();
                return;
            }

            var amountText = AmountToAddTextBox.Text.Replace(',', '.');
            if (!decimal.TryParse(amountText, NumberStyles.AllowDecimalPoint,
                                CultureInfo.InvariantCulture, out decimal amountToAdd))
            {
                MessageBox.Show("Por favor, insira um valor válido.\nExemplo: 50,00", "Valor Inválido",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                AmountToAddTextBox.Focus();
                AmountToAddTextBox.SelectAll();
                return;
            }

            // Permitir valores negativos para remoções, mas alertar
            if (amountToAdd < 0)
            {
                var result = MessageBox.Show(
                    $"Vai remover {Math.Abs(amountToAdd).ToString("C", _culture)} do objetivo.\n\n" +
                    "Tem a certeza que quer continuar?",
                    "Confirmar Remoção",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.No)
                    return;
            }

            // Verificar se o valor resultante não ficará negativo
            var newTotal = _savingsTarget.CurrentAmount + amountToAdd;
            if (newTotal < 0)
            {
                MessageBox.Show(
                    $"O valor resultante seria negativo ({newTotal.ToString("C", _culture)}).\n\n" +
                    "Por favor, insira um valor válido.",
                    "Valor Inválido",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                AmountToAddTextBox.Focus();
                AmountToAddTextBox.SelectAll();
                return;
            }

            try
            {
                // Desabilitar controles durante o processo
                SaveButton.IsEnabled = false;
                SaveButton.Content = "⏳ A guardar...";
                CancelButton.IsEnabled = false;

                // Obter notas (opcional)
                var notes = string.IsNullOrWhiteSpace(NotesTextBox.Text) ? null : NotesTextBox.Text.Trim();

                // Atualizar progresso usando o novo método com histórico
                var success = await _savingsService.UpdateSavingsAmountAsync(_savingsTarget.Id, amountToAdd, notes);

                if (success)
                {
                    var newAmount = _savingsTarget.CurrentAmount + amountToAdd;
                    var isCompleted = newAmount >= _savingsTarget.TargetAmount;

                    var message = $"✅ Progresso atualizado com sucesso!\n\n" +
                                 $"🎯 Objetivo: {_savingsTarget.Name}\n" +
                                 $"💰 Valor adicionado: {amountToAdd.ToString("C", _culture)}\n" +
                                 $"📊 Novo total: {newAmount.ToString("C", _culture)}\n" +
                                 $"🎯 Meta: {_savingsTarget.TargetAmount.ToString("C", _culture)}";

                    if (isCompleted)
                    {
                        message += "\n\n🎉 Parabéns! Objetivo atingido!";
                    }
                    else
                    {
                        var remaining = _savingsTarget.TargetAmount - newAmount;
                        message += $"\n💼 Falta: {remaining.ToString("C", _culture)}";
                    }

                    if (!string.IsNullOrEmpty(notes))
                    {
                        message += $"\n📝 Nota: {notes}";
                    }

                    MessageBox.Show(message, "Progresso Atualizado",
                                  MessageBoxButton.OK, MessageBoxImage.Information);

                    this.DialogResult = true;
                    this.Close();
                }
                else
                {
                    MessageBox.Show("Erro ao atualizar progresso. Tente novamente.", "Erro",
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro: {ex.Message}", "Erro",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Reabilitar controles
                SaveButton.IsEnabled = true;
                SaveButton.Content = "💾 Guardar";
                CancelButton.IsEnabled = true;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        // Validação de input numérico
        private void AmountToAddTextBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            var textBox = sender as System.Windows.Controls.TextBox;
            var newText = textBox.Text.Insert(textBox.CaretIndex, e.Text);

            // Permitir números, vírgula, ponto e sinal negativo
            if (!IsValidDecimalInput(newText))
            {
                e.Handled = true;
            }
        }

        private bool IsValidDecimalInput(string text)
        {
            if (string.IsNullOrEmpty(text)) return true;

            // Permitir sinal negativo no início
            if (text == "-") return true;

            // Substituir vírgula por ponto para validação uniforme
            var normalizedText = text.Replace(',', '.');

            // Verificar se é um número decimal válido (incluindo negativos)
            return decimal.TryParse(normalizedText, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                                  CultureInfo.InvariantCulture, out _);
        }

        // Atalhos de teclado
        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            switch (e.Key)
            {
                case System.Windows.Input.Key.Enter:
                    if (SaveButton.IsEnabled)
                    {
                        Save_Click(this, e);
                        e.Handled = true;
                    }
                    break;

                case System.Windows.Input.Key.Escape:
                    Cancel_Click(this, e);
                    e.Handled = true;
                    break;

                default:
                    base.OnKeyDown(e);
                    break;
            }
        }

        // Evento quando a janela é carregada
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Garantir que o campo de valor tenha foco
            AmountToAddTextBox.Focus();
            AmountToAddTextBox.SelectAll();
        }
    }
}