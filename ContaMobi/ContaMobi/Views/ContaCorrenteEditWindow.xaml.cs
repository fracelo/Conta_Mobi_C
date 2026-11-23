using Microsoft.UI.Xaml;
using ContaMobi.Data;
using ContaMobi.Models;
using ContaMobi.Services;
using System;
using System.Globalization;
using System.Linq;
using Windows.Graphics;

namespace ContaMobi.Views
{
    public sealed partial class ContaCorrenteEditWindow : Window
    {
        private readonly ContaCorrente _conta;
        private readonly bool _ehNovo;

        public ContaCorrenteEditWindow(ContaCorrente conta)
        {
            this.InitializeComponent();
            ContaMobi.Helpers.UIThemeHelper.AplicarBarraTituloRetro(this);
            this.AppWindow.Resize(new SizeInt32(520, 520));

            _conta = conta;

            using (var db = new AppDbContext())
            {
                _ehNovo = string.IsNullOrEmpty(_conta.ContaUuid) || db.ContasCorrentes.Find(_conta.ContaUuid) == null;
            }

            TxtTitulo.Text = _ehNovo ? "[ NOVO CADASTRO DE CONTA CORRENTE ]" : "[ EDITANDO CONTA CORRENTE ]";
            BtnExcluir.Visibility = _ehNovo ? Visibility.Collapsed : Visibility.Visible;

            CarregarBancos();
            PreencherCampos();
        }

        private void CarregarBancos()
        {
            using var db = new AppDbContext();
            var bancos = db.Bancos
                .Where(b => b.Ativo)
                .OrderBy(b => b.DescricaoBanco)
                .ToList();

            CboBanco.ItemsSource = bancos;
            CboBanco.DisplayMemberPath = "ExibicaoFormatada";
            CboBanco.SelectedValuePath = "CodigoBanco";
        }

        private void PreencherCampos()
        {
            TxtDescricao.Text = _conta.Descricao ?? string.Empty;
            CboBanco.SelectedValue = _conta.CodigoBanco > 0 ? _conta.CodigoBanco : (int?)null;
            DpDataCriacao.Date = _conta.DataCriacao;
            TxtSaldoInicial.Text = _conta.SaldoInicial.ToString("N2", new CultureInfo("pt-BR"));
        }

        private void BtnSalvar_Click(object sender, RoutedEventArgs e)
        {
            TxtMensagem.Visibility = Visibility.Collapsed;

            string descricao = TxtDescricao.Text.Trim();
            if (string.IsNullOrEmpty(descricao))
            {
                ExibirErro("> [ERRO] A descricao da conta e obrigatoria.");
                return;
            }

            if (CboBanco.SelectedValue == null || (int)CboBanco.SelectedValue <= 0)
            {
                ExibirErro("> [ERRO] Selecione uma instituicao bancaria valida.");
                return;
            }

            int codigoBanco = (int)CboBanco.SelectedValue;

            string saldoTexto = TxtSaldoInicial.Text.Trim().Replace("R$", "").Trim();
            if (!decimal.TryParse(saldoTexto, NumberStyles.Any, new CultureInfo("pt-BR"), out decimal saldoInicial))
            {
                ExibirErro("> [ERRO] Valor do saldo inicial invalido.");
                return;
            }

            DateTime dataCriacao = DpDataCriacao.Date?.DateTime.Date ?? DateTime.Today;
            string usuarioUuid = App.SupabaseClient?.Auth.CurrentUser?.Id ?? string.Empty;

            try
            {
                using var db = new AppDbContext();
                using var transaction = db.Database.BeginTransaction();

                string targetContaUuid;

                if (_ehNovo)
                {
                    targetContaUuid = string.IsNullOrEmpty(_conta.ContaUuid) ? Guid.NewGuid().ToString() : _conta.ContaUuid;

                    var nova = new ContaCorrente
                    {
                        ContaUuid = targetContaUuid,
                        CodigoBanco = codigoBanco,
                        UsuarioUuid = usuarioUuid,
                        Descricao = descricao,
                        DataCriacao = dataCriacao,
                        SaldoInicial = saldoInicial,
                        CriadoEm = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    db.ContasCorrentes.Add(nova);
                    db.SaveChanges();
                }
                else
                {
                    targetContaUuid = _conta.ContaUuid;
                    var existente = db.ContasCorrentes.Find(targetContaUuid);
                    if (existente != null)
                    {
                        existente.CodigoBanco = codigoBanco;
                        existente.Descricao = descricao;
                        existente.DataCriacao = dataCriacao;
                        existente.SaldoInicial = saldoInicial;
                        existente.UpdatedAt = DateTime.UtcNow;
                        db.SaveChanges();
                    }
                }

                // Sincroniza saldo inicial e cascata
                SaldoContaService.AtualizarSaldoInicialConta(db, targetContaUuid, dataCriacao, saldoInicial);
                db.SaveChanges();

                transaction.Commit();
                this.Close();
            }
            catch (Exception ex)
            {
                ExibirErro($"> [ERRO AO SALVAR]: {ex.Message}");
            }
        }

        private void BtnExcluir_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var db = new AppDbContext();
                var existente = db.ContasCorrentes.Find(_conta.ContaUuid);
                if (existente != null)
                {
                    db.ContasCorrentes.Remove(existente);
                    db.SaveChanges();
                }
                this.Close();
            }
            catch (Exception ex)
            {
                ExibirErro($"> [ERRO AO EXCLUIR]: {ex.Message}");
            }
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void ExibirErro(string msg)
        {
            TxtMensagem.Text = msg;
            TxtMensagem.Visibility = Visibility.Visible;
        }
    }
}