using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data;

namespace ContaMobi.Data
{
    public class PermissoesPlanoAtual
    {
        public string UsuarioUuid { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string NomeCompleto { get; set; } = string.Empty;
        public string PlanoUuid { get; set; } = string.Empty;
        public string PlanoCodigo { get; set; } = "gratis";
        public string PlanoNome { get; set; } = "Plano Grátis";
        public decimal ValorPlano { get; set; } = 0m;
        public string CicloCobranca { get; set; } = "gratis";
        public DateTime? DataVencimento { get; set; }
        public bool PlanoVencido { get; set; }

        public int? LimiteContas { get; set; } = 1;
        public int? LimiteCartoes { get; set; } = 0;

        public bool AcessoContasPagar { get; set; } = true;
        public bool AcessoContasReceber { get; set; } = true;
        public bool AcessoOrcamento { get; set; } = false;
        public bool AcessoFluxoCaixa { get; set; } = false;
    }

    public static class PlanoPermissaoService
    {
        private static bool _estruturaVerificada = false;

        /// <summary>
        /// Garante que as tabelas 'planos', 'usuarios' e a view 'vw_permissoes_usuario'
        /// existam no SQLite local com a mesma estrutura do Supabase.
        /// </summary>
        public static void GarantirEstruturaPlanosEUsuarios()
        {
            if (_estruturaVerificada) return;

            using var db = new AppDbContext();

            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS planos (
                    planouuid TEXT NOT NULL CONSTRAINT PK_planos PRIMARY KEY,
                    nome TEXT NOT NULL,
                    valor NUMERIC(15, 2) DEFAULT 0.00,
                    limite_transacoes INTEGER NULL,
                    criado_em TEXT NULL,
                    limite_bancos INTEGER DEFAULT 3,
                    limite_contas INTEGER DEFAULT 3,
                    updated_at TEXT NULL,
                    codigo TEXT NULL,
                    valor_anual_parcela NUMERIC(15, 2) NOT NULL DEFAULT 0.00,
                    valor_anual_total NUMERIC(15, 2) NOT NULL DEFAULT 0.00,
                    limite_cartoes INTEGER DEFAULT 0,
                    acesso_contas_pagar INTEGER NOT NULL DEFAULT 1,
                    acesso_contas_receber INTEGER NOT NULL DEFAULT 1,
                    acesso_orcamento INTEGER NOT NULL DEFAULT 0,
                    acesso_fluxo_caixa INTEGER NOT NULL DEFAULT 0,
                    ordem_exibicao INTEGER NOT NULL DEFAULT 1
                );

                CREATE UNIQUE INDEX IF NOT EXISTS idx_planos_codigo ON planos (codigo);

                CREATE TABLE IF NOT EXISTS usuarios (
                    usuariouuid TEXT NOT NULL CONSTRAINT PK_usuarios PRIMARY KEY,
                    planouuid TEXT NULL,
                    email TEXT NOT NULL,
                    nomecompleto TEXT NOT NULL,
                    cpf TEXT NULL,
                    datavencimento TEXT NULL,
                    sexo TEXT NULL,
                    datanascimento TEXT NULL,
                    criado_em TEXT NULL,
                    celular TEXT NULL,
                    ciclo_cobranca TEXT DEFAULT 'gratis',
                    mp_payment_id TEXT NULL
                );
            ");

            // Adiciona colunas novas caso as tabelas já existissem de versões anteriores
            var colsPlanos = ObterColunasTabela(db, "planos");
            if (!colsPlanos.Contains("codigo"))
                db.Database.ExecuteSqlRaw("ALTER TABLE planos ADD COLUMN codigo TEXT NULL;");
            if (!colsPlanos.Contains("valor_anual_parcela"))
                db.Database.ExecuteSqlRaw("ALTER TABLE planos ADD COLUMN valor_anual_parcela NUMERIC(15, 2) NOT NULL DEFAULT 0.00;");
            if (!colsPlanos.Contains("valor_anual_total"))
                db.Database.ExecuteSqlRaw("ALTER TABLE planos ADD COLUMN valor_anual_total NUMERIC(15, 2) NOT NULL DEFAULT 0.00;");
            if (!colsPlanos.Contains("limite_cartoes"))
                db.Database.ExecuteSqlRaw("ALTER TABLE planos ADD COLUMN limite_cartoes INTEGER DEFAULT 0;");
            if (!colsPlanos.Contains("acesso_contas_pagar"))
                db.Database.ExecuteSqlRaw("ALTER TABLE planos ADD COLUMN acesso_contas_pagar INTEGER NOT NULL DEFAULT 1;");
            if (!colsPlanos.Contains("acesso_contas_receber"))
                db.Database.ExecuteSqlRaw("ALTER TABLE planos ADD COLUMN acesso_contas_receber INTEGER NOT NULL DEFAULT 1;");
            if (!colsPlanos.Contains("acesso_orcamento"))
                db.Database.ExecuteSqlRaw("ALTER TABLE planos ADD COLUMN acesso_orcamento INTEGER NOT NULL DEFAULT 0;");
            if (!colsPlanos.Contains("acesso_fluxo_caixa"))
                db.Database.ExecuteSqlRaw("ALTER TABLE planos ADD COLUMN acesso_fluxo_caixa INTEGER NOT NULL DEFAULT 0;");
            if (!colsPlanos.Contains("ordem_exibicao"))
                db.Database.ExecuteSqlRaw("ALTER TABLE planos ADD COLUMN ordem_exibicao INTEGER NOT NULL DEFAULT 1;");

            var colsUsuarios = ObterColunasTabela(db, "usuarios");
            if (!colsUsuarios.Contains("celular"))
                db.Database.ExecuteSqlRaw("ALTER TABLE usuarios ADD COLUMN celular TEXT NULL;");
            if (!colsUsuarios.Contains("ciclo_cobranca"))
                db.Database.ExecuteSqlRaw("ALTER TABLE usuarios ADD COLUMN ciclo_cobranca TEXT DEFAULT 'gratis';");
            if (!colsUsuarios.Contains("mp_payment_id"))
                db.Database.ExecuteSqlRaw("ALTER TABLE usuarios ADD COLUMN mp_payment_id TEXT NULL;");

            // Garante que exista o plano 'gratis' no SQLite para o CROSS JOIN da view nunca retornar vazio
            db.Database.ExecuteSqlRaw(@"
                INSERT INTO planos (
                    planouuid, nome, valor, limite_bancos, limite_contas, codigo,
                    valor_anual_parcela, valor_anual_total, limite_cartoes,
                    acesso_contas_pagar, acesso_contas_receber, acesso_orcamento, acesso_fluxo_caixa, ordem_exibicao
                )
                SELECT
                    '00000000-0000-0000-0000-000000000001', 'Plano Grátis', 0.00, 3, 1, 'gratis',
                    0.00, 0.00, 0,
                    1, 1, 0, 0, 1
                WHERE NOT EXISTS (
                    SELECT 1 FROM planos WHERE lower(codigo) = 'gratis'
                );
            ");

            // Cria/atualiza a view espelho de public.vw_permissoes_usuario
            db.Database.ExecuteSqlRaw(@"
                DROP VIEW IF EXISTS vw_permissoes_usuario;
                CREATE VIEW vw_permissoes_usuario AS
                WITH plano_gratis AS (
                    SELECT
                        planos.planouuid,
                        planos.nome,
                        planos.valor,
                        planos.limite_transacoes,
                        planos.criado_em,
                        planos.limite_bancos,
                        planos.limite_contas,
                        planos.updated_at,
                        planos.codigo,
                        planos.valor_anual_parcela,
                        planos.valor_anual_total,
                        planos.limite_cartoes,
                        planos.acesso_contas_pagar,
                        planos.acesso_contas_receber,
                        planos.acesso_orcamento,
                        planos.acesso_fluxo_caixa,
                        planos.ordem_exibicao
                    FROM planos
                    WHERE lower(planos.codigo) = 'gratis'
                    LIMIT 1
                )
                SELECT
                    u.usuariouuid,
                    u.email,
                    u.nomecompleto,
                    u.datavencimento,
                    u.ciclo_cobranca,
                    CASE
                        WHEN lower(COALESCE(p_orig.codigo, 'gratis')) <> 'gratis'
                             AND u.datavencimento IS NOT NULL
                             AND date(u.datavencimento) < date('now', 'localtime')
                        THEN 1
                        ELSE 0
                    END AS plano_vencido,
                    p_efetivo.planouuid,
                    p_efetivo.codigo AS plano_codigo,
                    p_efetivo.nome AS plano_nome,
                    p_efetivo.valor,
                    p_efetivo.limite_contas,
                    p_efetivo.limite_cartoes,
                    p_efetivo.acesso_contas_pagar,
                    p_efetivo.acesso_contas_receber,
                    p_efetivo.acesso_orcamento,
                    p_efetivo.acesso_fluxo_caixa
                FROM usuarios u
                LEFT JOIN planos p_orig ON lower(p_orig.planouuid) = lower(u.planouuid)
                CROSS JOIN plano_gratis pg
                JOIN planos p_efetivo ON lower(p_efetivo.planouuid) = lower(
                    CASE
                        WHEN p_orig.planouuid IS NULL THEN pg.planouuid
                        WHEN lower(COALESCE(p_orig.codigo, 'gratis')) <> 'gratis'
                             AND u.datavencimento IS NOT NULL
                             AND date(u.datavencimento) < date('now', 'localtime')
                        THEN pg.planouuid
                        ELSE p_orig.planouuid
                    END
                );
            ");

            _estruturaVerificada = true;
        }

        private static HashSet<string> ObterColunasTabela(AppDbContext db, string tabela)
        {
            var colunas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var conn = db.Database.GetDbConnection();
            bool abriuAqui = conn.State != ConnectionState.Open;
            if (abriuAqui) conn.Open();

            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"PRAGMA table_info('{tabela}');";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string? nome = reader["name"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(nome))
                        colunas.Add(nome.Trim());
                }
            }
            finally
            {
                if (abriuAqui) conn.Close();
            }

            return colunas;
        }

