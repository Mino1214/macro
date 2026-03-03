using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NBitcoin;
using Nethereum.Util;

namespace MainPatchedImproved;

/// <summary>
/// Tron Network 모드: 랜덤 니모닉 → m/44'/195'/0'/0/0 파생 → Tron 주소 → TronGrid로 TRX/USDT 잔고 조회 후 로그 출력.
/// </summary>
public static class TronNetworkRunner
{
    private const string TronGridBase = "https://api.trongrid.io";
    private const string UsdtTrc20Contract = "TR7NHqjeKQxGTCi8q8ZY4pL8otSzgjLj6t";

    /// <summary>API 키는 data/tron_api_key.txt 또는 exe 폴더의 tron_api_key.txt에서 읽음. 없으면 헤더 없이 요청 (제한적).</summary>
    private static string? GetApiKey()
    {
        var baseDir = ChromeImageMatcher.BaseDir;
        var paths = new[]
        {
            Path.Combine(baseDir, "tron_api_key.txt"),
            Path.Combine(ChromeImageMatcher.ExeDir, "tron_api_key.txt")
        };
        foreach (var p in paths)
        {
            if (!File.Exists(p)) continue;
            try
            {
                var key = File.ReadAllText(p).Trim();
                if (!string.IsNullOrEmpty(key)) return key;
            }
            catch { }
        }
        return null;
    }

    /// <summary>니모닉(12단어) → BIP44 m/44'/195'/0'/0/0 → Tron Base58 주소 (T...). 유효 BIP39 실패 시 체크섬 무시하고 시드 생성(SafePal/지갑 붙여넣기와 동일 공간 탐색).</summary>
    public static string? MnemonicToTronAddress(string[] words12)
    {
        if (words12 == null || words12.Length != 12) return null;
        try
        {
            var sentence = string.Join(" ", words12);
            var mnemo = new Mnemonic(sentence, Wordlist.English);
            var extKey = mnemo.DeriveExtKey();
            return DeriveTronAddressFromExtKey(extKey);
        }
        catch
        {
            // 체크섬 틀린 12단어 → BIP39 시드만 PBKDF2로 생성 후 파생 (지갑 붙여넣기와 같은 주소 공간)
            return MnemonicToTronAddressNoChecksum(words12);
        }
    }

    /// <summary>체크섬 검증 없이 12단어 → PBKDF2 시드 → m/44'/195'/0'/0/0 → Tron 주소 (SafePal/Trust 붙여넣기와 동일)</summary>
    private static string? MnemonicToTronAddressNoChecksum(string[] words12)
    {
        if (words12 == null || words12.Length != 12) return null;
        try
        {
            var seed = Bip39SeedFromMnemonicNoChecksum(string.Join(" ", words12));
            if (seed == null || seed.Length != 64) return null;
            var extKey = ExtKey.CreateFromSeed(seed);
            return DeriveTronAddressFromExtKey(extKey);
        }
        catch { return null; }
    }

    private static string? DeriveTronAddressFromExtKey(ExtKey extKey)
    {
        try
        {
            var path = KeyPath.Parse("m/44'/195'/0'/0/0");
            var derived = extKey.Derive(path);
            var key = derived.PrivateKey;
            var pubKey = key.PubKey.ToBytes(false);
            if (pubKey.Length != 65) return null;
            var pubKeyNoPrefix = pubKey.AsSpan(1, 64).ToArray();
            var hash = Sha3Keccack.Current.CalculateHash(pubKeyNoPrefix);
            var addrBytes = new byte[21];
            addrBytes[0] = 0x41;
            Buffer.BlockCopy(hash, hash.Length - 20, addrBytes, 1, 20);
            return NBitcoin.DataEncoders.Encoders.Base58Check.EncodeData(addrBytes);
        }
        catch { return null; }
    }

    /// <summary>BIP39 체크섬 없이 니모닉 문장 → PBKDF2-HMAC-SHA512 시드 64바이트</summary>
    private static byte[]? Bip39SeedFromMnemonicNoChecksum(string mnemonicSentence)
    {
        if (string.IsNullOrWhiteSpace(mnemonicSentence)) return null;
        var passphrase = "";
        var nfkd = NormalizationForm.FormKD;
        var pass = mnemonicSentence.Normalize(nfkd);
        var salt = ("mnemonic" + passphrase).Normalize(nfkd);
        var passBytes = Encoding.UTF8.GetBytes(pass);
        var saltBytes = Encoding.UTF8.GetBytes(salt);
        return Pbkdf2HmacSha512(passBytes, saltBytes, 2048, 64);
    }

