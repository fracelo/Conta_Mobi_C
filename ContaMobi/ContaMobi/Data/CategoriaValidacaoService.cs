using ContaMobi.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace ContaMobi.Data
{
    public class ResultadoVinculoCategoria
    {
        public int QtdLancamentos { get; set; }
        public int QtdContasPR { get; set; }
        public bool PossuiMovimentacao => QtdLancamentos > 0 || QtdContasPR > 0;

        public string MensagemBloqueioExclusao =>
            $"Esta categoria possui vínculos registrados ({QtdLancamentos} lançamento(s) em conta corrente e {QtdContasPR} título(s) em Contas a Pagar/Receber).\n\n" +
            "Para excluir esta categoria, primeiro altere os lançamentos e/ou Contas a Pagar/Receber vinculados para outra categoria.";

        public string MensagemBloqueioEdicao =>
            $"Categoria em uso ({QtdLancamentos} lançamento(s) e {QtdContasPR} Conta(s) P&R): " +
            "somente a DESCRIÇÃO pode ser alterada. Tipo (D/C), Nível e Exclusão estão bloqueados até que os registros sejam trocados para outra categoria.";
    }

    public static class CategoriaValidacaoService
    {
        public static ResultadoVinculoCategoria VerificarVinculos(string? categoriaUuid)
        {
            if (string.IsNullOrWhiteSpace(categoriaUuid))
                return new ResultadoVinculoCategoria();

            string id = categoriaUuid.Trim();

            using var db = new AppDbContext();

            int qtdLancamentos = db.Set<LancamentoConta>()
                .AsNoTracking()
                .Count(l => l.CategoriaUuid == id);

            int qtdContasPR = db.Set<ContaPR>()
                .AsNoTracking()
                .Count(c => c.CategoriaUuid == id);

            return new ResultadoVinculoCategoria
            {
                QtdLancamentos = qtdLancamentos,
                QtdContasPR = qtdContasPR
            };
        }
    }
}