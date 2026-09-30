using System;
using System.Collections.Generic;
using System.Linq;

namespace first
{
    public sealed class AnalysisResult
    {
        public int IfCount { get; internal set; }
        public int LoopCount { get; internal set; }
        public int SwitchCount { get; internal set; }
        public int SwitchCaseCount { get; internal set; }
        public int TernaryCount { get; internal set; }
        public int TotalStatements { get; internal set; }
        public int AbsoluteComplexity { get; internal set; }
        public int MaxNesting { get; internal set; }
        public IList<string> Warnings { get; internal set; }

        public double RelativeComplexity
        {
            get
            {
                return TotalStatements == 0 ? 0.0 : (double)AbsoluteComplexity / TotalStatements;
            }
        }

        internal AnalysisResult()
        {
            Warnings = new List<string>();
        }
    }

    public static class GroovyAnalyzer
    {
        public static AnalysisResult Analyze(string source)
        {
            AnalysisResult result = new AnalysisResult();
            GroovyLexer lexer = new GroovyLexer(source ?? String.Empty);
            List<GroovyToken> tokens = lexer.Tokenize();
            result.Warnings = lexer.Warnings;

            GroovyParser parser = new GroovyParser(tokens, result);
            parser.Parse();
            parser.CheckDelimiters();

            result.AbsoluteComplexity = result.IfCount + result.LoopCount +
                result.SwitchCaseCount + result.TernaryCount;
            return result;
        }
    }

    internal enum GroovyTokenKind
    {
        Identifier,
        Number,
        String,
        Symbol,
        NewLine
    }

    internal sealed class GroovyToken
    {
        public string Text { get; private set; }
        public GroovyTokenKind Kind { get; private set; }
        public int Line { get; private set; }
        public int Column { get; private set; }

        public GroovyToken(string text, GroovyTokenKind kind, int line, int column)
        {
            Text = text;
            Kind = kind;
            Line = line;
            Column = column;
        }
    }

    internal sealed class GroovyLexer
    {
        private readonly string _source;
        private readonly List<GroovyToken> _tokens = new List<GroovyToken>();
        private readonly List<string> _warnings = new List<string>();
        private int _index;
        private int _line = 1;
        private int _column = 1;

        public List<string> Warnings
        {
            get { return _warnings; }
        }

        public GroovyLexer(string source)
        {
            _source = source;
        }

        public List<GroovyToken> Tokenize()
        {
            while (_index < _source.Length)
            {
                char c = Current;

                if (c == ' ' || c == '\t' || c == '\f')
                {
                    Advance();
                    continue;
                }

                if (c == '\r' || c == '\n')
                {
                    int line = _line;
                    int column = _column;
                    ReadNewLine();
                    _tokens.Add(new GroovyToken("\\n", GroovyTokenKind.NewLine, line, column));
                    continue;
                }

                if (c == '\\' && IsNewLine(Peek(1)))
                {
                    Advance();
                    ReadNewLine();
                    continue;
                }

                if (c == '#' && _line == 1 && _column == 1)
                {
                    SkipLineComment();
                    continue;
                }

                if (c == '/' && Peek(1) == '/')
                {
                    SkipLineComment();
                    continue;
                }

                if (c == '/' && Peek(1) == '*')
                {
                    ReadBlockComment();
                    continue;
                }

                if (c == '$' && Peek(1) == '/')
                {
                    ReadDollarSlashyString();
                    continue;
                }

                if (c == '\'' || c == '"')
                {
                    ReadQuotedString(c);
                    continue;
                }

                if (c == '/' && CanStartSlashyString())
                {
                    ReadSlashyString();
                    continue;
                }

                if (Char.IsLetter(c) || c == '_' || c == '$')
                {
                    ReadIdentifier();
                    continue;
                }

                if (Char.IsDigit(c))
                {
                    ReadNumber();
                    continue;
                }

                ReadSymbol();
            }

            return _tokens;
        }

        private char Current
        {
            get { return _source[_index]; }
        }

        private char Peek(int offset)
        {
            int at = _index + offset;
            return at >= 0 && at < _source.Length ? _source[at] : '\0';
        }

        private static bool IsNewLine(char c)
        {
            return c == '\r' || c == '\n';
        }

        private void Advance()
        {
            if (_index >= _source.Length)
                return;

            if (_source[_index] == '\r')
            {
                _index++;
                if (_index < _source.Length && _source[_index] == '\n')
                    _index++;
                _line++;
                _column = 1;
                return;
            }

            if (_source[_index] == '\n')
            {
                _index++;
                _line++;
                _column = 1;
                return;
            }

            _index++;
            _column++;
        }

        private void ReadNewLine()
        {
            Advance();
        }

        private void SkipLineComment()
        {
            while (_index < _source.Length && !IsNewLine(Current))
                Advance();
        }

