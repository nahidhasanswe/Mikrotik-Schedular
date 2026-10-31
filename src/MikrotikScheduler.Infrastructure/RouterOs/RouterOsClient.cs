using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using MikrotikScheduler.Application.Common;

namespace MikrotikScheduler.Infrastructure.RouterOs;

/// <summary>Minimal implementation of the MikroTik RouterOS binary API (port 8728 / 8729 for TLS).</summary>
public class RouterOsClient : IRouterOsClient
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(8);

    public async Task<IRouterOsSession> ConnectAsync(RouterConnection c, CancellationToken ct)
    {
        var tcp = new TcpClient();
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(ConnectTimeout);
            try
            {
                await tcp.ConnectAsync(c.Host, c.Port, cts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new RouterConnectionException($"Timed out connecting to {c.Host}:{c.Port}.");
            }

            Stream stream = tcp.GetStream();
            if (c.UseSsl)
            {
                // RouterOS ships with self-signed certificates, so validation is intentionally relaxed.
                var ssl = new SslStream(stream, false, (_, _, _, _) => true);
                await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = c.Host }, cts.Token);
                stream = ssl;
            }

            var session = new RouterOsSession(tcp, stream);
            await session.LoginAsync(c.Username, c.Password, ct);
            return session;
        }
        catch (RouterConnectionException)
        {
            tcp.Dispose();
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            tcp.Dispose();
            throw new RouterConnectionException($"Cannot connect to {c.Host}:{c.Port}: {ex.Message}", ex);
        }
    }
}

internal sealed class RouterOsSession(TcpClient tcp, Stream stream) : IRouterOsSession
{
    private static readonly TimeSpan IoTimeout = TimeSpan.FromSeconds(15);

    public async Task LoginAsync(string user, string password, CancellationToken ct)
    {
        var reply = await ExecuteAsync(["/login", $"=name={user}", $"=password={password}"], ct);
        // RouterOS < 6.43 answers with a challenge instead of accepting the plain password.
        if (reply.Count > 0 && reply[0].TryGetValue("ret", out var challenge))
        {
            var chal = Convert.FromHexString(challenge);
            var data = new byte[1 + Encoding.UTF8.GetByteCount(password) + chal.Length];
            Encoding.UTF8.GetBytes(password, 0, password.Length, data, 1);
            chal.CopyTo(data, data.Length - chal.Length);
            var response = "00" + Convert.ToHexString(MD5.HashData(data)).ToLowerInvariant();
            await ExecuteAsync(["/login", $"=name={user}", $"=response={response}"], ct);
        }
    }

    public async Task<IReadOnlyList<PppSecret>> GetPppSecretsAsync(CancellationToken ct)
    {
        var rows = await ExecuteAsync(["/ppp/secret/print"], ct);
        return rows.Select(r => new PppSecret(
                r.GetValueOrDefault(".id") ?? "",
                r.GetValueOrDefault("name") ?? "",
                string.Equals(r.GetValueOrDefault("disabled"), "true", StringComparison.OrdinalIgnoreCase)))
            .Where(s => s.Id.Length > 0 && s.Name.Length > 0)
            .ToList();
    }

    public Task EnablePppSecretAsync(string id, CancellationToken ct) =>
        ExecuteAsync(["/ppp/secret/set", $"=.id={id}", "=disabled=no"], ct);

    // ---- protocol ----
    private async Task<List<Dictionary<string, string>>> ExecuteAsync(string[] words, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(IoTimeout);
        try
        {
            var buffer = new MemoryStream();
            foreach (var w in words) WriteWord(buffer, w);
            buffer.WriteByte(0);
            await stream.WriteAsync(buffer.ToArray(), cts.Token);
            await stream.FlushAsync(cts.Token);

            var rows = new List<Dictionary<string, string>>();
            string? error = null;
            while (true)
            {
                var sentence = await ReadSentenceAsync(cts.Token);
                if (sentence.Count == 0) continue;
                var type = sentence[0];
                var attrs = new Dictionary<string, string>();
                foreach (var w in sentence.Skip(1))
                {
                    if (w.Length < 2 || w[0] != '=') continue;
                    var eq = w.IndexOf('=', 1);
                    if (eq < 0) attrs[w[1..]] = "";
                    else attrs[w[1..eq]] = w[(eq + 1)..];
                }

                switch (type)
                {
                    case "!re": rows.Add(attrs); break;
                    case "!trap": error = attrs.GetValueOrDefault("message") ?? "RouterOS error."; break;
                    case "!fatal": throw new RouterConnectionException($"RouterOS fatal: {string.Join(' ', sentence.Skip(1))}");
                    case "!done":
                        if (error != null) throw new RouterConnectionException(error);
                        if (attrs.Count > 0 && rows.Count == 0) rows.Add(attrs); // e.g. login challenge (=ret=)
                        return rows;
                }
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new RouterConnectionException("Router did not respond in time.");
        }
    }

    private async Task<List<string>> ReadSentenceAsync(CancellationToken ct)
    {
        var words = new List<string>();
        while (true)
        {
            var len = await ReadLengthAsync(ct);
            if (len == 0) return words;
            var data = new byte[len];
            await stream.ReadExactlyAsync(data, ct);
            words.Add(Encoding.UTF8.GetString(data));
        }
    }

    private async Task<int> ReadLengthAsync(CancellationToken ct)
    {
        var one = new byte[1];
        await stream.ReadExactlyAsync(one, ct);
        int b = one[0];
        if ((b & 0x80) == 0) return b;
        int extra, value;
        if ((b & 0xC0) == 0x80) { extra = 1; value = b & 0x3F; }
        else if ((b & 0xE0) == 0xC0) { extra = 2; value = b & 0x1F; }
        else if ((b & 0xF0) == 0xE0) { extra = 3; value = b & 0x0F; }
        else { extra = 4; value = 0; }
        var rest = new byte[extra];
        await stream.ReadExactlyAsync(rest, ct);
        foreach (var r in rest) value = (value << 8) | r;
        return value;
    }

    private static void WriteWord(Stream s, string word)
    {
        var bytes = Encoding.UTF8.GetBytes(word);
        var len = bytes.Length;
        if (len < 0x80) s.WriteByte((byte)len);
        else if (len < 0x4000) { len |= 0x8000; s.WriteByte((byte)(len >> 8)); s.WriteByte((byte)len); }
        else if (len < 0x200000) { len |= 0xC00000; s.WriteByte((byte)(len >> 16)); s.WriteByte((byte)(len >> 8)); s.WriteByte((byte)len); }
        else { len |= unchecked((int)0xE0000000); s.WriteByte((byte)(len >> 24)); s.WriteByte((byte)(len >> 16)); s.WriteByte((byte)(len >> 8)); s.WriteByte((byte)len); }
        s.Write(bytes);
    }

    public ValueTask DisposeAsync()
    {
        try { stream.Dispose(); } catch { /* ignore */ }
        tcp.Dispose();
        return ValueTask.CompletedTask;
    }
}
