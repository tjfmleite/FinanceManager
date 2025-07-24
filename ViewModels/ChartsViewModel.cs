using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using FinanceManager.Models;
using FinanceManager.Services;
using FinanceManager.Helpers;
using LiveCharts;
using LiveCharts.Wpf;

namespace FinanceManager.ViewModels
{
    public class ChartsViewModel : BaseViewModel
    {
        private readonly ExpenseService _expenseService;
        private readonly IncomeService _incomeService;
        private readonly User _currentUser;

        private string _analysisText = "A carregar análise...";
        private string _monthlyComparisonText = "A carregar comparação...";
        private bool _hasData = false;
        private DateTime _selectedMonth = DateTime.Now;

        public ChartsViewModel(User user)
        {
            _currentUser = user;
            _expenseService = new ExpenseService();
            _incomeService = new IncomeService();

            // Inicializar coleções de gráficos VAZIAS
            ExpensesByCategory = new SeriesCollection();
            MonthlyTrends = new SeriesCollection();
            IncomeVsExpenses = new SeriesCollection();

            MonthlyLabels = new List<string>();

            // Comandos
            RefreshCommand = new RelayCommand(async () => await RefreshDataAsync());
            PreviousMonthCommand = new RelayCommand(() => { SelectedMonth = SelectedMonth.AddMonths(-1); });
            NextMonthCommand = new RelayCommand(() => { SelectedMonth = SelectedMonth.AddMonths(1); });

            // Carregar dados iniciais
            _ = Task.Run(RefreshDataAsync);
        }

        #region Propriedades

        public DateTime SelectedMonth
        {
            get => _selectedMonth;
            set
            {
                if (SetProperty(ref _selectedMonth, value))
                {
                    OnPropertyChanged(nameof(SelectedMonthText));
                    _ = Task.Run(RefreshDataAsync);
                }
            }
        }

        public string SelectedMonthText => SelectedMonth.ToString("MMMM yyyy", new System.Globalization.CultureInfo("pt-PT"));

        public string AnalysisText
        {
            get => _analysisText;
            set => SetProperty(ref _analysisText, value);
        }

        public string MonthlyComparisonText
        {
            get => _monthlyComparisonText;
            set => SetProperty(ref _monthlyComparisonText, value);
        }

        public bool HasData
        {
            get => _hasData;
            set => SetProperty(ref _hasData, value);
        }

        // Gráficos
        public SeriesCollection ExpensesByCategory { get; set; }
        public SeriesCollection MonthlyTrends { get; set; }
        public SeriesCollection IncomeVsExpenses { get; set; }
        public List<string> MonthlyLabels { get; set; }

        #endregion

        #region Comandos

        public ICommand RefreshCommand { get; }
        public ICommand PreviousMonthCommand { get; }
        public ICommand NextMonthCommand { get; }

        #endregion

        #region Métodos

        public async Task RefreshDataAsync()
        {
            try
            {
                LoggingService.LogInfo("Atualizando análises dos gráficos...");

                // Verificar se há dados REAIS para análise
                var hasAnyData = await CheckForRealDataAsync();
                HasData = hasAnyData;

                if (!hasAnyData)
                {
                    ShowNoDataMessage();
                    return;
                }

                // Atualizar análises baseadas APENAS em dados reais
                await UpdateMonthlyAnalysisAsync();
                await UpdateChartsAsync();
                await UpdateComparisonAnalysisAsync();

                LoggingService.LogInfo("Análises atualizadas com dados reais");
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao atualizar análises", ex);
                AnalysisText = "Erro ao carregar análise. Tente novamente.";
                MonthlyComparisonText = "Erro ao carregar comparação.";
            }
        }

        private async Task<bool> CheckForRealDataAsync()
        {
            try
            {
                // Verificar se há dados REAIS (pelo menos algumas despesas ou receitas)
                var expenses = await _expenseService.GetExpensesByUserIdAsync(_currentUser.Id);
                var incomes = await _incomeService.GetIncomesByUserIdAsync(_currentUser.Id);

                // Só retorna true se houver dados reais do utilizador
                return expenses.Any() || incomes.Any();
            }
            catch
            {
                return false;
            }
        }

