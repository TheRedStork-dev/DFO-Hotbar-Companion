using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace DfoHotbarCompanion;

public sealed record LiveDungeonMessage(string Action,string? Key,string? Name,string? RegionName,int RegionIndex,int MapId,int DungeonType,int X,int Y,bool Elite,int Blocks,double PlayerX=0,double PlayerZ=0,double PlayerYaw=0);

public sealed class LiveBridgeServer : IDisposable
{
    readonly TcpListener listener = new(IPAddress.Loopback, 42113);
    readonly CancellationTokenSource stop = new();
    public event Action<LiveDungeonMessage>? Message;
    public event Action<Exception>? Error;
    public void Start() { listener.Start(); _ = AcceptLoop(); }
    async Task AcceptLoop() { while(!stop.IsCancellationRequested) { try { var client=await listener.AcceptTcpClientAsync(stop.Token); _=Handle(client); } catch(OperationCanceledException){break;} catch { if(!stop.IsCancellationRequested) await Task.Delay(250); } } }
    async Task Handle(TcpClient client)
    {
        using(client) using(var stream=client.GetStream()) try
        {
            var headerBytes=new List<byte>(); var one=new byte[1];
            while(headerBytes.Count<16384) { if(await stream.ReadAsync(one,stop.Token)==0)return; headerBytes.Add(one[0]); int n=headerBytes.Count; if(n>=4&&headerBytes[n-4]==13&&headerBytes[n-3]==10&&headerBytes[n-2]==13&&headerBytes[n-1]==10)break; }
            string headers=Encoding.ASCII.GetString(headerBytes.ToArray()); int length=0;
            foreach(string line in headers.Split("\r\n")) if(line.StartsWith("Content-Length:",StringComparison.OrdinalIgnoreCase)) int.TryParse(line[15..].Trim(),out length);
            byte[] body=Array.Empty<byte>();
            if(length>0&&length<=65536) { body=new byte[length]; int read=0; while(read<length){int got=await stream.ReadAsync(body.AsMemory(read,length-read),stop.Token);if(got==0)break;read+=got;}if(read!=length)body=Array.Empty<byte>(); }
            else if(headers.Contains("Transfer-Encoding: chunked",StringComparison.OrdinalIgnoreCase)) body=await ReadChunked(stream);
            if(body.Length>0) { var msg=JsonSerializer.Deserialize<LiveDungeonMessage>(body,new JsonSerializerOptions{PropertyNameCaseInsensitive=true}); if(msg!=null)Message?.Invoke(msg); }
            const string response="HTTP/1.1 204 No Content\r\nAccess-Control-Allow-Origin: *\r\nAccess-Control-Allow-Methods: POST, OPTIONS\r\nAccess-Control-Allow-Headers: Content-Type\r\nConnection: close\r\nContent-Length: 0\r\n\r\n";
            await stream.WriteAsync(Encoding.ASCII.GetBytes(response),stop.Token);
        }
        catch(Exception ex) { Error?.Invoke(ex); }
    }
    async Task<byte[]> ReadChunked(NetworkStream stream)
    {
        using var body=new MemoryStream();
        while(body.Length<=65536) { string line=await ReadLine(stream); if(!int.TryParse(line.Split(';')[0],System.Globalization.NumberStyles.HexNumber,null,out int size)||size<0)break; if(size==0){await ReadLine(stream);break;} var chunk=new byte[size];int read=0;while(read<size){int got=await stream.ReadAsync(chunk.AsMemory(read,size-read),stop.Token);if(got==0)return Array.Empty<byte>();read+=got;}await body.WriteAsync(chunk,stop.Token);await ReadLine(stream); }
        return body.Length<=65536?body.ToArray():Array.Empty<byte>();
    }
    async Task<string> ReadLine(NetworkStream stream) { var bytes=new List<byte>();var one=new byte[1];while(bytes.Count<128){if(await stream.ReadAsync(one,stop.Token)==0)break;if(one[0]==10)break;if(one[0]!=13)bytes.Add(one[0]);}return Encoding.ASCII.GetString(bytes.ToArray()); }
    public void Dispose() { stop.Cancel();listener.Stop();stop.Dispose(); }
}