        /// <summary>
        /// Consulta diretamente a view local 'vw_permissoes_usuario', retornando o plano efetivo
        /// (já aplicando fallback para 'gratis' caso não tenha plano ou esteja vencido).
        /// </summary>
        public static PermissoesPlanoAtual ObterPermissoesUsuario(string usuarioUuid)
        {
            GarantirEstruturaPlanosEUsuarios();

            var padraoGratis = new PermissoesPlanoAtual();
            if (string.IsNullOrWhiteSpace(usuarioUuid))
                return padraoGratis;

            using var db = new AppDbContext();
            var conn = db.Database.GetDbConnection();
            bool abriuAqui = conn.State != ConnectionState.Open;
            if (abriuAqui) conn.Open();

            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT
                        usuariouuid,
                        email,
                        nomecompleto,
                        datavencimento,
                        ciclo_cobranca,
                        plano_vencido,
                        planouuid,
                        plano_codigo,
                        plano_nome,
                        valor,
                        limite_contas,
                        limite_cartoes,
                        acesso_contas_pagar,
                        acesso_contas_receber,
                        acesso_orcamento,
                        acesso_fluxo_caixa
                    FROM vw_permissoes_usuario
                    WHERE lower(usuariouuid) = lower(@uid)
                    LIMIT 1;";

                var param = cmd.CreateParameter();
                param.ParameterName = "@uid";
                param.Value = usuarioUuid.Trim();
                cmd.Parameters.Add(param);

                using var reader = cmd.ExecuteReader();
                if (!reader.Read())
                    return padraoGratis;

                DateTime? dtVenc = null;
                string? dtVencStr = reader["datavencimento"]?.ToString();
                if (!string.IsNullOrWhiteSpace(dtVencStr) && DateTime.TryParse(dtVencStr, out DateTime parsed))
                {
                    dtVenc = parsed.Date;
                }

                return new PermissoesPlanoAtual
                {
                    UsuarioUuid = reader["usuariouuid"]?.ToString() ?? string.Empty,
                    Email = reader["email"]?.ToString() ?? string.Empty,
                    NomeCompleto = reader["nomecompleto"]?.ToString() ?? string.Empty,
                    DataVencimento = dtVenc,
                    CicloCobranca = reader["ciclo_cobranca"]?.ToString() ?? "gratis",
                    PlanoVencido = reader["plano_vencido"] != DBNull.Value && Convert.ToInt32(reader["plano_vencido"]) == 1,
                    PlanoUuid = reader["planouuid"]?.ToString() ?? string.Empty,
                    PlanoCodigo = reader["plano_codigo"]?.ToString() ?? "gratis",
                    PlanoNome = reader["plano_nome"]?.ToString() ?? "Plano Grátis",
                    ValorPlano = reader["valor"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["valor"]),
                    LimiteContas = reader["limite_contas"] == DBNull.Value ? null : Convert.ToInt32(reader["limite_contas"]),
                    LimiteCartoes = reader["limite_cartoes"] == DBNull.Value ? null : Convert.ToInt32(reader["limite_cartoes"]),
                    AcessoContasPagar = reader["acesso_contas_pagar"] != DBNull.Value && Convert.ToInt32(reader["acesso_contas_pagar"]) == 1,
                    AcessoContasReceber = reader["acesso_contas_receber"] != DBNull.Value && Convert.ToInt32(reader["acesso_contas_receber"]) == 1,
                    AcessoOrcamento = reader["acesso_orcamento"] != DBNull.Value && Convert.ToInt32(reader["acesso_orcamento"]) == 1,
                    AcessoFluxoCaixa = reader["acesso_fluxo_caixa"] != DBNull.Value && Convert.ToInt32(reader["acesso_fluxo_caixa"]) == 1
                };
            }
            finally
            {
                if (abriuAqui) conn.Close();
            }
        }
    }
}