        private void ReadBlockComment()
        {
            int startLine = _line;
            int startColumn = _column;
            Advance();
            Advance();
            bool closed = false;

            while (_index < _source.Length)
            {
                if (Current == '*' && Peek(1) == '/')
                {
                    Advance();
                    Advance();
                    closed = true;
                    break;
                }
                if (Current == '\r' || Current == '\n')
                {
                    int line = _line;
                    int column = _column;
                    ReadNewLine();
                    _tokens.Add(new GroovyToken("\\n", GroovyTokenKind.NewLine, line, column));
                    continue;
                }
                Advance();
            }

            if (!closed)
                _warnings.Add("Незакрытый комментарий /* */ (строка " + startLine + ", столбец " + startColumn + ").");
        }

        private void ReadQuotedString(char quote)
        {
            int startLine = _line;
            int startColumn = _column;
            bool triple = Peek(1) == quote && Peek(2) == quote;
            Advance();
            if (triple)
            {
                Advance();
                Advance();
            }

            bool closed = false;
            while (_index < _source.Length)
            {
                if (Current == '\\')
                {
                    Advance();
                    if (_index < _source.Length)
                        Advance();
                    continue;
                }

                if (Current == quote)
                {
                    if (!triple)
                    {
                        Advance();
                        closed = true;
                        break;
                    }

                    if (Peek(1) == quote && Peek(2) == quote)
                    {
                        Advance();
                        Advance();
                        Advance();
                        closed = true;
                        break;
                    }
                }

                Advance();
            }

            if (!closed)
                _warnings.Add("Незакрытая строка (строка " + startLine + ", столбец " + startColumn + ").");

            _tokens.Add(new GroovyToken("<строка>", GroovyTokenKind.String, startLine, startColumn));
        }

        private void ReadSlashyString()
        {
            int startLine = _line;
            int startColumn = _column;
            Advance();
            bool closed = false;

            while (_index < _source.Length)
            {
                if (Current == '\\')
                {
                    Advance();
                    if (_index < _source.Length)
                        Advance();
                    continue;
                }
                if (Current == '/')
                {
                    Advance();
                    closed = true;
                    break;
                }
                Advance();
            }

            if (!closed)
                _warnings.Add("Незакрытая slashy-строка /.../ (строка " + startLine + ", столбец " + startColumn + ").");
            _tokens.Add(new GroovyToken("<строка>", GroovyTokenKind.String, startLine, startColumn));
        }

        private void ReadDollarSlashyString()
        {
            int startLine = _line;
            int startColumn = _column;
            Advance(); 
            Advance(); 
            bool closed = false;

            while (_index < _source.Length)
            {
                if (Current == '/' && Peek(1) == '$')
                {
                    Advance();
                    Advance();
                    closed = true;
                    break;
                }
                Advance();
            }

            if (!closed)
                _warnings.Add("Незакрытая dollar-slashy строка $/.../$ (строка " + startLine + ", столбец " + startColumn + ").");
            _tokens.Add(new GroovyToken("<строка>", GroovyTokenKind.String, startLine, startColumn));
        }

        private bool CanStartSlashyString()
        {
            if (_tokens.Count == 0)
                return true;

            GroovyToken previous = _tokens[_tokens.Count - 1];
            if (previous.Kind == GroovyTokenKind.NewLine)
                return true;

            if (previous.Kind == GroovyTokenKind.Identifier)
            {
                string word = previous.Text;
                return word == "return" || word == "throw" || word == "case" ||
                    word == "assert" || word == "in" || word == "as";
            }

            if (previous.Kind == GroovyTokenKind.String || previous.Kind == GroovyTokenKind.Number)
                return false;

            string symbol = previous.Text;
            return symbol == "=" || symbol == "(" || symbol == "[" || symbol == "{" ||
                symbol == "," || symbol == ":" || symbol == "->" || symbol == "!" ||
                symbol == "~" || symbol == "==" || symbol == "!=" || symbol == "&&" ||
                symbol == "||" || symbol == "?" || symbol == "?:" || symbol == "=~" ||
                symbol == "==~";
        }

        private void ReadIdentifier()
        {
            int start = _index;
            int line = _line;
            int column = _column;
            while (_index < _source.Length &&
                (Char.IsLetterOrDigit(Current) || Current == '_' || Current == '$'))
            {
                Advance();
            }
            _tokens.Add(new GroovyToken(_source.Substring(start, _index - start), GroovyTokenKind.Identifier, line, column));
        }

        private void ReadNumber()
        {
            int start = _index;
            int line = _line;
            int column = _column;
            while (_index < _source.Length &&
                (Char.IsLetterOrDigit(Current) || Current == '_' || Current == '.'))
            {
                if (Current == '.' && (Peek(1) == '.' || Peek(1) == '<'))
                    break;
                Advance();
            }
            _tokens.Add(new GroovyToken(_source.Substring(start, _index - start), GroovyTokenKind.Number, line, column));
        }

