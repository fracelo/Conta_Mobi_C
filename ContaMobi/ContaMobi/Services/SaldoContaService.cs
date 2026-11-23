using System;
using System.Linq;
using ContaMobi.Data;
using ContaMobi.Models;

namespace ContaMobi.Services
{
    public static class SaldoContaService
    {
        /// <summary>
        /// Inicializa ou recalcula o saldo da conta na data informada e replica o efeito em cascata.
        /// </summary>
        public static void AtualizarSaldoInicialConta(AppDbContext db, string contaUuid, DateTime dataCriacao, decimal novoSaldoInicial)
        {
            DateTime dataBase = dataCriacao.Date;

            // 1. Procura o registro do saldo consolidado na data de abertura
            var saldoDoDia = db.SaldoContasCorrentes
                .FirstOrDefault(s => s.ContaUuid == contaUuid && s.Data == dataBase);

            if (saldoDoDia == null)
            {
                saldoDoDia = new SaldoContaCorrente
                {
                    SaldoId = Guid.NewGuid().ToString(),
                    ContaUuid = contaUuid,
                    Data = dataBase,
                    SaldoInicial = novoSaldoInicial,
                    TotalCredito = 0.00m,
                    TotalDebito = 0.00m,
                    SaldoFinal = novoSaldoInicial,
                    UpdatedAt = DateTime.UtcNow
                };
                db.SaldoContasCorrentes.Add(saldoDoDia);
            }
            else
            {
                // Se já existia, atualiza o saldo inicial e recalcula o saldo final do dia
                saldoDoDia.SaldoInicial = novoSaldoInicial;
                saldoDoDia.SaldoFinal = saldoDoDia.SaldoInicial + saldoDoDia.TotalCredito - saldoDoDia.TotalDebito;
                saldoDoDia.UpdatedAt = DateTime.UtcNow;
            }

            // 2. Cascata para datas posteriores (se existirem registros futuros da conta)
            RecalcularSaldosPosteriores(db, contaUuid, dataBase, saldoDoDia.SaldoFinal);
        }

        private static void RecalcularSaldosPosteriores(AppDbContext db, string contaUuid, DateTime dataBase, decimal saldoAcumuladoAnterior)
        {
            var registrosFuturos = db.SaldoContasCorrentes
                .Where(s => s.ContaUuid == contaUuid && s.Data > dataBase)
                .OrderBy(s => s.Data)
                .ToList();

            decimal corrente = saldoAcumuladoAnterior;

            foreach (var reg in registrosFuturos)
            {
                reg.SaldoInicial = corrente;
                reg.SaldoFinal = reg.SaldoInicial + reg.TotalCredito - reg.TotalDebito;
                reg.UpdatedAt = DateTime.UtcNow;

                corrente = reg.SaldoFinal;
            }
        }
    }
}