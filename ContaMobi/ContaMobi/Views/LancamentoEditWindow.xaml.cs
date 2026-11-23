using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.EntityFrameworkCore;
using ContaMobi.Data;
using ContaMobi.Helpers;
using ContaMobi.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Windows.Graphics;
using Windows.UI;

namespace ContaMobi.Views
{
	public sealed partial class LancamentoEditWindow : Window
	{
		private readonly CultureInfo _cultura = new("pt-BR");
		private readonly LancamentoConta? _lancamentoEditando = null;
		private bool EhEdicao => _lancamentoEditando != null;

		private List<Categoria> _categoriasDisponiveis = new();
		private List<Entidade> _todasEntidades = new();

		private Entidade? _entidadeSelecionada = null;

		private decimal _saldoAnteriorNumerico = 0m;
		private decimal _valorDigitadoAbsoluto = 0m;
		private decimal _saldoAtualNumerico = 0m;

		private bool _carregandoTela = false;
		private bool _formatandoTexto = false;
		private bool _retornandoDeModal = false;

		// Construtor para INCLUSÃO
		public LancamentoEditWindow(string? contaUuidPreSelecionada = null)
		{
			this.InitializeComponent();
			UIThemeHelper.AplicarBarraTituloRetro(this);
			this.AppWindow.Resize(new SizeInt32(820, 760));

			_carregandoTela = true;
			TxtDataLancamento.Text = DateTime.Today.ToString("dd/MM/yyyy");
			TxtDataConciliacao.Text = DateTime.Today.ToString("dd/MM/yyyy");
			CarregarDadosAuxiliares(contaUuidPreSelecionada);
			_carregandoTela = false;

			AtualizarEstadoPorDataEConta();
		}

		// Construtor para EDIÇÃO
		public LancamentoEditWindow(LancamentoConta lancamentoParaEditar)
		{
			this.InitializeComponent();
			UIThemeHelper.AplicarBarraTituloRetro(this);
			this.AppWindow.Resize(new SizeInt32(820, 760));

			_lancamentoEditando = lancamentoParaEditar;
			this.Title = "CONTA MOBI // EDITAR LANCAMENTO";
			BtnSalvar.Content = "[F10] SALVAR ALTERACOES";

			_carregandoTela = true;
			CarregarDadosAuxiliares(lancamentoParaEditar.ContaUuid);
			PreencherCamposParaEdicao(lancamentoParaEditar);
			_carregandoTela = false;

			AtualizarEstadoPorDataEConta();
			RecarregarCategoriasCombo(lancamentoParaEditar.Tipo, lancamentoParaEditar.CategoriaUuid);
			AplicarFormatacaoVisualValor();
			RecalcularSaldoAtualProjetado();
		}

		private void PreencherCamposParaEdicao(LancamentoConta lanc)
		{
			_formatandoTexto = true;

			TxtDataLancamento.Text = lanc.DataLancamento.ToString("dd/MM/yyyy");
			SelecionarTipoNoCombo(lanc.Tipo);

			TxtDescricao.Text = lanc.Descricao ?? string.Empty;

			if (!string.IsNullOrWhiteSpace(lanc.EntidadeUuid))
			{
				_entidadeSelecionada = _todasEntidades.FirstOrDefault(e => e.EntidadeUuid == lanc.EntidadeUuid);
				AsbEntidade.Text = _entidadeSelecionada?.NomeRazao ?? string.Empty;
			}
			else
			{
				_entidadeSelecionada = null;
				AsbEntidade.Text = string.Empty;
			}

			_valorDigitadoAbsoluto = Math.Abs(lanc.Valor);

			string tagConc = lanc.Conciliado ? "S" : "N";
			foreach (ComboBoxItem item in CboConciliado.Items)
			{
				if (item.Tag?.ToString() == tagConc)
				{
					CboConciliado.SelectedItem = item;
					break;
				}
			}

			if (lanc.Conciliado)
			{
				TxtDataConciliacao.IsEnabled = true;
				TxtDataConciliacao.Text = (lanc.DataConciliacao ?? lanc.DataLancamento).ToString("dd/MM/yyyy");
			}
			else
			{
				TxtDataConciliacao.IsEnabled = false;
				TxtDataConciliacao.Text = string.Empty;
			}

			_formatandoTexto = false;
		}