        private void ShowNoDataMessage()
        {
            AnalysisText = "📊 Ainda não há dados suficientes para análise.\n\n" +
                          "💡 Para começar a ver análises:\n" +
                          "• Adicione algumas despesas\n" +
                          "• Registre suas receitas\n" +
                          "• Volte aqui depois de ter alguns dados\n\n" +
                          "📈 As análises aparecerão automaticamente quando houver dados reais!";

            MonthlyComparisonText = "Aguardando dados reais para comparação mensal...";

            // LIMPAR COMPLETAMENTE todos os gráficos - SEM dados fictícios
            ExpensesByCategory.Clear();
            MonthlyTrends.Clear();
            IncomeVsExpenses.Clear();
            MonthlyLabels.Clear();

            // Notificar mudanças
            OnPropertyChanged(nameof(ExpensesByCategory));
            OnPropertyChanged(nameof(MonthlyTrends));
            OnPropertyChanged(nameof(IncomeVsExpenses));
            OnPropertyChanged(nameof(MonthlyLabels));
        }

        private async Task UpdateMonthlyAnalysisAsync()
        {
            try
            {
                var analysis = await _incomeService.GetMonthlyAnalysisAsync(_currentUser.Id, SelectedMonth.Year, SelectedMonth.Month);

                // SÓ mostrar análise se houver dados reais
                if (analysis.TotalIncome == 0 && analysis.TotalExpenses == 0)
                {
                    AnalysisText = $"📅 {analysis.MonthName}\n\n" +
                                  "📊 Ainda não há dados para este mês.\n\n" +
                                  "💡 Adicione receitas e despesas para ver a análise detalhada.";
                    return;
                }

                var culture = new System.Globalization.CultureInfo("pt-PT");
                var analysisReport = $"📅 Análise de {analysis.MonthName}\n\n";

                // Receitas e Despesas (apenas se houver dados)
                if (analysis.TotalIncome > 0)
                    analysisReport += $"💰 Receitas: {analysis.FormattedIncome}\n";

                if (analysis.TotalExpenses > 0)
                    analysisReport += $"💸 Despesas: {analysis.FormattedExpenses}\n";

                analysisReport += $"💼 Saldo: {analysis.FormattedBalance}";

                if (analysis.Balance >= 0)
                    analysisReport += " ✅\n\n";
                else
                    analysisReport += " ⚠️\n\n";

                // Taxa de poupança (só se houver receitas)
                if (analysis.TotalIncome > 0)
                {
                    analysisReport += $"📈 Taxa de Poupança: {analysis.FormattedSavingsRate}\n\n";
                }

                // Análise das categorias principais (só se houver dados)
                if (analysis.ExpensesByCategory.Any())
                {
                    var topExpenseCategory = analysis.ExpensesByCategory.OrderByDescending(x => x.Value).First();
                    var expensePercentage = analysis.TotalExpenses > 0 ? (topExpenseCategory.Value / analysis.TotalExpenses) * 100 : 0;

                    analysisReport += $"🏷️ Maior Despesa: {topExpenseCategory.Key}\n";
                    analysisReport += $"   {topExpenseCategory.Value.ToString("C", culture)} ({expensePercentage:F1}%)\n\n";
                }

                if (analysis.IncomesByCategory.Any())
                {
                    var topIncomeCategory = analysis.IncomesByCategory.OrderByDescending(x => x.Value).First();
                    analysisReport += $"💼 Maior Receita: {topIncomeCategory.Key}\n";
                    analysisReport += $"   {topIncomeCategory.Value.ToString("C", culture)}\n\n";
                }

                // Recomendações baseadas nos dados REAIS
                analysisReport += "💡 Recomendações:\n";

                if (analysis.Balance < 0)
                {
                    analysisReport += "• ⚠️ Gastos superiores às receitas\n";
                    analysisReport += "• 📉 Considere reduzir despesas\n";
                    analysisReport += "• 💰 Procure aumentar suas receitas\n";
                }
                else if (analysis.SavingsRate < 10 && analysis.TotalIncome > 0)
                {
                    analysisReport += "• 📊 Taxa de poupança baixa (<10%)\n";
                    analysisReport += "• 🎯 Tente poupar pelo menos 20%\n";
                }
                else if (analysis.SavingsRate >= 20)
                {
                    analysisReport += "• 🎉 Excelente taxa de poupança!\n";
                    analysisReport += "• 📈 Continue assim!\n";
                }
                else if (analysis.TotalIncome == 0 && analysis.TotalExpenses > 0)
                {
                    analysisReport += "• 💰 Adicione suas receitas para análise completa\n";
                }

                AnalysisText = analysisReport;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro na análise mensal", ex);
                AnalysisText = "Erro ao gerar análise mensal.";
            }
        }

