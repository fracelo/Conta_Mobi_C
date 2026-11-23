using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.EntityFrameworkCore;
using ContaMobi.Data;
using ContaMobi.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Windows.Graphics;

namespace ContaMobi.Views
{
    public sealed partial class EntidadeEditWindow : Window
    {
        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(8)
        };

        private readonly Entidade _entidade;
        private readonly bool _ehNovo;
        private readonly ObservableCollection<Contato> _contatosEmMemoria = new();
        private Contato? _contatoEmEdicao = null;

        public EntidadeEditWindow(Entidade entidade)
        {
            this.InitializeComponent();
            ContaMobi.Helpers.UIThemeHelper.AplicarBarraTituloRetro(this);
            this.AppWindow.Resize(new SizeInt32(860, 820));

            _entidade = entidade;

            using (var db = new AppDbContext())
            {
                _ehNovo = string.IsNullOrEmpty(_entidade.EntidadeUuid) || db.Entidades.Find(_entidade.EntidadeUuid) == null;
            }

            TxtTitulo.Text = _ehNovo ? "[ NOVO CADASTRO DE ENTIDADE // CONTATOS ]" : "[ EDITANDO ENTIDADE // CONTATOS ]";
            BtnExcluir.Visibility = _ehNovo ? Visibility.Collapsed : Visibility.Visible;

            ListaContatos.ItemsSource = _contatosEmMemoria;

            PreencherCampos();
            CarregarContatosDoBanco();
        }

        private void PreencherCampos()
        {
            TxtNomeRazao.Text = _entidade.NomeRazao ?? string.Empty;
            TxtCpfCnpj.Text = _entidade.CpfCnpj ?? string.Empty;
            TxtRgIe.Text = _entidade.RgIe ?? string.Empty;
            TxtCep.Text = _entidade.Cep ?? string.Empty;
            TxtLogradouro.Text = _entidade.Logradouro ?? string.Empty;
            TxtNumero.Text = _entidade.Numero ?? string.Empty;
            TxtBairro.Text = _entidade.Bairro ?? string.Empty;
            TxtComplemento.Text = _entidade.Complemento ?? string.Empty;
            TxtCidade.Text = _entidade.Cidade ?? string.Empty;
            TxtUf.Text = _entidade.Uf ?? string.Empty;

            if (_entidade.TipoPessoa == "J")
                RbJuridica.IsChecked = true;
            else
                RbFisica.IsChecked = true;

            AtualizarRotulosPessoa();

            foreach (ComboBoxItem item in CboTipoEntidade.Items)
            {
                if (item.Content.ToString()!.Equals(_entidade.TipoEntidade, StringComparison.OrdinalIgnoreCase))
                {
                    CboTipoEntidade.SelectedItem = item;
                    break;
                }
            }
        }

        private void CarregarContatosDoBanco()
        {
            _contatosEmMemoria.Clear();

            if (!_ehNovo && !string.IsNullOrEmpty(_entidade.EntidadeUuid))
            {
                using var db = new AppDbContext();
                var registros = db.Contatos
                    .AsNoTracking()
                    .Where(c => c.EntidadeUuid == _entidade.EntidadeUuid)
                    .OrderBy(c => c.NomeDescricao)
                    .ToList();

                foreach (var c in registros)
                {
                    _contatosEmMemoria.Add(c);
                }
            }
        }

        private void RbPessoa_Changed(object sender, RoutedEventArgs e) => AtualizarRotulosPessoa();

        private void AtualizarRotulosPessoa()
        {
            if (LblNomeRazao == null || LblCpfCnpj == null || LblRgIe == null) return;

            bool ehJuridica = RbJuridica.IsChecked == true;
            LblNomeRazao.Text = ehJuridica ? "RAZAO SOCIAL (*):" : "NOME COMPLETO (*):";
            LblCpfCnpj.Text = ehJuridica ? "CNPJ:" : "CPF:";
            LblRgIe.Text = ehJuridica ? "INSCRICAO ESTADUAL (IE):" : "RG:";
        }

        #region Consulta de Endereço por CEP (ViaCEP)

