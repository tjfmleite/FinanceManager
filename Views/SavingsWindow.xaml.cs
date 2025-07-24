using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using FinanceManager.Models;
using FinanceManager.Services;

namespace FinanceManager.Views
{
    public partial class SavingsWindow : Window
    {
        private readonly User _currentUser;
        private readonly SavingsService _savingsService;
        private ObservableCollection<SavingsTarget> _savingsTargets;

        public SavingsWindow(User currentUser)
        {
            InitializeComponent();
            _currentUser = currentUser;
            _savingsService = new SavingsService();
            _savingsTargets = new ObservableCollection<SavingsTarget>();

            // Configurar ListView
            SavingsListView.ItemsSource = _savingsTargets;

            // Configurar validação numérica nos campos de entrada
            if (TargetAmountTextBox != null)
                TargetAmountTextBox.PreviewTextInput += NumericTextBox_PreviewTextInput;
            if (CurrentAmountTextBox != null)
                CurrentAmountTextBox.PreviewTextInput += NumericTextBox_PreviewTextInput;

            // Carregar dados
            LoadSavingsTargets();

            // Focar no primeiro campo
            this.Loaded += SavingsWindow_Loaded;
        }

        private void SavingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (NameTextBox != null)
                NameTextBox.Focus();
        }

        private async void LoadSavingsTargets()
        {
            try
            {
                var targets = await _savingsService.GetSavingsTargetsByUserIdAsync(_currentUser.Id);

                _savingsTargets.Clear();
                foreach (var target in targets.OrderByDescending(t => t.StartDate))
                {
                    _savingsTargets.Add(target);
                }

                // Atualizar título da janela
                this.Title = $"🎯 Objetivos de Poupança ({_savingsTargets.Count} objetivos)";

                // Mostrar mensagem de status
                if (_savingsTargets.Count == 0)
                {
                    ShowStatus("💡 Crie o seu primeiro objetivo de poupança usando o formulário à direita", false);
                }
                else
                {
                    ShowStatus($"✅ {_savingsTargets.Count} objetivos carregados com sucesso", true);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao carregar objetivos de poupança: {ex.Message}", "Erro",
                              MessageBoxButton.OK, MessageBoxImage.Error);
                ShowStatus("❌ Erro ao carregar objetivos", false);
            }
        }

        #region Métodos de Eventos dos Botões