        private void ReadSymbol()
        {
            int line = _line;
            int column = _column;
            string[] operators = new string[]
            {
                "==~", "..<", "...", "?.", "?:", "*." , ".@", "::", "->",
                "==", "!=", "<=", ">=", "&&", "||", "++", "--", "+=", "-=",
                "*=", "/=", "%=", "**", "=~", ">>", "<<", "&=", "|=", "^="
            };

            for (int i = 0; i < operators.Length; i++)
            {
                string op = operators[i];
                if (_index + op.Length <= _source.Length &&
                    String.CompareOrdinal(_source, _index, op, 0, op.Length) == 0)
                {
                    for (int j = 0; j < op.Length; j++)
                        Advance();
                    _tokens.Add(new GroovyToken(op, GroovyTokenKind.Symbol, line, column));
                    return;
                }
            }

            string symbol = Current.ToString();
            Advance();
            _tokens.Add(new GroovyToken(symbol, GroovyTokenKind.Symbol, line, column));
        }
    }

    internal sealed class GroovyParser
    {
        private readonly List<GroovyToken> _tokens;
        private readonly AnalysisResult _result;
        private int _position;
        private int _limit;

        private sealed class SwitchArm
        {
            public int LabelStart;
            public int BodyStart;
            public int BodyEnd;
            public bool IsDefault;
            public int CaseOrdinal;
        }

        private sealed class TernaryFrame
        {
            public int ParenDepth;
            public int BracketDepth;
            public int BraceDepth;
            public bool HasColon;
        }

        public GroovyParser(List<GroovyToken> tokens, AnalysisResult result)
        {
            _tokens = tokens;
            _result = result;
            _limit = tokens.Count;
        }

        public void Parse()
        {
            _position = 0;
            _limit = _tokens.Count;
            ParseSequence(0);
        }

        public void CheckDelimiters()
        {
            Stack<GroovyToken> stack = new Stack<GroovyToken>();
            for (int i = 0; i < _tokens.Count; i++)
            {
                GroovyToken token = _tokens[i];
                if (token.Text == "(" || token.Text == "[" || token.Text == "{")
                {
                    stack.Push(token);
                }
                else if (token.Text == ")" || token.Text == "]" || token.Text == "}")
                {
                    if (stack.Count == 0 || !IsMatchingPair(stack.Peek().Text, token.Text))
                    {
                        _result.Warnings.Add("Лишняя закрывающая скобка '" + token.Text + "' (строка " + token.Line + ").");
                    }
                    else
                    {
                        stack.Pop();
                    }
                }
            }

            while (stack.Count > 0)
            {
                GroovyToken token = stack.Pop();
                _result.Warnings.Add("Не закрыта скобка '" + token.Text + "' (строка " + token.Line + ").");
            }
        }

        private void ParseSequence(int controlDepth)
        {
            while (_position < _limit)
            {
                SkipSeparators();
                if (_position >= _limit)
                    return;

                if (Is("}"))
                {
                    _result.Warnings.Add("Лишняя закрывающая скобка '}' (строка " + Current.Line + ").");
                    _position++;
                    continue;
                }

                int before = _position;
                if (Is("if"))
                {
                    ParseIf(controlDepth);
                }
                else if (Is("for") || Is("while"))
                {
                    ParseLoop(controlDepth);
                }
                else if (Is("do"))
                {
                    ParseDoWhile(controlDepth);
                }
                else if (Is("switch"))
                {
                    ParseSwitch(controlDepth);
                }
                else if (Is("try"))
                {
                    ParseTry(controlDepth);
                }
                else if (Is("synchronized"))
                {
                    ParseSynchronized(controlDepth);
                }
                else if (Is("class") || Is("interface") || Is("trait") || Is("enum") || Is("record") || IsModifiedTypeDeclaration())
                {
                    ParseTypeDeclaration();
                }
                else if (Is("import") || Is("package"))
                {
                    SkipToStatementEnd();
                }
                else if (Is("@"))
                {
                    SkipAnnotation();
                }
                else if (IsLabelledControlStatement())
                {
                    _position += 2;
                    ParseSequenceStatement(controlDepth);
                }
                else if (Is(";"))
                {
                    _position++;
                }
                else if (Is("case") || Is("default"))
                {
                    _result.Warnings.Add("Метка case/default встретилась вне тела switch (строка " + Current.Line + ").");
                    SkipToStatementEnd();
                }
                else if (TryParseMethodDeclaration())
                {
                    
                }
                else if (Is("{"))
                {
                    ParseBlock(controlDepth);
                }
                else
                {
                    ParseSimpleStatement(controlDepth);
                }

                if (_position <= before)
                    _position = before + 1;
            }
        }

        private void ParseIf(int controlDepth)
        {
            _position++;
            _result.IfCount++;
            _result.TotalStatements++;
            ObserveNesting(controlDepth);
            ConsumeHeader(controlDepth, "if");
            ParseControlledBody(controlDepth + 1, "if");

            SkipNewLines();
            if (Is("else"))
            {
                _position++;
                SkipNewLines();
                if (Is("if"))
                {
                    ParseIf(controlDepth + 1);
                }
                else
                {
                    ParseControlledBody(controlDepth + 1, "else");
                }
            }
        }