        private async Task UpdateChartsAsync()
        {
            try
            {
                // LIMPAR gráficos primeiro
                ExpensesByCategory.Clear();
                MonthlyTrends.Clear();
                IncomeVsExpenses.Clear();
                MonthlyLabels.Clear();

                // Gráfico 1: Despesas por Categoria (apenas dados reais)
                await UpdateExpensesByCategoryChart();

                // Gráfico 2: Tendências mensais (apenas dados reais)
                await UpdateMonthlyTrendsChart();

                // Gráfico 3: Receitas vs Despesas (apenas dados reais)
                await UpdateIncomeVsExpensesChart();

                // Notificar mudanças
                OnPropertyChanged(nameof(ExpensesByCategory));
                OnPropertyChanged(nameof(MonthlyTrends));
                OnPropertyChanged(nameof(IncomeVsExpenses));
                OnPropertyChanged(nameof(MonthlyLabels));
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao atualizar gráficos", ex);
            }
        }

        private async Task UpdateExpensesByCategoryChart()
        {
            try
            {
                var startDate = new DateTime(SelectedMonth.Year, SelectedMonth.Month, 1);
                var endDate = startDate.AddMonths(1).AddDays(-1);

                var expensesByCategory = await _expenseService.GetExpensesByCategoryAsync(_currentUser.Id, startDate, endDate);

                // SÓ criar gráfico se houver dados REAIS
                if (expensesByCategory.Any())
                {
                    var culture = new System.Globalization.CultureInfo("pt-PT");
                    var colors = new[] { "#FF2196F3", "#FF4CAF50", "#FFFF9800", "#FFE91E63",
                                       "#FF9C27B0", "#FF607D8B", "#FFFF5722", "#FF795548" };
                    int colorIndex = 0;

                    foreach (var category in expensesByCategory.OrderByDescending(x => x.Value))
                    {
                        var color = colors[colorIndex % colors.Length];

                        ExpensesByCategory.Add(new PieSeries
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

                // SÓ criar gráfico se houver dados REAIS
                if (trends.Any() && trends.Sum(t => t.Amount) > 0)
                {
                    var expenseValues = new ChartValues<decimal>();

                    MonthlyLabels.Clear();
                    foreach (var trend in trends)
                    {
                        MonthlyLabels.Add(trend.MonthName);
                        expenseValues.Add(trend.Amount);
                    }

                    MonthlyTrends.Add(new LineSeries
                    {
                        Title = "Despesas Mensais",
                        Values = expenseValues,
                        PointGeometry = DefaultGeometries.Circle,
                        PointGeometrySize = 8,
                        Fill = System.Windows.Media.Brushes.Transparent,
                        Stroke = System.Windows.Media.Brushes.Red
                    });
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
                bool hasAnyData = false;

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

                    // Verificar se há pelo menos algum dado
                    if (totalIncome > 0 || totalExpenses > 0)
                        hasAnyData = true;
                }

                // SÓ criar gráfico se houver dados REAIS
                if (hasAnyData)
                {
                    IncomeVsExpenses.Add(new ColumnSeries
                    {
                        Title = "Receitas",
                        Values = incomeValues,
                        Fill = System.Windows.Media.Brushes.Green
                    });

                    IncomeVsExpenses.Add(new ColumnSeries
                    {
                        Title = "Despesas",
                        Values = expenseValues,
                        Fill = System.Windows.Media.Brushes.Red
                    });
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro no gráfico receitas vs despesas", ex);
            }
        }

        private async Task UpdateComparisonAnalysisAsync()
        {
            try
            {
                var currentMonth = DateTime.Now;
                var previousMonth = currentMonth.AddMonths(-1);

                var currentAnalysis = await _incomeService.GetMonthlyAnalysisAsync(_currentUser.Id, currentMonth.Year, currentMonth.Month);
                var previousAnalysis = await _incomeService.GetMonthlyAnalysisAsync(_currentUser.Id, previousMonth.Year, previousMonth.Month);

                // SÓ fazer comparação se houver dados em pelo menos um dos meses
                if (currentAnalysis.TotalIncome == 0 && currentAnalysis.TotalExpenses == 0 &&
                    previousAnalysis.TotalIncome == 0 && previousAnalysis.TotalExpenses == 0)
                {
                    MonthlyComparisonText = "📊 Comparação Mensal\n\n" +
                                          "Ainda não há dados suficientes para comparação.\n" +
                                          "Adicione receitas e despesas para ver a evolução mensal.";
                    return;
                }

                var culture = new System.Globalization.CultureInfo("pt-PT");
                var comparison = $"📊 Comparação Mensal\n\n";

                comparison += $"📅 {currentAnalysis.MonthName} vs {previousAnalysis.MonthName}\n\n";

                // Comparar receitas (só se houver dados)
                if (currentAnalysis.TotalIncome > 0 || previousAnalysis.TotalIncome > 0)
                {
                    var incomeChange = currentAnalysis.TotalIncome - previousAnalysis.TotalIncome;
                    var incomeChangePercent = previousAnalysis.TotalIncome > 0 ? (incomeChange / previousAnalysis.TotalIncome) * 100 : 0;

                    comparison += $"💰 Receitas:\n";
                    comparison += $"   Atual: {currentAnalysis.FormattedIncome}\n";
                    comparison += $"   Anterior: {previousAnalysis.FormattedIncome}\n";
                    comparison += $"   Variação: {incomeChange.ToString("C", culture)} ({incomeChangePercent:+0.0;-0.0;0.0}%)\n\n";
                }

                // Comparar despesas (só se houver dados)
                if (currentAnalysis.TotalExpenses > 0 || previousAnalysis.TotalExpenses > 0)
                {
                    var expenseChange = currentAnalysis.TotalExpenses - previousAnalysis.TotalExpenses;
                    var expenseChangePercent = previousAnalysis.TotalExpenses > 0 ? (expenseChange / previousAnalysis.TotalExpenses) * 100 : 0;

                    comparison += $"💸 Despesas:\n";
                    comparison += $"   Atual: {currentAnalysis.FormattedExpenses}\n";
                    comparison += $"   Anterior: {previousAnalysis.FormattedExpenses}\n";
                    comparison += $"   Variação: {expenseChange.ToString("C", culture)} ({expenseChangePercent:+0.0;-0.0;0.0}%)\n\n";
                }

                // Comparar saldo
                var balanceChange = currentAnalysis.Balance - previousAnalysis.Balance;
                comparison += $"💼 Saldo:\n";
                comparison += $"   Atual: {currentAnalysis.FormattedBalance}\n";
                comparison += $"   Anterior: {previousAnalysis.FormattedBalance}\n";
                comparison += $"   Variação: {balanceChange.ToString("C", culture)}\n\n";

                // Tendência baseada em dados reais
                comparison += "📈 Tendência:\n";
                if (currentAnalysis.TotalIncome == 0 && currentAnalysis.TotalExpenses == 0)
                {
                    comparison += "📊 Ainda sem dados neste mês.\n";
                }
                else if (previousAnalysis.TotalIncome == 0 && previousAnalysis.TotalExpenses == 0)
                {
                    comparison += "🎉 Primeiro mês com dados registados!\n";
                }
                else
                {
                    // Análise comparativa real
                    var incomeChange = currentAnalysis.TotalIncome - previousAnalysis.TotalIncome;
                    var expenseChange = currentAnalysis.TotalExpenses - previousAnalysis.TotalExpenses;

                    if (incomeChange > 0 && expenseChange < 0)
                    {
                        comparison += "🎉 Excelente! Receitas aumentaram e despesas diminuíram.\n";
                    }
                    else if (incomeChange > 0 && expenseChange > 0)
                    {
                        if (incomeChange > expenseChange)
                            comparison += "✅ Bom! Receitas cresceram mais que as despesas.\n";
                        else
                            comparison += "⚠️ Atenção! Despesas cresceram mais que as receitas.\n";
                    }
                    else if (incomeChange < 0 && expenseChange < 0)
                    {
                        if (Math.Abs(expenseChange) > Math.Abs(incomeChange))
                            comparison += "👍 Positivo! Reduziu mais despesas que receitas.\n";
                        else
                            comparison += "⚠️ Cuidado! Receitas diminuíram mais que as despesas.\n";
                    }
                    else if (incomeChange < 0 && expenseChange > 0)
                    {
                        comparison += "🚨 Atenção! Receitas diminuíram e despesas aumentaram.\n";
                    }
                    else
                    {
                        comparison += "📊 Situação estável comparada ao mês anterior.\n";
                    }
                }

                MonthlyComparisonText = comparison;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro na comparação mensal", ex);
                MonthlyComparisonText = "Erro ao gerar comparação mensal.";
            }
        }

        #endregion
    }
}