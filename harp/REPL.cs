namespace WSOFT.AliceScript.CLI;

using System.Text;
using global::AliceScript;
using global::AliceScript.Parsing;
using global::AliceScript.PreProcessing;

public enum MoveLineState
{
    Up,
    Down,
    Enter,
    NewLine
}

public class REPL
{
    private const string Filename = "main";
    [STAThread]
    public void Run()
    {
        Runtime.Init();
        ParsingScript script = ParsingScript.GetTopLevelScript();
        Interpreter.Instance.DebugMode = true;
        Interpreter.Instance.OnOutput += (_, e) => Console.Write(e.Output);
        Interpreter.Instance.OnData += (_, e) => Console.Write(e.Output);
        Interpreter.Instance.OnDebug += (_, e) => Console.WriteLine(e.Output);

        ThrowErrorManager.NotCatch = false;
        ThrowErrorManager.ThrowError += (_, e) =>
        {
            if (e.ErrorCode == Exceptions.BREAK_POINT)
            {
                EncounteredBreakPoint(e);
                e.Handled = true;
                return;
            }

            // スクリプトを終了する
            script.SetDone();

            Console.WriteLine($"Error : {e.ErrorCode} (0x{(int)e.ErrorCode:X3})");
            if (!string.IsNullOrEmpty(e.Message))
                Console.WriteLine($"        {e.Message}");
            if (!string.IsNullOrEmpty(e.HelpLink))
                Console.WriteLine($"   See: {e.HelpLink}");
            Console.WriteLine($"    at: {e.Script.OriginalLine}  (line {e.Script.OriginalLineNumber + 1})");

            if (e.Script is not null && e.Script.StackTrace.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("Stack Trace:");
                foreach (var stack in e.Script.StackTrace)
                {
                    Console.WriteLine($"  at {stack} at {stack.LineNumber + 1}");
                }
            }
            e.Handled = true;
        };

        if (Console.IsInputRedirected)
        {
            string code = ReadConsoleToEnd();
            ExecuteAndPrint(code, ref script);
            return;
        }
        while (true)
        {
            string code = InputCode();
            ExecuteAndPrint(code, ref script);
        }
    }
    private static void EncounteredBreakPoint(ThrowErrorEventArgs e)
    {
        switch (Console.ReadKey(true).KeyChar)
        {
            case 'o':
            case 'O':
                // スクリプトを続行する
                e.Script.SetDone();
                Console.WriteLine("Continuing script execution...");
                return;
            case 'l':
            case 'L':
                while (true)
                {
                    var result = e.Script.ProcessStatement();
                    if (!e.Script.StillValid())
                    {
                        return;
                    }
                }
                return;
            default:
                // スクリプトを終了する
                Console.WriteLine("Exiting script execution...");
                return;
        }
    }
    private static void ExecuteAndPrint(string code, ref ParsingScript script)
    {
        script = GetChildScript(script, code);
        Variable result = script.Process();
        if (result.Type != Variable.VarType.VARIABLE && result.Type != Variable.VarType.VOID)
        {
            Console.WriteLine(result.ToString());
        }
    }
    private static string ReadConsoleToEnd()
    {
        StringBuilder sb = new StringBuilder();
        string? line;
        while ((line = Console.ReadLine()) != null)
        {
            sb.AppendLine(line);
        }
        return sb.ToString();
    }
    private static ParsingScript GetChildScript(ParsingScript parent, string code)
    {
        string scriptStr = PreProcessor.ConvertToScript(code, out var char2Line, out var defines, out var settings, Filename);
        ParsingScript script = parent.GetChildScript(scriptStr);
        script = script.GetChildScript(scriptStr);
        script.Filename = Filename;
        script.Char2Line = char2Line;
        script.Defines = defines;
        script.Settings = settings;
        script.OriginalScript = code;
        return script;
    }
    private string InputCode()
    {
        List<string> lines = new List<string>();

        int lineCount = 0;
        int indentCount = 0;
        int cBracks = 0; // 波かっこ
        int sBracks = 0; // 角かっこ
        int pBracks = 0; // 丸かっこ

        string nextLine = new(' ', indentCount);

        while (true)
        {
            Console.Write($"alice:{lineCount + 1:D2}> ");
            var (line, moveState) = GetLine(nextLine, indentCount);

            Console.SetCursorPosition(0, Console.CursorTop);
            Console.Write($"alice:{lineCount + 1:D2}: {line}");

            if (lineCount >= lines.Count)
            {
                lines.Add(line);
            }
            else
            {
                lines[lineCount] = line;
            }

            // インデントの数をカウント
            indentCount = CountStr(line, " ");

            // かっこを数える
            cBracks += CountBrackets(line, '{', '}');
            sBracks += CountBrackets(line, '[', ']');
            pBracks += CountBrackets(line, '(', ')');

            if (moveState == MoveLineState.Up)
            {
                bool inTop = lineCount == 0;
                lineCount = Math.Max(0, lineCount - 1);
                nextLine = lines[lineCount];
                // かっこの数を戻す
                cBracks -= CountBrackets(nextLine, '{', '}');
                sBracks -= CountBrackets(nextLine, '[', ']');
                pBracks -= CountBrackets(nextLine, '(', ')');

                if (inTop)
                {
                    Console.SetCursorPosition(0, Console.CursorTop);
                    continue;
                }
                Console.SetCursorPosition(0, Math.Max(Console.CursorTop - 1, 0));
                continue;
            }
            else
            {
                if (lineCount + 1 >= lines.Count)
                {
                    if (moveState == MoveLineState.Down)
                    {
                        Console.SetCursorPosition(0, Console.CursorTop);
                        continue;
                    }
                    // 新しい行を追加する
                    if (moveState != MoveLineState.NewLine && cBracks == 0 && sBracks == 0 && pBracks == 0)
                    {
                        break;
                    }
                    lines.Add(new string(' ', indentCount));
                }
                lineCount++;
                Console.WriteLine();
                nextLine = lines[lineCount];
                continue;
            }
        }
        Console.WriteLine();
        return lines.Count == 0 ? string.Empty : string.Join(Environment.NewLine, lines);
    }
    /// <summary>
    /// かっこの数を数える
    /// </summary>
    /// <param name="str">対象の文字列</param>
    /// <param name="openBracket">開き文字</param>
    /// <param name="closeBracket">閉じ文字</param>
    /// <returns>かっこの数</returns>
    private static int CountBrackets(string str, char openBracket, char closeBracket)
    {
        bool inString = false;
        bool inComment = false;
        bool inLineComment = false;
        int count = 0;

        for (int i = 0; i < str.Length; i++)
        {
            char c = str[i];
            char next = i + 1 < str.Length ? str[i + 1] : '\0';

            // 文字列リテラル内かチェック
            if (c == '"' && !inComment && !inLineComment && (i == 0 || str[i - 1] != '\\'))
            {
                inString = !inString;
                continue;
            }

            // コメント内かチェック
            if (c == '/' && next == '*' && !inString && !inComment && !inLineComment)
            {
                inComment = true;
                i++;
                continue;
            }

            if (c == '*' && next == '/' && inComment)
            {
                inComment = false;
                i++;
                continue;
            }

            if (c == '/' && next == '/' && !inString && !inComment && !inLineComment)
            {
                inLineComment = true;
                i++;
                continue;
            }

            if (c == '\n' && inLineComment)
            {
                inLineComment = false;
                continue;
            }

            // 括弧のカウント
            if (!inString && !inComment && !inLineComment)
            {
                if (c == openBracket) count++;
                else if (c == closeBracket) count--;
            }
        }

        return count;
    }
    private static readonly char[] WordSplitChars = Constants.TOKEN_SEPARATION;
    private static (string, MoveLineState) GetLine(string defaultText = "", int indentCount = 2)
    {
        StringBuilder buffer = new(defaultText);
        int defaultCursorLeft = Console.CursorLeft + 1;
        int cursorPosition = defaultText.Length;

        int GetDisplayWidth(string text)
        {
            int width = 0;
            foreach (char c in text)
            {
                // 全角文字かどうかを判定
                width += IsFullWidth(c) ? 2 : 1;
            }
            return width;
        }

        // 指定位置までの表示幅を計算
        int GetDisplayPositionWidth(StringBuilder sb, int position)
        {
            return GetDisplayWidth(sb.ToString(0, position));
        }

        while (true)
        {
            int displayWidth = GetDisplayPositionWidth(buffer, cursorPosition);
            int safePosition = Math.Min(defaultCursorLeft + displayWidth, Console.BufferWidth - 1);
            Console.Write($"\x1B[{defaultCursorLeft}G\x1B[0K{buffer}\x1B[{safePosition}G");
            // TODO: 上は可読性が最悪だがパフォーマンス上しょうがない、ようするに下と同じこと
            //Console.SetCursorPosition(defaultCursorLeft, Console.CursorTop); // カーソルを一旦行頭へ
            //Console.Write(buffer.ToString()); // バッファの内容を表示
            //Console.SetCursorPosition(cursorLeft, Console.CursorTop); // カーソルを入力位置に戻す

            var keyInfo = Console.ReadKey(true);

            switch (keyInfo.Key)
            {
                case ConsoleKey.Enter:
                    return (buffer.ToString(), MoveLineState.Enter);
                case ConsoleKey.Backspace:
                    if (cursorPosition > 0) // カーソルが先頭より右にある場合のみ
                    {
                        buffer.Remove(cursorPosition - 1, 1);
                        cursorPosition--; // カーソルを左に移動
                    }
                    break;
                case ConsoleKey.Delete:
                    if (cursorPosition < buffer.Length)
                    {
                        buffer.Remove(cursorPosition, 1);
                    }
                    break;
                case ConsoleKey.LeftArrow:
                    if (cursorPosition > 0) // 0より大きい場合のみ左に移動可能
                    {
                        cursorPosition--; // カーソルを左に移動
                    }
                    break;
                case ConsoleKey.RightArrow:
                    if (cursorPosition < buffer.Length) // バッファの長さ未満の場合のみ右に移動可能
                    {
                        cursorPosition++; // カーソルを右に移動
                    }
                    break;
                case ConsoleKey.UpArrow:
                    return (buffer.ToString(), MoveLineState.Up);
                case ConsoleKey.DownArrow:
                    return (buffer.ToString(), keyInfo.Modifiers.HasFlag(ConsoleModifiers.Shift) ? MoveLineState.NewLine : MoveLineState.Down);
                case ConsoleKey.Tab:
                    // タブキーでインデントを追加
                    int tabSize = indentCount > 0 ? indentCount : 2; // デフォルトのインデントサイズ
                    if (keyInfo.Modifiers.HasFlag(ConsoleModifiers.Shift))
                    {
                        if (cursorPosition >= tabSize && StartsWithBlanks(buffer, tabSize))
                        {
                            buffer.Remove(0, tabSize);
                            cursorPosition -= tabSize; // カーソルを右に移動
                        }
                    }
                    else
                    {
                        buffer.Insert(cursorPosition, new string(' ', tabSize));
                        cursorPosition += tabSize; // カーソルを右に移動
                    }
                    break;
                case ConsoleKey.Home:
                    cursorPosition = 0;
                    break;
                case ConsoleKey.End:
                    cursorPosition = buffer.Length;
                    break;
                default:
                    if (keyInfo.Modifiers.HasFlag(ConsoleModifiers.Alt))
                    {
                        switch (keyInfo.Key)
                        {
                            case ConsoleKey.LeftArrow:
                                if (cursorPosition <= 0) break;
                                int moveToLeft = buffer.ToString().LastIndexOfAny(WordSplitChars, cursorPosition - 1);
                                cursorPosition = moveToLeft > 0 ? moveToLeft : 0;
                                break;
                            case ConsoleKey.RightArrow:
                                if (cursorPosition >= buffer.Length) break;
                                int moveToRight = buffer.ToString().IndexOfAny(WordSplitChars, cursorPosition + 1);
                                cursorPosition = moveToRight > 0 ? moveToRight : buffer.Length;
                                break;
                        }
                    }
                    else if (!char.IsControl(keyInfo.KeyChar))
                    {
                        buffer.Insert(cursorPosition, keyInfo.KeyChar); // カーソル位置に文字を挿入
                        cursorPosition++; // カーソルを右に移動
                    }
                    break;
            }
        }
    }
    /// <summary>
    /// 全角文字かどうかを判定する
    /// </summary>
    /// <param name="c">対象の文字</param>
    /// <returns>全角であればtrue、そうでなければfalse</returns>
    private static bool IsFullWidth(char c)
    {
        return
            (c >= '\u1100' && c <= '\u115F') || // Hangul Jamo
            (c >= '\u2E80' && c <= '\u2FFF') || // CJK部首補助～康熙部首
            (c >= '\u3000' && c <= '\u303F') || // CJK記号と句読点
            (c >= '\u3040' && c <= '\u309F') || // ひらがな
            (c >= '\u30A0' && c <= '\u30FF') || // カタカナ
            (c >= '\u3100' && c <= '\u312F') || // 注音字母
            (c >= '\u3130' && c <= '\u318F') || // ハングル互換字母
            (c >= '\u3190' && c <= '\u319F') || // かなの補助
            (c >= '\u31A0' && c <= '\u31BF') || // 注音字母拡張
            (c >= '\u31F0' && c <= '\u31FF') || // カタカナ拡張
            (c >= '\u3200' && c <= '\u32FF') || // 囲みCJK文字
            (c >= '\u3300' && c <= '\u33FF') || // CJK互換文字
            (c >= '\u3400' && c <= '\u4DBF') || // CJK統合漢字拡張A
            (c >= '\u4E00' && c <= '\u9FFF') || // CJK統合漢字
            (c >= '\uA000' && c <= '\uA48F') || // イ族音節
            (c >= '\uA490' && c <= '\uA4CF') || // イ族部首
            (c >= '\uAC00' && c <= '\uD7AF') || // ハングル音節
            (c >= '\uF900' && c <= '\uFAFF') || // CJK互換漢字
            (c >= '\uFF00' && c <= '\uFFEF');   // 全角形
    }
    private static bool StartsWithBlanks(StringBuilder sb, int n)
    {
        if (sb == null)
        {
            return false;
        }

        if (n <= 0)
        {
            return true;
        }

        if (sb.Length < n)
        {
            return false;
        }

        for (int i = 0; i < n; i++)
        {
            if (!char.IsWhiteSpace(sb[i]))
            {
                return false;
            }
        }
        return true;
    }
    private static int CountStr(string str, string subStr)
    {
        if (string.IsNullOrEmpty(str) || string.IsNullOrEmpty(subStr))
            return 0;

        return (str.Length - str.Replace(subStr, "").Length) / subStr.Length;
    }
}