        private void ParseLoop(int controlDepth)
        {
            string keyword = Current.Text;
            _position++;
            _result.LoopCount++;
            _result.TotalStatements++;
            ObserveNesting(controlDepth);
            ConsumeHeader(controlDepth, keyword);
            ParseControlledBody(controlDepth + 1, keyword);
        }

        private void ParseDoWhile(int controlDepth)
        {
            _position++;
            _result.LoopCount++;
            _result.TotalStatements++;
            ObserveNesting(controlDepth);
            ParseControlledBody(controlDepth + 1, "do");

            SkipNewLines();
            if (Is(";"))
            {
                _position++;
                SkipNewLines();
            }

            if (Is("while"))
            {
                _position++;
                ConsumeHeader(controlDepth, "while");
                if (Is(";"))
                    _position++;
            }
            else
            {
                _result.Warnings.Add("После do не найдено завершающее while.");
            }
        }

        private void ParseSwitch(int controlDepth)
        {
            _position++;
            _result.SwitchCount++;
            _result.TotalStatements++;
            ConsumeHeader(controlDepth, "switch");
            SkipNewLines();

            if (!Is("{"))
            {
                _result.Warnings.Add("После switch не найден блок с case-метками.");
                if (_position < _limit)
                    ParseControlledBody(controlDepth + 1, "switch");
                return;
            }

            int open = _position;
            int close = FindMatching(open, "{", "}");
            if (close < 0)
            {
                _result.Warnings.Add("Не удалось найти закрывающую скобку тела switch.");
                _position = _limit;
                return;
            }

            List<SwitchArm> arms = ReadSwitchArms(open, close, controlDepth);
            int caseCount = 0;
            for (int i = 0; i < arms.Count; i++)
            {
                if (!arms[i].IsDefault)
                {
                    arms[i].CaseOrdinal = caseCount;
                    caseCount++;
                }
            }

            _result.SwitchCaseCount += caseCount;
            if (caseCount > 0)
                ObserveNesting(controlDepth + caseCount - 1);

            if (arms.Count == 0)
            {
                ParseRange(open + 1, close, controlDepth);
            }
            else
            {
                if (open + 1 < arms[0].LabelStart)
                    ParseRange(open + 1, arms[0].LabelStart, controlDepth);

                for (int i = 0; i < arms.Count; i++)
                {
                    SwitchArm arm = arms[i];
                    int bodyDepth = arm.IsDefault
                        ? controlDepth + caseCount
                        : controlDepth + arm.CaseOrdinal + 1;
                    if (arm.BodyStart < arm.BodyEnd)
                        ParseRange(arm.BodyStart, arm.BodyEnd, bodyDepth);
                }
            }

            _position = close + 1;
        }

        private List<SwitchArm> ReadSwitchArms(int open, int close, int controlDepth)
        {
            List<SwitchArm> arms = new List<SwitchArm>();
            int parenDepth = 0;
            int bracketDepth = 0;
            int braceDepth = 0;
            int i = open + 1;

            while (i < close)
            {
                GroovyToken token = _tokens[i];
                bool topLevel = parenDepth == 0 && bracketDepth == 0 && braceDepth == 0;
                if (topLevel && (token.Text == "case" || token.Text == "default"))
                {
                    bool isDefault = token.Text == "default";
                    int colon = FindCaseColon(i + 1, close);
                    if (colon < 0)
                    {
                        _result.Warnings.Add("У метки " + token.Text + " не найден символ ':' (строка " + token.Line + ").");
                        i++;
                        continue;
                    }

                    if (!isDefault)
                        CountConditionalExpressions(i + 1, colon, controlDepth);

                    SwitchArm arm = new SwitchArm();
                    arm.LabelStart = i;
                    arm.BodyStart = colon + 1;
                    arm.IsDefault = isDefault;
                    arm.CaseOrdinal = -1;
                    arms.Add(arm);
                    i = colon + 1;
                    continue;
                }

                UpdateNestingCounters(token.Text, ref parenDepth, ref bracketDepth, ref braceDepth);
                i++;
            }

            for (int armIndex = 0; armIndex < arms.Count; armIndex++)
            {
                arms[armIndex].BodyEnd = armIndex + 1 < arms.Count
                    ? arms[armIndex + 1].LabelStart
                    : close;
            }

            return arms;
        }

        private int FindCaseColon(int start, int end)
        {
            int parenDepth = 0;
            int bracketDepth = 0;
            int braceDepth = 0;
            int ternaryDepth = 0;

            for (int i = start; i < end; i++)
            {
                string text = _tokens[i].Text;
                bool topLevel = parenDepth == 0 && bracketDepth == 0 && braceDepth == 0;

                if (topLevel && text == ":")
                {
                    if (ternaryDepth > 0)
                    {
                        ternaryDepth--;
                    }
                    else
                    {
                        return i;
                    }
                    continue;
                }

                if (topLevel && text == "?")
                    ternaryDepth++;

                UpdateNestingCounters(text, ref parenDepth, ref bracketDepth, ref braceDepth);
            }
            return -1;
        }

