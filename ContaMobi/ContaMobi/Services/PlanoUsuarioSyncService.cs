using ContaMobi.Data;
using ContaMobi.Models;
using Microsoft.EntityFrameworkCore;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ContaMobi.Services
{
    public static class PlanoUsuarioSyncService
    {
        /// <summary>
        /// Cache em memória das permissões efetivas do usuário logado (já avaliadas pela view vw_permissoes_usuario).
        /// </summary>
        public static PermissoesPlanoAtual PermissoesAtuais { get; private set; } = new();

        /// <summary>
        /// Sincroniza 'planos' e o registro do usuário autenticado em 'usuarios' do Supabase para o SQLite local.
        /// Se estiver offline, mantém os dados locais já salvos e avalia o vencimento offline pela view.
        /// </summary>
        public static async Task<PermissoesPlanoAtual> SincronizarPlanosEUsuarioAsync()
        {
            PlanoPermissaoService.GarantirEstruturaPlanosEUsuarios();

            var userAuth = App.SupabaseClient?.Auth.CurrentUser;
            string usuarioUuid = userAuth?.Id ?? string.Empty;

            if (string.IsNullOrWhiteSpace(usuarioUuid))
            {
                PermissoesAtuais = new PermissoesPlanoAtual();
                return PermissoesAtuais;
            }

            try
            {
                if (App.SupabaseClient != null)
                {
                    // 1. Baixa todos os planos cadastrados no Supabase
                    var respPlanos = await App.SupabaseClient
                        .From<PlanoSupabaseDto>()
                        .Get();

                    var planosRemotos = respPlanos?.Models ?? new();

                    if (planosRemotos.Count > 0)
                    {
                        using var db = new AppDbContext();

                        foreach (var dto in planosRemotos)
                        {
                            if (string.IsNullOrWhiteSpace(dto.PlanoUuid))
                                continue;

                            string uuidNorm = dto.PlanoUuid.Trim();
                            string? codigoNorm = string.IsNullOrWhiteSpace(dto.Codigo) ? null : dto.Codigo.Trim();

                            // Localiza por UUID ou pelo código único (substitui o registro semente 'gratis' se o UUID mudou)
                            var existente = db.Planos.FirstOrDefault(p =>
                                p.PlanoUuid.ToLower() == uuidNorm.ToLower() ||
                                (codigoNorm != null && p.Codigo != null && p.Codigo.ToLower() == codigoNorm.ToLower()));

                            if (existente != null && !string.Equals(existente.PlanoUuid, uuidNorm, StringComparison.OrdinalIgnoreCase))
                            {
                                // Desvincula temporariamente usuários locais apontando para o UUID semente antes de trocá-lo
                                var usuariosVinculados = db.Usuarios
                                    .Where(u => u.PlanoUuid != null && u.PlanoUuid.ToLower() == existente.PlanoUuid.ToLower())
                                    .ToList();

                                foreach (var u in usuariosVinculados)
                                    u.PlanoUuid = null;

                                db.SaveChanges();
                                db.Planos.Remove(existente);
                                db.SaveChanges();
                                existente = null;
                            }

                            if (existente == null)
                            {
                                db.Planos.Add(new Plano
                                {
                                    PlanoUuid = uuidNorm,
                                    Nome = dto.Nome ?? "Plano",
                                    Codigo = codigoNorm,
                                    Valor = dto.Valor,
                                    ValorAnualParcela = dto.ValorAnualParcela,
                                    ValorAnualTotal = dto.ValorAnualTotal,
                                    LimiteTransacoes = dto.LimiteTransacoes,
                                    LimiteBancos = dto.LimiteBancos,
                                    LimiteContas = dto.LimiteContas,
                                    LimiteCartoes = dto.LimiteCartoes,
                                    AcessoContasPagar = dto.AcessoContasPagar,
                                    AcessoContasReceber = dto.AcessoContasReceber,
                                    AcessoOrcamento = dto.AcessoOrcamento,
                                    AcessoFluxoCaixa = dto.AcessoFluxoCaixa,
                                    OrdemExibicao = dto.OrdemExibicao,
                                    CriadoEm = dto.CriadoEm,
                                    UpdatedAt = dto.UpdatedAt ?? DateTime.UtcNow
                                });
                            }
                            else
                            {
                                existente.Nome = dto.Nome ?? existente.Nome;
                                existente.Codigo = codigoNorm;
                                existente.Valor = dto.Valor;
                                existente.ValorAnualParcela = dto.ValorAnualParcela;
                                existente.ValorAnualTotal = dto.ValorAnualTotal;
                                existente.LimiteTransacoes = dto.LimiteTransacoes;
                                existente.LimiteBancos = dto.LimiteBancos;
                                existente.LimiteContas = dto.LimiteContas;
                                existente.LimiteCartoes = dto.LimiteCartoes;
                                existente.AcessoContasPagar = dto.AcessoContasPagar;
                                existente.AcessoContasReceber = dto.AcessoContasReceber;
                                existente.AcessoOrcamento = dto.AcessoOrcamento;
                                existente.AcessoFluxoCaixa = dto.AcessoFluxoCaixa;
                                existente.OrdemExibicao = dto.OrdemExibicao;
                                existente.UpdatedAt = dto.UpdatedAt ?? DateTime.UtcNow;
                            }
                        }

                        db.SaveChanges();
                    }

                    // 2. Baixa o perfil e assinatura atualizados do usuário logado
                    var respUsuario = await App.SupabaseClient
                        .From<UsuarioSupabaseDto>()
                        .Where(u => u.UsuarioUuid == usuarioUuid)
                        .Get();

                    var usuarioRemoto = respUsuario?.Models?.FirstOrDefault();

                    if (usuarioRemoto != null)
                    {
                        using var db = new AppDbContext();

                        string? planoUuidValido = null;
                        if (!string.IsNullOrWhiteSpace(usuarioRemoto.PlanoUuid))
                        {
                            string pId = usuarioRemoto.PlanoUuid.Trim();
                            bool planoExiste = db.Planos.AsNoTracking().Any(p => p.PlanoUuid.ToLower() == pId.ToLower());
                            if (planoExiste)
                                planoUuidValido = pId;
                        }

                        var usuarioLocal = db.Usuarios.FirstOrDefault(u => u.UsuarioUuid.ToLower() == usuarioUuid.ToLower());
                        if (usuarioLocal == null)
                        {
                            db.Usuarios.Add(new Usuario
                            {
                                UsuarioUuid = usuarioUuid,
                                PlanoUuid = planoUuidValido,
                                Email = usuarioRemoto.Email ?? userAuth?.Email ?? string.Empty,
                                NomeCompleto = usuarioRemoto.NomeCompleto ?? userAuth?.Email ?? "Usuário",
                                Cpf = usuarioRemoto.Cpf,
                                Celular = usuarioRemoto.Celular,
                                Sexo = usuarioRemoto.Sexo,
                                DataNascimento = usuarioRemoto.DataNascimento,
                                DataVencimento = usuarioRemoto.DataVencimento,
                                CicloCobranca = string.IsNullOrWhiteSpace(usuarioRemoto.CicloCobranca) ? "gratis" : usuarioRemoto.CicloCobranca,
                                MpPaymentId = usuarioRemoto.MpPaymentId,
                                CriadoEm = usuarioRemoto.CriadoEm ?? DateTime.UtcNow
                            });
                        }
                        else
                        {
                            usuarioLocal.PlanoUuid = planoUuidValido;
                            usuarioLocal.Email = usuarioRemoto.Email ?? usuarioLocal.Email;
                            usuarioLocal.NomeCompleto = usuarioRemoto.NomeCompleto ?? usuarioLocal.NomeCompleto;
                            usuarioLocal.Cpf = usuarioRemoto.Cpf;
                            usuarioLocal.Celular = usuarioRemoto.Celular;
                            usuarioLocal.Sexo = usuarioRemoto.Sexo;
                            usuarioLocal.DataNascimento = usuarioRemoto.DataNascimento;
                            usuarioLocal.DataVencimento = usuarioRemoto.DataVencimento;
                            usuarioLocal.CicloCobranca = string.IsNullOrWhiteSpace(usuarioRemoto.CicloCobranca) ? "gratis" : usuarioRemoto.CicloCobranca;
                            usuarioLocal.MpPaymentId = usuarioRemoto.MpPaymentId;
                        }

                        db.SaveChanges();
                    }
                }
            }
            catch (Exception ex)
            {
                // Em caso de falha de rede/offline, segue com o cache local do SQLite
                System.Diagnostics.Debug.WriteLine($"[SYNC PLANOS/USUARIO]: {ex.Message}");
            }

            // 3. Garante que exista um registro local mínimo do usuário autenticado (caso seja o 1º acesso offline)
            GarantirUsuarioMinimoLocal(usuarioUuid, userAuth?.Email);

            // 4. Consulta a view local vw_permissoes_usuario e atualiza o cache de permissões
            PermissoesAtuais = PlanoPermissaoService.ObterPermissoesUsuario(usuarioUuid);
            return PermissoesAtuais;
        }

        private static void GarantirUsuarioMinimoLocal(string usuarioUuid, string? email)
        {
            try
            {
                using var db = new AppDbContext();
                bool existe = db.Usuarios.AsNoTracking().Any(u => u.UsuarioUuid.ToLower() == usuarioUuid.ToLower());
                if (!existe)
                {
                    var planoGratis = db.Planos.AsNoTracking()
                        .FirstOrDefault(p => p.Codigo != null && p.Codigo.ToLower() == "gratis");

                    db.Usuarios.Add(new Usuario
                    {
                        UsuarioUuid = usuarioUuid,
                        PlanoUuid = planoGratis?.PlanoUuid,
                        Email = email ?? $"{usuarioUuid}@local",
                        NomeCompleto = email ?? "Usuário",
                        CicloCobranca = "gratis",
                        CriadoEm = DateTime.UtcNow
                    });
                    db.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[USUARIO MINIMO LOCAL]: {ex.Message}");
            }
        }

        #region DTOs Postgrest (Supabase)

        [Table("planos")]
        private class PlanoSupabaseDto : BaseModel
        {
            [Supabase.Postgrest.Attributes.PrimaryKey("planouuid", false)]
            public string PlanoUuid { get; set; } = string.Empty;

            [Column("nome")]
            public string? Nome { get; set; }

            [Column("codigo")]
            public string? Codigo { get; set; }

            [Column("valor")]
            public decimal Valor { get; set; }

            [Column("valor_anual_parcela")]
            public decimal ValorAnualParcela { get; set; }

            [Column("valor_anual_total")]
            public decimal ValorAnualTotal { get; set; }

            [Column("limite_transacoes")]
            public int? LimiteTransacoes { get; set; }

            [Column("limite_bancos")]
            public int? LimiteBancos { get; set; }

            [Column("limite_contas")]
            public int? LimiteContas { get; set; }

            [Column("limite_cartoes")]
            public int? LimiteCartoes { get; set; }

            [Column("acesso_contas_pagar")]
            public bool AcessoContasPagar { get; set; }

            [Column("acesso_contas_receber")]
            public bool AcessoContasReceber { get; set; }

            [Column("acesso_orcamento")]
            public bool AcessoOrcamento { get; set; }

            [Column("acesso_fluxo_caixa")]
            public bool AcessoFluxoCaixa { get; set; }

            [Column("ordem_exibicao")]
            public short OrdemExibicao { get; set; }

            [Column("criado_em")]
            public DateTime? CriadoEm { get; set; }

            [Column("updated_at")]
            public DateTime? UpdatedAt { get; set; }
        }

        [Table("usuarios")]
        private class UsuarioSupabaseDto : BaseModel
        {
            [Supabase.Postgrest.Attributes.PrimaryKey("usuariouuid", false)]
            public string UsuarioUuid { get; set; } = string.Empty;

            [Column("planouuid")]
            public string? PlanoUuid { get; set; }

            [Column("email")]
            public string? Email { get; set; }

            [Column("nomecompleto")]
            public string? NomeCompleto { get; set; }

            [Column("cpf")]
            public string? Cpf { get; set; }

            [Column("celular")]
            public string? Celular { get; set; }

            [Column("sexo")]
            public string? Sexo { get; set; }

            [Column("datanascimento")]
            public DateTime? DataNascimento { get; set; }

            [Column("datavencimento")]
            public DateTime? DataVencimento { get; set; }

            [Column("ciclo_cobranca")]
            public string? CicloCobranca { get; set; }

            [Column("mp_payment_id")]
            public string? MpPaymentId { get; set; }

            [Column("criado_em")]
            public DateTime? CriadoEm { get; set; }
        }

        #endregion
    }
}