		private void CarregarDadosAuxiliares(string? contaUuidPreSelecionada)
		{
			using var db = new AppDbContext();

			var contas = db.ContasCorrentes
				.AsNoTracking()
				.Include(c => c.Banco)
				.OrderBy(c => c.Descricao)
				.ToList();

			CboContaCorrente.Items.Clear();
			ComboBoxItem? itemParaSelecionar = null;

			foreach (var c in contas)
			{
				var item = new ComboBoxItem
				{
					Content = $"{c.Descricao} ({c.BancoFormatado})",
					Tag = c.ContaUuid
				};
				CboContaCorrente.Items.Add(item);

				if (!string.IsNullOrEmpty(contaUuidPreSelecionada) && c.ContaUuid == contaUuidPreSelecionada)
				{
					itemParaSelecionar = item;
				}
			}

			if (itemParaSelecionar != null)
				CboContaCorrente.SelectedItem = itemParaSelecionar;
			else if (CboContaCorrente.Items.Count > 0)
				CboContaCorrente.SelectedIndex = 0;

			RecarregarEntidadesDoBanco();
		}

		private void RecarregarEntidadesDoBanco()
		{
			using var db = new AppDbContext();
			_todasEntidades = db.Entidades
				.AsNoTracking()
				.OrderBy(e => e.NomeRazao)
				.ToList();
		}

		private void RecarregarCategoriasCombo(string tipo, string? categoriaUuidParaSelecionar = null)
		{
			CboCategoria.ItemsSource = null;
			CboCategoria.Items.Clear();
			CboCategoria.Items.Add(new ComboBoxItem
			{
				Content = "[ SELECIONE UMA CATEGORIA ]",
				Tag = null
			});

			try
			{
				using var db = new AppDbContext();
				bool ehCredito = string.Equals(tipo, "C", StringComparison.OrdinalIgnoreCase);

				var todasCategorias = db.Categorias
					.AsNoTracking()
					.Where(c => c.CategoriaUuid != TransferenciaService.CategoriaTransferenciaUuid)
					.OrderBy(c => c.Descricao)
					.ToList();

				bool EhDoTipo(Categoria c)
				{
					string t = (c.Tipo ?? string.Empty).Trim().ToUpperInvariant();
					if (ehCredito)
						return t == "C" || t == "R" || t.StartsWith("REC") || t.StartsWith("CRED") || t == "E";
					else
						return t == "D" || t == "P" || t.StartsWith("DES") || t.StartsWith("DEB") || t == "S";
				}

				_categoriasDisponiveis = todasCategorias
					.Where(c => EhDoTipo(c) && c.Nivel >= 2 && c.AceitaLancamento)
					.OrderBy(c => c.Descricao)
					.ToList();

				var mapaTodas = todasCategorias
					.GroupBy(c => c.CategoriaUuid.Trim(), StringComparer.OrdinalIgnoreCase)
					.ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

				var filhosPorPai = _categoriasDisponiveis
					.Where(c => !string.IsNullOrWhiteSpace(c.PaiUuid))
					.GroupBy(c => c.PaiUuid!.Trim(), StringComparer.OrdinalIgnoreCase)
					.ToDictionary(g => g.Key, g => g.OrderBy(f => f.Descricao).ToList(), StringComparer.OrdinalIgnoreCase);

				var paisOrdenados = filhosPorPai.Keys
					.Where(paiId => mapaTodas.ContainsKey(paiId))
					.Select(paiId => mapaTodas[paiId])
					.OrderBy(p => p.Descricao)
					.ToList();

				ComboBoxItem? itemParaSelecionar = null;
				var adicionados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

				foreach (var pai in paisOrdenados)
				{
					string paiId = pai.CategoriaUuid.Trim();
					if (!filhosPorPai.TryGetValue(paiId, out var filhas) || filhas.Count == 0)
						continue;

					CboCategoria.Items.Add(new ComboBoxItem
					{
						Content = pai.Descricao.ToUpperInvariant(),
						Tag = null,
						IsEnabled = false,
						FontWeight = FontWeights.Bold,
						Foreground = new SolidColorBrush(Color.FromArgb(255, 130, 145, 160))
					});

					foreach (var filha in filhas)
					{
						var itemFilha = new ComboBoxItem
						{
							Content = $"   └─ {filha.Descricao}",
							Tag = filha
						};
						CboCategoria.Items.Add(itemFilha);
						adicionados.Add(filha.CategoriaUuid.Trim());

						if (!string.IsNullOrWhiteSpace(categoriaUuidParaSelecionar) &&
							string.Equals(filha.CategoriaUuid.Trim(), categoriaUuidParaSelecionar.Trim(), StringComparison.OrdinalIgnoreCase))
						{
							itemParaSelecionar = itemFilha;
						}
					}
				}

				foreach (var solta in _categoriasDisponiveis.Where(c => !adicionados.Contains(c.CategoriaUuid.Trim())))
				{
					var itemSolto = new ComboBoxItem
					{
						Content = $"   └─ {solta.Descricao}",
						Tag = solta
					};
					CboCategoria.Items.Add(itemSolto);

					if (!string.IsNullOrWhiteSpace(categoriaUuidParaSelecionar) &&
						string.Equals(solta.CategoriaUuid.Trim(), categoriaUuidParaSelecionar.Trim(), StringComparison.OrdinalIgnoreCase))
					{
						itemParaSelecionar = itemSolto;
					}
				}

				if (itemParaSelecionar != null)
					CboCategoria.SelectedItem = itemParaSelecionar;
				else
					CboCategoria.SelectedIndex = 0;
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Erro ao carregar Categorias: {ex.Message}");
			}
		}

