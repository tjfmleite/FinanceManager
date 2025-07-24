using System;
using System.Windows;
using System.Windows.Controls;
using FinanceManager.Services;

namespace FinanceManager.Views
{
    public abstract class BaseWindow : Window
    {
        protected BaseWindow()
        {
            this.Loaded += BaseWindow_Loaded;
        }

        private void BaseWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoggingService.LogInfo($"Window opened: {this.GetType().Name}");
        }

        protected void ShowValidationError(string message, Control? controlToFocus = null)
        {
            MessageBox.Show(message, "Campo Obrigatório",
                          MessageBoxButton.OK, MessageBoxImage.Warning);

            controlToFocus?.Focus();
            if (controlToFocus is TextBox textBox)
                textBox.SelectAll();
        }

        protected void ShowErrorMessage(string message, Exception? exception = null)
        {
            LoggingService.LogError($"Error in {this.GetType().Name}: {message}", exception);
            MessageBox.Show(message, "Erro",
                          MessageBoxButton.OK, MessageBoxImage.Error);
        }

        protected void ShowSuccessMessage(string message)
        {
            LoggingService.LogInfo($"Success in {this.GetType().Name}: {message}");
            MessageBox.Show(message, "Sucesso",
                          MessageBoxButton.OK, MessageBoxImage.Information);
        }

        protected void ShowInfoMessage(string message)
        {
            MessageBox.Show(message, "Informação",
                          MessageBoxButton.OK, MessageBoxImage.Information);
        }

        protected bool ConfirmAction(string message, string title = "Confirmar")
        {
            var result = MessageBox.Show(message, title,
                                       MessageBoxButton.YesNo, MessageBoxImage.Question);
            return result == MessageBoxResult.Yes;
        }

        protected void SafeExecute(Action action, string operationName = "")
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Erro ao {operationName}", ex);
            }
        }

        protected async System.Threading.Tasks.Task SafeExecuteAsync(Func<System.Threading.Tasks.Task> action, string operationName = "")
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Erro ao {operationName}", ex);
            }
        }
    }
}
