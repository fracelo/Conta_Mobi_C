using ContaMobi.Data;
using ContaMobi.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.UI;

namespace ContaMobi.Views
{
    public sealed partial class SobrePage : Page
    {
        // Configure aqui as URLs oficiais do portal web e do suporte
        private const string UrlSiteOficial = "https://contamobi.vercel.app";
        private const string UrlMudarPlano = "https://contamobi.vercel.app/#planos";
        private const string UrlAjudaSuporte = "https://contamobi.vercel.app/#ajuda";
        private const string EmailSuporte = "suporte@contamobi.com.br";
        private const string VersaoSistema = "1.0.0";

        private readonly CultureInfo _cultura = new("pt-BR");

        public SobrePage()
        {
            this.InitializeComponent();

            LnkAjudaOnline.Content = UrlAjudaSuporte;
            LnkEmailSuporte.Content = EmailSuporte;
            LnkSiteOficial.Content = UrlSiteOficial;

            this.Loaded += async (s, e) =>
            {
                PreencherDadosTela(PlanoUsuarioSyncService.PermissoesAtuais);
                await AtualizarAssinaturaAsync(silencioso: true);
            };
        }

        private void PreencherDadosTela(PermissoesPlanoAtual perm)
        {
            string nomeTitular = string.IsNullOrWhiteSpace(perm.NomeCompleto)
                ? (App.SupabaseClient?.Auth.CurrentUser?.Email ?? "Usuário Local")
                : perm.NomeCompleto;

            string emailTitular = string.IsNullOrWhiteSpace(perm.Email)
                ? (App.SupabaseClient?.Auth.CurrentUser?.Email ?? "-")
                : perm.Email;

            TxtUsuarioTitular.Text = $"Titular: {nomeTitular} ({emailTitular})";

            TxtNomePlano.Text = perm.PlanoNome;
            TxtValorPlano.Text = perm.ValorPlano > 0m
                ? $"{perm.ValorPlano.ToString("C2", _cultura)} / mês"
                : "Gratuito (R$ 0,00)";

            string cicloFormatado = (perm.CicloCobranca ?? "gratis").Trim().ToLowerInvariant() switch
            {
                "mensal" => "MENSAL",
                "anual" => "ANUAL",
                _ => "GRÁTIS"
            };
            TxtCicloPlano.Text = cicloFormatado;

            bool ehGratis = string.Equals(perm.PlanoCodigo, "gratis", StringComparison.OrdinalIgnoreCase);

            if (ehGratis || !perm.DataVencimento.HasValue)
            {
                TxtVencimentoPlano.Text = "Sem vencimento";
                TxtVencimentoPlano.Foreground = new SolidColorBrush(Color.FromArgb(255, 0, 255, 102));
                TxtAvisoVencimento.Text = "Acesso contínuo ao plano gratuito";
                TxtAvisoVencimento.Foreground = new SolidColorBrush(Color.FromArgb(255, 102, 153, 119));
            }
            else
            {
                TxtVencimentoPlano.Text = perm.DataVencimento.Value.ToString("dd/MM/yyyy");
                if (perm.PlanoVencido)
                {
                    TxtVencimentoPlano.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 85, 85));
                    TxtAvisoVencimento.Text = "Assinatura expirada — renove para reativar";
                    TxtAvisoVencimento.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 85, 85));
                }
                else
                {
                    int diasRestantes = (perm.DataVencimento.Value.Date - DateTime.Today).Days;
                    TxtVencimentoPlano.Foreground = new SolidColorBrush(Color.FromArgb(255, 0, 255, 102));
                    TxtAvisoVencimento.Text = diasRestantes == 0
                        ? "Vence hoje"
                        : $"{diasRestantes} dia(s) restante(s)";
                    TxtAvisoVencimento.Foreground = new SolidColorBrush(Color.FromArgb(255, 102, 153, 119));
                }
            }

            // Badge de Status
            if (perm.PlanoVencido)
            {
                TxtStatusPlano.Text = "ASSINATURA VENCIDA";
                TxtStatusPlano.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 85, 85));
                BadgeStatusPlano.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 255, 85, 85));
                BadgeStatusPlano.Background = new SolidColorBrush(Color.FromArgb(255, 35, 8, 8));
            }
            else
            {
                TxtStatusPlano.Text = "ATIVO";
                TxtStatusPlano.Foreground = new SolidColorBrush(Color.FromArgb(255, 0, 255, 102));
                BadgeStatusPlano.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 0, 255, 102));
                BadgeStatusPlano.Background = new SolidColorBrush(Color.FromArgb(255, 5, 31, 5));
            }

            // Resumo de limites e módulos do plano
            string contasTexto = perm.LimiteContas.HasValue
                ? $"Até {perm.LimiteContas.Value} conta(s) corrente(s)"
                : "Contas Correntes ilimitadas";

            string cartoesTexto = !perm.LimiteCartoes.HasValue
                ? "Cartões de Crédito ilimitados"
                : (perm.LimiteCartoes.Value <= 0
                    ? "Cartões de Crédito: Não incluso"
                    : $"Até {perm.LimiteCartoes.Value} cartão(ões) de crédito");

            var modulos = new List<string>();
            if (perm.AcessoContasPagar) modulos.Add("Contas a Pagar");
            if (perm.AcessoContasReceber) modulos.Add("Contas a Receber");
            if (perm.AcessoOrcamento) modulos.Add("Orçamento");
            if (perm.AcessoFluxoCaixa) modulos.Add("Fluxo de Caixa");

            string modulosTexto = modulos.Count > 0 ? string.Join(", ", modulos) : "Básico";

            TxtRecursosPlano.Text =
                $"• {contasTexto}\n" +
                $"• {cartoesTexto}\n" +
                $"• Módulos: {modulosTexto}";
        }

        private async Task AtualizarAssinaturaAsync(bool silencioso)
        {
            if (!silencioso)
            {
                BtnAtualizarPlano.IsEnabled = false;
                TxtStatusSyncPlano.Text = "Sincronizando com o servidor...";
            }

            try
            {
                var permAtualizada = await PlanoUsuarioSyncService.SincronizarPlanosEUsuarioAsync();
                PreencherDadosTela(permAtualizada);

                if (!silencioso)
                {
                    TxtStatusSyncPlano.Text = $"Atualizado às {DateTime.Now:HH:mm:ss} ({permAtualizada.PlanoNome})";
                }
            }
            catch (Exception ex)
            {
                if (!silencioso)
                {
                    TxtStatusSyncPlano.Text = $"Falha ao sincronizar: {ex.Message}";
                }
            }
            finally
            {
                if (!silencioso)
                {
                    BtnAtualizarPlano.IsEnabled = true;
                }
            }
        }

        private async void BtnAtualizarPlano_Click(object sender, RoutedEventArgs e)
        {
            await AtualizarAssinaturaAsync(silencioso: false);
        }

        private async void BtnMudarPlano_Click(object sender, RoutedEventArgs e)
        {
            await AbrirUrlExternaAsync(UrlMudarPlano);
        }

        private async void BtnAjuda_Click(object sender, RoutedEventArgs e)
        {
            await AbrirUrlExternaAsync(UrlAjudaSuporte);
        }

        private async void BtnSiteOficial_Click(object sender, RoutedEventArgs e)
        {
            await AbrirUrlExternaAsync(UrlSiteOficial);
        }

        private async void BtnSuporteEmail_Click(object sender, RoutedEventArgs e)
        {
            var perm = PlanoUsuarioSyncService.PermissoesAtuais;
            string assunto = Uri.EscapeDataString($"Suporte Conta Mobi v{VersaoSistema} - {perm.PlanoNome}");
            string corpo = Uri.EscapeDataString(
                $"Olá, preciso de ajuda no Conta Mobi.\n\n" +
                $"Usuário: {perm.NomeCompleto} ({perm.Email})\n" +
                $"ID: {perm.UsuarioUuid}\n" +
                $"Plano: {perm.PlanoNome} ({perm.CicloCobranca})\n" +
                $"Versão: {VersaoSistema}\n\n" +
                $"Descreva sua dúvida abaixo:\n");

            await AbrirUrlExternaAsync($"mailto:{EmailSuporte}?subject={assunto}&body={corpo}");
        }

        private void BtnCopiarDiagnostico_Click(object sender, RoutedEventArgs e)
        {
            var perm = PlanoUsuarioSyncService.PermissoesAtuais;
            string textoDiagnostico =
                $"CONTA MOBI v{VersaoSistema}\n" +
                $"Usuário: {perm.NomeCompleto} ({perm.Email})\n" +
                $"UUID: {perm.UsuarioUuid}\n" +
                $"Plano: {perm.PlanoNome} [{perm.PlanoCodigo}] | Ciclo: {perm.CicloCobranca}\n" +
                $"Vencimento: {(perm.DataVencimento.HasValue ? perm.DataVencimento.Value.ToString("dd/MM/yyyy") : "N/A")}";

            var dataPackage = new DataPackage();
            dataPackage.SetText(textoDiagnostico);
            Clipboard.SetContent(dataPackage);

            TxtFeedbackCopia.Text = "> Dados copiados para a área de transferência! Cole no e-mail ou chat de suporte.";
            TxtFeedbackCopia.Visibility = Visibility.Visible;
        }

        private static async Task AbrirUrlExternaAsync(string url)
        {
            try
            {
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    await Launcher.LaunchUriAsync(uri);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERRO ABRIR URL]: {ex.Message}");
            }
        }
    }
}