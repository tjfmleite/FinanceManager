using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using FinanceManager.Models;
using FinanceManager.ViewModels;
using FinanceManager.Services;
using LiveCharts;
using LiveCharts.Wpf;
using System.Windows.Controls;

namespace FinanceManager.Views
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private readonly User _currentUser;
        private readonly ExpenseService _expenseService;
        private readonly InvestmentService _investmentService;
        private readonly IncomeService _incomeService;
        private readonly RecurringExpenseService _recurringExpenseService;
        private readonly CultureInfo _culture = new CultureInfo("pt-PT");

        // Timer para alertas automáticos
        private System.Windows.Threading.DispatcherTimer _recurringAlertsTimer;

        public MainWindow(User user)
        {
            InitializeComponent();
            _currentUser = user;
            _expenseService = new ExpenseService();
            _investmentService = new InvestmentService();
            _incomeService = new IncomeService();
            _recurringExpenseService = new RecurringExpenseService();

            _viewModel = new MainViewModel();
            _viewModel.CurrentUser = user;

            this.DataContext = _viewModel;

            // Configurar título da janela
            this.Title = $"Finance Manager - {user.Username}";

            // Inicializar timer de alertas de despesas recorrentes
            InitializeRecurringExpensesTimer();

            // Carregar dados iniciais
            LoadData();

            // Processar despesas recorrentes ao iniciar
            _ = ProcessRecurringExpensesOnStartup();

            LoggingService.LogInfo($"MainWindow iniciada para utilizador: {user.Username}");
        }

        #region Recurring Expenses Support

        /// <summary>
        /// Inicializar timer de alertas de despesas recorrentes
        /// </summary>
        private void InitializeRecurringExpensesTimer()
        {
            try
            {
                // Timer para verificar alertas de despesas recorrentes a cada 30 minutos
                _recurringAlertsTimer = new System.Windows.Threading.DispatcherTimer();
                _recurringAlertsTimer.Interval = TimeSpan.FromMinutes(30);
                _recurringAlertsTimer.Tick += async (s, e) => await CheckRecurringExpenseAlertsAsync();
                _recurringAlertsTimer.Start();

                LoggingService.LogInfo("Timer de alertas de despesas recorrentes iniciado (30 min)");
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao inicializar timer de alertas", ex);
            }
        }

        /// <summary>
        /// Verifica e exibe alertas de despesas recorrentes
        /// </summary>
        private async Task CheckRecurringExpenseAlertsAsync()
        {
            try
            {
                LoggingService.LogInfo("Verificando alertas de despesas recorrentes...");

                var stats = await _recurringExpenseService.GetRecurringExpenseStatsAsync(_currentUser.Id);

                // Verificar se há despesas em atraso
                if (stats.Overdue > 0)
                {
                    ShowRecurringAlert(
                        $"⚠️ {stats.Overdue} despesa(s) recorrente(s) em atraso",
                        "Erro"
                    );
                }
                // Verificar se há despesas vencendo hoje
                else if (stats.DueToday > 0)
                {
                    ShowRecurringAlert(
                        $"🔔 {stats.DueToday} despesa(s) recorrente(s) vencem hoje",
                        "Aviso"
                    );
                }
                // Verificar se há despesas vencendo em breve
                else if (stats.DueSoon > 0)
                {
                    ShowRecurringAlert(
                        $"💡 {stats.DueSoon} despesa(s) recorrente(s) vencem em breve",
                        "Info"
                    );
                }

                LoggingService.LogInfo($"Alertas verificados: {stats.Overdue} em atraso, {stats.DueToday} hoje, {stats.DueSoon} em breve");
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao verificar alertas de despesas recorrentes", ex);
            }
        }

        /// <summary>
        /// Mostra um alerta simples de despesas recorrentes
        /// </summary>
        private void ShowRecurringAlert(string message, string type)
        {
            try
            {
                var result = MessageBox.Show(
                    message + "\n\nDeseja ver as despesas recorrentes?",
                    "Alerta de Despesas Recorrentes",
                    MessageBoxButton.YesNo,
                    type == "Erro" ? MessageBoxImage.Warning : MessageBoxImage.Information
                );

                if (result == MessageBoxResult.Yes)
                {
                    OpenRecurringExpensesWindow();
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao mostrar alerta", ex);
            }
        }

        /// <summary>
        /// Abre a janela de despesas recorrentes
        /// </summary>
        private void OpenRecurringExpensesWindow()
        {
            try
            {
                LoggingService.LogInfo("Abrindo janela de despesas recorrentes");

                var recurringWindow = new RecurringExpensesWindow(_currentUser);
                recurringWindow.Owner = this;
                recurringWindow.ShowDialog();

                // Recarregar dados após fechar a janela
                _ = Task.Run(async () =>
                {
                    await Task.Delay(500);
                    await Dispatcher.InvokeAsync(async () => await LoadData());
                });
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao abrir janela de despesas recorrentes", ex);
                MessageBox.Show($"Erro ao abrir despesas recorrentes: {ex.Message}", "Erro",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Parar timer ao fechar a janela
        /// </summary>
        private void StopRecurringExpensesTimer()
        {
            try
            {
                _recurringAlertsTimer?.Stop();
                LoggingService.LogInfo("Timer de alertas parado");
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao parar timer", ex);
            }
        }

        #endregion

        /// <summary>
        /// Processa despesas recorrentes automaticamente ao iniciar a aplicação
        /// </summary>
        private async Task ProcessRecurringExpensesOnStartup()
        {
            try
            {
                LoggingService.LogInfo("Verificando despesas recorrentes vencidas...");

                // Verificar se há despesas vencidas
                var dueExpenses = await _recurringExpenseService.GetDueRecurringExpensesAsync(_currentUser.Id);

                if (dueExpenses.Any())
                {
                    // Perguntar ao utilizador se quer processar automaticamente
                    var result = MessageBox.Show(
                        "🔔 Despesas Recorrentes Pendentes\n\n" +
                        $"Foram detectadas {dueExpenses.Count} despesas recorrentes vencidas (renda, contas, etc.).\n\n" +
                        "Quer processar automaticamente estas despesas agora?\n" +
                        "Isto irá criar as despesas normais correspondentes.",
                        "Despesas Recorrentes",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        var processResult = await _recurringExpenseService.ProcessRecurringExpensesAsync(_currentUser.Id);

                        if (processResult.IsSuccess && processResult.ProcessedCount > 0)
                        {
                            MessageBox.Show(
                                $"✅ Processamento Concluído!\n\n" +
                                $"📊 {processResult.ProcessedCount} despesas recorrentes foram processadas:\n\n" +
                                string.Join("\n", processResult.ProcessedExpenses.Take(5).Select(e => $"• {e}")) +
                                (processResult.ProcessedExpenses.Count > 5 ? $"\n... e mais {processResult.ProcessedExpenses.Count - 5}" : "") +
                                "\n\n💡 As despesas foram adicionadas às suas despesas normais.",
                                "Despesas Processadas",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);

                            // Recarregar dados do dashboard
                            await LoadData();
                        }
                    }
                    else
                    {
                        // Mostrar resumo das despesas pendentes
                        var summary = string.Join("\n", dueExpenses.Take(5).Select(e => $"• {e.Description} - {e.FormattedAmount}"));
                        if (dueExpenses.Count > 5)
                            summary += $"\n... e mais {dueExpenses.Count - 5} despesas";

                        MessageBox.Show(
                            $"📋 Resumo de Despesas Recorrentes\n\n{summary}\n\n" +
                            "💡 Pode processar estas despesas mais tarde no menu 'Despesas Recorrentes'.",
                            "Despesas Pendentes",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                }
                else
                {
                    LoggingService.LogInfo("Nenhuma despesa recorrente vencida encontrada");
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao verificar despesas recorrentes no startup", ex);
            }
        }

        private async Task LoadData()
        {
            try
            {
                LoggingService.LogInfo($"Carregando dados para utilizador: {_currentUser.Username}");

                // Verificar alertas de despesas recorrentes
                await CheckRecurringExpenseAlertsAsync();

                // 1. Atualizar estatísticas do dashboard (receitas, despesas, saldo)
                await UpdateDashboardStats();

                // 2. Atualizar gráfico com receitas e despesas
                await UpdateCategoryChart();

                // 3. Atualizar informações do utilizador na interface
                UpdateUserInfo();

                LoggingService.LogInfo("Dados carregados e dashboard atualizado com sucesso");
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao carregar dados da MainWindow", ex);
                MessageBox.Show($"Erro ao carregar dados: {ex.Message}", "Erro",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async Task UpdateDashboardStats()
        {
            try
            {
                LoggingService.LogInfo($"=== INÍCIO UpdateDashboardStats para utilizador {_currentUser.Id} ===");

                var now = DateTime.Now;
                var startOfMonth = new DateTime(now.Year, now.Month, 1);
                var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

                LoggingService.LogInfo($"Período de cálculo: {startOfMonth:dd/MM/yyyy} a {endOfMonth:dd/MM/yyyy}");

                // Obter dados REAIS do mês atual
                var totalIncomesThisMonth = await _incomeService.GetTotalIncomeAsync(_currentUser.Id, startOfMonth, endOfMonth);
                var totalExpensesThisMonth = await _expenseService.GetTotalExpensesAsync(_currentUser.Id, startOfMonth, endOfMonth);

                // Obter estatísticas de despesas recorrentes
                var recurringStats = await _recurringExpenseService.GetRecurringExpenseStatsAsync(_currentUser.Id);

                var balanceThisMonth = totalIncomesThisMonth - totalExpensesThisMonth;
                var portfolioValue = await _investmentService.GetPortfolioValueAsync(_currentUser.Id);

                LoggingService.LogInfo($"Valores calculados:");
                LoggingService.LogInfo($"  - Receitas do mês: {totalIncomesThisMonth}");
                LoggingService.LogInfo($"  - Despesas do mês: {totalExpensesThisMonth}");
                LoggingService.LogInfo($"  - Despesas recorrentes mensais: {recurringStats.TotalMonthlyAmount}");
                LoggingService.LogInfo($"  - Saldo do mês: {balanceThisMonth}");
                LoggingService.LogInfo($"  - Portfólio: {portfolioValue}");

                // ATUALIZAR CARDS com os valores reais
                if (TotalIncomesText != null)
                {
                    TotalIncomesText.Text = totalIncomesThisMonth.ToString("C", _culture);
                }

                if (TotalExpensesText != null)
                {
                    TotalExpensesText.Text = totalExpensesThisMonth.ToString("C", _culture);
                }

                if (ThisMonthText != null)
                {
                    ThisMonthText.Text = balanceThisMonth.ToString("C", _culture);

                    var border = ThisMonthText.Parent as Border;
                    if (border != null)
                    {
                        border.Background = balanceThisMonth >= 0 ?
                            System.Windows.Media.Brushes.Green :
                            System.Windows.Media.Brushes.Red;
                    }
                }

                if (PortfolioValueText != null)
                {
                    PortfolioValueText.Text = portfolioValue.ToString("C", _culture);
                }

                // Mostrar despesas recorrentes no dashboard (se existir elemento)
                try
                {
                    if (RecurringExpensesText != null)
                    {
                        RecurringExpensesText.Text = recurringStats.FormattedMonthlyAmount;
                    }
                }
                catch
                {
                    // Ignorar se elemento não existir
                }

                // Mostrar alertas de despesas recorrentes (se existir elemento)
                try
                {
                    if (RecurringAlertsText != null)
                    {
                        var alertCount = recurringStats.DueToday + recurringStats.Overdue;
                        if (alertCount > 0)
                        {
                            RecurringAlertsText.Text = $"🔔 {alertCount} pendentes";
                            RecurringAlertsText.Foreground = System.Windows.Media.Brushes.Red;
                        }
                        else
                        {
                            RecurringAlertsText.Text = "✅ Em dia";
                            RecurringAlertsText.Foreground = System.Windows.Media.Brushes.Green;
                        }
                    }
                }
                catch
                {
                    // Ignorar se elemento não existir
                }

                // Atualizar resumo mensal com despesas recorrentes
                UpdateMonthlySummaryFields(totalIncomesThisMonth, totalExpensesThisMonth, balanceThisMonth, recurringStats);

                // Atualizar timestamp
                if (LastUpdateText != null)
                    LastUpdateText.Text = $"Última atualização: {DateTime.Now:HH:mm}";

                LoggingService.LogInfo($"=== FIM UpdateDashboardStats - SUCESSO ===");
            }
            catch (Exception ex)
            {
                LoggingService.LogError("ERRO CRÍTICO em UpdateDashboardStats", ex);

                // Valores padrão em caso de erro
                if (TotalExpensesText != null) TotalExpensesText.Text = "€ 0,00";
                if (ThisMonthText != null) ThisMonthText.Text = "€ 0,00";
                if (PortfolioValueText != null) PortfolioValueText.Text = "€ 0,00";
                if (TotalIncomesText != null) TotalIncomesText.Text = "€ 0,00";
            }
        }

        private void UpdateUserInfo()
        {
            try
            {
                // Atualizar horário na status bar
                if (CurrentTimeText != null)
                    CurrentTimeText.Text = DateTime.Now.ToString("HH:mm", _culture);

                LoggingService.LogInfo($"Interface atualizada para {_currentUser.Username}");
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao atualizar informações do utilizador", ex);
            }
        }

        private async Task UpdateCategoryChart()
        {
            try
            {
                // Obter dados do mês atual
                var now = DateTime.Now;
                var startOfMonth = new DateTime(now.Year, now.Month, 1);
                var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

                var expensesByCategory = await _expenseService.GetExpensesByCategoryAsync(_currentUser.Id, startOfMonth, endOfMonth);
                var incomesByCategory = await _incomeService.GetIncomesByCategoryAsync(_currentUser.Id, startOfMonth, endOfMonth);

                var series = new SeriesCollection();

                // Cores para despesas (tons vermelhos/laranja)
                var expenseColors = new[] { "#FFE53E3E", "#FFFF6B6B", "#FFFF8E53", "#FFFF6B35", "#FFD63031", "#FFFE4A49" };

                // Cores para receitas (tons verdes/azuis)
                var incomeColors = new[] { "#FF00B894", "#FF00CEC9", "#FF74B9FF", "#FF6C5CE7", "#FFA29BFE", "#FFFD79A8" };

                int expenseColorIndex = 0;
                int incomeColorIndex = 0;

                // ADICIONAR DESPESAS ao gráfico
                if (expensesByCategory.Any())
                {
                    foreach (var category in expensesByCategory.OrderByDescending(x => x.Value))
                    {
                        if (category.Value > 0)
                        {
                            var color = expenseColors[expenseColorIndex % expenseColors.Length];

                            series.Add(new PieSeries
                            {
                                Title = $"💸 {category.Key} ({category.Value.ToString("C", _culture)})",
                                Values = new ChartValues<decimal> { category.Value },
                                DataLabels = true,
                                Fill = new System.Windows.Media.SolidColorBrush(
                                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color))
                            });

                            expenseColorIndex++;
                        }
                    }
                }

                // ADICIONAR RECEITAS ao gráfico
                if (incomesByCategory.Any())
                {
                    foreach (var category in incomesByCategory.OrderByDescending(x => x.Value))
                    {
                        if (category.Value > 0)
                        {
                            var color = incomeColors[incomeColorIndex % incomeColors.Length];

                            series.Add(new PieSeries
                            {
                                Title = $"💰 {category.Key} ({category.Value.ToString("C", _culture)})",
                                Values = new ChartValues<decimal> { category.Value },
                                DataLabels = true,
                                Fill = new System.Windows.Media.SolidColorBrush(
                                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color))
                            });

                            incomeColorIndex++;
                        }
                    }
                }

                // Atualizar gráfico se há dados
                if (series.Any() && CategoryChart != null)
                {
                    CategoryChart.Series = series;
                    LoggingService.LogInfo($"Gráfico atualizado com {series.Count} categorias (receitas + despesas)");
                }
                else
                {
                    // Limpar gráfico se não há dados
                    if (CategoryChart != null)
                        CategoryChart.Series = new SeriesCollection();

                    LoggingService.LogInfo("Nenhum dado encontrado para o gráfico de categorias");
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao atualizar gráfico de categorias", ex);
            }
        }

        private void UpdateMonthlySummaryFields(decimal totalIncome, decimal totalExpenses, decimal balance, RecurringExpenseStats recurringStats)
        {
            try
            {
                // Atualizar nome do mês atual
                if (CurrentMonthName != null)
                    CurrentMonthName.Text = DateTime.Now.ToString("MMMM yyyy", _culture);

                // Valores mensais
                if (MonthlyIncome != null)
                    MonthlyIncome.Text = totalIncome.ToString("C", _culture);

                if (MonthlyExpenses != null)
                    MonthlyExpenses.Text = totalExpenses.ToString("C", _culture);

                if (MonthlyBalance != null)
                {
                    MonthlyBalance.Text = balance.ToString("C", _culture);
                    MonthlyBalance.Foreground = balance >= 0 ?
                        System.Windows.Media.Brushes.Green :
                        System.Windows.Media.Brushes.Red;
                }

                // Informações de despesas recorrentes (se existirem elementos)
                try
                {
                    if (MonthlyRecurringExpenses != null)
                    {
                        MonthlyRecurringExpenses.Text = recurringStats.FormattedMonthlyAmount;
                    }

                    if (RecurringExpenseCount != null)
                    {
                        RecurringExpenseCount.Text = $"{recurringStats.ActiveRecurring} ativas";
                    }
                }
                catch
                {
                    // Ignorar se elementos não existirem
                }

                // Taxa de poupança ajustada com despesas recorrentes
                if (SavingsRate != null)
                {
                    var adjustedExpenses = totalExpenses + recurringStats.TotalMonthlyAmount;
                    var adjustedBalance = totalIncome - adjustedExpenses;
                    var savingsRate = totalIncome > 0 ? (adjustedBalance / totalIncome) * 100 : 0;

                    SavingsRate.Text = $"{savingsRate:F1}%";
                    SavingsRate.Foreground = savingsRate >= 20 ?
                        System.Windows.Media.Brushes.Green :
                        savingsRate >= 10 ?
                        System.Windows.Media.Brushes.Orange :
                        System.Windows.Media.Brushes.Red;
                }

                // Atualizar status com informações de despesas recorrentes
                UpdateDashboardStatus(totalIncome, totalExpenses, balance, recurringStats);

                LoggingService.LogInfo("Campos do resumo mensal atualizados com sucesso");
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao atualizar campos do resumo mensal", ex);
            }
        }

        private void UpdateDashboardStatus(decimal totalIncome, decimal totalExpenses, decimal balance, RecurringExpenseStats recurringStats)
        {
            try
            {
                if (StatusText == null) return;

                // Verificar alertas de despesas recorrentes primeiro
                var urgentAlerts = recurringStats.DueToday + recurringStats.Overdue;

                if (urgentAlerts > 0)
                {
                    StatusText.Text = $"🔔 {urgentAlerts} despesas recorrentes pendentes - Clique em 'Despesas Recorrentes'";
                    StatusText.Foreground = System.Windows.Media.Brushes.Red;
                    return;
                }

                // Status normal baseado na situação financeira
                if (totalIncome == 0 && totalExpenses == 0)
                {
                    StatusText.Text = "💡 Adicione receitas e despesas para ver análises detalhadas";
                    StatusText.Foreground = System.Windows.Media.Brushes.Gray;
                }
                else if (balance < 0)
                {
                    StatusText.Text = "⚠️ Atenção: Despesas superiores às receitas este mês";
                    StatusText.Foreground = System.Windows.Media.Brushes.Red;
                }
                else
                {
                    var savingsRate = totalIncome > 0 ? (balance / totalIncome) * 100 : 0;

                    if (savingsRate >= 20)
                    {
                        StatusText.Text = $"🎉 Excelente gestão! {recurringStats.ActiveRecurring} despesas recorrentes ativas";
                        StatusText.Foreground = System.Windows.Media.Brushes.Green;
                    }
                    else if (savingsRate >= 10)
                    {
                        StatusText.Text = $"👍 Boa gestão financeira. {recurringStats.ActiveRecurring} despesas recorrentes monitorizadas";
                        StatusText.Foreground = System.Windows.Media.Brushes.DarkGreen;
                    }
                    else
                    {
                        StatusText.Text = $"📊 Considere otimizar as {recurringStats.ActiveRecurring} despesas recorrentes";
                        StatusText.Foreground = System.Windows.Media.Brushes.Orange;
                    }
                }

                LoggingService.LogInfo($"Status do dashboard atualizado: {StatusText.Text}");
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao atualizar status do dashboard", ex);

                if (StatusText != null)
                {
                    StatusText.Text = "❌ Erro ao atualizar status";
                    StatusText.Foreground = System.Windows.Media.Brushes.Red;
                }
            }
        }

        private async void RefreshDashboard_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Mostrar status de carregamento
                if (StatusText != null)
                    StatusText.Text = "🔄 A atualizar dashboard...";

                // Forçar atualização completa dos dados
                await LoadData();

                // Mostrar status de sucesso
                if (StatusText != null)
                    StatusText.Text = "✅ Dashboard atualizado com sucesso";

                LoggingService.LogInfo("Dashboard atualizado manualmente pelo utilizador");
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao atualizar dashboard manualmente", ex);

                if (StatusText != null)
                    StatusText.Text = "❌ Erro ao atualizar dashboard";
            }
        }

        #region Menu Navigation

        private void ShowDashboard_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (DashboardPanel != null)
                {
                    DashboardPanel.Visibility = Visibility.Visible;
                }
                LoggingService.LogInfo("Dashboard focado");

                // Recarregar dados do dashboard
                LoadData();
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao mostrar dashboard", ex);
            }
        }

        private void ShowExpenses_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LoggingService.LogInfo("Abrindo janela de despesas");
                var expensesWindow = new ExpensesWindow(_currentUser);
                expensesWindow.Owner = this;
                expensesWindow.ShowDialog();

                // Recarregar dados após fechar janela de despesas
                LoadData();
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao abrir janela de despesas", ex);
                MessageBox.Show($"Erro ao abrir janela de despesas: {ex.Message}", "Erro",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowRecurringExpenses_Click(object sender, RoutedEventArgs e)
        {
            OpenRecurringExpensesWindow();
        }

        private async void ProcessRecurringExpenses_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ShowStatus("⏳ Processando despesas recorrentes...", true);

                var result = await _recurringExpenseService.ProcessRecurringExpensesAsync(_currentUser.Id);

                if (result.IsSuccess)
                {
                    if (result.ProcessedCount > 0)
                    {
                        MessageBox.Show(
                            $"✅ Processamento Concluído!\n\n" +
                            $"📊 {result.ProcessedCount} despesas recorrentes processadas\n\n" +
                            $"As despesas foram adicionadas automaticamente.",
                            "Sucesso",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);

                        // Recarregar dados
                        await LoadData();
                        ShowStatus("✅ Despesas recorrentes processadas com sucesso", true);
                    }
                    else
                    {
                        ShowStatus("ℹ️ Nenhuma despesa recorrente vencida encontrada", true);
                    }
                }
                else
                {
                    ShowStatus("❌ Erro ao processar despesas recorrentes", false);
                    MessageBox.Show($"Erro: {result.Message}", "Erro",
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                ShowStatus("❌ Erro inesperado", false);
                LoggingService.LogError("Erro ao processar despesas recorrentes manualmente", ex);
                MessageBox.Show($"Erro inesperado: {ex.Message}", "Erro",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowStatus(string message, bool isSuccess)
        {
            if (StatusText != null)
            {
                StatusText.Text = message;
                StatusText.Foreground = isSuccess ?
                    System.Windows.Media.Brushes.Green :
                    System.Windows.Media.Brushes.Red;
            }
        }

        private void ShowIncome_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LoggingService.LogInfo("Mostrando opções de receitas");

                // Criar menu contextual para escolher entre adicionar ou gerir
                var contextMenu = new System.Windows.Controls.ContextMenu();

                var addIncomeItem = new System.Windows.Controls.MenuItem
                {
                    Header = "➕ Adicionar Nova Receita"
                };
                addIncomeItem.Click += (s, args) => AddIncome_Click(sender, e);

                var manageIncomesItem = new System.Windows.Controls.MenuItem
                {
                    Header = "📋 Gerir Todas as Receitas"
                };
                manageIncomesItem.Click += (s, args) => ShowIncomesManagement_Click(sender, e);

                contextMenu.Items.Add(addIncomeItem);
                contextMenu.Items.Add(new System.Windows.Controls.Separator());
                contextMenu.Items.Add(manageIncomesItem);

                // Mostrar menu na posição do botão
                if (sender is FrameworkElement element)
                {
                    contextMenu.PlacementTarget = element;
                    contextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                    contextMenu.IsOpen = true;
                }
                else
                {
                    // Fallback: abrir janela de gestão diretamente
                    ShowIncomesManagement_Click(sender, e);
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao mostrar opções de receitas", ex);
                MessageBox.Show($"Erro ao mostrar opções de receitas: {ex.Message}", "Erro",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowIncomesManagement_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LoggingService.LogInfo("Abrindo janela de gestão de receitas");
                var incomesWindow = new IncomesWindow(_currentUser);
                incomesWindow.Owner = this;
                incomesWindow.ShowDialog();

                // Recarregar dados após fechar janela de receitas
                LoadData();
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao abrir janela de gestão de receitas", ex);
                MessageBox.Show($"Erro ao abrir janela de gestão de receitas: {ex.Message}", "Erro",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowInvestments_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LoggingService.LogInfo("Abrindo janela de portfólio");

                var portfolioWindow = new PortfolioWindow(_currentUser);
                portfolioWindow.Owner = this;
                portfolioWindow.ShowDialog();

                // Recarregar dados após fechar janela de portfólio
                LoadData();
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao abrir janela de portfólio", ex);
                MessageBox.Show($"Erro ao abrir janela de portfólio: {ex.Message}", "Erro",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowSavings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LoggingService.LogInfo("Abrindo janela de poupanças");
                var savingsWindow = new SavingsWindow(_currentUser);
                savingsWindow.Owner = this;
                savingsWindow.ShowDialog();

                // Recarregar dados
                LoadData();
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao abrir janela de poupanças", ex);
                MessageBox.Show($"Erro ao abrir janela de poupanças: {ex.Message}", "Erro",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowCharts_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LoggingService.LogInfo("Abrindo janela de gráficos");

                var chartsWindow = new ChartsWindow(_currentUser);
                chartsWindow.Owner = this;
                chartsWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao abrir janela de gráficos", ex);
                MessageBox.Show($"Erro ao abrir janela de gráficos: {ex.Message}", "Erro",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowNotes_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LoggingService.LogInfo("Abrindo janela de notas");
                var notesWindow = new NotesWindow(_currentUser);
                notesWindow.Owner = this;
                notesWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao abrir janela de notas", ex);
                MessageBox.Show($"Erro ao abrir janela de notas: {ex.Message}", "Erro",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Quick Actions

        private async void AddExpense_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LoggingService.LogInfo("Abrindo janela de adicionar despesa");
                var addExpenseWindow = new AddExpenseWindow(_currentUser);
                addExpenseWindow.Owner = this;

                if (addExpenseWindow.ShowDialog() == true)
                {
                    // Forçar atualização completa após adicionar despesa
                    await LoadData();

                    // Mostrar confirmação visual
                    if (StatusText != null)
                        StatusText.Text = "✅ Despesa adicionada e dashboard atualizado";
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao abrir janela de adicionar despesa", ex);
                MessageBox.Show($"Erro ao abrir janela de adicionar despesa: {ex.Message}", "Erro",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void AddIncome_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LoggingService.LogInfo("Abrindo janela de adicionar receita");
                var addIncomeWindow = new AddIncomeWindow(_currentUser);
                addIncomeWindow.Owner = this;

                if (addIncomeWindow.ShowDialog() == true)
                {
                    // Forçar atualização completa após adicionar receita
                    await LoadData();

                    // Mostrar confirmação visual
                    if (StatusText != null)
                        StatusText.Text = "✅ Receita adicionada e dashboard atualizado";
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao abrir janela de adicionar receita", ex);
                MessageBox.Show($"Erro ao abrir janela de adicionar receita: {ex.Message}", "Erro",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddInvestment_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LoggingService.LogInfo("Abrindo janela de adicionar investimento");
                var addInvestmentWindow = new AddInvestmentWindow(_currentUser);
                addInvestmentWindow.Owner = this;

                if (addInvestmentWindow.ShowDialog() == true)
                {
                    // Recarregar dados após adicionar investimento
                    LoadData();
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao abrir janela de adicionar investimento", ex);
                MessageBox.Show($"Erro ao abrir janela de adicionar investimento: {ex.Message}", "Erro",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddSavingsTarget_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LoggingService.LogInfo("Abrindo janela de adicionar objetivo de poupança");
                var addSavingsWindow = new AddSavingsTargetWindow(_currentUser);
                addSavingsWindow.Owner = this;

                if (addSavingsWindow.ShowDialog() == true)
                {
                    // Recarregar dados após adicionar objetivo
                    LoadData();
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao abrir janela de adicionar poupança", ex);
                MessageBox.Show($"Erro ao abrir janela de poupança: {ex.Message}", "Erro",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Settings and System

        private async void ShowSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LoggingService.LogInfo("Abrindo definições");

                var settingsInfo = MessageBox.Show(
                    "🔧 Definições do Finance Manager\n\n" +
                    "📊 Base de dados: Configurada automaticamente\n" +
                    "📁 Localização: %AppData%\\FinanceManager\n" +
                    "🗂️ Logs: Ativados automaticamente\n" +
                    "🌍 Idioma: Português (Portugal)\n" +
                    "💰 Moeda: Euro (€)\n\n" +
                    "Funcionalidades avançadas em desenvolvimento.\n\n" +
                    "Deseja ver estatísticas da base de dados?",
                    "Definições",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (settingsInfo == MessageBoxResult.Yes)
                {
                    await ShowDatabaseStats();
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao abrir definições", ex);
                MessageBox.Show($"Erro ao abrir definições: {ex.Message}", "Erro",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ShowDatabaseStats()
        {
            try
            {
                var stats = await Helpers.DatabaseHelper.GetDatabaseStatsAsync();
                var logStats = LoggingService.GetLogStatistics();

                MessageBox.Show(
                    $"📊 Estatísticas do Sistema\n\n" +
                    $"👥 Utilizadores: {stats.TotalUsers}\n" +
                    $"💸 Despesas: {stats.TotalExpenses}\n" +
                    $"🔄 Despesas Recorrentes: {stats.TotalRecurringExpenses}\n" +
                    $"💰 Receitas: {stats.TotalIncomes}\n" +
                    $"🎯 Objetivos de Poupança: {stats.TotalSavingsTargets}\n" +
                    $"📈 Investimentos: {stats.TotalInvestments}\n" +
                    $"📝 Notas: {stats.TotalNotes}\n" +
                    $"💾 Tamanho da BD: {stats.DatabaseSize} MB\n\n" +
                    $"📋 Ficheiros de Log: {logStats.TotalFiles}\n" +
                    $"📁 Tamanho dos Logs: {logStats.TotalSizeMB} MB\n\n" +
                    $"🖥️ Sistema: Finance Manager v1.0\n" +
                    $"📅 Data: {DateTime.Now.ToString("dd/MM/yyyy HH:mm", _culture)}",
                    "Estatísticas do Sistema",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao obter estatísticas", ex);
                MessageBox.Show("Erro ao obter estatísticas da base de dados.", "Erro",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "🚪 Terminar Sessão\n\n" +
                "Tem a certeza que quer sair do Finance Manager?\n\n" +
                "Todos os dados estão guardados automaticamente.",
                "Confirmar Saída",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                LoggingService.LogInfo($"Utilizador {_currentUser.Username} fez logout");

                try
                {
                    var loginWindow = new LoginWindow();
                    loginWindow.Show();
                    this.Close();
                }
                catch (Exception ex)
                {
                    LoggingService.LogError("Erro ao fazer logout", ex);
                    MessageBox.Show("Erro ao voltar ao login. A aplicação será encerrada.", "Erro",
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                    Application.Current.Shutdown();
                }
            }
        }

        #endregion

        #region Window Events

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                LoggingService.LogInfo($"Aplicação fechada pelo utilizador {_currentUser.Username}");

                // Parar timer de alertas
                StopRecurringExpensesTimer();

                // Limpeza de logs antigos
                LoggingService.CleanOldLogs();

                // Mostrar mensagem de despedida
                var result = MessageBox.Show(
                    "👋 Até à próxima!\n\n" +
                    "Obrigado por usar o Finance Manager.\n" +
                    "Todos os seus dados estão guardados em segurança.\n\n" +
                    "Quer mesmo encerrar a aplicação?",
                    "Finance Manager",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true; // Cancelar o encerramento
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro no encerramento da aplicação", ex);
                // Não cancelar o encerramento mesmo se houver erro
            }
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validar base de dados ao carregar
                var dbValid = await Helpers.DatabaseHelper.TestDatabaseConnectionAsync();

                if (!dbValid)
                {
                    MessageBox.Show(
                        "⚠️ Problema com a Base de Dados\n\n" +
                        "Foi detectado um problema com a base de dados.\n" +
                        "A aplicação tentará corrigir automaticamente.\n\n" +
                        "Se o problema persistir, contacte o suporte.",
                        "Aviso",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    // Tentar corrigir
                    await Helpers.DatabaseHelper.ValidateAndFixDatabaseAsync();
                }

                // Mostrar mensagem de boas-vindas para novos utilizadores apenas se não há despesas OU receitas
                var now = DateTime.Now;
                var startOfMonth = new DateTime(now.Year, now.Month, 1);
                var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

                var hasExpenses = (await _expenseService.GetTotalExpensesAsync(_currentUser.Id, startOfMonth, endOfMonth)) > 0;
                var hasIncomes = (await _incomeService.GetTotalIncomeAsync(_currentUser.Id, startOfMonth, endOfMonth)) > 0;

                if (!hasExpenses && !hasIncomes)
                {
                    ShowWelcomeMessage();
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao carregar MainWindow", ex);
            }
        }

        private void ShowWelcomeMessage()
        {
            var result = MessageBox.Show(
                $"🎉 Bem-vindo ao Finance Manager, {_currentUser.Username}!\n\n" +
                "🚀 Para começar rapidamente:\n\n" +
                "1️⃣ Use o menu lateral para navegar\n" +
                "2️⃣ Adicione despesas e receitas\n" +
                "3️⃣ Configure despesas recorrentes\n" +
                "4️⃣ Defina objetivos de poupança\n" +
                "5️⃣ Explore gráficos e análises\n\n" +
                "💡 Dica: Use as 'Ações Rápidas' no menu lateral!\n\n" +
                "Comece a explorar as funcionalidades?",
                "Bem-vindo!",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
            {
                // Focar no menu de despesas
                ShowExpenses_Click(null, null);
            }
        }

        #endregion

        #region Keyboard Shortcuts

        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            // Atalhos de teclado globais
            if (e.Key == System.Windows.Input.Key.F1)
            {
                ShowSettings_Click(this, e);
                e.Handled = true;
            }
            else if (e.Key == System.Windows.Input.Key.F5)
            {
                LoadData();
                e.Handled = true;
            }
            else if (e.Key == System.Windows.Input.Key.N &&
                     System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control)
            {
                AddExpense_Click(this, e);
                e.Handled = true;
            }
            else if (e.Key == System.Windows.Input.Key.R &&
                     System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control)
            {
                ShowRecurringExpenses_Click(this, e);
                e.Handled = true;
            }
            else if (e.Key == System.Windows.Input.Key.Escape)
            {
                this.Close();
                e.Handled = true;
            }

            base.OnKeyDown(e);
        }

        #endregion
    }
}