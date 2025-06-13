namespace WSOFT.AliceScript.CLI;

using AliceScript;

class Program
{
    static void Main(string[] args)
    {
        REPL repl = new();
        repl.Run();
    }
}