        private void ParseTry(int controlDepth)
        {
            _position++;
            _result.TotalStatements++;
            SkipNewLines();
            ParseControlledBody(controlDepth, "try");

            SkipNewLines();
            while (Is("catch"))
            {
                _position++;
                ConsumeCatchHeader();
                ParseControlledBody(controlDepth, "catch");
                SkipNewLines();
            }

            if (Is("finally"))
            {
                _position++;
                ParseControlledBody(controlDepth, "finally");
            }
        }

        private void ParseSynchronized(int controlDepth)
        {
            _position++;
            _result.TotalStatements++;
            ConsumeHeader(controlDepth, "synchronized");
            ParseControlledBody(controlDepth, "synchronized");
        }

        private void ConsumeCatchHeader()
        {
            SkipNewLines();
            if (Is("("))
            {
                int close = FindMatching(_position, "(", ")");
                if (close >= 0)
                    _position = close + 1;
            }
        }

        private void ConsumeHeader(int expressionDepth, string owner)
        {
            SkipNewLines();
            if (Is("("))
            {
                int close = FindMatching(_position, "(", ")");
                if (close < 0)
                {
                    _result.Warnings.Add("Не закрыты круглые скобки в заголовке " + owner + " (строка " + Current.Line + ").");
                    _position++;
                    return;
                }

                CountConditionalExpressions(_position + 1, close, expressionDepth);
                _position = close + 1;
                return;
            }

            int start = _position;
            while (_position < _limit && !Is("{") && !IsNewLineToken(Current) && !Is(";"))
                _position++;
            CountConditionalExpressions(start, _position, expressionDepth);
        }

        private void ParseControlledBody(int controlDepth, string owner)
        {
            SkipNewLines();
            if (_position >= _limit || Is("}") || Is("else") || Is("case") || Is("default"))
            {
                _result.Warnings.Add("Не найдено тело оператора " + owner + ".");
                return;
            }

            if (Is("{"))
                ParseBlock(controlDepth);
            else
                ParseSequenceStatement(controlDepth);
        }

        private void ParseSequenceStatement(int controlDepth)
        {
            if (Is("if"))
                ParseIf(controlDepth);
            else if (Is("for") || Is("while"))
                ParseLoop(controlDepth);
            else if (Is("do"))
                ParseDoWhile(controlDepth);
            else if (Is("switch"))
                ParseSwitch(controlDepth);
            else if (Is("try"))
                ParseTry(controlDepth);
            else if (Is("synchronized"))
                ParseSynchronized(controlDepth);
            else if (IsLabelledControlStatement())
            {
                _position += 2;
                ParseSequenceStatement(controlDepth);
            }
            else if (Is("{"))
                ParseBlock(controlDepth);
            else if (!Is("}") && _position < _limit)
                ParseSimpleStatement(controlDepth);
        }

        private void ParseBlock(int controlDepth)
        {
            if (!Is("{"))
                return;

            int open = _position;
            int close = FindMatching(open, "{", "}");
            if (close < 0)
            {
                _result.Warnings.Add("Не закрыт блок '{' (строка " + Current.Line + ").");
                ParseRange(open + 1, _limit, controlDepth);
                _position = _limit;
                return;
            }

            ParseRange(open + 1, close, controlDepth);
            _position = close + 1;
        }

        private void ParseSimpleStatement(int controlDepth)
        {
            int start = _position;
            int parenDepth = 0;
            int bracketDepth = 0;
            int expressionStart = start;
            bool hasContent = false;
            int lastSignificant = -1;

            while (_position < _limit)
            {
                GroovyToken token = _tokens[_position];
                string text = token.Text;

                if (token.Kind == GroovyTokenKind.NewLine && parenDepth == 0 && bracketDepth == 0)
                {
                    if (ShouldContinueAcrossNewLine(lastSignificant, _position + 1))
                    {
                        _position++;
                        continue;
                    }
                    break;
                }

                if (text == ";" && parenDepth == 0 && bracketDepth == 0)
                {
                    _position++;
                    break;
                }

                if (text == "}" && parenDepth == 0 && bracketDepth == 0)
                    break;

                if (text == "{")
                {
                    CountConditionalExpressions(expressionStart, _position, controlDepth);
                    int close = FindMatching(_position, "{", "}");
                    if (close < 0)
                    {
                        _result.Warnings.Add("Не закрыта closure/блок '{' (строка " + token.Line + ").");
                        _position++;
                        expressionStart = _position;
                        hasContent = true;
                        continue;
                    }

                    int bodyStart = FindClosureBodyStart(_position + 1, close);
                    if (bodyStart < close)
                        ParseRange(bodyStart, close, controlDepth);
                    _position = close + 1;
                    expressionStart = _position;
                    hasContent = true;
                    lastSignificant = _position - 1;
                    continue;
                }

                if (text == "(")
                    parenDepth++;
                else if (text == ")" && parenDepth > 0)
                    parenDepth--;
                else if (text == "[")
                    bracketDepth++;
                else if (text == "]" && bracketDepth > 0)
                    bracketDepth--;

                if (token.Kind != GroovyTokenKind.NewLine)
                {
                    hasContent = true;
                    lastSignificant = _position;
                }
                _position++;
            }

            CountConditionalExpressions(expressionStart, _position, controlDepth);
            if (hasContent)
                _result.TotalStatements++;

            if (_position == start && _position < _limit && !Is("}"))
                _position++;
        }