		private string? ObterContaSelecionadaUuid()
		{
			return (CboContaCorrente?.SelectedItem as ComboBoxItem)?.Tag?.ToString();
		}

		private string? ObterTipoSelecionado()
		{
			return (CboTipo?.SelectedItem as ComboBoxItem)?.Tag?.ToString();
		}

		private bool ObterDataLancamentoValida(out DateTime dataValida)
		{
			return DateTime.TryParseExact(
				TxtDataLancamento?.Text?.Trim() ?? string.Empty,
				"dd/MM/yyyy",
				_cultura,
				DateTimeStyles.None,
				out dataValida);
		}

		#region Regras de Habilitação, Saldo Anterior e Tipo (D ou C)

		private void CboContaCorrente_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (_carregandoTela) return;
			AtualizarEstadoPorDataEConta();
		}

		private void TxtDataLancamento_TextChanged(object sender, TextChangedEventArgs e)
		{
			FormatarCampoData(TxtDataLancamento);
			if (_carregandoTela) return;

			// Atualiza automaticamente a Data de Conciliação enquanto o lançamento estiver marcado como conciliado
			bool conciliado = ((CboConciliado?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "S") == "S";
			if (conciliado && TxtDataConciliacao != null)
			{
				_formatandoTexto = true;
				TxtDataConciliacao.Text = TxtDataLancamento.Text;
				_formatandoTexto = false;
			}

			if (TxtDataLancamento.Text.Length == 10)
			{
				AtualizarEstadoPorDataEConta();
			}
			else
			{
				CboTipo.IsEnabled = false;
				CboCategoria.IsEnabled = false;
				BtnAddCategoria.IsEnabled = false;
				PainelCamposDependentes.IsEnabled = false;
				BtnSalvar.IsEnabled = false;
			}
		}

		private void TxtDataLancamento_LostFocus(object sender, RoutedEventArgs e)
		{
			if (!ObterDataLancamentoValida(out _))
			{
				ExibirMensagem("> [ERRO] Informe uma Data valida (dd/mm/aaaa). Sem uma data o lancamento nao pode seguir.", true);
			}
			else
			{
				TxtMensagem.Visibility = Visibility.Collapsed;
				AtualizarEstadoPorDataEConta();
			}
		}

		private void AtualizarEstadoPorDataEConta()
		{
			if (CboContaCorrente == null || TxtDataLancamento == null || TxtSaldoAnterior == null)
				return;

			string? contaUuid = ObterContaSelecionadaUuid();
			bool temDataValida = ObterDataLancamentoValida(out DateTime dataLanc);

			if (string.IsNullOrEmpty(contaUuid) || !temDataValida)
			{
				CboTipo.IsEnabled = false;
				CboCategoria.IsEnabled = false;
				BtnAddCategoria.IsEnabled = false;
				PainelCamposDependentes.IsEnabled = false;
				BtnSalvar.IsEnabled = false;
				return;
			}

			CboTipo.IsEnabled = true;

			_saldoAnteriorNumerico = MovimentacaoContaService.ObterSaldoAnterior(contaUuid, dataLanc);
			AtualizarExibicaoSaldoAnterior();

			AtualizarEstadoCamposPorTipo();
			RecalcularSaldoAtualProjetado();
		}

		private void AtualizarExibicaoSaldoAnterior()
		{
			if (TxtSaldoAnterior == null) return;

			TxtSaldoAnterior.Text = $"Saldo Anterior..: {_saldoAnteriorNumerico.ToString("C2", _cultura)}";
			TxtSaldoAnterior.Foreground = _saldoAnteriorNumerico >= 0
				? new SolidColorBrush(Color.FromArgb(255, 0, 255, 102))
				: new SolidColorBrush(Color.FromArgb(255, 255, 85, 85));
		}

		private void CboTipo_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (_carregandoTela) return;
			AtualizarEstadoCamposPorTipo();
		}

