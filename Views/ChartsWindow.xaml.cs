using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using FinanceManager.Models;
using FinanceManager.Services;
using LiveCharts;
using LiveCharts.Wpf;

namespace FinanceManager.Views
{
    public partial class ChartsWindow : Window
    {
        private readonly User _currentUser;
        private readonly ExpenseService _expenseService;
        private readonly IncomeService _incomeService;

        public ChartsWindow(User currentUser)
        {
            InitializeComponent();
            _currentUser = currentUser;
            _expenseService = new ExpenseService();
            _incomeService = new IncomeService();

            LoadChartData();
        }

        private async void LoadChartData()
        {
            try
            {
                LoggingService.LogInfo("Carregando dados para gráficos...");

                // Verificar se há dados suficientes
                var hasRealData = await CheckForRealDataAsync();

                if (!hasRealData)
                {
                    // **MUDANÇA CRÍTICA**: Não mostrar mensagem prematuramente
                    // Em vez de fechar, apenas deixar gráficos vazios com mensagens informativas
                    ShowEmptyChartsWithMessage();
                    return;
                }

                // Carregar análises baseadas em dados reais
                await UpdateChartsAsync();
                await UpdateAnalysisAsync();

                LoggingService.LogInfo("Gráficos carregados com dados reais do utilizador");
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao carregar dados dos gráficos", ex);
                MessageBox.Show($"Erro ao carregar dados dos gráficos: {ex.Message}", "Erro",
                              MessageBoxButton.OK, MessageBoxImage.Error);
                // Não fechar automaticamente - deixar o utilizador decidir
            }
        }

        private async Task<bool> CheckForRealDataAsync()
        {
            try
            {
                // Verificar se há dados REAIS do utilizador atual
                var expenses = await _expenseService.GetExpensesByUserIdAsync(_currentUser.Id);
                var incomes = await _incomeService.GetIncomesByUserIdAsync(_currentUser.Id);

                var hasData = expenses.Any() || incomes.Any();

                LoggingService.LogInfo($"Verificação de dados: {expenses.Count} despesas, {incomes.Count} receitas");

                return hasData;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao verificar dados reais", ex);
                return false;
            }
        }

        private async Task UpdateChartsAsync()
        {
            try
            {
                LoggingService.LogInfo("Atualizando gráficos com dados reais...");

                // LIMPAR todos os gráficos primeiro - SEM dados fictícios
                ClearAllCharts();

                // Gráfico 1: Despesas por Categoria (mês atual) - APENAS dados reais
                await UpdateExpensesByCategoryChart();

                // Gráfico 2: Tendências mensais (últimos 6 meses) - APENAS dados reais
                await UpdateMonthlyTrendsChart();

                // Gráfico 3: Receitas vs Despesas (últimos 6 meses) - APENAS dados reais
                await UpdateIncomeVsExpensesChart();
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao atualizar gráficos", ex);
            }
        }

        private void ClearAllCharts()
        {
            try
            {
                // Limpar TODOS os gráficos para evitar dados fictícios
                if (CategoryPieChart != null)
                    CategoryPieChart.Series = new SeriesCollection();

                if (MonthlyChart != null)
                    MonthlyChart.Series = new SeriesCollection();

                if (IncomeVsExpensesChart != null)
                    IncomeVsExpensesChart.Series = new SeriesCollection();

                LoggingService.LogInfo("Gráficos limpos - sem dados fictícios");
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao limpar gráficos", ex);
            }
        }

        private async Task UpdateExpensesByCategoryChart()
        {
            try
            {
                var now = DateTime.Now;
                var startDate = new DateTime(now.Year, now.Month, 1);
                var endDate = startDate.AddMonths(1).AddDays(-1);

                var expensesByCategory = await _expenseService.GetExpensesByCategoryAsync(_currentUser.Id, startDate, endDate);

                LoggingService.LogInfo($"Despesas por categoria encontradas: {expensesByCategory.Count}");

                // SÓ criar gráfico se houver dados REAIS
                if (expensesByCategory.Any() && expensesByCategory.Values.Sum() > 0)
                {
                    var series = new SeriesCollection();
                    var colors = new[] { "#FF2196F3", "#FF4CAF50", "#FFFF9800", "#FFE91E63",
                                       "#FF9C27B0", "#FF607D8B", "#FFFF5722", "#FF795548" };
                    int colorIndex = 0;

                    var culture = new System.Globalization.CultureInfo("pt-PT");

                    foreach (var category in expensesByCategory.OrderByDescending(x => x.Value))
                    {
                        if (category.Value > 0) // SÓ adicionar se valor > 0
                        {
                            var color = colors[colorIndex % colors.Length];

                            series.Add(new PieSeries
                            {
                                Title = $"{category.Key} ({category.Value.ToString("C", culture)})",
                                Values = new ChartValues<decimal> { category.Value },
                                DataLabels = true,
                                Fill = new System.Windows.Media.SolidColorBrush(
                                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color))
                            });

                            colorIndex++;
                        }
                    }

                    // Atualizar gráfico APENAS se há dados
                    if (series.Any() && CategoryPieChart != null)
                    {
                        CategoryPieChart.Series = series;
                        LoggingService.LogInfo($"Gráfico de categorias atualizado com {series.Count} categorias reais");
                    }
                }
                else
                {
                    LoggingService.LogInfo("Nenhuma despesa encontrada para o gráfico de categorias");
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro no gráfico de categorias", ex);
            }
        }

