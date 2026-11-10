using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

// Plain .NET worker: no Unity calls, sensor simulation, or breathing-phase inference.
// UDP is for a trusted experiment LAN. Native belt acquisition is supplied by the PC adapter.
public sealed class BreathStreamRecorder : IDisposable
{
    public const int DefaultPort=5006;
    const double SignalTimeout=1.5, SyncTimeout=10, SyncWindow=30;
    static readonly CultureInfo Invariant=CultureInfo.InvariantCulture;
    readonly string prefix,session;
    readonly double appAnchor,monoAnchor;
    readonly int port;
    readonly object gate=new object();
    readonly BlockingCollection<Row> rows=new BlockingCollection<Row>(4096);
    readonly List<ClockPoint> clocks=new List<ClockPoint>();
    UdpClient socket;
    Thread receiver,writer;
    IPEndPoint peer;
    string bridge;
    double peerSeen=-1,lastSample=-1,lastSync=-1;
    long sequence=-1,sequenceGaps,malformed,dropped,samples,mappedSamples;
    long pingSequence=-1;
    double pingT1,pingT2,pingT3;
    volatile bool stopping;
    volatile string status="waiting_for_belt",error;
    Context context;
    public string Status=>status;
    public string Error=>error;
    public string SessionId=>session;
    public string RespirationPath=>prefix+"_respiration.csv";

