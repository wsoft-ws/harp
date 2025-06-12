namespace WSOFT.AliceScript.CLI;

using AliceScript;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("AliceScript Shell");
        Console.WriteLine("新しいスクリプトを入力してください。");
        Console.WriteLine("入力が完了したら、空行を入力してください。");

        REPL repl = new REPL();
        repl.Run();

    }
}
