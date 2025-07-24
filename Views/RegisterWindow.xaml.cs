using System;
using System.Windows;
using FinanceManager.Services;

namespace FinanceManager.Views
{
    public partial class RegisterWindow : Window
    {
        private readonly UserService _userService;

        public RegisterWindow()
        {
            InitializeComponent();
            _userService = new UserService();

            // Focar no primeiro campo
            UsernameTextBox.Focus();
        }

        private async void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            // Validações
            if (string.IsNullOrWhiteSpace(UsernameTextBox.Text))
            {
                ShowStatus("❌ Por favor, insira um username.", false);
                UsernameTextBox.Focus();
                return;
            }

            if (UsernameTextBox.Text.Length < 3)
            {
                ShowStatus("❌ O username deve ter pelo menos 3 caracteres.", false);
                UsernameTextBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(EmailTextBox.Text))
            {
                ShowStatus("❌ Por favor, insira um email.", false);
                EmailTextBox.Focus();
                return;
            }

            // Validação básica de email
            if (!EmailTextBox.Text.Contains("@") || !EmailTextBox.Text.Contains("."))
            {
                ShowStatus("❌ Por favor, insira um email válido.", false);
                EmailTextBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(PasswordBox.Password))
            {
                ShowStatus("❌ Por favor, insira uma password.", false);
                PasswordBox.Focus();
                return;
            }

            if (PasswordBox.Password.Length < 4)
            {
                ShowStatus("❌ A password deve ter pelo menos 4 caracteres.", false);
                PasswordBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(ConfirmPasswordBox.Password))
            {
                ShowStatus("❌ Por favor, confirme a password.", false);
                ConfirmPasswordBox.Focus();
                return;
            }

            if (PasswordBox.Password != ConfirmPasswordBox.Password)
            {
                ShowStatus("❌ As passwords não coincidem.", false);
                ConfirmPasswordBox.Focus();
                return;
            }

            try
            {
                // Desabilitar controlos durante o processo
                SetControlsEnabled(false);
                ShowStatus("⏳ A criar conta...", true);

                // Tentar registar o utilizador
                var success = await _userService.RegisterAsync(
                    UsernameTextBox.Text.Trim(),
                    EmailTextBox.Text.Trim(),
                    PasswordBox.Password);

                if (success)
                {
                    ShowStatus("✅ Conta criada com sucesso!", true);

                    // Mostrar mensagem de sucesso detalhada
                    var result = MessageBox.Show(
                        $"🎉 Parabéns!\n\n" +
                        $"A sua conta foi criada com sucesso:\n\n" +
                        $"👤 Username: {UsernameTextBox.Text}\n" +
                        $"📧 Email: {EmailTextBox.Text}\n\n" +
                        $"Pode agora fazer login com as suas credenciais.\n\n" +
                        $"Deseja fazer login agora?",
                        "Registo Concluído com Sucesso!",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    // Fechar janela de registo
                    this.DialogResult = true;
                    this.Close();
                }
                else
                {
                    ShowStatus("❌ Este username já existe. Escolha outro.", false);
                    UsernameTextBox.Focus();
                    UsernameTextBox.SelectAll();
                }
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("email"))
                {
                    ShowStatus("❌ Este email já está registado.", false);
                    EmailTextBox.Focus();
                    EmailTextBox.SelectAll();
                }
                else
                {
                    ShowStatus($"❌ Erro: {ex.Message}", false);
                }
            }
            finally
            {
                // Reabilitar controlos
                SetControlsEnabled(true);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Tem a certeza que quer cancelar o registo?",
                "Cancelar Registo",
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
            UsernameTextBox.IsEnabled = enabled;
            EmailTextBox.IsEnabled = enabled;
            PasswordBox.IsEnabled = enabled;
            ConfirmPasswordBox.IsEnabled = enabled;
            RegisterButton.IsEnabled = enabled;
            CancelButton.IsEnabled = enabled;

            // Mudar texto do botão durante processo
            if (!enabled)
            {
                RegisterButton.Content = "⏳ A Criar...";
            }
            else
            {
                RegisterButton.Content = "✅ Registar";
            }
        }

        private void ShowStatus(string message, bool isSuccess)
        {
            StatusTextBlock.Text = message;
            StatusTextBlock.Foreground = isSuccess ?
                System.Windows.Media.Brushes.Green :
                System.Windows.Media.Brushes.Red;
        }

        // Permitir Enter para submeter
        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter && RegisterButton.IsEnabled)
            {
                RegisterButton_Click(sender, e);
            }
            else if (e.Key == System.Windows.Input.Key.Escape)
            {
                CancelButton_Click(sender, e);
            }
        }

        // Auto-focar nos campos seguintes
        private void UsernameTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
                EmailTextBox.Focus();
        }

        private void EmailTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
                PasswordBox.Focus();
        }

        private void PasswordBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
                ConfirmPasswordBox.Focus();
        }

        private void ConfirmPasswordBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
                RegisterButton_Click(sender, e);
        }

        // Método para voltar ao login
        private void BackToLogin_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}