    struct Context
    {
        public int frame,group,phase,question;
        public double transition;
        public bool active;
    }
    struct ClockPoint {public double at,offset,rtt;}
    struct Row
    {
        public string kind,state,bridge,unit,timeBasis,sequenceState;
        public double received,pcTime,deviceTime,value,mapped,offset,rtt,t1,t2,t3,t4;
        public bool hasDevice,mappingValid;
        public long sequence,gap;
        public Context context;
    }
    public BreathStreamRecorder(string eyeCsvPath,double appSeconds,double stopwatchSeconds,int listenPort=DefaultPort)
    {
        prefix=Path.Combine(Path.GetDirectoryName(eyeCsvPath),Path.GetFileNameWithoutExtension(eyeCsvPath));
        session=Path.GetFileNameWithoutExtension(eyeCsvPath);
        appAnchor=appSeconds;monoAnchor=stopwatchSeconds;port=listenPort;
        context.active=true;context.question=-1;
        StreamWriter breath=null,sync=null;
        try {
            breath=Open(RespirationPath);
            sync=Open(prefix+"_clock_sync.csv");
            breath.WriteLine("# schema=1;session_id="+session+";app_seconds=estimated_PC_sample_time_on_Unity_app_clock;context=latest_Unity_frame_at_receive");
            breath.WriteLine("kind,status,receive_app_seconds,app_seconds,source_pc_seconds,device_seconds,time_basis,value,unit,bridge_id,sequence,sequence_state,sequence_gap_observed,sync_valid,clock_offset_seconds,rtt_seconds,context_frame_at_receive,group_at_receive,phase_at_receive,question_at_receive,transition_app_seconds,app_active,queue_dropped_rows");
            sync.WriteLine("# schema=1;session_id="+session+";offset_maps_PC_monotonic_to_Unity_app_clock");
            sync.WriteLine("receive_app_seconds,bridge_id,sequence,pc_t1,quest_t2,quest_t3,pc_t4,offset_seconds,rtt_seconds");
            breath.Flush();sync.Flush();WriteManifest(false);
            var breathOutput=breath;var syncOutput=sync;
            writer=new Thread(()=>WriteRows(breathOutput,syncOutput)){IsBackground=true,Name="Serene respiration CSV"};
            writer.Start();
        } catch {breath?.Dispose();sync?.Dispose();throw;}
        Enqueue(new Row{kind="state",state=status,received=Now,context=Snapshot()});
        try {
            socket=new UdpClient(new IPEndPoint(IPAddress.Any,port));
            socket.Client.ReceiveTimeout=250;
            receiver=new Thread(Receive){IsBackground=true,Name="Serene respiration UDP"};receiver.Start();
        } catch(Exception e) {
            socket?.Close();socket=null;error=e.Message;SetStatus("listener_unavailable");
        }
    }
    static StreamWriter Open(string path)=>new StreamWriter(path,false,new UTF8Encoding(true),65536);
    public static double MonotonicSeconds=>(double)Stopwatch.GetTimestamp()/Stopwatch.Frequency;
    double Now=>appAnchor+(MonotonicSeconds-monoAnchor);
    // Called once per Unity frame, with only primitive values. Never performs I/O.
    public void SetContext(int frame,int group,int phase,int question,double transition,bool active)
    {
        lock(gate)context=new Context{frame=frame,group=group,phase=phase,question=question,transition=transition,active=active};
    }
    Context Snapshot() {lock(gate)return context;}
    void Enqueue(Row row)
    {
        try {if(!rows.TryAdd(row))Interlocked.Increment(ref dropped);}
        catch(InvalidOperationException) {Interlocked.Increment(ref dropped);}
    }
    void SetStatus(string next)
    {
        if(status==next)return;
        status=next;Enqueue(new Row{kind="state",state=next,received=Now,bridge=bridge,context=Snapshot()});
    }
    void CheckStatus()
    {
        if(!Snapshot().active)SetStatus("app_inactive");
        else if(lastSample<0)SetStatus("waiting_for_belt");
        else if(Now-lastSample>SignalTimeout)SetStatus("signal_lost");
        else SetStatus("receiving");
    }
    void Receive()
    {
        try {
            while(!stopping) {
                try {
                    var sender=new IPEndPoint(IPAddress.Any,0);
                    byte[] packet=socket.Receive(ref sender);
                    double received=Now;
                    if(packet.Length>2048 || !Handle(Encoding.ASCII.GetString(packet),sender,received))Interlocked.Increment(ref malformed);
                } catch(SocketException e) {
                    if(stopping)break;
                    if(e.SocketErrorCode!=SocketError.TimedOut && e.SocketErrorCode!=SocketError.WouldBlock &&
                       e.SocketErrorCode!=SocketError.ConnectionReset && e.SocketErrorCode!=SocketError.ConnectionRefused)throw;
                }
                CheckStatus();
            }
        } catch(ObjectDisposedException) {if(!stopping){error="UDP socket closed unexpectedly";SetStatus("network_error");}}
        catch(Exception e) {if(!stopping){error=e.Message;SetStatus("network_error");}}
    }
    bool Handle(string message,IPEndPoint sender,double received)
    {
        string[] p=message.Split('|');
        if(p.Length<3 || p[1]!="1")return false;
        if(p[0]=="HELLO" && p.Length==3 && Identifier(p[2],64)) {
            if(peer!=null && (!peer.Equals(sender) || bridge!=p[2]) && received-peerSeen<=3)return false;
            if(peer==null || !peer.Equals(sender) || bridge!=p[2]) {
                peer=sender;bridge=p[2];clocks.Clear();lastSync=-1;sequence=-1;pingSequence=-1;lastSample=-1;
                Enqueue(new Row{kind="state",state="bridge_connected",received=received,bridge=bridge,context=Snapshot()});
            }
            peerSeen=received;Send("WELCOME|1|"+session,sender);return true;
        }
        if(peer==null || !peer.Equals(sender) || p.Length<4 || p[2]!=session || p[3]!=bridge)return false;
        if(p[0]=="PING" && p.Length==6 && Integer(p[4],out long ping) && Number(p[5],out double t1) && t1>=0) {
            pingSequence=ping;pingT1=t1;pingT2=received;pingT3=Now;peerSeen=received;
            Send("PONG|1|"+session+"|"+bridge+"|"+ping+"|"+N(t1)+"|"+N(pingT2)+"|"+N(pingT3),sender);return true;
        }
        if(p[0]=="SYNC" && p.Length==9 && Integer(p[4],out long syncSeq) && syncSeq==pingSequence &&
           Number(p[5],out t1) && Number(p[6],out double t2) && Number(p[7],out double t3) && Number(p[8],out double t4) &&
           t1==pingT1 && t2==pingT2 && t3==pingT3 && t4>=t1 && received-t3<=2) {
            double rtt=(t4-t1)-(t3-t2),offset=((t2-t1)+(t3-t4))/2;
            if(!(rtt>=0 && rtt<=2) || double.IsNaN(offset) || double.IsInfinity(offset))return false;
            pingSequence=-1;peerSeen=received;lastSync=received;
            clocks.RemoveAll(point=>received-point.at>SyncWindow);
            clocks.Add(new ClockPoint{at=received,offset=offset,rtt=rtt});
            Enqueue(new Row{kind="sync",received=received,bridge=bridge,sequence=syncSeq,t1=t1,t2=t2,t3=t3,t4=t4,offset=offset,rtt=rtt});return true;
        }
        if(p[0]!="BREATH" || p.Length!=10 || !Integer(p[4],out long seq) || !Number(p[5],out double pcTime) || pcTime<0 ||
           !Number(p[6],out double value) || !Identifier(p[7],32) ||
           (p[9]!="pc_receive" && p[9]!="pc_acquisition"))return false;
        double deviceTime=0;bool hasDevice=p[8].Length>0;
        if(hasDevice && !Number(p[8],out deviceTime))return false;
        var best=new ClockPoint{rtt=double.PositiveInfinity};int recent=0;
        foreach(var point in clocks)if(received-point.at<=SyncWindow) {recent++;if(point.rtt<best.rtt)best=point;}
        bool mapping=recent>=3 && lastSync>=0 && received-lastSync<=SyncTimeout;
        long gap=sequence>=0 && seq>sequence?seq-sequence-1:0;
        string seqState=seq<=sequence?"out_of_order_or_duplicate":"in_order";
        if(seq>sequence) {sequence=seq;lastSample=received;}
        sequenceGaps+=gap;peerSeen=received;Interlocked.Increment(ref samples);
        if(mapping)Interlocked.Increment(ref mappedSamples);
        var ctx=Snapshot();
        Enqueue(new Row{kind="sample",state=ctx.active?"received":"app_inactive",received=received,
            bridge=bridge,sequence=seq,pcTime=pcTime,value=value,unit=p[7],hasDevice=hasDevice,deviceTime=deviceTime,
            timeBasis=p[9],mapped=mapping?pcTime+best.offset:0,mappingValid=mapping,offset=best.offset,rtt=best.rtt,
            gap=gap,sequenceState=seqState,context=ctx});return true;
    }
    void Send(string value,IPEndPoint target) {byte[] bytes=Encoding.ASCII.GetBytes(value);socket.Send(bytes,bytes.Length,target);}
    static bool Identifier(string s,int limit)
    {
        if(s.Length==0 || s.Length>limit)return false;
        foreach(char c in s)if(!(c>='a'&&c<='z') && !(c>='A'&&c<='Z') && !(c>='0'&&c<='9') && "_.:/-".IndexOf(c)<0)return false;
        return true;
    }
    static bool Integer(string s,out long value)=>long.TryParse(s,NumberStyles.None,Invariant,out value) && value>=0 && value<long.MaxValue;
    static bool Number(string s,out double value)=>double.TryParse(s,NumberStyles.Float,Invariant,out value) && !double.IsNaN(value) && !double.IsInfinity(value);
    static string N(double value)=>value.ToString("R",Invariant);
    void WriteRows(StreamWriter breath,StreamWriter sync)
    {
        try {
            using(breath)using(sync) {
                var timer=Stopwatch.StartNew();
                while(!rows.IsCompleted) {
                    if(rows.TryTake(out Row r,250)) {
                        if(r.kind=="sync")sync.WriteLine(string.Join(",",N(r.received),r.bridge,r.sequence.ToString(Invariant),N(r.t1),N(r.t2),N(r.t3),N(r.t4),N(r.offset),N(r.rtt)));
                        else {
                            bool sample=r.kind=="sample";
                            breath.WriteLine(string.Join(",",r.kind,r.state,N(r.received),sample&&r.mappingValid?N(r.mapped):"",sample?N(r.pcTime):"",
                                sample&&r.hasDevice?N(r.deviceTime):"",r.timeBasis,sample?N(r.value):"",r.unit,r.bridge,sample?r.sequence.ToString(Invariant):"",
                                r.sequenceState,sample?r.gap.ToString(Invariant):"",sample?(r.mappingValid?"1":"0"):"",sample&&r.mappingValid?N(r.offset):"",
                                sample&&r.mappingValid?N(r.rtt):"",r.context.frame.ToString(Invariant),r.context.group.ToString(Invariant),r.context.phase.ToString(Invariant),
                                r.context.question.ToString(Invariant),N(r.context.transition),r.context.active?"1":"0",Interlocked.Read(ref dropped).ToString(Invariant)));
                        }
                    }
                    if(timer.ElapsedMilliseconds>=1000) {breath.Flush();sync.Flush();timer.Restart();}
                }
                breath.Flush();sync.Flush();
            }
        } catch(Exception e) {error=e.Message;status="write_error";}
        finally {try {WriteManifest(true);}catch(Exception e){error=e.Message;}}
    }
    static string Quote(string s)
    {
        var b=new StringBuilder("\"");
        foreach(char c in s??"") {
            if(c=='"' || c=='\\')b.Append('\\').Append(c);
            else if(c<32)b.Append("\\u").Append(((int)c).ToString("x4",Invariant));
            else b.Append(c);
        }
        return b.Append('"').ToString();
    }
    void WriteManifest(bool completed)
    {
        string json="{\n  \"schema\":1,\n  \"session_id\":"+Quote(session)+",\n  \"eye_csv\":"+Quote(Path.GetFileName(prefix)+".csv")+",\n"+
            "  \"respiration_csv\":"+Quote(Path.GetFileName(RespirationPath))+",\n  \"clock_sync_csv\":"+Quote(Path.GetFileName(prefix)+"_clock_sync.csv")+",\n"+
            "  \"app_anchor_seconds\":"+N(appAnchor)+",\n  \"stopwatch_anchor_seconds\":"+N(monoAnchor)+",\n  \"udp_port\":"+port+",\n"+
            "  \"eye_time_basis\":\"Unity frame snapshot; input_update_seconds is not native eye acquisition time\",\n"+
            "  \"respiration_time_basis\":\"PC monotonic clock mapped by minimum-RTT calibration in a 30-second window; at least 3 exchanges; expires after 10 seconds\",\n"+
            "  \"accuracy_note\":\"Software clock estimate only. RTT is not total sensor latency or a proven error bound. Device-clock mapping and physical accuracy need belt-specific validation.\",\n"+
            "  \"completed\":"+(completed?"true":"false")+",\n  \"status\":"+Quote(status)+",\n  \"error\":"+Quote(error)+",\n"+
            "  \"samples_received\":"+Interlocked.Read(ref samples)+",\n  \"samples_clock_mapped\":"+Interlocked.Read(ref mappedSamples)+",\n"+
            "  \"sequence_gaps_observed\":"+sequenceGaps+",\n  \"malformed_or_unaccepted_packets\":"+Interlocked.Read(ref malformed)+",\n"+
            "  \"queue_dropped_rows\":"+Interlocked.Read(ref dropped)+"\n}\n";
        File.WriteAllText(prefix+"_session.json",json,new UTF8Encoding(false));
    }
    public void Dispose()
    {
        if(stopping)return;stopping=true;socket?.Close();
        receiver?.Join(1000);
        Enqueue(new Row{kind="state",state="stopped",received=Now,bridge=bridge,context=Snapshot()});
        rows.CompleteAdding();writer?.Join(2000);
    }
}
