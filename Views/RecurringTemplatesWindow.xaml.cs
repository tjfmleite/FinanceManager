using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using FinanceManager.Models;
using FinanceManager.Services;

namespace FinanceManager.Views
{
    public partial class RecurringTemplatesWindow : Window
    {
        private readonly User _currentUser;
        private readonly RecurringExpenseService _recurringService;
        private ObservableCollection<RecurringExpenseTemplate> _templates;

        public RecurringTemplatesWindow(User currentUser)
        {
            InitializeComponent();
            _currentUser = currentUser;
            _recurringService = new RecurringExpenseService();
            _templates = new ObservableCollection<RecurringExpenseTemplate>();

            InitializeWindow();
            LoadTemplates();
        }

        private void InitializeWindow()
        {
            try
            {
                // Verificar se o TemplatesListBox existe no XAML
                if (TemplatesListBox != null)
                {
                    TemplatesListBox.ItemsSource = _templates;
                    LoggingService.LogInfo("TemplatesListBox configurado com sucesso");
                }
                else
                {
                    LoggingService.LogWarning("TemplatesListBox não encontrado no XAML");

                    // Criar MessageBox como fallback se XAML não tiver o controle
                    MessageBox.Show("Aviso: Interface de templates não completamente carregada.",
                                  "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao inicializar janela de templates", ex);
            }
        }

        private void LoadTemplates()
        {
            try
            {
                LoggingService.LogInfo("Carregando templates de despesas recorrentes");

                var templates = _recurringService.GetCommonTemplates();
                _templates.Clear();

                foreach (var template in templates)
                {
                    _templates.Add(template);
                }

                LoggingService.LogInfo($"Carregados {_templates.Count} templates");

                // Atualizar título da janela
                if (_templates.Count > 0)
                {
                    this.Title = $"📋 Templates de Despesas ({_templates.Count} disponíveis)";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao carregar modelos: {ex.Message}", "Erro",
                              MessageBoxButton.OK, MessageBoxImage.Error);
                LoggingService.LogError("Erro ao carregar modelos", ex);
            }
        }

        private void Select_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Verificar se TemplatesListBox existe e tem item selecionado
                if (TemplatesListBox?.SelectedItem is RecurringExpenseTemplate selectedTemplate)
                {
                    ApplySelectedTemplate(selectedTemplate);
                }
                else
                {
                    // Fallback: Mostrar lista de templates disponíveis
                    ShowTemplateSelectionFallback();
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro no evento Select_Click", ex);
                MessageBox.Show($"Erro ao selecionar template: {ex.Message}", "Erro",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplySelectedTemplate(RecurringExpenseTemplate selectedTemplate)
        {
            try
            {
                LoggingService.LogInfo($"Template selecionado: {selectedTemplate.Name}");

                // Criar despesa recorrente baseada no template usando o método do service
                var recurringExpense = _recurringService.CreateFromTemplate(selectedTemplate, _currentUser.Id);

                // Abrir janela de adição/edição com os dados pré-preenchidos
                var addWindow = new AddRecurringExpenseWindow(_currentUser, recurringExpense);
                addWindow.Owner = this.Owner;

                if (addWindow.ShowDialog() == true)
                {
                    LoggingService.LogInfo("Template aplicado com sucesso");
                    this.DialogResult = true;
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Erro ao aplicar template {selectedTemplate.Name}", ex);
                MessageBox.Show($"Erro ao usar modelo: {ex.Message}", "Erro",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowTemplateSelectionFallback()
        {
            try
            {
                if (_templates.Count == 0)
                {
                    MessageBox.Show("Nenhum template disponível no momento.", "Informação",
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Criar uma string com todos os templates
                var templatesList = "Selecione um template:\n\n";
                for (int i = 0; i < _templates.Count && i < 10; i++) // Limitar a 10 para não ficar muito grande
                {
                    var template = _templates[i];
                    templatesList += $"{i + 1}. {template.Name} - {template.FormattedAmount} ({template.Frequency})\n";
                }

                if (_templates.Count > 10)
                {
                    templatesList += $"\n... e mais {_templates.Count - 10} templates disponíveis.";
                }

                templatesList += "\nPor favor, selecione um template na lista ou pressione Cancel para fechar.";

                var result = MessageBox.Show(templatesList, "Templates Disponíveis",
                                            MessageBoxButton.OKCancel, MessageBoxImage.Information);

                if (result == MessageBoxResult.OK)
                {
                    // Como não podemos fazer seleção via MessageBox, aplicar o primeiro template como exemplo
                    ApplySelectedTemplate(_templates[0]);
                }
                else
                {
                    MessageBox.Show("Por favor, selecione um template na lista.", "Seleção Necessária",
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao mostrar fallback de seleção", ex);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            LoggingService.LogInfo("Seleção de template cancelada pelo usuário");
            this.DialogResult = false;
            this.Close();
        }

        // Método auxiliar para filtrar templates por categoria (se necessário)
        private void FilterTemplatesByCategory(string category)
        {
            try
            {
                if (string.IsNullOrEmpty(category) || category == "Todas")
                {
                    LoadTemplates(); // Recarregar todos
                    return;
                }

                var allTemplates = _recurringService.GetCommonTemplates();
                var filteredTemplates = allTemplates.Where(t => t.Category.Equals(category, StringComparison.OrdinalIgnoreCase));

                _templates.Clear();
                foreach (var template in filteredTemplates)
                {
                    _templates.Add(template);
                }

                LoggingService.LogInfo($"Filtrados {_templates.Count} templates para categoria '{category}'");
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Erro ao filtrar templates por categoria '{category}'", ex);
            }
        }

        // Método para buscar templates por nome/descrição (se necessário)
        private void SearchTemplates(string searchText)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(searchText))
                {
                    LoadTemplates(); // Recarregar todos
                    return;
                }

                var allTemplates = _recurringService.GetCommonTemplates();
                var searchResults = allTemplates.Where(t =>
                    t.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    t.Description.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    t.Category.Contains(searchText, StringComparison.OrdinalIgnoreCase));

                _templates.Clear();
                foreach (var template in searchResults)
                {
                    _templates.Add(template);
                }

                LoggingService.LogInfo($"Encontrados {_templates.Count} templates para busca '{searchText}'");
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Erro ao buscar templates com texto '{searchText}'", ex);
            }
        }

        // Event handlers para filtros/busca (se implementados no XAML)
        private void CategoryFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem item)
                {
                    FilterTemplatesByCategory(item.Content?.ToString() ?? "");
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro no filtro de categoria", ex);
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (sender is TextBox textBox)
                {
                    SearchTemplates(textBox.Text);
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro na busca de templates", ex);
            }
        }

        // Método para mostrar detalhes de um template
        private void ShowTemplateDetails(RecurringExpenseTemplate template)
        {
            try
            {
                if (template == null) return;

                var details = $"📋 Detalhes do Template\n\n" +
                             $"📝 Nome: {template.Name}\n" +
                             $"📄 Descrição: {template.Description}\n" +
                             $"🏷️ Categoria: {template.Category}\n" +
                             $"💰 Valor Sugerido: {template.FormattedAmount}\n" +
                             $"⏰ Frequência: {template.Frequency}\n\n" +
                             $"Deseja usar este template?";

                var result = MessageBox.Show(details, "Detalhes do Template",
                                            MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    ApplySelectedTemplate(template);
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao mostrar detalhes do template", ex);
            }
        }

        // Event handler para duplo clique (se implementado no XAML)
        private void TemplatesListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                if (TemplatesListBox?.SelectedItem is RecurringExpenseTemplate selectedTemplate)
                {
                    ApplySelectedTemplate(selectedTemplate);
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro no duplo clique do template", ex);
            }
        }

        // Override para keyboard shortcuts
        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            try
            {
                switch (e.Key)
                {
                    case System.Windows.Input.Key.Enter:
                        if (TemplatesListBox?.SelectedItem != null)
                        {
                            Select_Click(this, new RoutedEventArgs());
                            e.Handled = true;
                        }
                        break;

                    case System.Windows.Input.Key.Escape:
                        Cancel_Click(this, new RoutedEventArgs());
                        e.Handled = true;
                        break;

                    case System.Windows.Input.Key.F1:
                        ShowHelpDialog();
                        e.Handled = true;
                        break;

                    default:
                        base.OnKeyDown(e);
                        break;
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro no tratamento de teclas", ex);
                base.OnKeyDown(e);
            }
        }

        private void ShowHelpDialog()
        {
            try
            {
                var helpText = "💡 Ajuda - Templates de Despesas Recorrentes\n\n" +
                              "📋 Como usar:\n" +
                              "1. Selecione um template da lista\n" +
                              "2. Clique em 'Selecionar' ou pressione Enter\n" +
                              "3. Ajuste os valores na janela seguinte\n" +
                              "4. Confirme para criar a despesa recorrente\n\n" +
                              "⌨️ Atalhos:\n" +
                              "• Enter - Selecionar template\n" +
                              "• Esc - Cancelar\n" +
                              "• F1 - Esta ajuda\n\n" +
                              "💰 Os valores sugeridos podem ser alterados na próxima janela.";

                MessageBox.Show(helpText, "💡 Ajuda", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao mostrar ajuda", ex);
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Focar no ListBox se existir
                TemplatesListBox?.Focus();

                // Selecionar primeiro item se houver templates
                if (_templates.Count > 0 && TemplatesListBox != null)
                {
                    TemplatesListBox.SelectedIndex = 0;
                }

                LoggingService.LogInfo("Janela de templates carregada com sucesso");
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao carregar janela de templates", ex);
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                // Cleanup se necessário
                LoggingService.LogInfo("Fechando janela de templates");
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao fechar janela de templates", ex);
            }
        }

        // Método para recarregar templates (se necessário)
        public void RefreshTemplates()
        {
            try
            {
                LoadTemplates();
                LoggingService.LogInfo("Templates recarregados manualmente");
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao recarregar templates", ex);
            }
        }

        // Método para obter template selecionado (se necessário)
        public RecurringExpenseTemplate GetSelectedTemplate()
        {
            try
            {
                return TemplatesListBox?.SelectedItem as RecurringExpenseTemplate;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Erro ao obter template selecionado", ex);
                return null;
            }
        }

        // Método para aplicar template por nome (se necessário)
        public bool ApplyTemplateByName(string templateName)
        {
            try
            {
                var template = _templates.FirstOrDefault(t =>
                    t.Name.Equals(templateName, StringComparison.OrdinalIgnoreCase));

                if (template != null)
                {
                    ApplySelectedTemplate(template);
                    return true;
                }

                LoggingService.LogWarning($"Template com nome '{templateName}' não encontrado");
                return false;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Erro ao aplicar template por nome '{templateName}'", ex);
                return false;
            }
        }
    }
}