		private void AtualizarEstadoCamposPorTipo()
		{
			string? tipo = ObterTipoSelecionado();
			bool tipoEscolhido = (tipo == "D" || tipo == "C")
				&& ObterDataLancamentoValida(out _)
				&& !string.IsNullOrEmpty(ObterContaSelecionadaUuid());

			CboCategoria.IsEnabled = tipoEscolhido;
			BtnAddCategoria.IsEnabled = tipoEscolhido;
			PainelCamposDependentes.IsEnabled = tipoEscolhido;
			BtnSalvar.IsEnabled = tipoEscolhido;

			if (!tipoEscolhido || string.IsNullOrEmpty(tipo))
				return;

			LblCategoria.Text = tipo == "C"
				? "CATEGORIA DE RECEITA (*):"
				: "CATEGORIA DE DESPESA (*):";

			string? categoriaAtualUuid = ((CboCategoria.SelectedItem as ComboBoxItem)?.Tag as Categoria)?.CategoriaUuid;
			RecarregarCategoriasCombo(tipo, categoriaAtualUuid);

			AplicarFormatacaoVisualValor();
			RecalcularSaldoAtualProjetado();
		}

		private void SelecionarTipoNoCombo(string tipoAlvo)
		{
			foreach (ComboBoxItem item in CboTipo.Items)
			{
				if (item.Tag?.ToString() == tipoAlvo)
				{
					CboTipo.SelectedItem = item;
					break;
				}
			}
		}

		#endregion

		#region Botões [+] ADD (Categoria e Entidade em Modo Modal)

		private void BtnAddCategoria_Click(object sender, RoutedEventArgs e)
		{
			string tipoInicial = ObterTipoSelecionado() ?? "D";

			HashSet<string> idsTodasAntes;
			Categoria? grupoPaiPadrao;

			using (var db = new AppDbContext())
			{
				idsTodasAntes = db.Categorias
					.AsNoTracking()
					.Select(c => c.CategoriaUuid)
					.ToHashSet();

				grupoPaiPadrao = db.Categorias
					.AsNoTracking()
					.FirstOrDefault(c => c.Tipo == tipoInicial && c.Nivel == 1);
			}

			var novaCategoria = new Categoria
			{
				CategoriaUuid = Guid.NewGuid().ToString(),
				Tipo = tipoInicial,
				Nivel = grupoPaiPadrao != null ? (short)2 : (short)1,
				PaiUuid = grupoPaiPadrao?.CategoriaUuid,
				AceitaLancamento = true
			};

			var editorCategoria = new CategoriaEditWindow(novaCategoria);
			UIThemeHelper.AbrirModal(editorCategoria, this);

			editorCategoria.Closed += (s, ev) =>
			{
				this.DispatcherQueue.TryEnqueue(() =>
				{
					try
					{
						_retornandoDeModal = true;

						using var db = new AppDbContext();
						var novasSubcategorias = db.Categorias
							.AsNoTracking()
							.Where(c => c.Nivel >= 2
									 && c.AceitaLancamento
									 && c.CategoriaUuid != TransferenciaService.CategoriaTransferenciaUuid
									 && !idsTodasAntes.Contains(c.CategoriaUuid))
							.OrderByDescending(c => c.CriadoEm)
							.ToList();

						string tipoSelecionadoAgora = ObterTipoSelecionado() ?? tipoInicial;

						var recemCriada = novasSubcategorias.FirstOrDefault(c => c.Tipo == tipoSelecionadoAgora)
										  ?? novasSubcategorias.FirstOrDefault();

						if (recemCriada != null)
						{
							if (recemCriada.Tipo != tipoSelecionadoAgora)
							{
								SelecionarTipoNoCombo(recemCriada.Tipo);
								tipoSelecionadoAgora = recemCriada.Tipo;
							}

							RecarregarCategoriasCombo(tipoSelecionadoAgora, recemCriada.CategoriaUuid);
							TxtDescricao.Focus(FocusState.Programmatic);
						}
						else
						{
							RecarregarCategoriasCombo(tipoSelecionadoAgora);
						}
					}
					finally
					{
						_retornandoDeModal = false;
					}
				});
			};
		}