        private async Task UpdateMonthlyTrendsChart()
        {
            try
            {
                var trends = await _expenseService.GetExpenseTrendsAsync(_currentUser.Id, 6);

                LoggingService.LogInfo($"Tendências mensais encontradas: {trends.Count}");

                // SÓ criar gráfico se houver dados REAIS
                if (trends.Any() && trends.Sum(t => t.Amount) > 0)
                {
                    var expenseValues = new ChartValues<decimal>();
                    var monthLabels = new string[6];

                    bool hasRealData = false;
                    for (int i = 0; i < trends.Count && i < 6; i++)
                    {
                        expenseValues.Add(trends[i].Amount);
                        monthLabels[i] = trends[i].MonthName;

                        if (trends[i].Amount > 0)
                            hasRealData = true;
                    }

                    // SÓ mostrar gráfico se há dados reais
                    if (hasRealData)
                    {
                        var series = new SeriesCollection
                        {
                            new LineSeries
                            {
                                Title = "Despesas Mensais",
                                Values = expenseValues,
                                PointGeometry = DefaultGeometries.Circle,
                                PointGeometrySize = 8,
                                Fill = System.Windows.Media.Brushes.Transparent,
                                Stroke = System.Windows.Media.Brushes.Red
                            }
                        };

                        if (MonthlyChart != null)
                        {
                            MonthlyChart.Series = series;
                            // Configurar labels dos meses se o gráfico suportar
                            LoggingService.LogInfo("Gráfico de tendências mensais atualizado com dados reais");
                        }
                    }
                }
                else
                {
                    LoggingService.LogInfo("Nenhuma tendência mensal com dados reais encontrada");
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro no gráfico de tendências", ex);
            }
        }

        private async Task UpdateIncomeVsExpensesChart()
        {
            try
            {
                var incomeValues = new ChartValues<decimal>();
                var expenseValues = new ChartValues<decimal>();
                bool hasRealData = false;

                // Últimos 6 meses
                for (int i = 5; i >= 0; i--)
                {
                    var targetDate = DateTime.Now.AddMonths(-i);
                    var startDate = new DateTime(targetDate.Year, targetDate.Month, 1);
                    var endDate = startDate.AddMonths(1).AddDays(-1);

                    var totalIncome = await _incomeService.GetTotalIncomeAsync(_currentUser.Id, startDate, endDate);
                    var totalExpenses = await _expenseService.GetTotalExpensesAsync(_currentUser.Id, startDate, endDate);

                    incomeValues.Add(totalIncome);
                    expenseValues.Add(totalExpenses);

                    // Verificar se há dados reais
                    if (totalIncome > 0 || totalExpenses > 0)
                        hasRealData = true;
                }

                LoggingService.LogInfo($"Receitas vs Despesas - Dados reais encontrados: {hasRealData}");

                // SÓ criar gráfico se houver dados REAIS
                if (hasRealData)
                {
                    var series = new SeriesCollection
                    {
                        new ColumnSeries
                        {
                            Title = "Receitas",
                            Values = incomeValues,
                            Fill = System.Windows.Media.Brushes.Green
                        },
                        new ColumnSeries
                        {
                            Title = "Despesas",
                            Values = expenseValues,
                            Fill = System.Windows.Media.Brushes.Red
                        }
                    };

                    if (IncomeVsExpensesChart != null)
                    {
                        IncomeVsExpensesChart.Series = series;
                        LoggingService.LogInfo("Gráfico Receitas vs Despesas atualizado com dados reais");
                    }
                }
                else
                {
                    LoggingService.LogInfo("Nenhum dado real encontrado para Receitas vs Despesas");
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro no gráfico receitas vs despesas", ex);
            }
        }

        private async Task UpdateAnalysisAsync()
        {
            try
            {
                var now = DateTime.Now;
                var currentMonth = await _incomeService.GetMonthlyAnalysisAsync(_currentUser.Id, now.Year, now.Month);
                var previousMonth = await _incomeService.GetMonthlyAnalysisAsync(_currentUser.Id, now.AddMonths(-1).Year, now.AddMonths(-1).Month);

                var analysis = GenerateAnalysisText(currentMonth, previousMonth);

                // Atualizar texto da análise se o controlo existir
                if (AnalysisTextBlock != null)
                {
                    AnalysisTextBlock.Text = analysis;
                    LoggingService.LogInfo("Análise de texto atualizada com dados reais");
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro na análise", ex);
            }
        }

        private string GenerateAnalysisText(MonthlyAnalysis current, MonthlyAnalysis previous)
        {
            var culture = new System.Globalization.CultureInfo("pt-PT");
            var analysis = $"📊 Análise de {current.MonthName}\n\n";

            // SÓ mostrar dados se existirem
            if (current.TotalIncome == 0 && current.TotalExpenses == 0)
            {
                analysis += "📊 Ainda não há dados para este mês.\n\n";
                analysis += "💡 Adicione receitas e despesas para ver análises detalhadas.\n";
                analysis += "📈 As análises aparecerão automaticamente quando houver dados!";
                return analysis;
            }

            // Dados do mês atual (só se existirem)
            if (current.TotalIncome > 0)
                analysis += $"💰 Receitas: {current.FormattedIncome}\n";

            if (current.TotalExpenses > 0)
                analysis += $"💸 Despesas: {current.FormattedExpenses}\n";

            analysis += $"💼 Saldo: {current.FormattedBalance}";

            if (current.Balance >= 0)
                analysis += " ✅\n\n";
            else
                analysis += " ⚠️\n\n";

            // Taxa de poupança (só se há receitas)
            if (current.TotalIncome > 0)
            {
                analysis += $"📈 Taxa de Poupança: {current.FormattedSavingsRate}\n\n";
            }

            // Comparação com mês anterior (só se há dados)
            if ((previous.TotalIncome > 0 || previous.TotalExpenses > 0) &&
                (current.TotalIncome > 0 || current.TotalExpenses > 0))
            {
                var incomeChange = current.TotalIncome - previous.TotalIncome;
                var expenseChange = current.TotalExpenses - previous.TotalExpenses;

                analysis += "📊 Comparação com mês anterior:\n";

                if (current.TotalIncome > 0 || previous.TotalIncome > 0)
                {
                    analysis += $"Receitas: {incomeChange.ToString("C", culture)} ({(incomeChange >= 0 ? "+" : "")}{(previous.TotalIncome > 0 ? (incomeChange / previous.TotalIncome * 100).ToString("F1") : "0")}%)\n";
                }

                if (current.TotalExpenses > 0 || previous.TotalExpenses > 0)
                {
                    analysis += $"Despesas: {expenseChange.ToString("C", culture)} ({(expenseChange >= 0 ? "+" : "")}{(previous.TotalExpenses > 0 ? (expenseChange / previous.TotalExpenses * 100).ToString("F1") : "0")}%)\n\n";
                }
            }

            // Categoria principal de despesas (só se existir)
            if (current.ExpensesByCategory.Any())
            {
                var topCategory = current.ExpensesByCategory.OrderByDescending(x => x.Value).First();
                var percentage = current.TotalExpenses > 0 ? (topCategory.Value / current.TotalExpenses) * 100 : 0;

                analysis += $"🏷️ Maior Despesa: {topCategory.Key}\n";
                analysis += $"   {topCategory.Value.ToString("C", culture)} ({percentage:F1}%)\n\n";
            }

            // Recomendações baseadas nos dados REAIS
            analysis += "💡 Recomendações:\n";
            if (current.Balance < 0)
            {
                analysis += "• ⚠️ Gastos superiores às receitas\n";
                analysis += "• 📉 Considere reduzir despesas\n";
                analysis += "• 💰 Procure aumentar suas receitas\n";
            }
            else if (current.SavingsRate < 10 && current.TotalIncome > 0)
            {
                analysis += "• 📊 Taxa de poupança baixa (<10%)\n";
                analysis += "• 🎯 Tente poupar pelo menos 20%\n";
            }
            else if (current.SavingsRate >= 20)
            {
                analysis += "• 🎉 Excelente taxa de poupança!\n";
                analysis += "• 📈 Continue assim!\n";
            }
            else if (current.TotalIncome == 0 && current.TotalExpenses > 0)
            {
                analysis += "• 💰 Adicione suas receitas para análise completa\n";
            }

            return analysis;
        }

        private void ShowEmptyChartsWithMessage()
        {
            try
            {
                // Limpar todos os gráficos
                ClearAllCharts();

                // Mostrar mensagem informativa na análise
                if (AnalysisTextBlock != null)
                {
                    AnalysisTextBlock.Text =
                        "📊 Bem-vindo aos Gráficos e Análises!\n\n" +
                        "💡 Para começar a ver análises detalhadas:\n\n" +
                        "• Adicione algumas despesas usando o menu 'Despesas'\n" +
                        "• Registre suas receitas no menu 'Receitas'\n" +
                        "• Defina objetivos no menu 'Poupanças'\n" +
                        "• Volte aqui para ver gráficos automáticos\n\n" +
                        "📈 Os gráficos aparecerão automaticamente quando houver dados!\n\n" +
                        "🔄 Use o botão 'Atualizar' depois de adicionar dados.";
                }

                LoggingService.LogInfo("Gráficos configurados em modo vazio com mensagem informativa");
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao configurar gráficos vazios", ex);
            }
        }

        private void RefreshCharts_Click(object sender, RoutedEventArgs e)
        {
            LoggingService.LogInfo("Atualizando gráficos manualmente...");
            LoadChartData();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            LoggingService.LogInfo("ChartsWindow fechada pelo utilizador");
        }
    }
}