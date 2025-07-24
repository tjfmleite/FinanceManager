using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using FinanceManager.Services;
using FinanceManager.Helpers;

namespace FinanceManager.Views
{
    public partial class LoginWindow : Window
    {
        private readonly UserService _userService;
        private bool _isProcessing = false;

        public LoginWindow()
        {
            InitializeComponent();
            _userService = new UserService();

            InitializeWindow();
        }

        private void InitializeWindow()
        {
            // Permitir arrastar a janela
            this.MouseLeftButtonDown += (s, e) => this.DragMove();

            // Focar no campo username quando a janela carrega
            this.Loaded += (s, e) => UsernameTextBox.Focus();

            // Configurar eventos do UsernameTextBox para placeholder
            UsernameTextBox.TextChanged += UsernameTextBox_TextChanged;
            UsernameTextBox.GotFocus += UsernameTextBox_GotFocus;
            UsernameTextBox.LostFocus += UsernameTextBox_LostFocus;

            // Configurar eventos do PasswordBox para placeholder
            PasswordBox.PasswordChanged += PasswordBox_PasswordChanged;
            PasswordBox.GotFocus += PasswordBox_GotFocus;
            PasswordBox.LostFocus += PasswordBox_LostFocus;

            // Configurar animação de entrada
            this.Opacity = 0;
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(800));
            fadeIn.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            this.BeginAnimation(UIElement.OpacityProperty, fadeIn);

            // Verificar estado da base de dados (SEM mostrar mensagem)
            CheckDatabaseStatus();
        }

