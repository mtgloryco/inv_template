using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Infrastructure;

namespace InventoryManagementSystem.Services
{
    public class AccountingReportService
    {
        private readonly DatabaseService _databaseService;

        public AccountingReportService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        #region CRUD Operations

        public async Task<List<AccountingReport>> GetAllReportsAsync()
        {
            return await _databaseService.Connection.Table<AccountingReport>()
                .ToListAsync();
        }

        public async Task<List<ReportLine>> GetReportLinesAsync(int reportId)
        {
            return await _databaseService.Connection.Table<ReportLine>()
                .Where(rl => rl.ReportId == reportId)
                .ToListAsync();
        }

        public async Task<List<ReportLineComputation>> GetReportLineComputationsAsync(int reportLineId)
        {
            return await _databaseService.Connection.Table<ReportLineComputation>()
                .Where(rlc => rlc.ReportLineId == reportLineId)
                .ToListAsync();
        }

        public async Task<List<ReportLineComputation>> GetAllReportLineComputationsAsync()
        {
            return await _databaseService.Connection.Table<ReportLineComputation>()
                .ToListAsync();
        }

        public async Task AddReportLineAsync(ReportLine line, List<ReportLineComputation> computations)
        {
            await _databaseService.Connection.RunInTransactionAsync(conn =>
            {
                conn.Insert(line);
                foreach (var comp in computations)
                {
                    comp.ReportLineId = line.Id;
                    conn.Insert(comp);
                }
            });
        }

        public async Task UpdateReportLineAsync(ReportLine line, List<ReportLineComputation> computations)
        {
            await _databaseService.Connection.RunInTransactionAsync(conn =>
            {
                conn.Update(line);
                // Clear existing computations for this line and re-insert
                var existingComps = conn.Table<ReportLineComputation>()
                    .Where(c => c.ReportLineId == line.Id)
                    .ToList();
                foreach (var c in existingComps)
                {
                    conn.Delete(c);
                }
                foreach (var comp in computations)
                {
                    comp.ReportLineId = line.Id;
                    comp.Id = 0; // reset ID for auto-increment insertion
                    conn.Insert(comp);
                }
            });
        }

        public async Task DeleteReportLineAsync(int reportLineId)
        {
            await _databaseService.Connection.RunInTransactionAsync(conn =>
            {
                var line = conn.Find<ReportLine>(reportLineId);
                if (line != null)
                {
                    var existingComps = conn.Table<ReportLineComputation>()
                        .Where(c => c.ReportLineId == reportLineId)
                        .ToList();
                    foreach (var c in existingComps)
                    {
                        conn.Delete(c);
                    }
                    conn.Delete(line);
                }
            });
        }

        #endregion

        #region Formula Parsing

        public List<int> GetAccountIdsMatchingFormula(List<Account> accounts, string formula)
        {
            return GetAccountsMatchingFormula(accounts, formula).Select(a => a.Id).ToList();
        }

        public List<Account> GetAccountsMatchingFormula(List<Account> allAccounts, string formula)
        {
            var matching = new List<Account>();
            if (string.IsNullOrWhiteSpace(formula)) return matching;

            var parts = formula.Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                int index = trimmed.IndexOf("\\(");
                string prefix = trimmed;
                var exclusions = new List<string>();

                if (index != -1 && trimmed.EndsWith(")"))
                {
                    prefix = trimmed.Substring(0, index).Trim();
                    string exclStr = trimmed.Substring(index + 2, trimmed.Length - index - 3).Trim();
                    exclusions = exclStr.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                        .Select(e => e.Trim())
                                        .ToList();
                }

                foreach (var acc in allAccounts)
                {
                    if (acc.Code.StartsWith(prefix))
                    {
                        bool isExcluded = false;
                        foreach (var excl in exclusions)
                        {
                            if (acc.Code.StartsWith(excl))
                            {
                                isExcluded = true;
                                break;
                            }
                        }
                        if (!isExcluded && !matching.Any(m => m.Id == acc.Id))
                        {
                            matching.Add(acc);
                        }
                    }
                }
            }
            return matching;
        }

        #endregion

        #region Calculation Engine

