using System.Drawing;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Tesseract;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace MacroWinForms;

public record FlowResult(bool Success, double Balance, string Error = "");

public static class WalletFlowMacro
{
    public static readonly string BaseDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
    public static readonly string TemplatesDir = Path.Combine(BaseDir, "macros", "templates");
    public static readonly string DataDir = Path.Combine(BaseDir, "data");
    public static readonly string LogsDir = Path.Combine(BaseDir, "logs");
    public static readonly string MnemonicsFile = Path.Combine(DataDir, "mnemonics.txt");
    public static readonly string ResultsFile = Path.Combine(LogsDir, "results.csv");
    public static readonly string CoordsFile = Path.Combine(DataDir, "coords.json");

    private static Dictionary<string, (int X, int Y)>? _coordsCache;

    private static Dictionary<string, (int X, int Y)> LoadCoords()
    {
        if (_coordsCache != null) return _coordsCache;
        _coordsCache = new Dictionary<string, (int, int)>();
        if (!File.Exists(CoordsFile)) return _coordsCache;

        try
        {
            var raw = JObject.Parse(File.ReadAllText(CoordsFile));
            foreach (var (key, token) in raw)
            {
                if (token is not JObject obj) continue;
                var x = obj["x"]?.Value<int>();
                var y = obj["y"]?.Value<int>();
                if (x.HasValue && y.HasValue)
                    _coordsCache[key] = (x.Value, y.Value);
            }
        }
        catch { /* ignore */ }
        return _coordsCache;
    }

    private static (int X, int Y)? GetCoordForTemplate(string templateName)
    {
        var coords = LoadCoords();
        if (coords.TryGetValue(templateName, out var c)) return c;
        var baseName = Path.GetFileNameWithoutExtension(templateName);
        return coords.TryGetValue(baseName, out c) ? c : null;
    }

    /// <summary>
    /// 니모닉 파일에서 12단어 세트 목록 로드
    /// </summary>
    public static List<List<string>> LoadMnemonics(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"니모닉 파일을 찾을 수 없습니다: {path}");

        var sets = new List<List<string>>();
        foreach (var line in File.ReadAllLines(path))
        {
            var s = line.Trim();
            if (string.IsNullOrEmpty(s) || s.StartsWith("#")) continue;

            var parts = s.Contains(',')
                ? s.Split(',').Select(p => p.Trim()).Where(p => p.Length > 0).ToList()
                : s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).ToList();

