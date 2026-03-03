using System.Windows.Forms;

namespace MainPatchedImproved;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
#if TRONCLI
        TronCli.Run(args);
        return;
#endif
        ApplicationConfiguration.Initialize();
        try { Directory.CreateDirectory(ChromeImageMatcher.BaseDir); } catch { }

        try
        {
            Application.Run(new LoginForm());
        }
        catch (Exception ex)
        {
            string msg = $"{ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace}";
            try { File.WriteAllText(Path.Combine(ChromeImageMatcher.BaseDir, "error_log.txt"), msg); } catch { }
            MessageBox.Show(msg, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