        /// <summary>
        /// MÉTODO PRINCIPAL: Criar novo objetivo de poupança
        /// </summary>
        private async void CreateTarget_Click(object sender, RoutedEventArgs e)
        {
            // Validações do formulário
            if (string.IsNullOrWhiteSpace(NameTextBox?.Text))
            {
                ShowValidationError("Por favor, insira um nome para o objetivo.", NameTextBox);
                return;
            }

            if (string.IsNullOrWhiteSpace(TargetAmountTextBox?.Text))
            {
                ShowValidationError("Por favor, insira o valor objetivo.", TargetAmountTextBox);
                return;
            }

            // Validar valor objetivo
            if (!decimal.TryParse(TargetAmountTextBox.Text.Replace(",", "."),
                NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture,
                out decimal targetAmount) || targetAmount <= 0)
            {
                ShowValidationError("Por favor, insira um valor objetivo válido maior que zero.\nExemplo: 1500,00", TargetAmountTextBox);
                return;
            }

            // Validar valor inicial (opcional)
            decimal currentAmount = 0;
            if (!string.IsNullOrWhiteSpace(CurrentAmountTextBox?.Text))
            {
                if (!decimal.TryParse(CurrentAmountTextBox.Text.Replace(",", "."),
                    NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture,
                    out currentAmount) || currentAmount < 0)
                {
                    ShowValidationError("Por favor, insira um valor inicial válido.\nExemplo: 250,00", CurrentAmountTextBox);
                    return;
                }
            }

            // Validar se valor inicial não é maior que o objetivo
            if (currentAmount > targetAmount)
            {
                ShowValidationError("O valor inicial não pode ser maior que o valor objetivo.", CurrentAmountTextBox);
                return;
            }

            try
            {
                ShowStatus("⏳ A criar objetivo de poupança...", true);

                var savingsTarget = new SavingsTarget
                {
                    Name = NameTextBox.Text.Trim(),
                    TargetAmount = targetAmount,
                    CurrentAmount = currentAmount,
                    StartDate = DateTime.Now,
                    EndDate = EndDatePicker?.SelectedDate,
                    Description = DescriptionTextBox?.Text?.Trim(),
                    IsCompleted = currentAmount >= targetAmount,
                    UserId = _currentUser.Id
                };

                var success = await _savingsService.AddSavingsTargetAsync(savingsTarget);

                if (success)
                {
                    ShowStatus($"✅ Objetivo '{savingsTarget.Name}' criado com sucesso!", true);

                    // Mostrar mensagem de sucesso
                    var culture = new CultureInfo("pt-PT");
                    MessageBox.Show(
                        $"🎉 Objetivo de Poupança Criado!\n\n" +
                        $"🎯 Nome: {savingsTarget.Name}\n" +
                        $"💶 Meta: {savingsTarget.TargetAmount.ToString("C", culture)}\n" +
                        $"📊 Valor Inicial: {savingsTarget.CurrentAmount.ToString("C", culture)}\n" +
                        $"📈 Progresso: {savingsTarget.ProgressPercentage:F1}%\n" +
                        (savingsTarget.EndDate.HasValue ? $"📅 Prazo: {savingsTarget.EndDate.Value:dd/MM/yyyy}\n" : "") +
                        "\n🚀 Agora pode acompanhar e atualizar o seu progresso!",
                        "Sucesso",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    // Limpar formulário
                    ClearForm();

                    // Recarregar lista
                    LoadSavingsTargets();
                }
                else
                {
                    ShowStatus("❌ Erro ao criar objetivo de poupança", false);
                    MessageBox.Show("Erro ao criar objetivo de poupança. Tente novamente.", "Erro",
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                ShowStatus("❌ Erro inesperado ao criar objetivo", false);
                MessageBox.Show($"Erro inesperado: {ex.Message}", "Erro",
                              MessageBoxButton.OK, MessageBoxImage.Error);
                Services.LoggingService.LogError("Erro ao criar objetivo de poupança", ex);
            }
        }

        /// <summary>
        /// Método alternativo para compatibilidade
        /// </summary>
        private void AddSavingsTarget_Click(object sender, RoutedEventArgs e)
        {
            CreateTarget_Click(sender, e);
        }

        /// <summary>
        /// Atualizar progresso de um objetivo específico
        /// </summary>
        private void UpdateProgress_Click(object sender, RoutedEventArgs e)
        {
            // Obter o objetivo do contexto do botão
            var button = sender as Button;
            var selectedTarget = button?.DataContext as SavingsTarget;

            // Se não conseguiu obter do botão, tentar da seleção da lista
            if (selectedTarget == null)
            {
                selectedTarget = SavingsListView?.SelectedItem as SavingsTarget;
            }

            if (selectedTarget != null)
            {
                try
                {
                    var updateWindow = new UpdateSavingsProgressWindow(selectedTarget);
                    updateWindow.Owner = this;

                    if (updateWindow.ShowDialog() == true)
                    {
                        LoadSavingsTargets();
                        ShowStatus($"✅ Progresso do objetivo '{selectedTarget.Name}' atualizado!", true);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Erro ao abrir janela de atualização: {ex.Message}", "Erro",
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                    ShowStatus("❌ Erro ao abrir janela de atualização", false);
                }
            }
            else
            {
                MessageBox.Show("Por favor, selecione um objetivo para atualizar o progresso.", "Nenhuma Seleção",
                              MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// Eliminar um objetivo de poupança
        /// </summary>
        private async void DeleteTarget_Click(object sender, RoutedEventArgs e)
        {
            // Obter o objetivo do contexto do botão
            var button = sender as Button;
            var selectedTarget = button?.DataContext as SavingsTarget;

            // Se não conseguiu obter do botão, tentar da seleção da lista
            if (selectedTarget == null)
            {
                selectedTarget = SavingsListView?.SelectedItem as SavingsTarget;
            }

            if (selectedTarget != null)
            {
                var result = MessageBox.Show(
                    $"🗑️ Eliminar Objetivo de Poupança\n\n" +
                    $"Tem a certeza que quer eliminar este objetivo?\n\n" +
                    $"🎯 Nome: {selectedTarget.Name}\n" +
                    $"💶 Meta: {selectedTarget.FormattedTargetAmount}\n" +
                    $"📊 Progresso: {selectedTarget.FormattedProgressPercentage}\n" +
                    $"💰 Valor Atual: {selectedTarget.FormattedCurrentAmount}\n\n" +
                    $"⚠️ Esta ação não pode ser desfeita!\n" +
                    $"Todo o histórico de atualizações será perdido.",
                    "Confirmar Eliminação",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        ShowStatus("⏳ A eliminar objetivo...", true);
                        var success = await _savingsService.DeleteSavingsTargetAsync(selectedTarget.Id);

                        if (success)
                        {
                            ShowStatus("✅ Objetivo eliminado com sucesso!", true);
                            MessageBox.Show(
                                $"✅ Objetivo '{selectedTarget.Name}' eliminado com sucesso!",
                                "Objetivo Eliminado",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                            LoadSavingsTargets();
                        }
                        else
                        {
                            ShowStatus("❌ Erro ao eliminar objetivo", false);
                            MessageBox.Show("Erro ao eliminar objetivo. Tente novamente.", "Erro",
                                          MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        ShowStatus("❌ Erro inesperado ao eliminar", false);
                        MessageBox.Show($"Erro ao eliminar objetivo: {ex.Message}", "Erro",
                                      MessageBoxButton.OK, MessageBoxImage.Error);
                        Services.LoggingService.LogError("Erro ao eliminar objetivo", ex);
                    }
                }
            }
            else
            {
                MessageBox.Show("Por favor, selecione um objetivo para eliminar.", "Nenhuma Seleção",
                              MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// Marcar objetivo como concluído
        /// </summary>
        private async void CompleteTarget_Click(object sender, RoutedEventArgs e)
        {
            var selectedTarget = SavingsListView?.SelectedItem as SavingsTarget;

            if (selectedTarget != null)
            {
                if (selectedTarget.IsCompleted)
                {
                    MessageBox.Show("Este objetivo já está marcado como concluído.", "Já Concluído",
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var result = MessageBox.Show(
                    $"🎉 Marcar Objetivo como Concluído\n\n" +
                    $"Quer marcar este objetivo como concluído?\n\n" +
                    $"🎯 {selectedTarget.Name}\n" +
                    $"📊 Progresso atual: {selectedTarget.FormattedProgressPercentage}\n" +
                    $"💰 Valor atual: {selectedTarget.FormattedCurrentAmount}\n" +
                    $"🎯 Meta: {selectedTarget.FormattedTargetAmount}\n\n" +
                    $"Nota: Pode sempre reativar mais tarde se necessário.",
                    "Marcar como Concluído",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        var success = await _savingsService.CompleteSavingsTargetAsync(selectedTarget.Id);

                        if (success)
                        {
                            MessageBox.Show(
                                $"🎉 Parabéns!\n\n" +
                                $"O objetivo '{selectedTarget.Name}' foi marcado como concluído!\n\n" +
                                $"🎯 Meta alcançada: {selectedTarget.FormattedTargetAmount}\n" +
                                $"📅 Data de conclusão: {DateTime.Now:dd/MM/yyyy}\n\n" +
                                $"Continue assim com os seus objetivos financeiros! 💪",
                                "Objetivo Concluído",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                            LoadSavingsTargets();
                        }
                        else
                        {
                            MessageBox.Show("Erro ao marcar objetivo como concluído.", "Erro",
                                          MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Erro: {ex.Message}", "Erro",
                                      MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("Por favor, selecione um objetivo.", "Nenhuma Seleção",
                              MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// Mostrar estatísticas detalhadas
        /// </summary>
        private async void ShowStats_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ShowStatus("📊 A calcular estatísticas...", true);
                var stats = await _savingsService.GetSavingsStatisticsAsync(_currentUser.Id);

                if (stats.TotalTargets == 0)
                {
                    MessageBox.Show(
                        "📊 Estatísticas de Poupança\n\n" +
                        "Ainda não há objetivos de poupança criados.\n\n" +
                        "Crie o seu primeiro objetivo para começar a ver estatísticas detalhadas!",
                        "Sem Dados",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(
                        $"📊 Estatísticas de Poupança\n\n" +
                        $"🎯 Total de Objetivos: {stats.TotalTargets}\n" +
                        $"✅ Concluídos: {stats.CompletedTargets}\n" +
                        $"🔄 Ativos: {stats.ActiveTargets}\n\n" +
                        $"💰 Valor Total das Metas: {stats.FormattedTotalTargetAmount}\n" +
                        $"📊 Valor Atual Poupado: {stats.FormattedTotalCurrentAmount}\n" +
                        $"💼 Ainda Falta Poupar: {stats.FormattedTotalRemainingAmount}\n\n" +
                        $"📈 Progresso Geral: {stats.FormattedOverallProgress}\n" +
                        $"📊 Taxa de Conclusão: {stats.FormattedCompletionRate}\n" +
                        $"📉 Progresso Médio: {stats.FormattedAverageProgress}\n\n" +
                        $"🎉 Continue assim! Cada euro poupado é um passo em direção aos seus objetivos!",
                        "Estatísticas Detalhadas",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }

                ShowStatus("✅ Estatísticas calculadas com sucesso", true);
            }
            catch (Exception ex)
            {
                ShowStatus("❌ Erro ao calcular estatísticas", false);
                MessageBox.Show($"Erro ao obter estatísticas: {ex.Message}", "Erro",
                              MessageBoxButton.OK, MessageBoxImage.Error);
                Services.LoggingService.LogError("Erro ao calcular estatísticas", ex);
            }
        }

        /// <summary>
        /// Atualizar/recarregar dados
        /// </summary>
        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            ShowStatus("🔄 A atualizar dados...", true);
            LoadSavingsTargets();
        }

        /// <summary>
        /// Limpar formulário
        /// </summary>
        private void ClearForm_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
        }

        #endregion

        #region Métodos de Eventos da Interface

        /// <summary>
        /// Double-click na lista para editar
        /// </summary>
        private void SavingsListView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (SavingsListView?.SelectedItem is SavingsTarget selectedTarget)
            {
                UpdateProgress_Click(sender, new RoutedEventArgs());
            }
        }

        /// <summary>
        /// Validação de entrada numérica em tempo real
        /// </summary>
        private void NumericTextBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            var newText = textBox.Text.Insert(textBox.CaretIndex, e.Text);

            if (!IsValidDecimalInput(newText))
            {
                e.Handled = true;
            }
        }

        /// <summary>
        /// Validação para entrada decimal
        /// </summary>
        private bool IsValidDecimalInput(string text)
        {
            if (string.IsNullOrEmpty(text)) return true;

            var normalizedText = text.Replace(',', '.');
            return decimal.TryParse(normalizedText, NumberStyles.AllowDecimalPoint,
                                  CultureInfo.InvariantCulture, out _);
        }

        #endregion

        #region Métodos de Utilidade

        /// <summary>
        /// Limpar formulário de criação
        /// </summary>
        private void ClearForm()
        {
            if (NameTextBox != null)
            {
                NameTextBox.Clear();
                NameTextBox.Focus();
            }
            if (TargetAmountTextBox != null)
                TargetAmountTextBox.Clear();
            if (CurrentAmountTextBox != null)
                CurrentAmountTextBox.Text = "0,00";
            if (EndDatePicker != null)
                EndDatePicker.SelectedDate = null;
            if (DescriptionTextBox != null)
                DescriptionTextBox.Clear();
        }

        /// <summary>
        /// Mostrar erro de validação
        /// </summary>
        private void ShowValidationError(string message, Control controlToFocus)
        {
            MessageBox.Show(message, "Campo Obrigatório",
                          MessageBoxButton.OK, MessageBoxImage.Warning);

            controlToFocus?.Focus();
            if (controlToFocus is TextBox textBox)
            {
                textBox.SelectAll();
            }

            ShowStatus($"⚠️ {message}", false);
        }

        /// <summary>
        /// Mostrar mensagem de status
        /// </summary>
        private void ShowStatus(string message, bool isSuccess)
        {
            if (StatusMessage != null)
            {
                StatusMessage.Text = message;
                StatusMessage.Foreground = isSuccess ?
                    System.Windows.Media.Brushes.Green :
                    System.Windows.Media.Brushes.Red;

                // Auto-clear status após 5 segundos
                var timer = new System.Windows.Threading.DispatcherTimer();
                timer.Interval = TimeSpan.FromSeconds(5);
                timer.Tick += (s, e) => {
                    if (StatusMessage != null)
                        StatusMessage.Text = "";
                    timer.Stop();
                };
                timer.Start();
            }
        }

        #endregion

        #region Atalhos de Teclado

        /// <summary>
        /// Atalhos de teclado globais
        /// </summary>
        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            switch (e.Key)
            {
                case System.Windows.Input.Key.F2:
                    if (SavingsListView?.SelectedItem != null)
                        UpdateProgress_Click(this, e);
                    e.Handled = true;
                    break;

                case System.Windows.Input.Key.Delete:
                    if (SavingsListView?.SelectedItem != null)
                        DeleteTarget_Click(this, e);
                    e.Handled = true;
                    break;

                case System.Windows.Input.Key.F5:
                    Refresh_Click(this, e);
                    e.Handled = true;
                    break;

                case System.Windows.Input.Key.F1:
                    ShowHelp();
                    e.Handled = true;
                    break;

                case System.Windows.Input.Key.N when System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control:
                    NameTextBox?.Focus();
                    e.Handled = true;
                    break;

                case System.Windows.Input.Key.Enter when NameTextBox?.IsFocused == true:
                    TargetAmountTextBox?.Focus();
                    e.Handled = true;
                    break;

                case System.Windows.Input.Key.Enter when TargetAmountTextBox?.IsFocused == true:
                    CurrentAmountTextBox?.Focus();
                    e.Handled = true;
                    break;

                case System.Windows.Input.Key.Enter when CurrentAmountTextBox?.IsFocused == true:
                    EndDatePicker?.Focus();
                    e.Handled = true;
                    break;

                case System.Windows.Input.Key.Enter when DescriptionTextBox?.IsFocused == true:
                    CreateTarget_Click(this, e);
                    e.Handled = true;
                    break;

                default:
                    base.OnKeyDown(e);
                    break;
            }
        }

        /// <summary>
        /// Mostrar ajuda
        /// </summary>
        private void ShowHelp()
        {
            MessageBox.Show(
                "💡 Ajuda - Objetivos de Poupança\n\n" +
                "🎯 Nome: Dê um nome motivador ao seu objetivo\n" +
                "💶 Valor Objetivo: Quanto quer poupar (ex: 1500,00)\n" +
                "💰 Valor Inicial: Quanto já tem poupado (opcional)\n" +
                "📅 Data Limite: Quando quer atingir o objetivo (opcional)\n" +
                "📄 Descrição: Detalhes adicionais (opcional)\n\n" +
                "⌨️ Atalhos:\n" +
                "• Enter: Navegar entre campos\n" +
                "• Ctrl+N: Focar no campo nome\n" +
                "• F2: Atualizar progresso do objetivo selecionado\n" +
                "• Delete: Eliminar objetivo selecionado\n" +
                "• F5: Atualizar lista\n" +
                "• F1: Esta ajuda\n\n" +
                "💡 Dica: Duplo-clique num objetivo para atualizar o progresso!",
                "Ajuda",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        #endregion
    }
}
