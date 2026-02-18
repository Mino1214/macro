namespace MacroWinForms;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        Directory.CreateDirectory(WalletFlowMacro.DataDir);
        Directory.CreateDirectory(WalletFlowMacro.LogsDir);

        Application.Run(new WalletFlowForm());
    }
}
