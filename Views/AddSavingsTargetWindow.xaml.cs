using System;
using System.Globalization;
using System.Windows;
using FinanceManager.Models;
using FinanceManager.Services;

namespace FinanceManager.Views
{
    public partial class AddSavingsTargetWindow : Window
    {
        private readonly User _currentUser;
        private readonly SavingsService _savingsService;

        public AddSavingsTargetWindow(User currentUser)
        {
            InitializeComponent();
            _currentUser = currentUser;
            _savingsService = new SavingsService();

            // Definir data padrão
            StartDatePicker.SelectedDate = DateTime.Now;

            // Focar no primeiro campo
            NameTextBox.Focus();
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            // Validações
            if (string.IsNullOrWhiteSpace(NameTextBox.Text))
            {
                MessageBox.Show("Por favor, insira um nome para o objetivo.", "Campo Obrigatório",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                NameTextBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(TargetAmountTextBox.Text))
            {
                MessageBox.Show("Por favor, insira o valor meta.", "Campo Obrigatório",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                TargetAmountTextBox.Focus();
                return;
            }

            if (!decimal.TryParse(TargetAmountTextBox.Text.Replace(",", "."),
                NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture,
                out decimal targetAmount) || targetAmount <= 0)
            {
                MessageBox.Show("Por favor, insira um valor meta válido maior que zero.\nExemplo: 1500,00",
                              "Valor Inválido", MessageBoxButton.OK, MessageBoxImage.Warning);
                TargetAmountTextBox.Focus();
                TargetAmountTextBox.SelectAll();
                return;
            }

            decimal currentAmount = 0;
            if (!string.IsNullOrWhiteSpace(CurrentAmountTextBox.Text))
            {
                if (!decimal.TryParse(CurrentAmountTextBox.Text.Replace(",", "."),
                    NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture,
                    out currentAmount) || currentAmount < 0)
                {
                    MessageBox.Show("Por favor, insira um valor atual válido.\nExemplo: 250,00",
                                  "Valor Inválido", MessageBoxButton.OK, MessageBoxImage.Warning);
                    CurrentAmountTextBox.Focus();
                    CurrentAmountTextBox.SelectAll();
                    return;
                }
            }

            if (!StartDatePicker.SelectedDate.HasValue)
            {
                MessageBox.Show("Por favor, selecione uma data de início.", "Campo Obrigatório",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                StartDatePicker.Focus();
                return;
            }

            if (EndDatePicker.SelectedDate.HasValue &&
                EndDatePicker.SelectedDate.Value < StartDatePicker.SelectedDate.Value)
            {
                MessageBox.Show("A data limite deve ser posterior à data de início.", "Data Inválida",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                EndDatePicker.Focus();
                return;
            }

            try
            {
                // Desabilitar botão durante o processo
                SaveButton.IsEnabled = false;
                SaveButton.Content = "⏳ A guardar...";

                var savingsTarget = new SavingsTarget
                {
                    Name = NameTextBox.Text.Trim(),
                    TargetAmount = targetAmount,
                    CurrentAmount = currentAmount,
                    StartDate = StartDatePicker.SelectedDate.Value,
                    EndDate = EndDatePicker.SelectedDate,
                    IsCompleted = currentAmount >= targetAmount,
                    UserId = _currentUser.Id
                };

                var success = await _savingsService.AddSavingsTargetAsync(savingsTarget);

                if (success)
                {
                    MessageBox.Show(
                        $"✅ Objetivo '{savingsTarget.Name}' criado com sucesso!\n\n" +
                        $"Meta: {savingsTarget.TargetAmount:C}\n" +
                        $"Valor Atual: {savingsTarget.CurrentAmount:C}",
                        "Objetivo Criado",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    this.DialogResult = true;
                    this.Close();
                }
                else
                {
                    MessageBox.Show("Erro ao criar objetivo de poupança.", "Erro",
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
                // Reabilitar botão
                SaveButton.IsEnabled = true;
                SaveButton.Content = "✅ Guardar";
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}