        private void UsernameTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UsernamePlaceholder.Visibility = string.IsNullOrEmpty(UsernameTextBox.Text) ?
                Visibility.Visible : Visibility.Collapsed;
        }

        private void UsernameTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            UsernamePlaceholder.Visibility = Visibility.Collapsed;
        }

        private void UsernameTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(UsernameTextBox.Text))
            {
                UsernamePlaceholder.Visibility = Visibility.Visible;
            }
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            PasswordPlaceholder.Visibility = string.IsNullOrEmpty(PasswordBox.Password) ?
                Visibility.Visible : Visibility.Collapsed;
        }

        private void PasswordBox_GotFocus(object sender, RoutedEventArgs e)
        {
            PasswordPlaceholder.Visibility = Visibility.Collapsed;
        }

        private void PasswordBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(PasswordBox.Password))
            {
                PasswordPlaceholder.Visibility = Visibility.Visible;
            }
        }

        private async void CheckDatabaseStatus()
        {
            try
            {
                // Verificar BD silenciosamente - SEM mostrar mensagem ao utilizador
                var userCount = await _userService.GetTotalUsersCountAsync();

                // Log apenas para debug, não mostrar ao utilizador
                LoggingService.LogInfo($"BD verificada: {userCount} utilizadores encontrados");

                // Mostrar apenas mensagem neutra de boas-vindas com cor mais visível
                ShowStatus("Bem-vindo ao Finance Manager", Brushes.DarkSlateBlue);
            }
            catch (Exception ex)
            {
                // Log do erro mas não mostrar ao utilizador
                LoggingService.LogError("Erro ao verificar BD", ex);

                // Mostrar apenas mensagem genérica com cor visível
                ShowStatus("Sistema a inicializar...", Brushes.DarkSlateGray);
            }
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isProcessing) return;

            // Validações básicas
            if (string.IsNullOrWhiteSpace(UsernameTextBox.Text))
            {
                ShowStatus("Por favor, insira o username.", Brushes.LightCoral);
                UsernameTextBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(PasswordBox.Password))
            {
                ShowStatus("Por favor, insira a password.", Brushes.LightCoral);
                PasswordBox.Focus();
                return;
            }

            await PerformLogin();
        }

        private async Task PerformLogin()
        {
            try
            {
                _isProcessing = true;
                SetControlsEnabled(false);
                ShowStatus("A verificar credenciais...", Brushes.LightBlue);

                // Simular um pequeno delay para melhor UX
                await Task.Delay(500);

                var user = await _userService.LoginAsync(
                    UsernameTextBox.Text.Trim(),
                    PasswordBox.Password);

                if (user != null)
                {
                    ShowStatus("Login bem-sucedido! A abrir aplicação...", Brushes.LightGreen);

                    // Delay para mostrar mensagem de sucesso
                    await Task.Delay(1000);

                    // Animação de saída
                    var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
                    fadeOut.Completed += (s, e) =>
                    {
                        // Abrir janela principal
                        var mainWindow = new MainWindow(user);
                        mainWindow.Show();
                        this.Close();
                    };
                    this.BeginAnimation(UIElement.OpacityProperty, fadeOut);
                }
                else
                {
                    ShowStatus("Username ou password incorretos.", Brushes.LightCoral);

                    // Animação de erro (tremor)
                    await AnimateLoginError();

                    PasswordBox.Clear();
                    PasswordBox.Focus();
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Erro: {ex.Message}", Brushes.LightCoral);
                LoggingService.LogError("Erro no login", ex);
            }
            finally
            {
                _isProcessing = false;
                SetControlsEnabled(true);
            }
        }

        private async Task AnimateLoginError()
        {
            var originalMargin = this.Margin;

            for (int i = 0; i < 3; i++)
            {
                var shakeRight = new ThicknessAnimation(originalMargin,
                    new Thickness(originalMargin.Left + 8, originalMargin.Top,
                                 originalMargin.Right - 8, originalMargin.Bottom),
                    TimeSpan.FromMilliseconds(50));

                var shakeLeft = new ThicknessAnimation(
                    new Thickness(originalMargin.Left + 8, originalMargin.Top,
                                 originalMargin.Right - 8, originalMargin.Bottom),
                    new Thickness(originalMargin.Left - 8, originalMargin.Top,
                                 originalMargin.Right + 8, originalMargin.Bottom),
                    TimeSpan.FromMilliseconds(100));

                var shakeBack = new ThicknessAnimation(
                    new Thickness(originalMargin.Left - 8, originalMargin.Top,
                                 originalMargin.Right + 8, originalMargin.Bottom),
                    originalMargin,
                    TimeSpan.FromMilliseconds(50));

                this.BeginAnimation(FrameworkElement.MarginProperty, shakeRight);
                await Task.Delay(50);
                this.BeginAnimation(FrameworkElement.MarginProperty, shakeLeft);
                await Task.Delay(100);
                this.BeginAnimation(FrameworkElement.MarginProperty, shakeBack);
                await Task.Delay(50);
            }
        }

        private void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LoggingService.LogInfo("Abrindo janela de registo");

                var registerWindow = new RegisterWindow();
                registerWindow.Owner = this;

                // Animação de fade out temporário
                var fadeOut = new DoubleAnimation(1, 0.3, TimeSpan.FromMilliseconds(300));
                this.BeginAnimation(UIElement.OpacityProperty, fadeOut);

                var result = registerWindow.ShowDialog();

                // Animação de fade in de volta
                var fadeIn = new DoubleAnimation(0.3, 1, TimeSpan.FromMilliseconds(300));
                this.BeginAnimation(UIElement.OpacityProperty, fadeIn);

                if (result == true)
                {
                    ShowStatus("Conta criada com sucesso! Pode fazer login agora.", Brushes.LightGreen);

                    // Limpar campos para novo login
                    UsernameTextBox.Clear();
                    PasswordBox.Clear();
                    UsernameTextBox.Focus();
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao abrir janela de registo", ex);
                ShowStatus($"Erro ao abrir registo: {ex.Message}", Brushes.LightCoral);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Tem a certeza que quer sair do Finance Manager?",
                "Confirmar Saída",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                Application.Current.Shutdown();
            }
        }

        private void SetControlsEnabled(bool enabled)
        {
            UsernameTextBox.IsEnabled = enabled;
            PasswordBox.IsEnabled = enabled;
            LoginButton.IsEnabled = enabled;
            RegisterButton.IsEnabled = enabled;

            if (!enabled)
            {
                LoginButton.Content = "A entrar...";
                this.Cursor = Cursors.Wait;
            }
            else
            {
                LoginButton.Content = "🚀 Entrar";
                this.Cursor = Cursors.Arrow;
            }
        }

        private void ShowStatus(string message, Brush color)
        {
            StatusTextBlock.Text = message;
            StatusTextBlock.Foreground = color;

            // Animação de fade in para a mensagem
            StatusTextBlock.Opacity = 0;
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(400));
            StatusTextBlock.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        }

        // Navegação por teclado
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter:
                    if (!_isProcessing && LoginButton.IsEnabled)
                    {
                        LoginButton_Click(sender, e);
                    }
                    break;

                case Key.Escape:
                    CloseButton_Click(sender, e);
                    break;

                case Key.Tab:
                    // Navegação personalizada com Tab
                    if (UsernameTextBox.IsFocused)
                    {
                        PasswordBox.Focus();
                        e.Handled = true;
                    }
                    else if (PasswordBox.IsFocused)
                    {
                        LoginButton.Focus();
                        e.Handled = true;
                    }
                    else if (LoginButton.IsFocused)
                    {
                        RegisterButton.Focus();
                        e.Handled = true;
                    }
                    else if (RegisterButton.IsFocused)
                    {
                        UsernameTextBox.Focus();
                        e.Handled = true;
                    }
                    break;
            }
        }
    }
}