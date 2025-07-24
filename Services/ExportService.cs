using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using FinanceManager.Models;

namespace FinanceManager.Services
{
    public class ExportService
    {
        private readonly CultureInfo _culture = new CultureInfo("pt-PT");

        public async Task ExportInvestmentsAsync(List<Investment> investments, int userId)
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Title = "Exportar Portfólio",
                    Filter = "Ficheiro CSV (*.csv)|*.csv|Ficheiro Excel (*.xlsx)|*.xlsx|Ficheiro de texto (*.txt)|*.txt",
                    DefaultExt = "csv",
                    FileName = $"portfolio_{DateTime.Now:yyyy-MM-dd}.csv"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    var extension = Path.GetExtension(saveDialog.FileName).ToLower();

                    switch (extension)
                    {
                        case ".csv":
                            await ExportToCsvAsync(investments, saveDialog.FileName);
                            break;
                        case ".xlsx":
                            await ExportToExcelAsync(investments, saveDialog.FileName);
                            break;
                        case ".txt":
                            await ExportToTextAsync(investments, saveDialog.FileName);
                            break;
                        default:
                            MessageBox.Show("Formato de ficheiro não suportado.", "Erro",
                                          MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                    }

                    var result = MessageBox.Show(
                        $"✅ Portfólio exportado com sucesso!\n\n" +
                        $"📁 Ficheiro: {Path.GetFileName(saveDialog.FileName)}\n" +
                        $"📊 {investments.Count} investimentos exportados\n\n" +
                        $"Quer abrir a pasta do ficheiro?",
                        "Exportação Concluída",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (result == MessageBoxResult.Yes)
                    {
                        System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{saveDialog.FileName}\"");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro na exportação: {ex.Message}", "Erro",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ExportToCsvAsync(List<Investment> investments, string fileName)
        {
            await Task.Run(() =>
            {
                var csv = new StringBuilder();

                // Header
                csv.AppendLine("Nome;Tipo;Data de Compra;Quantidade;Preço de Compra;Preço Atual;Valor Total;Ganho/Perda;Ganho/Perda %;Símbolo;Descrição;Corretora");

                // Group investments by type for better organization
                var groupedByType = investments.GroupBy(i => i.Type).OrderBy(g => g.Key);

                foreach (var group in groupedByType)
                {
                    // Add type header
                    csv.AppendLine($"\n=== {group.Key.ToUpper()} ===");

                    // Type statistics
                    var typeStats = CalculateTypeStatistics(group.ToList());
                    csv.AppendLine($"Total do Tipo;{group.Count()} investimentos;Total Investido: {typeStats.totalCost.ToString("C", _culture)};Valor Atual: {typeStats.totalValue.ToString("C", _culture)};Ganho/Perda: {typeStats.totalProfitLoss.ToString("C", _culture)}");
                    csv.AppendLine(); // Empty line

                    // Investment data
                    foreach (var investment in group.OrderByDescending(i => i.PurchaseDate))
                    {
                        var currentPrice = investment.CurrentPrice ?? 0;
                        var currentValue = investment.Quantity * currentPrice;
                        var investedAmount = investment.Quantity * investment.PurchasePrice;
                        var profitLoss = currentValue - investedAmount;
                        var profitLossPercent = investment.PurchasePrice > 0 ?
                            ((currentPrice - investment.PurchasePrice) / investment.PurchasePrice * 100) : 0;

                        var line = $"{investment.Name};" +
                                 $"{investment.Type};" +
                                 $"{investment.PurchaseDate:dd/MM/yyyy};" +
                                 $"{investment.Quantity:F8};" +
                                 $"{investment.PurchasePrice:F2};" +
                                 $"{currentPrice:F2};" +
                                 $"{currentValue:F2};" +
                                 $"{profitLoss:F2};" +
                                 $"{profitLossPercent:F2}%;" +
                                 $"{ExtractSymbolFromName(investment.Name)};" +
                                 $"{investment.Description ?? ""};" +
                                 $"{investment.Broker ?? ""}";

                        csv.AppendLine(line);
                    }
                    csv.AppendLine(); // Empty line between types
                }

                // Add summary at the end
                var totalStats = CalculateTypeStatistics(investments);
                csv.AppendLine("\n=== RESUMO GERAL ===");
                csv.AppendLine($"Total de Investimentos;{investments.Count}");
                csv.AppendLine($"Total Investido;{totalStats.totalCost.ToString("C", _culture)}");
                csv.AppendLine($"Valor Atual;{totalStats.totalValue.ToString("C", _culture)}");
                csv.AppendLine($"Ganho/Perda Total;{totalStats.totalProfitLoss.ToString("C", _culture)}");
                csv.AppendLine($"Retorno %;{(totalStats.totalCost > 0 ? (totalStats.totalProfitLoss / totalStats.totalCost * 100) : 0):F2}%");
                csv.AppendLine($"Data de Exportação;{DateTime.Now:dd/MM/yyyy HH:mm:ss}");

                File.WriteAllText(fileName, csv.ToString(), Encoding.UTF8);
            });
        }

        private async Task ExportToExcelAsync(List<Investment> investments, string fileName)
        {
            // For now, export as CSV with .xlsx extension
            // You can implement proper Excel export using EPPlus or similar library
            await ExportToCsvAsync(investments, fileName.Replace(".xlsx", ".csv"));

            MessageBox.Show("📝 Nota: Exportação Excel ainda não implementada.\n" +
                          "Ficheiro exportado como CSV.", "Info",
                          MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task ExportToTextAsync(List<Investment> investments, string fileName)
        {
            await Task.Run(() =>
            {
                var text = new StringBuilder();

                text.AppendLine("=".PadRight(80, '='));
                text.AppendLine($"📈 RELATÓRIO DE PORTFÓLIO");
                text.AppendLine($"📅 Gerado em: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
                text.AppendLine("=".PadRight(80, '='));
                text.AppendLine();

                // Summary
                var totalStats = CalculateTypeStatistics(investments);
                text.AppendLine("📊 RESUMO GERAL:");
                text.AppendLine($"   Total de Investimentos: {investments.Count}");
                text.AppendLine($"   Valor Total Investido: {totalStats.totalCost.ToString("C", _culture)}");
                text.AppendLine($"   Valor Atual do Portfólio: {totalStats.totalValue.ToString("C", _culture)}");
                text.AppendLine($"   Ganho/Perda Total: {totalStats.totalProfitLoss.ToString("C", _culture)}");
                text.AppendLine($"   Retorno Percentual: {(totalStats.totalCost > 0 ? (totalStats.totalProfitLoss / totalStats.totalCost * 100) : 0):F2}%");
                text.AppendLine();

                // Group by type
                var groupedByType = investments.GroupBy(i => i.Type).OrderBy(g => g.Key);

                foreach (var group in groupedByType)
                {
                    text.AppendLine("-".PadRight(60, '-'));
                    text.AppendLine($"📁 CATEGORIA: {group.Key.ToUpper()}");
                    text.AppendLine("-".PadRight(60, '-'));

                    var typeStats = CalculateTypeStatistics(group.ToList());
                    text.AppendLine($"   Investimentos nesta categoria: {group.Count()}");
                    text.AppendLine($"   Valor investido: {typeStats.totalCost.ToString("C", _culture)}");
                    text.AppendLine($"   Valor atual: {typeStats.totalValue.ToString("C", _culture)}");
                    text.AppendLine($"   Ganho/Perda: {typeStats.totalProfitLoss.ToString("C", _culture)}");
                    text.AppendLine();

                    foreach (var investment in group.OrderByDescending(i => i.PurchaseDate))
                    {
                        var currentPrice = investment.CurrentPrice ?? 0;
                        var currentValue = investment.Quantity * currentPrice;
                        var investedAmount = investment.Quantity * investment.PurchasePrice;
                        var profitLoss = currentValue - investedAmount;
                        var profitLossPercent = investment.PurchasePrice > 0 ?
                            ((currentPrice - investment.PurchasePrice) / investment.PurchasePrice * 100) : 0;

                        text.AppendLine($"   📊 {investment.Name}");
                        text.AppendLine($"      Data de Compra: {investment.PurchaseDate:dd/MM/yyyy}");
                        text.AppendLine($"      Quantidade: {investment.Quantity:F8}");
                        text.AppendLine($"      Preço de Compra: {investment.PurchasePrice.ToString("C", _culture)}");
                        text.AppendLine($"      Preço Atual: {currentPrice.ToString("C", _culture)}");
                        text.AppendLine($"      Valor Total: {currentValue.ToString("C", _culture)}");
                        text.AppendLine($"      Ganho/Perda: {profitLoss.ToString("C", _culture)} ({profitLossPercent:F2}%)");
                        if (!string.IsNullOrEmpty(investment.Description))
                            text.AppendLine($"      Descrição: {investment.Description}");
                        if (!string.IsNullOrEmpty(investment.Broker))
                            text.AppendLine($"      Corretora: {investment.Broker}");
                        text.AppendLine();
                    }
                }

                text.AppendLine("=".PadRight(80, '='));
                text.AppendLine("📄 Fim do Relatório");
                text.AppendLine("=".PadRight(80, '='));

                File.WriteAllText(fileName, text.ToString(), Encoding.UTF8);
            });
        }

        private (decimal totalCost, decimal totalValue, decimal totalProfitLoss) CalculateTypeStatistics(List<Investment> investments)
        {
            var totalCost = investments.Sum(i => i.Quantity * i.PurchasePrice);
            var totalValue = investments.Sum(i => i.Quantity * (i.CurrentPrice ?? 0));
            var totalProfitLoss = totalValue - totalCost;

            return (totalCost, totalValue, totalProfitLoss);
        }

        private string ExtractSymbolFromName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "";

            var start = name.LastIndexOf('(');
            var end = name.LastIndexOf(')');

            if (start >= 0 && end > start)
            {
                return name.Substring(start + 1, end - start - 1).Trim().ToUpper();
            }

            return "";
        }

        public async Task<bool> ExportToExcel(List<Investment> investments, string filePath)
        {
            try
            {
                // This method can be called directly if you have a specific file path
                await ExportToExcelAsync(investments, filePath);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao exportar para Excel: {ex.Message}", "Erro",
                              MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        public async Task<bool> ExportToCsv(List<Investment> investments, string filePath)
        {
            try
            {
                await ExportToCsvAsync(investments, filePath);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao exportar para CSV: {ex.Message}", "Erro",
                              MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        public async Task<string> GenerateQuickReport(List<Investment> investments)
        {
            return await Task.Run(() =>
            {
                var report = new StringBuilder();
                var totalStats = CalculateTypeStatistics(investments);

                report.AppendLine($"📈 RELATÓRIO RÁPIDO - {DateTime.Now:dd/MM/yyyy HH:mm}");
                report.AppendLine($"Total: {investments.Count} investimentos");
                report.AppendLine($"Investido: {totalStats.totalCost.ToString("C", _culture)}");
                report.AppendLine($"Atual: {totalStats.totalValue.ToString("C", _culture)}");
                report.AppendLine($"Ganho/Perda: {totalStats.totalProfitLoss.ToString("C", _culture)}");
                report.AppendLine($"Retorno: {(totalStats.totalCost > 0 ? (totalStats.totalProfitLoss / totalStats.totalCost * 100) : 0):F2}%");

                var groupedByType = investments.GroupBy(i => i.Type).OrderByDescending(g => g.Sum(i => i.Quantity * (i.CurrentPrice ?? 0)));

                report.AppendLine("\nPor categoria:");
                foreach (var group in groupedByType)
                {
                    var typeStats = CalculateTypeStatistics(group.ToList());
                    report.AppendLine($"• {group.Key}: {typeStats.totalValue.ToString("C", _culture)} ({group.Count()} investimentos)");
                }

                return report.ToString();
            });
        }
    }
}