        private bool ShouldContinueAcrossNewLine(int previousIndex, int nextIndex)
        {
            if (previousIndex < 0 || previousIndex >= _tokens.Count)
                return false;

            string previous = _tokens[previousIndex].Text;
            string[] endingOperators = new string[]
            {
                "=", "+", "-", "*", "/", "%", ".", "?.", "*.", ",", ":",
                "?", "?:", "&&", "||", "->", "as", "in", "==", "!=", "<", ">",
                "<=", ">=", "+=", "-=", "*="
            };
            if (endingOperators.Contains(previous))
                return true;

            if (nextIndex < _limit)
            {
                string next = _tokens[nextIndex].Text;
                if (next == "." || next == "?." || next == "*." || next == "as" ||
                    next == "?" || next == ":")
                    return true;
            }
            return false;
        }

        private int FindClosureBodyStart(int start, int end)
        {
            int parenDepth = 0;
            int bracketDepth = 0;
            int braceDepth = 0;
            for (int i = start; i < end; i++)
            {
                string text = _tokens[i].Text;
                if (text == "->" && parenDepth == 0 && bracketDepth == 0 && braceDepth == 0)
                    return i + 1;
                UpdateNestingCounters(text, ref parenDepth, ref bracketDepth, ref braceDepth);
            }
            return start;
        }

        private bool IsLabelledControlStatement()
        {
            if (_position + 2 >= _limit || _tokens[_position].Kind != GroovyTokenKind.Identifier ||
                _tokens[_position + 1].Text != ":")
                return false;

            string next = _tokens[_position + 2].Text;
            return next == "if" || next == "for" || next == "while" || next == "do" || next == "switch";
        }

        private bool IsModifiedTypeDeclaration()
        {
            int index = _position;
            string[] modifiers = new string[]
            {
                "public", "private", "protected", "abstract", "final", "static", "sealed"
            };
            while (index < _limit && modifiers.Contains(_tokens[index].Text))
                index++;

            return index > _position && index < _limit &&
                (_tokens[index].Text == "class" || _tokens[index].Text == "interface" ||
                 _tokens[index].Text == "trait" || _tokens[index].Text == "enum" ||
                 _tokens[index].Text == "record");
        }

        private void ParseTypeDeclaration()
        {
            int start = _position;
            int open = FindNextTopLevelBrace(start, _limit);
            if (open < 0)
            {
                SkipToStatementEnd();
                return;
            }

            int close = FindMatching(open, "{", "}");
            if (close < 0)
            {
                _result.Warnings.Add("Не закрыт блок объявления типа (строка " + _tokens[start].Line + ").");
                ParseRange(open + 1, _limit, 0);
                _position = _limit;
                return;
            }

            ParseRange(open + 1, close, 0);
            _position = close + 1;
        }

