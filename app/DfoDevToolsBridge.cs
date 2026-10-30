using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace DfoHotbarCompanion;

public sealed class DfoDevToolsBridge
{
    public const int DebugPort = 42114;
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(2) };
    public static string InstalledExe => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "DFO Online", "DFO Online.exe");
    public async Task<bool> TryInjectAsync()
    {
        try
        {
            using var doc=JsonDocument.Parse(await Http.GetStringAsync($"http://127.0.0.1:{DebugPort}/json"));
            var page=doc.RootElement.EnumerateArray().FirstOrDefault(x=>x.TryGetProperty("type",out var type)&&type.GetString()=="page"&&x.TryGetProperty("url",out var url)&&(url.GetString()?.Contains("/play/",StringComparison.OrdinalIgnoreCase)??false));
            if(page.ValueKind==JsonValueKind.Undefined||!page.TryGetProperty("webSocketDebuggerUrl",out var socketUrl))return false;
            using var socket=new ClientWebSocket();await socket.ConnectAsync(new Uri(socketUrl.GetString()!),CancellationToken.None);
            string command=JsonSerializer.Serialize(new{id=1,method="Runtime.evaluate",@params=new{expression=BridgeScript,awaitPromise=false,returnByValue=true}});
            await socket.SendAsync(Encoding.UTF8.GetBytes(command),WebSocketMessageType.Text,true,CancellationToken.None);
            var buffer=new byte[4096];using var timeout=new CancellationTokenSource(1500);try{await socket.ReceiveAsync(buffer,timeout.Token);}catch{}
            try{await socket.CloseAsync(WebSocketCloseStatus.NormalClosure,"done",CancellationToken.None);}catch{}
            return true;
        }
        catch{return false;}
    }
    public bool IsDfoRunning()=>Process.GetProcessesByName("DFO Online").Length>0;
    public bool Launch(){if(!File.Exists(InstalledExe))return false;Process.Start(new ProcessStartInfo{FileName=InstalledExe,Arguments=$"--remote-debugging-port={DebugPort} --remote-debugging-address=127.0.0.1",UseShellExecute=true});return true;}
    const string BridgeScript="""
(()=>{
 if(window.__dflLiveBridgeVersion===2)return 'already';
 window.__dflLiveBridgeVersion=2;
 const endpoint='http://127.0.0.1:42113/live';
 const send=o=>fetch(endpoint,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(o)}).catch(()=>{});
 const message=(action,key,v)=>{const m=v?.mapTableData;if(!m||!v?.spawned)return;send({action,key:String(key),name:String(v.name||''),regionName:String(v.regionName||''),regionIndex:Number(v.regionIndex)||0,mapId:Number(m.mapId)||0,dungeonType:Number(m.dungeonType)||0,x:Math.trunc(Number(m.longitude||0)/128),y:499-Math.trunc(Number(m.latitude||0)/128),elite:!!v.elite,blocks:Number(v.dungeon?.blocks?.length)||0});};
 const set=Map.prototype.set,del=Map.prototype.delete;
 Map.prototype.set=function(k,v){const r=set.call(this,k,v);message('upsert',k,v);return r;};
 Map.prototype.delete=function(k){const v=this.get(k);const r=del.call(this,k);if(r)message('remove',k,v);return r;};
 const wsSend=WebSocket.prototype.send;let lastPoseAt=0;
 WebSocket.prototype.send=function(data){try{if(typeof data==='string'){const o=JSON.parse(data),p=o?.t==='pose'?o.p:o?.t==='hello'?o.pose:null,now=performance.now();if(p&&Number.isFinite(p.x)&&Number.isFinite(p.z)&&now-lastPoseAt>=250){lastPoseAt=now;send({action:'player',key:'',name:'',regionName:'',regionIndex:0,mapId:0,dungeonType:0,x:0,y:0,elite:false,blocks:0,playerX:p.x,playerZ:p.z,playerYaw:Number(p.yaw)||0});}}}catch{}return wsSend.call(this,data);};
 send({action:'reset',key:'',name:'',regionName:'',regionIndex:0,mapId:0,dungeonType:0,x:0,y:0,elite:false,blocks:0});
 return 'installed';
})()
""";
}

