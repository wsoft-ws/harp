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
    [STAThread]
    public void Run()
    {
        Runtime.Init();
        int linesCountInFile = 1;
        ParsingScript script = ParsingScript.GetTopLevelScript();

        while (true)
        {
            string code = InputCode(ref linesCountInFile);
            string scriptStr = PreProcessor.ConvertToScript(code, out var char2Line, out var defines, out var settings, "repl");
            script = script.GetChildScript(scriptStr);
            script.Char2Line = char2Line;
            script.Defines = defines;
            script.Settings = settings;
            script.OriginalScript = code;
            Variable result = script.Process();
            if (!result.IsNull())
            {
                Console.WriteLine(result.ToString());
            }
        }
    }
    private string InputCode(ref int linesCountInFile)
    {
        StringBuilder sb = new StringBuilder();
        List<string> lines = new List<string>();

        int lineCount = 0;
        int indentCount = 0;
        int cBracks = 0; // 波かっこ
        int sBracks = 0; // 角かっこ
        int pBracks = 0; // 丸かっこ

        string nextLine = new(' ', indentCount);

        while (true)
        {
            Console.Write($"{lineCount + linesCountInFile:D2}>");
            var (line, moveState) = GetLine(nextLine, indentCount);

            sb.AppendLine(line);
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
                Console.SetCursorPosition(0, Console.CursorTop - 1);
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
        linesCountInFile += lines.Count;
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
    private static (string, MoveLineState) GetLine(string defaultText = "", int indentCount = 2)
    {
        StringBuilder buffer = new(defaultText);
        int defaultCursorLeft = Console.CursorLeft + 1;
        int cursorPosition = defaultText.Length;
        while (true)
        {
            int safePosition = Math.Min(cursorPosition + defaultCursorLeft, Console.BufferWidth - 1);
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
                    if (!char.IsControl(keyInfo.KeyChar))
                    {
                        buffer.Insert(cursorPosition, keyInfo.KeyChar); // カーソル位置に文字を挿入
                        cursorPosition++; // カーソルを右に移動
                    }
                    break;
            }
        }
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