		private void AsbEntidade_GotFocus(object sender, RoutedEventArgs e)
		{
			if (_retornandoDeModal) return;
			FiltrarSugestoesEntidade(AsbEntidade.Text);
			AsbEntidade.IsSuggestionListOpen = true;
		}

		private void AsbEntidade_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
		{
			if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
			{
				_entidadeSelecionada = null;
				FiltrarSugestoesEntidade(sender.Text);
			}
		}

		private void FiltrarSugestoesEntidade(string? texto)
		{
			string termo = (texto ?? string.Empty).Trim().ToLowerInvariant();
			var filtradas = string.IsNullOrEmpty(termo)
				? _todasEntidades
				: _todasEntidades.Where(e =>
					e.NomeRazao.ToLowerInvariant().Contains(termo) ||
					(!string.IsNullOrEmpty(e.CpfCnpj) && e.CpfCnpj.Contains(termo))).ToList();

			AsbEntidade.DisplayMemberPath = "NomeRazao";
			AsbEntidade.TextMemberPath = "NomeRazao";
			AsbEntidade.ItemsSource = filtradas;
		}

		private void AsbEntidade_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
		{
			if (args.SelectedItem is Entidade ent)
			{
				_entidadeSelecionada = ent;
				sender.Text = ent.NomeRazao;
			}
		}

		private void BtnAddEntidade_Click(object sender, RoutedEventArgs e)
		{
			var idsExistentes = _todasEntidades.Select(x => x.EntidadeUuid).ToHashSet();

			string tipoSugerido = ObterTipoSelecionado() == "C" ? "Cliente" : "Fornecedor";
			var novaEntidade = new Entidade
			{
				EntidadeUuid = Guid.NewGuid().ToString(),
				TipoEntidade = tipoSugerido,
				TipoPessoa = "F",
				NomeRazao = AsbEntidade.Text?.Trim() ?? string.Empty
			};

			var editorEntidade = new EntidadeEditWindow(novaEntidade);
			UIThemeHelper.AbrirModal(editorEntidade, this);

			editorEntidade.Closed += (s, ev) =>
			{
				this.DispatcherQueue.TryEnqueue(() =>
				{
					try
					{
						_retornandoDeModal = true;
						RecarregarEntidadesDoBanco();
						FiltrarSugestoesEntidade(string.Empty);

						var recemCriada = _todasEntidades.FirstOrDefault(x => !idsExistentes.Contains(x.EntidadeUuid));
						if (recemCriada != null)
						{
							_entidadeSelecionada = recemCriada;
							AsbEntidade.Text = recemCriada.NomeRazao;
							TxtValor.Focus(FocusState.Programmatic);
						}
					}
					finally
					{
						_retornandoDeModal = false;
					}
				});
			};
		}

		#endregion

		#region Valor (Azul C / Vermelho Negativo D), Conciliação e Saldo Atual

		private void TxtValor_TextChanged(object sender, TextChangedEventArgs e)
		{
			if (_formatandoTexto) return;

			string digitos = new string(TxtValor.Text.Where(char.IsDigit).ToArray());
			if (string.IsNullOrEmpty(digitos))
			{
				_valorDigitadoAbsoluto = 0m;
				_formatandoTexto = true;
				TxtValor.Text = string.Empty;
				_formatandoTexto = false;
				RecalcularSaldoAtualProjetado();
				return;
			}

			if (decimal.TryParse(digitos, out decimal centavos))
			{
				_valorDigitadoAbsoluto = centavos / 100m;
			}

			AplicarFormatacaoVisualValor();
			RecalcularSaldoAtualProjetado();
		}