        private void TxtCep_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                BtnBuscarCep_Click(sender, new RoutedEventArgs());
                e.Handled = true;
            }
        }

        private async void BtnBuscarCep_Click(object sender, RoutedEventArgs e)
        {
            TxtMensagem.Visibility = Visibility.Collapsed;

            string cepLimpo = new string((TxtCep.Text ?? string.Empty).Where(char.IsDigit).ToArray());
            if (cepLimpo.Length != 8)
            {
                ExibirErro("> [AVISO] Informe um CEP valido com 8 digitos (ex: 87013000).");
                TxtCep.Focus(FocusState.Programmatic);
                return;
            }

            try
            {
                BtnBuscarCep.IsEnabled = false;
                BtnBuscarCep.Content = "[BUSCANDO...]";

                var endereco = await BuscarViaCepAsync(cepLimpo);

                if (endereco == null)
                {
                    ExibirErro($"> [AVISO] CEP {cepLimpo} nao encontrado na base dos Correios.");
                    return;
                }

                TxtCep.Text = !string.IsNullOrWhiteSpace(endereco.Cep)
                    ? endereco.Cep
                    : $"{cepLimpo.Substring(0, 5)}-{cepLimpo.Substring(5, 3)}";

                TxtCidade.Text = endereco.Cidade ?? string.Empty;
                TxtUf.Text = (endereco.Uf ?? string.Empty).ToUpperInvariant();
                TxtLogradouro.Text = endereco.Logradouro ?? string.Empty;
                TxtBairro.Text = endereco.Bairro ?? string.Empty;

                if (string.IsNullOrWhiteSpace(TxtComplemento.Text) && !string.IsNullOrWhiteSpace(endereco.Complemento))
                {
                    TxtComplemento.Text = endereco.Complemento;
                }

                if (!string.IsNullOrWhiteSpace(TxtLogradouro.Text))
                    TxtNumero.Focus(FocusState.Programmatic);
                else
                    TxtLogradouro.Focus(FocusState.Programmatic);
            }
            catch (Exception ex)
            {
                ExibirErro($"> [ERRO NA CONSULTA DO CEP]: Falha de conexao ({ex.Message})");
            }
            finally
            {
                BtnBuscarCep.IsEnabled = true;
                BtnBuscarCep.Content = "[BUSCAR CEP]";
            }
        }

        private static async Task<ViaCepResponse?> BuscarViaCepAsync(string cepLimpo)
        {
            string url = $"https://viacep.com.br/ws/{cepLimpo}/json/";
            using HttpResponseMessage response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync();
            var dados = JsonSerializer.Deserialize<ViaCepResponse>(json);

            if (dados == null || dados.CepInvalido)
                return null;

            return dados;
        }

        private sealed class ViaCepResponse
        {
            [JsonPropertyName("cep")]
            public string? Cep { get; set; }

            [JsonPropertyName("logradouro")]
            public string? Logradouro { get; set; }

            [JsonPropertyName("complemento")]
            public string? Complemento { get; set; }

            [JsonPropertyName("bairro")]
            public string? Bairro { get; set; }

            [JsonPropertyName("localidade")]
            public string? Cidade { get; set; }

            [JsonPropertyName("uf")]
            public string? Uf { get; set; }

            [JsonPropertyName("erro")]
            public JsonElement? Erro { get; set; }

            [JsonIgnore]
            public bool CepInvalido => Erro.HasValue &&
                (Erro.Value.ValueKind == JsonValueKind.True ||
                (Erro.Value.ValueKind == JsonValueKind.String && Erro.Value.GetString() == "true"));
        }

        #endregion

        #region Gestão do Grid de Contatos

        private void BtnAdicionarContato_Click(object sender, RoutedEventArgs e)
        {
            _contatoEmEdicao = null;
            LimparCamposContato();
            PainelNovoContato.Visibility = Visibility.Visible;
            TxtContatoNome.Focus(FocusState.Programmatic);
        }

        private void ListaContatos_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            if (ListaContatos.SelectedItem is Contato selecionado)
            {
                _contatoEmEdicao = selecionado;
                TxtContatoNome.Text = selecionado.NomeDescricao ?? string.Empty;
                TxtContatoWhats.Text = selecionado.Whatsapp ?? string.Empty;
                TxtContatoFone.Text = selecionado.Fone ?? string.Empty;
                TxtContatoEmail.Text = selecionado.Email ?? string.Empty;
                TxtContatoTelegram.Text = selecionado.Telegram ?? string.Empty;
                TxtContatoInstagram.Text = selecionado.Instagram ?? string.Empty;

                PainelNovoContato.Visibility = Visibility.Visible;
                TxtContatoNome.Focus(FocusState.Programmatic);
            }
        }

        private void BtnConfirmarContato_Click(object sender, RoutedEventArgs e)
        {
            string nome = TxtContatoNome.Text.Trim();
            if (string.IsNullOrEmpty(nome))
            {
                ExibirErro("> [ERRO CONTATO] Nome / Identificacao do contato e obrigatorio.");
                return;
            }

            if (_contatoEmEdicao == null)
            {
                var novo = new Contato
                {
                    ContatoUuid = Guid.NewGuid().ToString(),
                    EntidadeUuid = _entidade.EntidadeUuid,
                    NomeDescricao = nome,
                    Whatsapp = string.IsNullOrWhiteSpace(TxtContatoWhats.Text) ? null : TxtContatoWhats.Text.Trim(),
                    Fone = string.IsNullOrWhiteSpace(TxtContatoFone.Text) ? null : TxtContatoFone.Text.Trim(),
                    Email = string.IsNullOrWhiteSpace(TxtContatoEmail.Text) ? null : TxtContatoEmail.Text.Trim(),
                    Telegram = string.IsNullOrWhiteSpace(TxtContatoTelegram.Text) ? null : TxtContatoTelegram.Text.Trim(),
                    Instagram = string.IsNullOrWhiteSpace(TxtContatoInstagram.Text) ? null : TxtContatoInstagram.Text.Trim(),
                    CriadoEm = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _contatosEmMemoria.Add(novo);
            }
            else
            {
                _contatoEmEdicao.NomeDescricao = nome;
                _contatoEmEdicao.Whatsapp = string.IsNullOrWhiteSpace(TxtContatoWhats.Text) ? null : TxtContatoWhats.Text.Trim();
                _contatoEmEdicao.Fone = string.IsNullOrWhiteSpace(TxtContatoFone.Text) ? null : TxtContatoFone.Text.Trim();
                _contatoEmEdicao.Email = string.IsNullOrWhiteSpace(TxtContatoEmail.Text) ? null : TxtContatoEmail.Text.Trim();
                _contatoEmEdicao.Telegram = string.IsNullOrWhiteSpace(TxtContatoTelegram.Text) ? null : TxtContatoTelegram.Text.Trim();
                _contatoEmEdicao.Instagram = string.IsNullOrWhiteSpace(TxtContatoInstagram.Text) ? null : TxtContatoInstagram.Text.Trim();
                _contatoEmEdicao.UpdatedAt = DateTime.UtcNow;

                int idx = _contatosEmMemoria.IndexOf(_contatoEmEdicao);
                if (idx >= 0)
                {
                    _contatosEmMemoria[idx] = _contatoEmEdicao;
                }
            }

            LimparCamposContato();
            PainelNovoContato.Visibility = Visibility.Collapsed;
        }

        private void BtnCancelarContato_Click(object sender, RoutedEventArgs e)
        {
            LimparCamposContato();
            PainelNovoContato.Visibility = Visibility.Collapsed;
        }

        private void BtnRemoverContato_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Contato contatoParaRemover)
            {
                _contatosEmMemoria.Remove(contatoParaRemover);
            }
        }

        private void LimparCamposContato()
        {
            _contatoEmEdicao = null;
            TxtContatoNome.Text = string.Empty;
            TxtContatoWhats.Text = string.Empty;
            TxtContatoFone.Text = string.Empty;
            TxtContatoEmail.Text = string.Empty;
            TxtContatoTelegram.Text = string.Empty;
            TxtContatoInstagram.Text = string.Empty;
        }

        #endregion

        #region Gravacao da Entidade e Contatos

        private void BtnSalvar_Click(object sender, RoutedEventArgs e)
        {
            TxtMensagem.Visibility = Visibility.Collapsed;

            string nomeRazao = TxtNomeRazao.Text.Trim();
            if (string.IsNullOrEmpty(nomeRazao))
            {
                ExibirErro("> [ERRO] O Nome ou Razao Social e obrigatorio.");
                return;
            }

            string tipoEntidade = (CboTipoEntidade.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Cliente";
            string tipoPessoa = RbJuridica.IsChecked == true ? "J" : "F";
            string usuarioUuid = App.SupabaseClient?.Auth.CurrentUser?.Id ?? string.Empty;

            try
            {
                using var db = new AppDbContext();
                using var transaction = db.Database.BeginTransaction();

                string targetEntidadeUuid = string.IsNullOrEmpty(_entidade.EntidadeUuid)
                    ? Guid.NewGuid().ToString()
                    : _entidade.EntidadeUuid;

                if (_ehNovo)
                {
                    var nova = new Entidade
                    {
                        EntidadeUuid = targetEntidadeUuid,
                        UsuarioUuid = usuarioUuid,
                        TipoEntidade = tipoEntidade,
                        TipoPessoa = tipoPessoa,
                        NomeRazao = nomeRazao,
                        CpfCnpj = string.IsNullOrWhiteSpace(TxtCpfCnpj.Text) ? null : TxtCpfCnpj.Text.Trim(),
                        RgIe = string.IsNullOrWhiteSpace(TxtRgIe.Text) ? null : TxtRgIe.Text.Trim(),
                        Cep = string.IsNullOrWhiteSpace(TxtCep.Text) ? null : TxtCep.Text.Trim(),
                        Logradouro = string.IsNullOrWhiteSpace(TxtLogradouro.Text) ? null : TxtLogradouro.Text.Trim(),
                        Numero = string.IsNullOrWhiteSpace(TxtNumero.Text) ? null : TxtNumero.Text.Trim(),
                        Complemento = string.IsNullOrWhiteSpace(TxtComplemento.Text) ? null : TxtComplemento.Text.Trim(),
                        Bairro = string.IsNullOrWhiteSpace(TxtBairro.Text) ? null : TxtBairro.Text.Trim(),
                        Cidade = string.IsNullOrWhiteSpace(TxtCidade.Text) ? null : TxtCidade.Text.Trim(),
                        Uf = string.IsNullOrWhiteSpace(TxtUf.Text) ? null : TxtUf.Text.Trim().ToUpperInvariant(),
                        CriadoEm = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    db.Entidades.Add(nova);
                    db.SaveChanges();
                }
                else
                {
                    var existente = db.Entidades.Find(targetEntidadeUuid);
                    if (existente != null)
                    {
                        existente.TipoEntidade = tipoEntidade;
                        existente.TipoPessoa = tipoPessoa;
                        existente.NomeRazao = nomeRazao;
                        existente.CpfCnpj = string.IsNullOrWhiteSpace(TxtCpfCnpj.Text) ? null : TxtCpfCnpj.Text.Trim();
                        existente.RgIe = string.IsNullOrWhiteSpace(TxtRgIe.Text) ? null : TxtRgIe.Text.Trim();
                        existente.Cep = string.IsNullOrWhiteSpace(TxtCep.Text) ? null : TxtCep.Text.Trim();
                        existente.Logradouro = string.IsNullOrWhiteSpace(TxtLogradouro.Text) ? null : TxtLogradouro.Text.Trim();
                        existente.Numero = string.IsNullOrWhiteSpace(TxtNumero.Text) ? null : TxtNumero.Text.Trim();
                        existente.Complemento = string.IsNullOrWhiteSpace(TxtComplemento.Text) ? null : TxtComplemento.Text.Trim();
                        existente.Bairro = string.IsNullOrWhiteSpace(TxtBairro.Text) ? null : TxtBairro.Text.Trim();
                        existente.Cidade = string.IsNullOrWhiteSpace(TxtCidade.Text) ? null : TxtCidade.Text.Trim();
                        existente.Uf = string.IsNullOrWhiteSpace(TxtUf.Text) ? null : TxtUf.Text.Trim().ToUpperInvariant();
                        existente.UpdatedAt = DateTime.UtcNow;
                        db.SaveChanges();
                    }
                }

                // Sincroniza a coleção de contatos vinculados
                var contatosDoBanco = db.Contatos.Where(c => c.EntidadeUuid == targetEntidadeUuid).ToList();
                db.Contatos.RemoveRange(contatosDoBanco);

                foreach (var c in _contatosEmMemoria)
                {
                    db.Contatos.Add(new Contato
                    {
                        ContatoUuid = string.IsNullOrEmpty(c.ContatoUuid) ? Guid.NewGuid().ToString() : c.ContatoUuid,
                        EntidadeUuid = targetEntidadeUuid,
                        NomeDescricao = c.NomeDescricao,
                        Fone = c.Fone,
                        Email = c.Email,
                        Whatsapp = c.Whatsapp,
                        Telegram = c.Telegram,
                        Instagram = c.Instagram,
                        CriadoEm = c.CriadoEm ?? DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }

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
                var existente = db.Entidades.Find(_entidade.EntidadeUuid);
                if (existente != null)
                {
                    db.Entidades.Remove(existente);
                    db.SaveChanges();
                }
                this.Close();
            }
            catch (Exception ex)
            {
                ExibirErro($"> [ERRO AO EXCLUIR]: {ex.Message}");
            }
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e) => this.Close();

        private void ExibirErro(string msg)
        {
            TxtMensagem.Text = msg;
            TxtMensagem.Visibility = Visibility.Visible;
        }

        #endregion
    }
}