        public async Task<List<ReportLineResult>> ComputeReportBalancesAsync(int reportId)
        {
            var allLines = await GetReportLinesAsync(reportId);
            var allComputations = await _databaseService.Connection.Table<ReportLineComputation>().ToListAsync();
            var allAccounts = await _databaseService.Connection.Table<Account>().ToListAsync();
            var allJournalLines = await _databaseService.Connection.Table<JournalLine>().ToListAsync();

            var accountBalances = allJournalLines.GroupBy(l => l.AccountId)
                .ToDictionary(
                    g => g.Key,
                    g => (debit: g.Sum(x => x.Debit), credit: g.Sum(x => x.Credit))
                );

            var memo = new Dictionary<int, decimal>();
            var visiting = new HashSet<int>();
            var results = new List<ReportLineResult>();

            foreach (var line in allLines)
            {
                try
                {
                    decimal balance = ComputeLineBalance(line, allLines, allComputations, memo, visiting, allAccounts, accountBalances);
                    
                    results.Add(new ReportLineResult
                    {
                        LineId = line.Id,
                        Name = line.Name,
                        Code = line.Code,
                        Level = line.Level,
                        Foldability = line.Foldability,
                        GroupBy = line.GroupBy,
                        PrintOnNewPage = line.PrintOnNewPage,
                        HideIfZero = line.HideIfZero,
                        Balance = balance
                    });
                }
                catch (Exception)
                {
                    results.Add(new ReportLineResult
                    {
                        LineId = line.Id,
                        Name = line.Name,
                        Code = line.Code,
                        Level = line.Level,
                        Foldability = line.Foldability,
                        GroupBy = line.GroupBy,
                        PrintOnNewPage = line.PrintOnNewPage,
                        HideIfZero = line.HideIfZero,
                        Balance = 0
                    });
                }
            }

            return results;
        }

        private decimal ComputeLineBalance(
            ReportLine line,
            List<ReportLine> allLines,
            List<ReportLineComputation> allComputations,
            Dictionary<int, decimal> memo,
            HashSet<int> visiting,
            List<Account> allAccounts,
            Dictionary<int, (decimal debit, decimal credit)> accountBalances)
        {
            if (memo.TryGetValue(line.Id, out decimal cached))
                return cached;

            if (visiting.Contains(line.Id))
                throw new InvalidOperationException($"Circular dependency detected in report line calculations for line '{line.Name}' ({line.Code}).");

            visiting.Add(line.Id);

            decimal total = 0;
            var comps = allComputations.Where(c => c.ReportLineId == line.Id).ToList();

            foreach (var comp in comps)
            {
                if (comp.ComputationEngine == "Prefix of Account Codes")
                {
                    var matchingAccounts = GetAccountsMatchingFormula(allAccounts, comp.Formula);
                    foreach (var acc in matchingAccounts)
                    {
                        if (accountBalances.TryGetValue(acc.Id, out var bal))
                        {
                            total += CalculateAccountBalance(acc.Type, bal.debit, bal.credit);
                        }
                    }
                }
                else if (comp.ComputationEngine == "Sum of other lines")
                {
                    total += EvaluateSumFormula(comp.Formula, allLines, allComputations, memo, visiting, allAccounts, accountBalances);
                }
            }

            visiting.Remove(line.Id);
            memo[line.Id] = total;
            return total;
        }

        private decimal CalculateAccountBalance(string accountType, decimal debit, decimal credit)
        {
            if (string.IsNullOrEmpty(accountType))
                return debit - credit;

            if (accountType.StartsWith("Asset:", StringComparison.OrdinalIgnoreCase) || 
                accountType.StartsWith("Expense:", StringComparison.OrdinalIgnoreCase))
            {
                return debit - credit;
            }

            if (accountType.StartsWith("Liability:", StringComparison.OrdinalIgnoreCase) || 
                accountType.StartsWith("Equity:", StringComparison.OrdinalIgnoreCase) || 
                accountType.StartsWith("Income:", StringComparison.OrdinalIgnoreCase))
            {
                return credit - debit;
            }

            return debit - credit;
        }

        private decimal EvaluateSumFormula(
            string formula,
            List<ReportLine> allLines,
            List<ReportLineComputation> allComputations,
            Dictionary<int, decimal> memo,
            HashSet<int> visiting,
            List<Account> allAccounts,
            Dictionary<int, (decimal debit, decimal credit)> accountBalances)
        {
            if (string.IsNullOrWhiteSpace(formula)) return 0;

            var tokens = Tokenize(formula);
            if (tokens.Count == 0) return 0;

            for (int i = 0; i < tokens.Count; i++)
            {
                var t = tokens[i];
                if (t.Type == TokenType.Identifier)
                {
                    var refLine = allLines.FirstOrDefault(l => string.Equals(l.Code, t.Value, StringComparison.OrdinalIgnoreCase));
                    if (refLine != null)
                    {
                        decimal val = ComputeLineBalance(refLine, allLines, allComputations, memo, visiting, allAccounts, accountBalances);
                        t.Value = val.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        t.Type = TokenType.Number;
                    }
                    else
                    {
                        t.Value = "0";
                        t.Type = TokenType.Number;
                    }
                }
            }

            return EvaluateExpression(tokens);
        }

