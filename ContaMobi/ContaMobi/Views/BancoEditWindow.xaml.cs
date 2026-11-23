using Microsoft.UI.Xaml;
using ContaMobi.Data;
using ContaMobi.Models;
using System;
using Windows.Graphics;

namespace ContaMobi.Views
{
    public sealed partial class BancoEditWindow : Window
    {
        private readonly Banco _banco;
        private readonly bool _ehNovo;

        public BancoEditWindow(Banco banco)
        {
            this.InitializeComponent();
            ContaMobi.Helpers.UIThemeHelper.AplicarBarraTituloRetro(this);
            this.AppWindow.Resize(new SizeInt32(520, 520));

            _banco = banco;

            using (var db = new AppDbContext())
            {
                // Se CodigoBanco for 0 ou não existir no banco local, é novo cadastro
                _ehNovo = _banco.CodigoBanco == 0 || db.Bancos.Find(_banco.CodigoBanco) == null;
            }

            TxtTitulo.Text = _ehNovo ? "[ NOVO CADASTRO DE BANCO ]" : "[ EDITANDO BANCO ]";

            TxtCodigo.Text = _banco.CodigoBanco > 0 ? _banco.CodigoBanco.ToString() : string.Empty;
            TxtNome.Text = _banco.DescricaoBanco ?? string.Empty;
            TxtApelido.Text = _banco.Apelido ?? string.Empty;
            ChkAtivo.IsChecked = _banco.Ativo;

            // Se for edição, bloqueia a edição do código do banco (Chave Primária)
            if (!_ehNovo)
            {
                TxtCodigo.IsEnabled = false;
            }
        }

        private void BtnSalvar_Click(object sender, RoutedEventArgs e)
        {
            TxtMensagem.Visibility = Visibility.Collapsed;

            string codigoTexto = TxtCodigo.Text.Trim();
            string nome = TxtNome.Text.Trim();
            string apelido = TxtApelido.Text.Trim();

            if (!int.TryParse(codigoTexto, out int codigoBanco) || codigoBanco <= 0)
            {
                TxtMensagem.Text = "> [ERRO] Informe um codigo de banco numerico valido.";
                TxtMensagem.Visibility = Visibility.Visible;
                return;
            }

            if (string.IsNullOrEmpty(nome))
            {
                TxtMensagem.Text = "> [ERRO] A descricao/nome do banco e obrigatoria.";
                TxtMensagem.Visibility = Visibility.Visible;
                return;
            }

            try
            {
                using (var db = new AppDbContext())
                {
                    if (_ehNovo)
                    {
                        if (db.Bancos.Find(codigoBanco) != null)
                        {
                            TxtMensagem.Text = $"> [ERRO] O banco com codigo {codigoBanco} ja esta cadastrado.";
                            TxtMensagem.Visibility = Visibility.Visible;
                            return;
                        }

                        var novo = new Banco
                        {
                            CodigoBanco = codigoBanco,
                            DescricaoBanco = nome,
                            Apelido = string.IsNullOrEmpty(apelido) ? nome : apelido,
                            Ativo = ChkAtivo.IsChecked ?? true,
                            UpdatedAt = DateTime.UtcNow
                        };
                        db.Bancos.Add(novo);
                    }
                    else
                    {
                        var existente = db.Bancos.Find(_banco.CodigoBanco);
                        if (existente != null)
                        {
                            existente.DescricaoBanco = nome;
                            existente.Apelido = string.IsNullOrEmpty(apelido) ? nome : apelido;
                            existente.Ativo = ChkAtivo.IsChecked ?? true;
                            existente.UpdatedAt = DateTime.UtcNow;
                        }
                    }

                    db.SaveChanges();
                }

                this.Close();
            }
            catch (Exception ex)
            {
                TxtMensagem.Text = $"> [ERRO AO SALVAR]: {ex.Message}";
                TxtMensagem.Visibility = Visibility.Visible;
            }
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            TxtCodigo.Text = _banco.CodigoBanco > 0 ? _banco.CodigoBanco.ToString() : string.Empty;
            TxtNome.Text = _banco.DescricaoBanco ?? string.Empty;
            TxtApelido.Text = _banco.Apelido ?? string.Empty;
            ChkAtivo.IsChecked = _banco.Ativo;
            TxtMensagem.Visibility = Visibility.Collapsed;
        }

        private void BtnSair_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}