            if (parts.Count != 12) continue;
            sets.Add(parts);
        }
        return sets;
    }

    private static (int X, int Y)? WaitForTemplate(string templateName, double timeoutSec = 10.0, double threshold = 0.8)
    {
        var templatePath = Path.Combine(TemplatesDir, templateName);
        var endTime = DateTime.Now.AddSeconds(timeoutSec);

        while (DateTime.Now < endTime)
        {
            (int X, int Y)? pos = null;
            try
            {
                var multiscaleThreshold = templateName == "safepal.png" ? 0.7 : threshold;
                pos = ImageMatcher.FindOnScreenMultiscale(templatePath, null, multiscaleThreshold);
            }
            catch { /* ignore */ }

            if (pos == null && templateName == "safepal.png")
                pos = ImageMatcher.FindOnScreenOrb(templatePath);

            if (pos == null)
                pos = ImageMatcher.FindOnScreen(templatePath, null, threshold);

            if (pos.HasValue) return pos;
            Thread.Sleep(300);
        }
        return null;
    }

    private static (int X, int Y) WaitAndClick(string templateName, double timeoutSec = 10.0, double threshold = 0.8)
    {
        var coord = GetCoordForTemplate(templateName);
        if (coord.HasValue)
        {
            InputHelper.Click(coord.Value.X, coord.Value.Y);
            return (coord.Value.X, coord.Value.Y);
        }

        var pos = WaitForTemplate(templateName, timeoutSec, threshold);
        if (pos == null)
            throw new TimeoutException($"템플릿을 찾지 못했습니다: {templateName}");

        InputHelper.Click(pos.Value.X, pos.Value.Y);
        return pos.Value;
    }

    private static bool WaitForExists(string templateName, double timeoutSec = 10.0, double threshold = 0.8)
        => WaitForTemplate(templateName, timeoutSec, threshold) != null;

    private static void TypeText(string text, int intervalMs = 50)
    {
        if (string.IsNullOrEmpty(text)) return;
        InputHelper.TypeText(text, intervalMs);
    }

    private static void PressKey(string key) => InputHelper.PressKey(key);

    private static double ReadBalanceFromTemplateRegion(string templateName = "balance.png", double threshold = 0.8)
    {
        var templatePath = Path.Combine(TemplatesDir, templateName);
        var pos = ImageMatcher.FindOnScreen(templatePath, null, threshold);
        if (pos == null)
            throw new InvalidOperationException("잔고 템플릿(balance.png)을 화면에서 찾지 못했습니다.");

        int cx = pos.Value.X, cy = pos.Value.Y;
        int regionWidth = 260, regionHeight = 80;
        int x = Math.Max(0, cx - 30);
        int y = Math.Max(0, cy - regionHeight / 2);
        var region = new Rectangle(x, y, regionWidth, regionHeight);

        using var bmp = ImageMatcher.CaptureScreenToBitmap(region);
        using var mat = BitmapConverter.ToMat(bmp);
        using var gray = new Mat();
        Cv2.CvtColor(mat, gray, ColorConversionCodes.BGR2GRAY);
        using var thresh = new Mat();
        Cv2.Threshold(gray, thresh, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);

        var tessDataPath = Environment.GetEnvironmentVariable("TESSDATA_PREFIX")?.TrimEnd(Path.DirectorySeparatorChar)
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Tesseract-OCR");
        var tessDataDir = Path.Combine(tessDataPath, "tessdata");
        if (!Directory.Exists(tessDataDir)) tessDataDir = Path.Combine(BaseDir, "tessdata");
        if (!Directory.Exists(tessDataDir)) tessDataDir = "tessdata";

        string text;
        try
        {
            var tempPath = Path.Combine(Path.GetTempPath(), "macro_ocr_" + Guid.NewGuid().ToString("N") + ".png");
            try
            {
                using var bmpThresh = BitmapConverter.ToBitmap(thresh);
                bmpThresh.Save(tempPath, System.Drawing.Imaging.ImageFormat.Png);
                using var pix = Pix.LoadFromFile(tempPath);
                using var engine = new TesseractEngine(tessDataDir, "eng", EngineMode.Default);
                engine.SetVariable("tessedit_pageseg_mode", "7");
                using var page = engine.Process(pix);
                text = page.GetText();
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }
        catch
        {
            return 0.0;
        }

        var cleaned = Regex.Matches(text.Replace("$", ""), @"[0-9][0-9,\.]*");
        if (cleaned.Count == 0) return 0.0;
        var raw = cleaned[0].Value.Replace(",", "");
        return double.TryParse(raw, out var val) ? val : 0.0;
    }

    public static void DoLogin(string password, double threshold = 0.8)
    {
        WaitAndClick("safepal.png", 10, 0.2);
        WaitAndClick("passwordinput.png", 10, threshold);
        TypeText(password);
        WaitAndClick("passwordbutton.png", 10, threshold);
    }

    public static void GoToMnemonicImport(double threshold = 0.8)
    {
        WaitAndClick("selectionbutton.png", 10, threshold);
        WaitAndClick("selection2button.png", 10, threshold);
    }

    public static void EnterMnemonicWords(IEnumerable<string> words, double threshold = 0.8)
    {
        WaitAndClick("mainpage.png", 10, threshold);
        var list = words.ToList();
        for (int i = 0; i < list.Count; i++)
        {
            TypeText(list[i]);
            if (i < 11) PressKey("tab");
        }
        PressKey("enter");
    }

    public static bool CheckMnemonicErrorOrSuccess(double threshold = 0.8)
    {
        Thread.Sleep(2000);
        if (WaitForExists("mainpageerror.png", 3, threshold)) return false;
        if (WaitForExists("successpage.png", 10, threshold)) return true;
        if (WaitForExists("balance.png", 10, threshold)) return true;
        return false;
    }

    public static FlowResult RunFullFlowForMnemonic(List<string> mnemonicWords, string password, double threshold = 0.8)
    {
        try
        {
            DoLogin(password, threshold);
            GoToMnemonicImport(threshold);
            EnterMnemonicWords(mnemonicWords, threshold);

            if (!CheckMnemonicErrorOrSuccess(threshold))
                return new FlowResult(false, 0.0, "mnemonic_invalid");

            if (!WaitForExists("balance.png", 15, threshold))
                return new FlowResult(false, 0.0, "balance_not_visible");

            var balance = ReadBalanceFromTemplateRegion("balance.png", threshold);
            if (balance <= 0)
                return new FlowResult(false, balance, "balance_zero_or_parse_failed");

            return new FlowResult(true, balance, "");
        }
        catch (TimeoutException ex)
        {
            return new FlowResult(false, 0.0, $"timeout:{ex.Message}");
        }
        catch (Exception ex)
        {
            return new FlowResult(false, 0.0, $"exception:{ex.Message}");
        }
    }

    public static void AppendResultLog(List<string> mnemonicWords, FlowResult result, string resultsPath)
    {
        var dir = Path.GetDirectoryName(resultsPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var exists = File.Exists(resultsPath);
        using var writer = new StreamWriter(resultsPath, true, System.Text.Encoding.UTF8);
        if (!exists)
            writer.WriteLine("timestamp,status,balance,mnemonic,error");

        var ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var status = result.Success ? "success" : "fail";
        var mnemonicStr = string.Join(" ", mnemonicWords);
        var errorEscaped = result.Error.Replace("\"", "\"\"");
        writer.WriteLine($"{ts},{status},{result.Balance},\"{mnemonicStr}\",\"{errorEscaped}\"");
    }
}