		private void AplicarFormatacaoVisualValor()
		{
			if (TxtValor == null) return;

			string? tipo = ObterTipoSelecionado();

			TxtValor.Foreground = tipo == "C"
				? new SolidColorBrush(Color.FromArgb(255, 51, 153, 255))
				: new SolidColorBrush(Color.FromArgb(255, 255, 85, 85));

			if (_valorDigitadoAbsoluto <= 0m)
				return;

			_formatandoTexto = true;
			string numeroFormatado = _valorDigitadoAbsoluto.ToString("N2", _cultura);
			TxtValor.Text = tipo == "D" ? $"- {numeroFormatado}" : numeroFormatado;
			TxtValor.SelectionStart = TxtValor.Text.Length;
			_formatandoTexto = false;
		}

		private void RecalcularSaldoAtualProjetado()
		{
			if (TxtSaldoAtual == null) return;

			string? tipo = ObterTipoSelecionado();
			if (tipo == "C")
				_saldoAtualNumerico = _saldoAnteriorNumerico + _valorDigitadoAbsoluto;
			else if (tipo == "D")
				_saldoAtualNumerico = _saldoAnteriorNumerico - _valorDigitadoAbsoluto;
			else
				_saldoAtualNumerico = _saldoAnteriorNumerico;

			TxtSaldoAtual.Text = $"Saldo Atual: {_saldoAtualNumerico.ToString("C2", _cultura)}";
			TxtSaldoAtual.Foreground = _saldoAtualNumerico >= 0
				? new SolidColorBrush(Color.FromArgb(255, 0, 255, 102))
				: new SolidColorBrush(Color.FromArgb(255, 255, 85, 85));
		}

		private void CboConciliado_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (_carregandoTela || TxtDataConciliacao == null) return;

			string tag = (CboConciliado.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "S";
			if (tag == "S")
			{
				TxtDataConciliacao.IsEnabled = true;
				if (string.IsNullOrWhiteSpace(TxtDataConciliacao.Text))
				{
					_formatandoTexto = true;
					TxtDataConciliacao.Text = ObterDataLancamentoValida(out DateTime dtLanc)
						? dtLanc.ToString("dd/MM/yyyy")
						: DateTime.Today.ToString("dd/MM/yyyy");
					_formatandoTexto = false;
				}
			}
			else
			{
				_formatandoTexto = true;
				TxtDataConciliacao.Text = string.Empty;
				_formatandoTexto = false;
				TxtDataConciliacao.IsEnabled = false;
			}
		}

		private void TxtDataConciliacao_TextChanged(object sender, TextChangedEventArgs e)
		{
			FormatarCampoData(TxtDataConciliacao);
		}

		private void FormatarCampoData(TextBox tb)
		{
			if (_formatandoTexto || tb == null) return;

			_formatandoTexto = true;
			string digitos = new string(tb.Text.Where(char.IsDigit).ToArray());
			if (digitos.Length > 8) digitos = digitos.Substring(0, 8);

			string formatada = digitos.Length switch
			{
				<= 2 => digitos,
				<= 4 => $"{digitos[..2]}/{digitos[2..]}",
				_ => $"{digitos[..2]}/{digitos[2..4]}/{digitos[4..]}"
			};

			if (tb.Text != formatada)
			{
				tb.Text = formatada;
				tb.SelectionStart = formatada.Length;
			}
			_formatandoTexto = false;
		}

		#endregion

		#region Gravação do Lançamento e Atualização da Tabela de Saldos

