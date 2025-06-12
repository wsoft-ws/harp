namespace WSOFT.AliceScript.CLI;

using System.Text;

public class REPL
{
    [STAThread]
    public void Run()
    {
        Console.WriteLine("AliceScript Shell");
        Console.WriteLine("新しいスクリプトを入力してください。");
        Console.WriteLine("入力が完了したら、空行を入力してください。");
        string str = InputCode();
        Console.WriteLine("入力されたスクリプト:");
        Console.WriteLine(str);
    }
    private string InputCode()
    {
        StringBuilder sb = new StringBuilder();

        int lineCount = 0;
        int indentCount = 0;
        int prevIndentCount = 0;
        int indentDiff = 2;
        int cBracks = 0; // 波かっこ
        int sBracks = 0; // 角かっこ
        int pBracks = 0; // 丸かっこ

        while (true)
        {
            Console.Write($"{lineCount + 1:D2}>");
            string line = GetLine(new string(' ', indentCount), indentDiff);

            sb.AppendLine(line);
            lineCount++;

            // インデントの数をカウント
            prevIndentCount = indentCount;
            indentCount = CountStr(line, " ");
            indentDiff = indentCount - prevIndentCount > 0 ? indentCount - prevIndentCount : indentDiff;

            cBracks += CountStr(line, "{");
            cBracks -= CountStr(line, "}");
            sBracks += CountStr(line, "[");
            sBracks -= CountStr(line, "]");
            pBracks += CountStr(line, "(");
            pBracks -= CountStr(line, ")");

            if (line.Trim() == "" && cBracks == 0 && sBracks == 0 && pBracks == 0)
            {
                break;
            }
        }
        return sb.ToString();
    }
    private static string GetLine(string defaultText = "", int indentCount = 2)
    {
        StringBuilder buffer = new(defaultText);
        int defaultCursorLeft = Console.CursorLeft + 1;
        int cursorPosition = defaultText.Length;
        while (true)
        {
            Console.Write($"\x1B[{defaultCursorLeft}G\x1B[0K{buffer}\x1B[{cursorPosition + defaultCursorLeft}G");
            // TODO: 上は可読性が最悪だがパフォーマンス上しょうがない、ようするに下と同じこと
            //Console.SetCursorPosition(defaultCursorLeft, Console.CursorTop); // カーソルを一旦行頭へ
            //Console.Write(buffer.ToString()); // バッファの内容を表示
            //Console.SetCursorPosition(cursorLeft, Console.CursorTop); // カーソルを入力位置に戻す

            var keyInfo = Console.ReadKey(true);

            switch (keyInfo.Key)
            {
                case ConsoleKey.Enter:
                    break;
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
                case ConsoleKey.Tab:
                    // タブキーでインデントを追加
                    buffer.Insert(cursorPosition, new string(' ', indentCount));
                    cursorPosition += indentCount; // カーソルを右に移動
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
            if (keyInfo.Key == ConsoleKey.Enter)
            {
                Console.WriteLine(); // 改行
                break; // 入力終了
            }
        }
        return buffer.ToString();
    }
    private static int CountStr(string str, string subStr)
    {
        int count = 0;
        int index = 0;

        while ((index = str.IndexOf(subStr, index)) != -1)
        {
            count++;
            index += subStr.Length;
        }

        return count;
    }
}