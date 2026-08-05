using System;
using System.Collections.Generic;
using System.Linq;
using CashHub.Validation.Models;

namespace CashHub.Validation.Engine
{
    public sealed class RuleExecution
    {
        public RuleExecution(RuleDefinition definition, RuleOutcome outcome,
            IReadOnlyList<Finding> findings, long durationMs, string? engineNote = null)
        {
            Definition = definition;
            Outcome = outcome;
            Findings = findings;
            DurationMs = durationMs;
            EngineNote = engineNote;
        }

        public RuleDefinition Definition { get; }
        public RuleOutcome Outcome { get; }
        public IReadOnlyList<Finding> Findings { get; }
        public long DurationMs { get; }

        /// <summary>Nota técnica del motor (regla sin estrategia, excepción aislada).</summary>
        public string? EngineNote { get; }
    }

    public sealed class RunResult
    {
        public RunResult(IReadOnlyList<RuleExecution> executions)
        {
            Executions = executions;
        }

        public IReadOnlyList<RuleExecution> Executions { get; }

        public IEnumerable<Finding> FailFindings =>
            Executions.Where(e => e.Outcome == RuleOutcome.Fail).SelectMany(e => e.Findings);

        public bool HasSystemIssues =>
            Executions.Any(e => e.Outcome == RuleOutcome.Error || e.Outcome == RuleOutcome.NotImplemented);

        /// <summary>Pasa solo si ninguna regla ejecutable falló y el motor no tuvo incidentes.</summary>
        public bool Passed => !FailFindings.Any() && !HasSystemIssues;
    }

    /// <summary>
    /// Corre las reglas activas en orden, respetando la cadena de dependencias y
    /// aislando los errores de cada estrategia. NUNCA se detiene en el primer fallo:
    /// el cliente recibe la lista completa de errores.
    /// </summary>
    public sealed class ValidationEngine
    {
        private readonly IReadOnlyDictionary<string, IValidationRule> _strategies;
        private readonly Func<long> _stopwatchMs;

        public ValidationEngine(IEnumerable<IValidationRule> strategies, Func<long>? stopwatchMs = null)
        {
            _strategies = strategies.ToDictionary(s => s.Key, s => s);
            _stopwatchMs = stopwatchMs ?? (() => Environment.TickCount);
        }

        public RunResult Run(IEnumerable<RuleDefinition> rules, ValidationContext context)
        {
            var executions = new List<RuleExecution>();
            var outcomes = new Dictionary<string, RuleOutcome>();

            foreach (var rule in rules.Where(r => r.IsActive).OrderBy(r => r.Order))
            {
                var execution = Execute(rule, context, outcomes);
                executions.Add(execution);
                outcomes[rule.Key] = execution.Outcome;
            }

            return new RunResult(executions);
        }

        private RuleExecution Execute(RuleDefinition rule, ValidationContext context,
            IReadOnlyDictionary<string, RuleOutcome> outcomes)
        {
            if (rule.DependsOnKey != null)
            {
                // La dependencia debe haber PASADO. Si falló, se omitió o ni corrió,
                // esta regla no puede opinar con datos confiables: se omite.
                if (!outcomes.TryGetValue(rule.DependsOnKey, out var dep) || dep != RuleOutcome.Pass)
                {
                    return new RuleExecution(rule, RuleOutcome.Skipped, EmptyFindings, 0,
                        $"Omitida: su dependencia {rule.DependsOnKey} no paso.");
                }
            }

            if (!_strategies.TryGetValue(rule.Key, out var strategy))
            {
                return new RuleExecution(rule, RuleOutcome.NotImplemented, EmptyFindings, 0,
                    $"La regla {rule.Key} esta activa pero el motor no tiene estrategia registrada.");
            }

            var start = _stopwatchMs();
            try
            {
                var findings = strategy.Evaluate(rule, context);
                foreach (var finding in findings)
                {
                    finding.RenderedMessage = MessageRenderer.Render(rule.FailMessage, finding);
                }

                var outcome = findings.Count == 0 ? RuleOutcome.Pass : RuleOutcome.Fail;
                return new RuleExecution(rule, outcome, findings, _stopwatchMs() - start);
            }
            catch (Exception ex)
            {
                // Una estrategia rota no tumba la corrida: se aísla y se reporta.
                return new RuleExecution(rule, RuleOutcome.Error, EmptyFindings,
                    _stopwatchMs() - start, $"Excepcion en {rule.Key}: {ex.Message}");
            }
        }

        private static readonly IReadOnlyList<Finding> EmptyFindings = new List<Finding>();
    }
}