    private static byte[] Pbkdf2HmacSha512(byte[] password, byte[] salt, int iterations, int outputBytes)
    {
        const int blockSize = 64;
        var blockCount = (outputBytes + blockSize - 1) / blockSize;
        var result = new byte[blockCount * blockSize];
        for (int i = 1; i <= blockCount; i++)
        {
            var block = new byte[salt.Length + 4];
            Buffer.BlockCopy(salt, 0, block, 0, salt.Length);
            block[salt.Length] = (byte)(i >> 24);
            block[salt.Length + 1] = (byte)(i >> 16);
            block[salt.Length + 2] = (byte)(i >> 8);
            block[salt.Length + 3] = (byte)i;
            var u = HMACSHA512.HashData(password, block);
            var t = (byte[])u.Clone();
            for (int j = 1; j < iterations; j++)
            {
                u = HMACSHA512.HashData(password, u);
                for (int k = 0; k < blockSize; k++) t[k] ^= u[k];
            }
            Buffer.BlockCopy(t, 0, result, (i - 1) * blockSize, blockSize);
        }
        var final = new byte[outputBytes];
        Buffer.BlockCopy(result, 0, final, 0, outputBytes);
        return final;
    }

    public static void Run()
    {
        try { Directory.CreateDirectory(ChromeImageMatcher.BaseDir); } catch { }
        var apiKey = GetApiKey();
        if (string.IsNullOrEmpty(apiKey))
            AppLog.WriteLine("Tron Network: data/tron_api_key.txt 없음. TronGrid 요청 시 제한될 수 있음.");

        List<string> wordlist;
        try
        {
            wordlist = AutomationRunner.UseTestMnemonic ? new List<string>() : MainLoop.LoadWords();
        }
        catch (Exception ex)
        {
            AppLog.WriteLine("Tron Network: wordlist 로드 실패: " + ex.Message);
            return;
        }

        AppLog.WriteLine("Tron Network 모드 시작 (wordlist 랜덤 12단어 → 체크섬 무시 시 동일 파생, SafePal/지갑 붙여넣기와 같은 공간)");

        using var http = new HttpClient();
        http.Timeout = TimeSpan.FromSeconds(15);
        if (!string.IsNullOrEmpty(apiKey))
            http.DefaultRequestHeaders.Add("TRON-PRO-API-KEY", apiKey);

        var attempt = 0;
        var firstSuccessLogged = false;
        while (!MainLoop.CheckStop())
        {
            attempt++;
            string[] mnemonic;
            if (AutomationRunner.UseTestMnemonic)
                mnemonic = "claw film regular palm call kangaroo carbon matrix fall crater total sand".Split(' ');
            else
                mnemonic = MainLoop.Random12(wordlist);

            var phraseLine = string.Join(" ", mnemonic);
            AppLog.WriteAttemptedPhrase(phraseLine);
            WalletCountFile.Increment();

            var address = MnemonicToTronAddress(mnemonic);
            if (string.IsNullOrEmpty(address))
            {
                AppLog.ReplaceLastLine($" 니모닉 문구 탐색중 [{attempt}] 시도중");
                Thread.Sleep(200);
                continue;
            }

            (long trxSun, decimal? usdt) = (0, null);
            try
            {
                trxSun = GetTrxBalance(http, address);
                usdt = GetUsdtBalance(http, address);
                if (!firstSuccessLogged)
                {
                    firstSuccessLogged = true;
                    AppLog.ReplaceLastLine($" 니모닉 문구 탐색중 [{attempt}] 시도중");
                    AppLog.WriteLine("  (TronGrid 응답 정상 - 조회 파이프라인 동작 중)");
                }
            }
            catch (HttpRequestException ex)
            {
                AppLog.ReplaceLastLine($" 니모닉 문구 탐색중 [{attempt}] 시도중");
                var msg = ex.Message;
                if (ex.Message.Contains("429"))
                {
                    AppLog.WriteLine("  요청 제한(429) - 3초 대기 후 재시도");
                    Thread.Sleep(3000);
                }
                else
                    AppLog.WriteLine($"  API 오류: {msg}");
                Thread.Sleep(500);
                continue;
            }
            catch (Exception ex)
            {
                AppLog.ReplaceLastLine($" 니모닉 문구 탐색중 [{attempt}] 시도중");
                AppLog.WriteLine($"  API 오류: {ex.Message}");
                Thread.Sleep(500);
                continue;
            }

            var trxTrx = trxSun / 1_000_000m;
            var hasBalance = trxSun > 0 || (usdt.HasValue && usdt.Value > 0);
            if (hasBalance)
            {
                AppLog.ReplaceLastLine($" 니모닉 문구 탐색중 [{attempt}] 시도중");
                AppLog.WriteLineRed($"  HIT | {address} | TRX: {trxTrx} | USDT: {usdt?.ToString() ?? "-"}");
                try
                {
                    var path = Path.Combine(ChromeImageMatcher.BaseDir, "tron_hit_phrases.txt");
                    var line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + phraseLine + " " + address + Environment.NewLine;
                    File.AppendAllText(path, line, Encoding.UTF8);
                }
                catch { }
            }
            else
            {
                AppLog.ReplaceLastLine($" 니모닉 문구 탐색중 [{attempt}] 시도중");
                AppLog.WriteLine($"  {address} | TRX: {trxTrx} | USDT: {usdt?.ToString() ?? "0"}");
            }

            Thread.Sleep(150);
        }

        AppLog.WriteLine("Tron Network 작업 종료");
    }