        #region Tokenizer and Parser

        enum TokenType { Number, Identifier, Operator, OpenParenthesis, CloseParenthesis }

        class Token
        {
            public TokenType Type { get; set; }
            public string Value { get; set; } = string.Empty;
        }

        private static List<Token> Tokenize(string formula)
        {
            var tokens = new List<Token>();
            int i = 0;
            while (i < formula.Length)
            {
                char c = formula[i];
                if (char.IsWhiteSpace(c))
                {
                    i++;
                    continue;
                }

                if (c == '+' || c == '-' || c == '*' || c == '/')
                {
                    tokens.Add(new Token { Type = TokenType.Operator, Value = c.ToString() });
                    i++;
                }
                else if (c == '(')
                {
                    tokens.Add(new Token { Type = TokenType.OpenParenthesis, Value = "(" });
                    i++;
                }
                else if (c == ')')
                {
                    tokens.Add(new Token { Type = TokenType.CloseParenthesis, Value = ")" });
                    i++;
                }
                else if (char.IsDigit(c) || c == '.')
                {
                    var sb = new StringBuilder();
                    while (i < formula.Length && (char.IsDigit(formula[i]) || formula[i] == '.'))
                    {
                        sb.Append(formula[i]);
                        i++;
                    }
                    tokens.Add(new Token { Type = TokenType.Number, Value = sb.ToString() });
                }
                else if (char.IsLetter(c) || c == '_')
                {
                    var sb = new StringBuilder();
                    while (i < formula.Length && (char.IsLetterOrDigit(formula[i]) || formula[i] == '_'))
                    {
                        sb.Append(formula[i]);
                        i++;
                    }
                    tokens.Add(new Token { Type = TokenType.Identifier, Value = sb.ToString() });
                }
                else
                {
                    i++;
                }
            }
            return tokens;
        }

        private static decimal EvaluateExpression(List<Token> tokens)
        {
            var values = new Stack<decimal>();
            var operators = new Stack<Token>();

            for (int i = 0; i < tokens.Count; i++)
            {
                var t = tokens[i];
                if (t.Type == TokenType.Number)
                {
                    if (decimal.TryParse(t.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal val))
                    {
                        values.Push(val);
                    }
                    else
                    {
                        values.Push(0);
                    }
                }
                else if (t.Type == TokenType.OpenParenthesis)
                {
                    operators.Push(t);
                }
                else if (t.Type == TokenType.CloseParenthesis)
                {
                    while (operators.Count > 0 && operators.Peek().Type != TokenType.OpenParenthesis)
                    {
                        ApplyOperator(operators.Pop(), values);
                    }
                    if (operators.Count > 0) operators.Pop();
                }
                else if (t.Type == TokenType.Operator)
                {
                    if (t.Value == "-" && (i == 0 || tokens[i - 1].Type == TokenType.Operator || tokens[i - 1].Type == TokenType.OpenParenthesis))
                    {
                        values.Push(0);
                    }

                    while (operators.Count > 0 && GetPrecedence(operators.Peek().Value) >= GetPrecedence(t.Value))
                    {
                        ApplyOperator(operators.Pop(), values);
                    }
                    operators.Push(t);
                }
            }

            while (operators.Count > 0)
            {
                ApplyOperator(operators.Pop(), values);
            }

            return values.Count > 0 ? values.Pop() : 0;
        }

        private static int GetPrecedence(string op)
        {
            if (op == "+" || op == "-") return 1;
            if (op == "*" || op == "/") return 2;
            return 0;
        }

        private static void ApplyOperator(Token op, Stack<decimal> values)
        {
            if (values.Count < 2) return;
            decimal val2 = values.Pop();
            decimal val1 = values.Pop();
            switch (op.Value)
            {
                case "+": values.Push(val1 + val2); break;
                case "-": values.Push(val1 - val2); break;
                case "*": values.Push(val1 * val2); break;
                case "/": values.Push(val2 == 0 ? 0 : val1 / val2); break;
            }
        }

        #endregion
        #endregion
    }

    public class ReportLineResult
    {
        public int LineId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public int Level { get; set; }
        public string Foldability { get; set; } = string.Empty;
        public string GroupBy { get; set; } = string.Empty;
        public bool PrintOnNewPage { get; set; }
        public bool HideIfZero { get; set; }
        public decimal Balance { get; set; }

        public double Indentation => (Level - 1) * 20;
    }
}