        private bool TryParseMethodDeclaration()
        {
            if (_position >= _limit)
                return false;

            int start = _position;
            int openParen = -1;
            int parenDepth = 0;
            int scanned = 0;

            for (int i = start; i < _limit && scanned < 120; i++, scanned++)
            {
                GroovyToken token = _tokens[i];
                string text = token.Text;

                if (token.Kind == GroovyTokenKind.NewLine)
                {
                    if (i > start && _tokens[i - 1].Text != "," && _tokens[i - 1].Text != "(")
                        break;
                    continue;
                }
                if ((text == "=" || text == ";" || text == "{" || text == "}" ) && parenDepth == 0)
                    break;
                if (text == "(")
                {
                    if (parenDepth == 0)
                    {
                        openParen = i;
                        break;
                    }
                    parenDepth++;
                }
            }

            if (openParen < 0)
                return false;

            List<string> prefixIdentifiers = new List<string>();
            bool hasDot = false;
            bool hasEquals = false;
            for (int i = start; i < openParen; i++)
            {
                GroovyToken token = _tokens[i];
                if (token.Kind == GroovyTokenKind.Identifier)
                    prefixIdentifiers.Add(token.Text);
                if (token.Text == "." || token.Text == "?.")
                    hasDot = true;
                if (token.Text == "=")
                    hasEquals = true;
            }

            if (prefixIdentifiers.Count == 0 || hasDot || hasEquals)
                return false;

            string firstPrefixWord = prefixIdentifiers[0];
            string[] expressionWords = new string[]
            {
                "return", "throw", "assert", "new", "this", "super", "break", "continue",
                "if", "for", "while", "do", "switch", "try", "catch", "finally", "case", "default"
            };
            if (expressionWords.Contains(firstPrefixWord))
                return false;

            string[] declarationWords = new string[]
            {
                "def", "void", "public", "private", "protected", "static", "abstract",
                "final", "synchronized", "native", "strictfp"
            };
            bool explicitDeclaration = prefixIdentifiers.Any(delegate(string word)
            {
                return declarationWords.Contains(word);
            });
            bool strongPrefix = explicitDeclaration || prefixIdentifiers.Count >= 2;
            if (!strongPrefix)
                return false;

            int closeParen = FindMatching(openParen, "(", ")");
            if (closeParen < 0 || closeParen >= _limit)
                return false;

            int after = closeParen + 1;
            while (after < _limit && _tokens[after].Kind == GroovyTokenKind.NewLine)
                after++;

            if (after < _limit && IsAt(after, "throws"))
            {
                while (after < _limit && !IsAt(after, "{" ) && _tokens[after].Text != ";")
                    after++;
            }

            if (after < _limit && IsAt(after, "{"))
            {
                int closeBrace = FindMatching(after, "{", "}");
                if (closeBrace < 0)
                {
                    _result.Warnings.Add("Не закрыто тело метода (строка " + _tokens[start].Line + ").");
                    _position = _limit;
                    return true;
                }

                ParseRange(after + 1, closeBrace, 0);
                _position = closeBrace + 1;
                return true;
            }

            if (!explicitDeclaration)
                return false;

            _position = closeParen + 1;
            while (_position < _limit && _tokens[_position].Kind != GroovyTokenKind.NewLine && !Is(";"))
                _position++;
            if (Is(";"))
                _position++;
            return true;
        }

        private int FindNextTopLevelBrace(int start, int end)
        {
            int parenDepth = 0;
            int bracketDepth = 0;
            for (int i = start; i < end; i++)
            {
                string text = _tokens[i].Text;
                if (text == "{" && parenDepth == 0 && bracketDepth == 0)
                    return i;
                if (text == "(" )
                    parenDepth++;
                else if (text == ")" && parenDepth > 0)
                    parenDepth--;
                else if (text == "[")
                    bracketDepth++;
                else if (text == "]" && bracketDepth > 0)
                    bracketDepth--;
                else if (text == ";" && parenDepth == 0 && bracketDepth == 0)
                    return -1;
            }
            return -1;
        }

        private int FindMatching(int openIndex, string openText, string closeText)
        {
            if (openIndex < 0 || openIndex >= _tokens.Count || _tokens[openIndex].Text != openText)
                return -1;

            int depth = 0;
            for (int i = openIndex; i < _tokens.Count; i++)
            {
                if (_tokens[i].Text == openText)
                    depth++;
                else if (_tokens[i].Text == closeText)
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }
            return -1;
        }

        private void ParseRange(int start, int end, int controlDepth)
        {
            int savedPosition = _position;
            int savedLimit = _limit;
            _position = Math.Max(0, start);
            _limit = Math.Max(_position, Math.Min(end, _tokens.Count));
            ParseSequence(controlDepth);
            _position = savedPosition;
            _limit = savedLimit;
        }

        private void CountConditionalExpressions(int start, int end, int controlDepth)
        {
            List<TernaryFrame> frames = new List<TernaryFrame>();
            int parenDepth = 0;
            int bracketDepth = 0;
            int braceDepth = 0;
            int safeEnd = Math.Min(end, _tokens.Count);

            for (int i = Math.Max(0, start); i < safeEnd; i++)
            {
                string text = _tokens[i].Text;

                if (text == "?" || text == "?:")
                {
                    bool safeIndexOrWildcard = text == "?" && i + 1 < safeEnd &&
                        (_tokens[i + 1].Text == "[" || _tokens[i + 1].Text == ">");
                    if (!safeIndexOrWildcard &&
                        (text == "?:" || HasTernaryColon(i + 1, safeEnd)))
                    {
                        _result.TernaryCount++;
                        ObserveNesting(controlDepth + frames.Count);
                        if (text == "?")
                        {
                            TernaryFrame frame = new TernaryFrame();
                            frame.ParenDepth = parenDepth;
                            frame.BracketDepth = bracketDepth;
                            frame.BraceDepth = braceDepth;
                            frame.HasColon = false;
                            frames.Add(frame);
                        }
                    }
                }
                else if (text == ":")
                {
                    for (int frameIndex = frames.Count - 1; frameIndex >= 0; frameIndex--)
                    {
                        TernaryFrame frame = frames[frameIndex];
                        if (frame.ParenDepth != parenDepth || frame.BracketDepth != bracketDepth ||
                            frame.BraceDepth != braceDepth)
                            continue;

                        if (!frame.HasColon)
                        {
                            frame.HasColon = true;
                            break;
                        }
                        frames.RemoveAt(frameIndex);
                    }
                }

                if (text == ",")
                    RemoveTernariesAtScope(frames, parenDepth, bracketDepth, braceDepth);
                else if (text == ")" || text == "]" || text == "}")
                    RemoveTernariesAtScope(frames, parenDepth, bracketDepth, braceDepth);
                else if (text == ";" && parenDepth == 0 && bracketDepth == 0 && braceDepth == 0)
                    RemoveTernariesAtScope(frames, 0, 0, 0);

                UpdateNestingCounters(text, ref parenDepth, ref bracketDepth, ref braceDepth);
            }
        }

