using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace ContaMobi.Models
{
    [Table("Bancos")]
    public class Banco
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        [Column("codigo_banco")]
        public int CodigoBanco { get; set; }

        [Required]
        [MaxLength(150)]
        [Column("descricao_banco")]
        public string DescricaoBanco { get; set; } = string.Empty;

        [MaxLength(50)]
        [Column("telefone_sac")]
        public string? TelefoneSac { get; set; }

        [MaxLength(20)]
        [Column("codigo_ispb")]
        public string? CodigoIspb { get; set; }

        [MaxLength(255)]
        [Column("icone_url")]
        public string? IconeUrl { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [MaxLength(36)]
        [Column("usuariouuid")]
        public string? UsuarioUuid { get; set; }

        public bool Ativo { get; set; } = true;

        [NotMapped]
        public string CodigoCompensacao
        {
            get => CodigoBanco.ToString();
            set
            {
                if (int.TryParse(value, out int cod))
                    CodigoBanco = cod;
            }
        }

        [NotMapped]
        public string Nome
        {
            get => DescricaoBanco;
            set => DescricaoBanco = value ?? string.Empty;
        }

        [NotMapped]
        public string? Apelido
        {
            get => DescricaoBanco;
            set { }
        }

        [NotMapped]
        public string ExibicaoFormatada => $"{CodigoBanco.ToString().PadLeft(3, '0')} - {DescricaoBanco}";

        [NotMapped]
        public string StatusTexto => Ativo ? "ATIVO" : "INATIVO";

        [NotMapped]
        public string StatusCor => Ativo ? "#00FF66" : "#FF5555";
    }

    [Table("Categorias")]
    public class Categoria
    {
        [Key]
        [MaxLength(36)]
        [Column("categoriauuid")]
        public string CategoriaUuid { get; set; } = Guid.NewGuid().ToString();

        [Required]
        [MaxLength(36)]
        [Column("usuariouuid")]
        public string UsuarioUuid { get; set; } = string.Empty;

        [MaxLength(36)]
        [Column("paiuuid")]
        public string? PaiUuid { get; set; }

        [Required]
        [MaxLength(100)]
        [Column("descricao")]
        public string Descricao { get; set; } = string.Empty;

        [Required]
        [MaxLength(1)]
        [Column("tipo")]
        public string Tipo { get; set; } = "D";

        [Column("nivel")]
        public short Nivel { get; set; } = 1;

        [Column("aceita_lancamento")]
        public bool AceitaLancamento { get; set; } = false;

        [Column("criado_em")]
        public DateTime? CriadoEm { get; set; } = DateTime.UtcNow;

        [Column("atualizado_em")]
        public DateTime? AtualizadoEm { get; set; } = DateTime.UtcNow;

        [ForeignKey("PaiUuid")]
        public virtual Categoria? CategoriaPai { get; set; }

        public virtual ICollection<Categoria> Subcategorias { get; set; } = new List<Categoria>();

        [NotMapped]
        public string TipoDescricao => Tipo == "C" ? "RECEITA" : "DESPESA";

        [NotMapped]
        public string TipoCor => Tipo == "C" ? "#00FF66" : "#FF5555";

        [NotMapped]
        public string NivelDescricao => Nivel == 1 ? "PAI (GRUPO)" : "FILHO (SUB)";

        [NotMapped]
        public string DescricaoHierarquica => Nivel == 2 ? $"   └── {Descricao}" : $"[+] {Descricao}";

        [NotMapped]
        public string AceitaLancamentoTexto => AceitaLancamento ? "SIM" : "NÃO";

        [NotMapped]
        public string AceitaLancamentoCor => AceitaLancamento ? "#00FF66" : "#777777";

        [NotMapped]
        public bool EhPai => Nivel == 1;

        [NotMapped]
        public string PesoFonte => Nivel == 1 ? "Bold" : "Normal";

        public override string ToString() => Descricao ?? string.Empty;
    }

    [Table("ContasCorrentes")]
    public class ContaCorrente
    {
        [Key]
        [MaxLength(36)]
        [Column("contauuid")]
        public string ContaUuid { get; set; } = Guid.NewGuid().ToString();

        [Required]
        [Column("codigo_banco")]
        public int CodigoBanco { get; set; }

        [Required]
        [MaxLength(36)]
        [Column("usuariouuid")]
        public string UsuarioUuid { get; set; } = string.Empty;

        [Required]
        [MaxLength(150)]
        [Column("descricao")]
        public string Descricao { get; set; } = string.Empty;

        [Column("data_criacao")]
        public DateTime DataCriacao { get; set; } = DateTime.Today;

        [Column("saldo_inicial", TypeName = "decimal(15,2)")]
        public decimal SaldoInicial { get; set; } = 0.00m;

        [Column("criado_em")]
        public DateTime? CriadoEm { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("CodigoBanco")]
        public virtual Banco? Banco { get; set; }

        [NotMapped]
        public string BancoFormatado => Banco != null ? $"{Banco.CodigoBanco.ToString().PadLeft(3, '0')} - {Banco.DescricaoBanco}" : $"BANCO #{CodigoBanco}";

        [NotMapped]
        public string SaldoFormatado => SaldoInicial.ToString("C2", new CultureInfo("pt-BR"));

        [NotMapped]
        public string SaldoCor => SaldoInicial >= 0 ? "#00FF66" : "#FF5555";

        [NotMapped]
        public string DataCriacaoFormatada => DataCriacao.ToString("dd/MM/yyyy");
    }

    [Table("saldo_contas_correntes")]
    public class SaldoContaCorrente
    {
        [Key]
        [MaxLength(36)]
        [Column("saldoid")]
        public string SaldoId { get; set; } = Guid.NewGuid().ToString();

        [Required]
        [MaxLength(36)]
        [Column("contauuid")]
        public string ContaUuid { get; set; } = string.Empty;

        [Required]
        [Column("data")]
        public DateTime Data { get; set; }

        [Column("saldo_inicial", TypeName = "decimal(15,2)")]
        public decimal SaldoInicial { get; set; } = 0.00m;

        [Column("total_credito", TypeName = "decimal(15,2)")]
        public decimal TotalCredito { get; set; } = 0.00m;

        [Column("total_debito", TypeName = "decimal(15,2)")]
        public decimal TotalDebito { get; set; } = 0.00m;

        [Column("saldo_final", TypeName = "decimal(15,2)")]
        public decimal SaldoFinal { get; set; } = 0.00m;

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("ContaUuid")]
        public virtual ContaCorrente? ContaCorrente { get; set; }
    }

    [Table("orcamentos")]
    public class Orcamento
    {
        [Key]
        [MaxLength(36)]
        [Column("orcamentouuid")]
        public string OrcamentoUuid { get; set; } = Guid.NewGuid().ToString();

        [Required]
        [MaxLength(36)]
        [Column("usuariouuid")]
        public string UsuarioUuid { get; set; } = string.Empty;

        [Required]
        [MaxLength(36)]
        [Column("categoriauuid")]
        public string CategoriaUuid { get; set; } = string.Empty;

        [Required]
        [Column("ano")]
        public int Ano { get; set; }

        [Required]
        [Column("mes")]
        public int Mes { get; set; }

        [Column("valor_planejado", TypeName = "decimal(15,2)")]
        public decimal ValorPlanejado { get; set; } = 0.00m;

        [Column("valor_realizado", TypeName = "decimal(15,2)")]
        public decimal ValorRealizado { get; set; } = 0.00m;

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("CategoriaUuid")]
        public virtual Categoria? Categoria { get; set; }
    }

    [Table("entidades")]
    public class Entidade
    {
        [Key]
        [MaxLength(36)]
        [Column("entidadeuuid")]
        public string EntidadeUuid { get; set; } = Guid.NewGuid().ToString();

        [Required]
        [MaxLength(36)]
        [Column("usuariouuid")]
        public string UsuarioUuid { get; set; } = string.Empty;

        [Required]
        [MaxLength(30)]
        [Column("tipo_entidade")]
        public string TipoEntidade { get; set; } = "Cliente";

        [Required]
        [MaxLength(1)]
        [Column("tipo_pessoa")]
        public string TipoPessoa { get; set; } = "F";

        [Required]
        [MaxLength(150)]
        [Column("nome_razao")]
        public string NomeRazao { get; set; } = string.Empty;

        [MaxLength(20)]
        [Column("cpf_cnpj")]
        public string? CpfCnpj { get; set; }

        [MaxLength(20)]
        [Column("rg_ie")]
        public string? RgIe { get; set; }

        [MaxLength(10)]
        [Column("cep")]
        public string? Cep { get; set; }

        [MaxLength(150)]
        [Column("logradouro")]
        public string? Logradouro { get; set; }

        [MaxLength(20)]
        [Column("numero")]
        public string? Numero { get; set; }

        [MaxLength(50)]
        [Column("complemento")]
        public string? Complemento { get; set; }

        [MaxLength(100)]
        [Column("bairro")]
        public string? Bairro { get; set; }

        [MaxLength(100)]
        [Column("cidade")]
        public string? Cidade { get; set; }

        [MaxLength(2)]
        [Column("uf")]
        public string? Uf { get; set; }

        [Column("criado_em")]
        public DateTime? CriadoEm { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<Contato> Contatos { get; set; } = new List<Contato>();

        [NotMapped]
        public string TipoPessoaDescricao => TipoPessoa == "J" ? "PJ" : "PF";

        [NotMapped]
        public string DocumentoFormatado => string.IsNullOrWhiteSpace(CpfCnpj) ? "-" : CpfCnpj;

        [NotMapped]
        public string CidadeUfFormatada => (!string.IsNullOrEmpty(Cidade) && !string.IsNullOrEmpty(Uf))
            ? $"{Cidade}/{Uf}"
            : (Cidade ?? Uf ?? "-");

        [NotMapped]
        public string TipoEntidadeBadge => $"[{TipoEntidade.ToUpper()}]";

        public override string ToString() => NomeRazao ?? string.Empty;
    }

    [Table("contatos")]
    public class Contato
    {
        [Key]
        [MaxLength(36)]
        [Column("contatouuid")]
        public string ContatoUuid { get; set; } = Guid.NewGuid().ToString();

        [Required]
        [MaxLength(36)]
        [Column("entidadeuuid")]
        public string EntidadeUuid { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        [Column("nome_descricao")]
        public string NomeDescricao { get; set; } = string.Empty;

        [MaxLength(30)]
        [Column("fone")]
        public string? Fone { get; set; }

        [MaxLength(100)]
        [Column("email")]
        public string? Email { get; set; }

        [MaxLength(30)]
        [Column("whatsapp")]
        public string? Whatsapp { get; set; }

        [MaxLength(50)]
        [Column("telegram")]
        public string? Telegram { get; set; }

        [MaxLength(50)]
        [Column("instagram")]
        public string? Instagram { get; set; }

        [Column("criado_em")]
        public DateTime? CriadoEm { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("EntidadeUuid")]
        public virtual Entidade? Entidade { get; set; }

        [NotMapped]
        public string ResumoCanais
        {
            get
            {
                var canais = new List<string>();
                if (!string.IsNullOrWhiteSpace(Whatsapp)) canais.Add($"WA: {Whatsapp}");
                if (!string.IsNullOrWhiteSpace(Fone)) canais.Add($"TEL: {Fone}");
                if (!string.IsNullOrWhiteSpace(Email)) canais.Add(Email);
                if (!string.IsNullOrWhiteSpace(Telegram)) canais.Add($"TG: {Telegram}");
                if (!string.IsNullOrWhiteSpace(Instagram)) canais.Add($"IG: @{Instagram.Replace("@", "")}");

                return canais.Count > 0 ? string.Join(" | ", canais) : "(SEM CANAIS)";
            }
        }
    }

    [Table("lancamentos_contas")]
    public class LancamentoConta
    {
        [Key]
        [MaxLength(36)]
        [Column("lancamentouuid")]
        public string LancamentoUuid { get; set; } = Guid.NewGuid().ToString();

        [Required]
        [MaxLength(36)]
        [Column("usuariouuid")]
        public string UsuarioUuid { get; set; } = string.Empty;

        [Required]
        [MaxLength(36)]
        [Column("contauuid")]
        public string ContaUuid { get; set; } = string.Empty;

        [Required]
        [MaxLength(36)]
        [Column("categoriauuid")]
        public string CategoriaUuid { get; set; } = string.Empty;

        [MaxLength(36)]
        [Column("entidadeuuid")]
        public string? EntidadeUuid { get; set; }

        [Required]
        [Column("data_lancamento")]
        public DateTime DataLancamento { get; set; } = DateTime.Today;

        [Required]
        [MaxLength(150)]
        [Column("descricao")]
        public string Descricao { get; set; } = string.Empty;

        [Required]
        [MaxLength(1)]
        [Column("tipo")]


        public string Tipo { get; set; } = "D";

        [Column("valor", TypeName = "decimal(15,2)")]
        public decimal Valor { get; set; } = 0.00m;

        [Column("observacao")]
        public string? Observacao { get; set; }

        [Column("conciliado")]
        public bool Conciliado { get; set; } = true;

        [Column("data_conciliacao")]
        public DateTime? DataConciliacao { get; set; } = DateTime.Today;

        [MaxLength(36)]
        [Column("transferencia_uuid")]
        public string? TransferenciaUuid { get; set; }

        [NotMapped]
        public bool EhTransferencia => !string.IsNullOrWhiteSpace(TransferenciaUuid);

        [Column("criado_em")]
        public DateTime? CriadoEm { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("ContaUuid")]
        public virtual ContaCorrente? ContaCorrente { get; set; }

        [ForeignKey("CategoriaUuid")]
        public virtual Categoria? Categoria { get; set; }

        [ForeignKey("EntidadeUuid")]
        public virtual Entidade? Entidade { get; set; }

        [NotMapped]
        public string DescricaoConta => ContaCorrente?.Descricao ?? "-";

        [NotMapped]
        public string DataLancamentoFormatada => DataLancamento.ToString("dd/MM/yyyy");

        [NotMapped]
        public string NomeCategoria => Categoria?.Descricao ?? "-";

        [NotMapped]
        public string ValorFormatado
        {
            get
            {
                var cultura = new CultureInfo("pt-BR");
                decimal v = Math.Abs(Valor);
                return Tipo == "D" ? $"- {v.ToString("C2", cultura)}" : v.ToString("C2", cultura);
            }
        }

        [NotMapped]
        public string ValorCor => Tipo == "C" ? "#3399FF" : "#FF5555";

        [NotMapped]
        public string ConciliadoSN => Conciliado ? "S" : "N";

        [NotMapped]
        public string ConciliadoCor => Conciliado ? "#00FF66" : "#FFAA00";

        [NotMapped]
        public decimal SaldoAposLancamento { get; set; }

        [NotMapped]
        public string SaldoAposFormatado => SaldoAposLancamento.ToString("C2", new CultureInfo("pt-BR"));

        [NotMapped]
        public string SaldoAposCor => SaldoAposLancamento >= 0 ? "#00FF66" : "#FF5555";
    }

    [Table("contas_pr")]
    public class ContaPR
    {
        [Key]
        [MaxLength(36)]
        [Column("contaspruuid")]
        public string ContasPRUuid { get; set; } = Guid.NewGuid().ToString();

        [Required]
        [MaxLength(36)]
        [Column("loteuuid")]
        public string LoteUuid { get; set; } = Guid.NewGuid().ToString();

        [Required]
        [MaxLength(1)]
        [Column("tipo")]
        public string Tipo { get; set; } = "P";

        [Required]
        [MaxLength(36)]
        [Column("usuariouuid")]
        public string UsuarioUuid { get; set; } = string.Empty;

        [MaxLength(36)]
        [Column("contauuid")]
        public string? ContaUuid { get; set; }

        [Required]
        [MaxLength(36)]
        [Column("categoriauuid")]
        public string CategoriaUuid { get; set; } = string.Empty;

        [MaxLength(36)]
        [Column("entidadeuuid")]
        public string? EntidadeUuid { get; set; }

        [Required]
        [MaxLength(150)]
        [Column("descricao")]
        public string Descricao { get; set; } = string.Empty;

        [Required]
        [Column("data_lancamento")]
        public DateTime DataLancamento { get; set; } = DateTime.Today;

        [Required]
        [Column("data_vencimento")]
        public DateTime DataVencimento { get; set; } = DateTime.Today;

        [Column("valor_base", TypeName = "decimal(15,2)")]
        public decimal ValorBase { get; set; } = 0.00m;

        [Required]
        [MaxLength(1)]
        [Column("modo_parcelamento")]
        public string ModoParcelamento { get; set; } = "R";

        [Column("numero_parcela")]
        public int NumeroParcela { get; set; } = 1;

        [Column("total_parcelas")]
        public int TotalParcelas { get; set; } = 1;

        [Column("intervalo_dias")]
        public int IntervaloDias { get; set; } = 30;

        [Column("valor", TypeName = "decimal(15,2)")]
        public decimal Valor { get; set; } = 0.00m;

        [Column("multa", TypeName = "decimal(15,2)")]
        public decimal Multa { get; set; } = 0.00m;

        [Column("juro", TypeName = "decimal(15,2)")]
        public decimal Juro { get; set; } = 0.00m;

        [Column("acrescimo", TypeName = "decimal(15,2)")]
        public decimal Acrescimo { get; set; } = 0.00m;

        [Column("desconto", TypeName = "decimal(15,2)")]
        public decimal Desconto { get; set; } = 0.00m;

        [Column("valor_pago", TypeName = "decimal(15,2)")]
        public decimal ValorPago { get; set; } = 0.00m;

        [Column("data_pagamento")]
        public DateTime? DataPagamento { get; set; }

        [MaxLength(36)]
        [Column("lancamento_baixa_uuid")]
        public string? LancamentoBaixaUuid { get; set; }

        [Column("criado_em")]
        public DateTime? CriadoEm { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("ContaUuid")]
        public virtual ContaCorrente? ContaCorrente { get; set; }

        [ForeignKey("CategoriaUuid")]
        public virtual Categoria? Categoria { get; set; }

        [ForeignKey("EntidadeUuid")]
        public virtual Entidade? Entidade { get; set; }

        [NotMapped]
        public string DescricaoConta => ContaCorrente?.Descricao ?? "-";

        [NotMapped]
        public string NomeCategoria => Categoria?.Descricao ?? "-";

        [NotMapped]
        public string NomeEntidade => Entidade?.NomeRazao ?? "-";

        [NotMapped]
        public string DataLancamentoFormatada => DataLancamento.ToString("dd/MM/yyyy");

        [NotMapped]
        public string DataVencimentoFormatada => DataVencimento.ToString("dd/MM/yyyy");

        [NotMapped]
        public string DataPagamentoFormatada => DataPagamento.HasValue ? DataPagamento.Value.ToString("dd/MM/yyyy") : "-";

        [NotMapped]
        public string ParcelaFormatada => $"{NumeroParcela:D2}/{TotalParcelas:D2}";

        [NotMapped]
        public decimal ValorTotalComEncargos => Math.Max(0m, Valor + Multa + Juro + Acrescimo - Desconto);

        [NotMapped]
        public string ValorFormatado => Valor.ToString("C2", new CultureInfo("pt-BR"));

        [NotMapped]
        public string EncargosFormatado => (Multa + Juro + Acrescimo - Desconto).ToString("C2", new CultureInfo("pt-BR"));

        [NotMapped]
        public string ValorPagoFormatado => ValorPago.ToString("C2", new CultureInfo("pt-BR"));

        [NotMapped]
        public bool EstaQuitado => DataPagamento.HasValue || ValorPago > 0m;

        [NotMapped]
        public bool PodeBaixar => !EstaQuitado;

        [NotMapped]
        public string StatusTexto
        {
            get
            {
                if (EstaQuitado)
                    return Tipo == "P" ? "PAGO" : "RECEBIDO";
                if (DataVencimento.Date < DateTime.Today)
                    return "VENCIDO";
                return "ABERTO";
            }
        }

        [NotMapped]
        public string StatusCor
        {
            get
            {
                if (EstaQuitado) return "#00FF66";
                if (DataVencimento.Date < DateTime.Today) return "#FF5555";
                return "#FFAA00";
            }
        }

        [NotMapped]
        public string ValorCor => Tipo == "R" ? "#3399FF" : "#FF5555";
    }

    [Table("contas_pr_parcelas")]
    public class ContaPRParcela
    {
        [Key]
        [MaxLength(36)]
        [Column("parcelauuid")]
        public string ParcelaUuid { get; set; } = Guid.NewGuid().ToString();

        [Required]
        [MaxLength(36)]
        [Column("loteuuid")]
        public string LoteUuid { get; set; } = string.Empty;

        [Required]
        [MaxLength(36)]
        [Column("contaspruuid")]
        public string ContasPRUuid { get; set; } = Guid.NewGuid().ToString();

        [Column("numero_parcela")]
        public int NumeroParcela { get; set; } = 1;

        [Column("total_parcelas")]
        public int TotalParcelas { get; set; } = 1;

        [Required]
        [MaxLength(1)]
        [Column("modo_parcelamento")]
        public string ModoParcelamento { get; set; } = "R";

        [Column("intervalo_dias")]
        public int IntervaloDias { get; set; } = 30;

        [Column("valor_base", TypeName = "decimal(15,2)")]
        public decimal ValorBase { get; set; } = 0.00m;

        [Column("data_vencimento")]
        public DateTime DataVencimento { get; set; } = DateTime.Today;

        [Column("valor_parcela", TypeName = "decimal(15,2)")]
        public decimal ValorParcela { get; set; } = 0.00m;

        [Column("criado_em")]
        public DateTime? CriadoEm { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; } = DateTime.UtcNow;

        [NotMapped]
        public string NumeroFormatado => $"{NumeroParcela:D2}/{TotalParcelas:D2}";

        [NotMapped]
        public string DataVencimentoTexto { get; set; } = DateTime.Today.ToString("dd/MM/yyyy");

        [NotMapped]
        public string ValorParcelaTexto { get; set; } = "0,00";

        [NotMapped]
        public string DestaqueCor { get; set; } = "#00FF66";
    }

    public class OrcamentoMesResumo
    {
        private static readonly CultureInfo _cultura = new("pt-BR");

        public int Ano { get; set; }
        public int Mes { get; set; }
        public bool PossuiLancamentosOrcados { get; set; }

        public decimal SaldoAnterior { get; set; }
        public decimal TotalCreditos { get; set; }
        public decimal TotalDebitos { get; set; }
        public decimal SaldoFinal => SaldoAnterior + TotalCreditos - TotalDebitos;

        public decimal SaldoAnteriorRealizado { get; set; }
        public decimal TotalCreditosRealizado { get; set; }
        public decimal TotalDebitosRealizado { get; set; }
        public decimal SaldoFinalRealizado => SaldoAnteriorRealizado + TotalCreditosRealizado - TotalDebitosRealizado;

        public string ChaveMesAno => $"{Ano:D4}-{Mes:D2}";

        public string MesAnoFormatado
        {
            get
            {
                string nomeMes = new DateTime(Ano, Mes, 1).ToString("MMMM", _cultura).ToUpperInvariant();
                return $"{Mes:D2}/{Ano:D4} - {nomeMes}";
            }
        }

        public string StatusTag => PossuiLancamentosOrcados ? "[ATIVO]" : "[PROJETADO]";
        public string CorStatus => PossuiLancamentosOrcados ? "#00FF66" : "#8899AA";

        public string SaldoAnteriorFormatado => SaldoAnterior.ToString("C2", _cultura);
        public string TotalCreditosFormatado => TotalCreditos.ToString("C2", _cultura);
        public string TotalDebitosFormatado => TotalDebitos.ToString("C2", _cultura);
        public string SaldoFinalFormatado => SaldoFinal.ToString("C2", _cultura);

        public string CorSaldoAnterior => SaldoAnterior >= 0 ? "#00FF66" : "#FF5555";
        public string CorSaldoFinal => SaldoFinal >= 0 ? "#00FF66" : "#FF5555";
    }

    public class OrcamentoCategoriaItem : INotifyPropertyChanged
    {
        private static readonly CultureInfo _cultura = new("pt-BR");

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));

        public string OrcamentoUuid { get; set; } = string.Empty;
        public string CategoriaUuid { get; set; } = string.Empty;
        public string? PaiUuid { get; set; }
        public string CategoriaDescricao { get; set; } = string.Empty;
        public string Tipo { get; set; } = "D";

        public bool EhCategoriaPai { get; set; }

        private bool _temFilhas;
        public bool TemFilhas
        {
            get => _temFilhas;
            set
            {
                if (_temFilhas != value)
                {
                    _temFilhas = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(BotaoExpansaoTexto));
                    OnPropertyChanged(nameof(MostrarSubtotalPai));
                    OnPropertyChanged(nameof(MostrarEntradaEditavel));
                }
            }
        }

        private bool _estaExpandida = true;
        public bool EstaExpandida
        {
            get => _estaExpandida;
            set
            {
                if (_estaExpandida != value)
                {
                    _estaExpandida = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(BotaoExpansaoTexto));
                }
            }
        }

        private decimal _valorPlanejado;
        public decimal ValorPlanejado
        {
            get => _valorPlanejado;
            set
            {
                if (_valorPlanejado != value)
                {
                    _valorPlanejado = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PercentualAtingido));
                    OnPropertyChanged(nameof(PercentualFormatado));
                    OnPropertyChanged(nameof(CorDesvio));
                }
            }
        }

        private string _valorPlanejadoTexto = "0,00";
        public string ValorPlanejadoTexto
        {
            get => _valorPlanejadoTexto;
            set
            {
                if (_valorPlanejadoTexto != value)
                {
                    _valorPlanejadoTexto = value;
                    OnPropertyChanged();
                }
            }
        }

        private decimal _valorRealizado;
        public decimal ValorRealizado
        {
            get => _valorRealizado;
            set
            {
                if (_valorRealizado != value)
                {
                    _valorRealizado = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PercentualAtingido));
                    OnPropertyChanged(nameof(PercentualFormatado));
                    OnPropertyChanged(nameof(CorDesvio));
                }
            }
        }

        private string _valorRealizadoTexto = "0,00";
        public string ValorRealizadoTexto
        {
            get => _valorRealizadoTexto;
            set
            {
                if (_valorRealizadoTexto != value)
                {
                    _valorRealizadoTexto = value;
                    OnPropertyChanged();
                }
            }
        }

        private decimal _subtotalPlanejadoPai;
        public decimal SubtotalPlanejadoPai
        {
            get => _subtotalPlanejadoPai;
            set
            {
                if (_subtotalPlanejadoPai != value)
                {
                    _subtotalPlanejadoPai = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SubtotalPlanejadoPaiTexto));
                    OnPropertyChanged(nameof(PercentualAtingido));
                    OnPropertyChanged(nameof(PercentualFormatado));
                    OnPropertyChanged(nameof(CorDesvio));
                }
            }
        }

        private decimal _subtotalRealizadoPai;
        public decimal SubtotalRealizadoPai
        {
            get => _subtotalRealizadoPai;
            set
            {
                if (_subtotalRealizadoPai != value)
                {
                    _subtotalRealizadoPai = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SubtotalRealizadoPaiTexto));
                    OnPropertyChanged(nameof(PercentualAtingido));
                    OnPropertyChanged(nameof(PercentualFormatado));
                    OnPropertyChanged(nameof(CorDesvio));
                }
            }
        }

        public string SubtotalPlanejadoPaiTexto => _subtotalPlanejadoPai.ToString("C2", _cultura);
        public string SubtotalRealizadoPaiTexto => _subtotalRealizadoPai.ToString("C2", _cultura);

        public bool MostrarBotaoPai => EhCategoriaPai;
        public bool MostrarLinhaFilha => !EhCategoriaPai;

        public bool MostrarSubtotalPai => EhCategoriaPai && TemFilhas;
        public bool MostrarEntradaEditavel => !(EhCategoriaPai && TemFilhas);

        public string BotaoExpansaoTexto => TemFilhas ? (EstaExpandida ? "[-]" : "[+]") : "•";

        public string TituloPai => CategoriaDescricao.ToUpperInvariant();
        public string TituloFilha => $"└─ {CategoriaDescricao}";

        public string CorValor => Tipo == "C" ? "#3399FF" : "#FF5555";

        public decimal PercentualAtingido
        {
            get
            {
                decimal plan = MostrarSubtotalPai ? SubtotalPlanejadoPai : ValorPlanejado;
                decimal real = MostrarSubtotalPai ? SubtotalRealizadoPai : ValorRealizado;
                return plan > 0 ? (real / plan) * 100m : (real > 0 ? 100m : 0m);
            }
        }

        public string PercentualFormatado
        {
            get
            {
                decimal plan = MostrarSubtotalPai ? SubtotalPlanejadoPai : ValorPlanejado;
                decimal real = MostrarSubtotalPai ? SubtotalRealizadoPai : ValorRealizado;
                return plan > 0 ? $"{PercentualAtingido:N1}%" : (real > 0 ? "100%*" : "-");
            }
        }

        public string CorDesvio
        {
            get
            {
                decimal plan = MostrarSubtotalPai ? SubtotalPlanejadoPai : ValorPlanejado;
                decimal real = MostrarSubtotalPai ? SubtotalRealizadoPai : ValorRealizado;

                if (Tipo == "C")
                    return real >= plan ? "#00FF66" : "#FFAA00";
                else
                    return real > plan ? "#FF5555" : "#00FF66";
            }
        }
    }
}