    private static long GetTrxBalance(HttpClient http, string base58Address)
    {
        var body = new { address = base58Address };
        var req = new HttpRequestMessage(HttpMethod.Post, TronGridBase + "/wallet/getaccount")
        {
            Content = JsonContent.Create(body)
        };
        var res = http.Send(req);
        res.EnsureSuccessStatusCode();
        var json = res.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.TryGetProperty("balance", out var bal))
            return bal.GetInt64();
        return 0;
    }

    private static decimal? GetUsdtBalance(HttpClient http, string base58Address)
    {
        // triggerconstantcontract: contractAddress, functionSelector "balanceOf(address)", parameter = address (32 bytes hex)
        var addrHex = Base58ToHexAddress(base58Address);
        if (string.IsNullOrEmpty(addrHex)) return null;
        var param = addrHex.PadLeft(64, '0');
        var body = new
        {
            owner_address = base58Address,
            contract_address = UsdtTrc20Contract,
            function_selector = "balanceOf(address)",
            parameter = param
        };
        var req = new HttpRequestMessage(HttpMethod.Post, TronGridBase + "/wallet/triggerconstantcontract")
        {
            Content = JsonContent.Create(body)
        };
        var res = http.Send(req);
        res.EnsureSuccessStatusCode();
        var json = res.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.TryGetProperty("constant_result", out var arr) && arr.GetArrayLength() > 0)
        {
            var hex = arr[0].GetString();
            if (!string.IsNullOrEmpty(hex) && hex.Length >= 16)
            {
                var bytes = HexToBytes(hex);
                if (bytes.Length >= 32)
                {
                    // USDT 6 decimals
                    var raw = BytesToUInt64(bytes.AsSpan(16, 16));
                    return raw / 1_000_000m;
                }
            }
        }
        return null;
    }

    private static string? Base58ToHexAddress(string base58)
    {
        try
        {
            var decoded = NBitcoin.DataEncoders.Encoders.Base58Check.DecodeData(base58);
            if (decoded.Length != 21 || decoded[0] != 0x41) return null;
            return Convert.ToHexString(decoded.AsSpan(1)).ToLowerInvariant();
        }
        catch { return null; }
    }

    private static byte[] HexToBytes(string hex)
    {
        var list = new List<byte>();
        for (int i = 0; i + 2 <= hex.Length; i += 2)
            list.Add(Convert.ToByte(hex.Substring(i, 2), 16));
        return list.ToArray();
    }

    private static ulong BytesToUInt64(ReadOnlySpan<byte> bigEndian16)
    {
        ulong v = 0;
        for (int i = 0; i < 8 && i < bigEndian16.Length; i++)
            v = (v << 8) | bigEndian16[i];
        return v;
    }
}