        private static void RemoveTernariesAtScope(List<TernaryFrame> frames, int parenDepth, int bracketDepth, int braceDepth)
        {
            for (int i = frames.Count - 1; i >= 0; i--)
            {
                TernaryFrame frame = frames[i];
                if (frame.ParenDepth == parenDepth && frame.BracketDepth == bracketDepth &&
                    frame.BraceDepth == braceDepth)
                {
                    frames.RemoveAt(i);
                }
            }
        }

        private bool HasTernaryColon(int start, int end)
        {
            int paren = 0;
            int bracket = 0;
            int brace = 0;
            int nestedQuestions = 0;
            int safeEnd = Math.Min(end, _tokens.Count);
            for (int i = start; i < safeEnd; i++)
            {
                string text = _tokens[i].Text;
                if (text == ";" && paren == 0 && bracket == 0 && brace == 0)
                    return false;
                if (text == "," && paren == 0 && bracket == 0 && brace == 0)
                    return false;

                if (text == "?" && !(i + 1 < safeEnd &&
                    (_tokens[i + 1].Text == "[" || _tokens[i + 1].Text == ">")))
                {
                    nestedQuestions++;
                }
                else if (text == ":")
                {
                    if (nestedQuestions == 0 && paren == 0 && bracket == 0 && brace == 0)
                        return true;
                    if (nestedQuestions > 0)
                        nestedQuestions--;
                }
                UpdateNestingCounters(text, ref paren, ref bracket, ref brace);
            }
            return false;
        }

        private void SkipAnnotation()
        {
            while (_position < _limit && Is("@"))
            {
                _position++;
                if (_position < _limit && _tokens[_position].Kind == GroovyTokenKind.Identifier)
                    _position++;

                if (Is("("))
                {
                    int close = FindMatching(_position, "(", ")");
                    if (close >= 0)
                        _position = close + 1;
                }
                while (_position < _limit && _tokens[_position].Kind == GroovyTokenKind.NewLine)
                    _position++;
            }
        }

        private void SkipToStatementEnd()
        {
            int paren = 0;
            int bracket = 0;
            int brace = 0;
            while (_position < _limit)
            {
                GroovyToken token = _tokens[_position];
                if (token.Kind == GroovyTokenKind.NewLine && paren == 0 && bracket == 0 && brace == 0)
                {
                    _position++;
                    return;
                }
                if (token.Text == ";" && paren == 0 && bracket == 0 && brace == 0)
                {
                    _position++;
                    return;
                }
                UpdateNestingCounters(token.Text, ref paren, ref bracket, ref brace);
                _position++;
            }
        }

        private void SkipSeparators()
        {
            while (_position < _limit &&
                (_tokens[_position].Kind == GroovyTokenKind.NewLine || _tokens[_position].Text == ";"))
            {
                _position++;
            }
        }

        private void SkipNewLines()
        {
            while (_position < _limit && _tokens[_position].Kind == GroovyTokenKind.NewLine)
                _position++;
        }

        private void ObserveNesting(int depth)
        {
            if (depth > _result.MaxNesting)
                _result.MaxNesting = depth;
        }

        private bool Is(string text)
        {
            return _position < _limit && _tokens[_position].Text == text;
        }

        private bool IsAt(int index, string text)
        {
            return index >= 0 && index < _limit && _tokens[index].Text == text;
        }

        private GroovyToken Current
        {
            get { return _tokens[_position]; }
        }

        private static bool IsNewLineToken(GroovyToken token)
        {
            return token != null && token.Kind == GroovyTokenKind.NewLine;
        }

        private static bool IsMatchingPair(string open, string close)
        {
            return (open == "(" && close == ")") ||
                (open == "[" && close == "]") ||
                (open == "{" && close == "}");
        }

        private static void UpdateNestingCounters(string text, ref int paren, ref int bracket, ref int brace)
        {
            if (text == "(")
                paren++;
            else if (text == ")" && paren > 0)
                paren--;
            else if (text == "[")
                bracket++;
            else if (text == "]" && bracket > 0)
                bracket--;
            else if (text == "{")
                brace++;
            else if (text == "}" && brace > 0)
                brace--;
        }
    }
}