		private void BtnSalvar_Click(object sender, RoutedEventArgs e)
		{
			TxtMensagem.Visibility = Visibility.Collapsed;

			string? contaUuid = ObterContaSelecionadaUuid();
			if (string.IsNullOrEmpty(contaUuid))
			{
				ExibirMensagem("> [ERRO] Selecione uma Conta Corrente.", true);
				return;
			}

			if (!ObterDataLancamentoValida(out DateTime dataLancamento))
			{
				ExibirMensagem("> [ERRO] Informe uma Data de Lancamento valida.", true);
				TxtDataLancamento.Focus(FocusState.Programmatic);
				return;
			}

			string? tipo = ObterTipoSelecionado();
			if (tipo != "D" && tipo != "C")
			{
				ExibirMensagem("> [ERRO] Escolha o Tipo do lancamento (D ou C).", true);
				return;
			}

			Categoria? categoriaSelecionada = (CboCategoria.SelectedItem as ComboBoxItem)?.Tag as Categoria
										   ?? CboCategoria.SelectedItem as Categoria;

			if (categoriaSelecionada == null)
			{
				ExibirMensagem("> [ERRO] Selecione uma Categoria valida no combo.", true);
				CboCategoria.Focus(FocusState.Programmatic);
				return;
			}

			string descricao = TxtDescricao.Text.Trim();
			if (string.IsNullOrEmpty(descricao))
			{
				ExibirMensagem("> [ERRO] A Descricao (Historico) e obrigatoria.", true);
				TxtDescricao.Focus(FocusState.Programmatic);
				return;
			}

			if (_valorDigitadoAbsoluto <= 0m)
			{
				ExibirMensagem("> [ERRO] Informe um Valor maior que zero.", true);
				TxtValor.Focus(FocusState.Programmatic);
				return;
			}

			if (_entidadeSelecionada == null && !string.IsNullOrWhiteSpace(AsbEntidade.Text))
			{
				_entidadeSelecionada = _todasEntidades.FirstOrDefault(ent =>
					ent.NomeRazao.Equals(AsbEntidade.Text.Trim(), StringComparison.OrdinalIgnoreCase));
			}

			bool conciliado = ((CboConciliado.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "S") == "S";
			DateTime? dataConciliacao = null;

			if (conciliado)
			{
				if (DateTime.TryParseExact(TxtDataConciliacao.Text.Trim(), "dd/MM/yyyy", _cultura, DateTimeStyles.None, out DateTime dtConc))
					dataConciliacao = dtConc.Date;
				else
					dataConciliacao = dataLancamento.Date;
			}

			string usuarioUuid = _lancamentoEditando?.UsuarioUuid
				?? App.SupabaseClient?.Auth.CurrentUser?.Id
				?? string.Empty;

			try
			{
				var lancamento = new LancamentoConta
				{
					LancamentoUuid = _lancamentoEditando?.LancamentoUuid ?? Guid.NewGuid().ToString(),
					UsuarioUuid = usuarioUuid,
					ContaUuid = contaUuid,
					CategoriaUuid = categoriaSelecionada.CategoriaUuid,
					EntidadeUuid = _entidadeSelecionada?.EntidadeUuid,
					DataLancamento = dataLancamento.Date,
					Descricao = descricao,
					Tipo = tipo,
					Valor = _valorDigitadoAbsoluto,
					Conciliado = conciliado,
					DataConciliacao = dataConciliacao
				};

				if (EhEdicao)
				{
					MovimentacaoContaService.AtualizarLancamento(lancamento);
					this.Close();
					return;
				}

				MovimentacaoContaService.IncluirLancamento(lancamento);

				_saldoAnteriorNumerico = _saldoAtualNumerico;
				AtualizarExibicaoSaldoAnterior();

				_valorDigitadoAbsoluto = 0m;
				_formatandoTexto = true;
				TxtValor.Text = string.Empty;
				_formatandoTexto = false;
				TxtDescricao.Text = string.Empty;
				if (CboCategoria.Items.Count > 0)
					CboCategoria.SelectedIndex = 0;
				_entidadeSelecionada = null;
				AsbEntidade.Text = string.Empty;

				RecalcularSaldoAtualProjetado();
				ExibirMensagem("> [OK] Lancamento gravado e tabela de Saldos recalculada com sucesso!", false);
				CboCategoria.Focus(FocusState.Programmatic);
			}
			catch (Exception ex)
			{
				ExibirMensagem($"> [ERRO AO GRAVAR LANCAMENTO]: {ex.Message}", true);
			}
		}

		private void BtnFechar_Click(object sender, RoutedEventArgs e) => this.Close();

		private void ExibirMensagem(string msg, bool ehErro)
		{
			TxtMensagem.Text = msg;
			TxtMensagem.Foreground = ehErro
				? new SolidColorBrush(Color.FromArgb(255, 255, 85, 85))
				: new SolidColorBrush(Color.FromArgb(255, 0, 255, 102));
			TxtMensagem.Visibility = Visibility.Visible;
		}

		#endregion
	}
}