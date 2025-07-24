namespace FinanceManager.Constants
{
    public static class AppConstants
    {
        public const string APP_NAME = "Finance Manager";
        public const string APP_VERSION = "1.0.0";
        public const string DATABASE_NAME = "finance.db";
        public const string FOLDER_NAME = "FinanceManager";

        // Mensagens comuns
        public static class Messages
        {
            public const string CAMPO_OBRIGATORIO = "Campo Obrigatório";
            public const string ERRO = "Erro";
            public const string SUCESSO = "Sucesso";
            public const string CONFIRMAR = "Confirmar";
            public const string INFORMACAO = "Informação";

            public const string NENHUMA_SELECAO = "Por favor, selecione um item para continuar.";
            public const string OPERACAO_NAO_PODE_SER_DESFEITA = "⚠️ Esta ação não pode ser desfeita!";
            public const string DADOS_NAO_GUARDADOS = "Tem alterações não guardadas.\n\nTem a certeza que quer cancelar?";
        }

        // Categorias de despesas
        public static class ExpenseCategories
        {
            public const string ALIMENTACAO = "Alimentação";
            public const string TRANSPORTE = "Transporte";
            public const string CASA = "Casa";
            public const string SAUDE = "Saúde";
            public const string ENTRETENIMENTO = "Entretenimento";
            public const string COMPRAS = "Compras";
            public const string EDUCACAO = "Educação";
            public const string OUTROS = "Outros";

            public static readonly string[] ALL_CATEGORIES = {
                ALIMENTACAO, TRANSPORTE, CASA, SAUDE,
                ENTRETENIMENTO, COMPRAS, EDUCACAO, OUTROS
            };
        }

        // Tipos de investimento
        public static class InvestmentTypes
        {
            public const string ACAO = "Ação";
            public const string ETF = "ETF";
            public const string FUNDO_INVESTIMENTO = "Fundo de Investimento";
            public const string OBRIGACAO = "Obrigação";
            public const string POUPANCA_BANCO = "Poupança Banco";
            public const string CERTIFICADO_TESOURO = "Certificado do Tesouro";
            public const string CRIPTO_MOEDA = "Criptomoeda";
            public const string IMOVEIS = "Imóveis";
            public const string COMMODITIES = "Commodities";
            public const string OUTROS = "Outros";

            public static readonly string[] ALL_TYPES = {
                ACAO, ETF, FUNDO_INVESTIMENTO, OBRIGACAO,
                POUPANCA_BANCO, CERTIFICADO_TESOURO,
                CRIPTO_MOEDA, IMOVEIS, COMMODITIES, OUTROS
            };
        }

        // Formatos de data
        public static class DateFormats
        {
            public const string SHORT_DATE = "dd/MM/yyyy";
            public const string LONG_DATE = "dd/MM/yyyy HH:mm";
            public const string FILE_DATE = "yyyy-MM-dd";
            public const string MONTH_YEAR = "MM/yyyy";
        }

        // Configurações de validação
        public static class Validation
        {
            public const int MIN_USERNAME_LENGTH = 3;
            public const int MIN_PASSWORD_LENGTH = 4;
            public const int MAX_DESCRIPTION_LENGTH = 200;
            public const int MAX_NAME_LENGTH = 100;

            public const decimal MIN_AMOUNT = 0.01m;
            public const decimal MAX_AMOUNT = 9999999.99m;
        }

        // Cores para gráficos
        public static class ChartColors
        {
            public static readonly string[] COLORS = {
                "#FF2196F3", "#FF4CAF50", "#FFFF9800", "#FFE91E63",
                "#FF9C27B0", "#FF607D8B", "#FFFF5722", "#FF795548",
                "#FF9E9E9E", "#FF3F51B5", "#FFCDDC39", "#FFFF5722"
            };
        }
    }

    public static class FileExtensions
    {
        public const string CSV = ".csv";
        public const string TXT = ".txt";
        public const string PDF = ".pdf";
        public const string EXCEL = ".xlsx";
    }

    public static class Emojis
    {
        public const string MONEY = "💰";
        public const string CHART = "📊";
        public const string INVESTMENT = "📈";
        public const string SAVINGS = "🎯";
        public const string EXPENSE = "💸";
        public const string SUCCESS = "✅";
        public const string ERROR = "❌";
        public const string WARNING = "⚠️";
        public const string INFO = "💡";
        public const string DELETE = "🗑️";
        public const string EDIT = "✏️";
        public const string ADD = "➕";
        public const string REFRESH = "🔄";
        public const string EXPORT = "📁";
        public const string CALENDAR = "📅";
        public const string TAG = "🏷️";
        public const string USER = "👤";
        public const string SEARCH = "🔍";
    }
}
