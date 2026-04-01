using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>
/// AuraVT — OSCReceiver
/// Listens on a UDP port for VMC protocol messages (OSC format).
/// Compatible with VSeeFace, VTube Studio, MeowFace, and any VMC sender.
///
/// VMC messages we handle:
///   /VMC/Ext/Blend/Val  string name, float value  → blendshape weight
///   /VMC/Ext/Blend/Apply                           → commit blendshapes
///   /VMC/Ext/Bone/Pos   string name, float[7]     → bone pos+rot
///   /VMC/Ext/Root/Pos   string name, float[7]     → root transform
///
/// OSC wire format (simplified):
///   - Address string (null-padded to 4-byte boundary)
///   - Type tag string starting with ',' (null-padded)
///   - Arguments (big-endian, 4-byte aligned)
/// </summary>
public class OSCReceiver : MonoBehaviour
{
    // ── Public data (written by background thread, read by main thread) ────────
    // Lock _dataLock before accessing.
    public readonly Dictionary<string, float> BlendShapes = new Dictionary<string, float>();
    public readonly Dictionary<string, (Vector3 pos, Quaternion rot)> Bones
        = new Dictionary<string, (Vector3, Quaternion)>();
    public Vector3    RootPosition;
    public Quaternion RootRotation = Quaternion.identity;
    public bool       DataReady;
    public float      LastReceiveTime;

    // ── Config ────────────────────────────────────────────────────────────────
    [SerializeField] private FaceTrackingConfig config;

    // ── Internals ─────────────────────────────────────────────────────────────
    private UdpClient  _udp;
    private Thread     _thread;
    private bool       _running;
    private readonly object _dataLock = new object();

    // Staging buffer — background thread writes here, main thread swaps on Apply
    private readonly Dictionary<string, float> _stagingBlends = new Dictionary<string, float>();

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public void StartListening(int port)
    {
        StopListening();
        try
        {
            _udp     = new UdpClient(port);
            _running = true;
            _thread  = new Thread(ReceiveLoop) { IsBackground = true, Name = "AuraVT_OSC" };
            _thread.Start();
            Debug.Log($"[AuraVT] OSCReceiver: Listening on UDP port {port}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AuraVT] OSCReceiver: Failed to bind port {port}: {ex.Message}");
        }
    }

    public void StopListening()
    {
        _running = false;
        _udp?.Close();
        _thread?.Join(200);
        _udp    = null;
        _thread = null;
    }

    void OnDestroy() => StopListening();

    // ── Background receive loop ───────────────────────────────────────────────

    private void ReceiveLoop()
    {
        var endpoint = new IPEndPoint(IPAddress.Any, 0);
        while (_running)
        {
            try
            {
                byte[] data = _udp.Receive(ref endpoint);
                ParseOSCPacket(data);
            }
            catch (SocketException) { /* UDP closed — exit */ break; }
            catch (Exception ex)   { Debug.LogWarning($"[AuraVT] OSC parse error: {ex.Message}"); }
        }
    }

    // ── OSC Parser ────────────────────────────────────────────────────────────

    private void ParseOSCPacket(byte[] data)
    {
        int offset = 0;

        // Read address string
        string address = ReadOSCString(data, ref offset);
        if (string.IsNullOrEmpty(address)) return;

        // Read type tag string (starts with ',')
        string typeTags = ReadOSCString(data, ref offset);
        if (string.IsNullOrEmpty(typeTags) || typeTags[0] != ',') return;
        typeTags = typeTags.Substring(1);  // strip leading ','

        // Parse arguments by type
        var args = new List<object>();
        foreach (char tag in typeTags)
        {
            switch (tag)
            {
                case 'f':
                    args.Add(ReadFloat(data, ref offset));
                    break;
                case 'i':
                    args.Add(ReadInt(data, ref offset));
                    break;
                case 's':
                    args.Add(ReadOSCString(data, ref offset));
                    break;
                default:
                    offset += 4; // Unknown — skip 4 bytes
                    break;
            }
        }

        DispatchMessage(address, args);
    }

    private void DispatchMessage(string address, List<object> args)
    {
        switch (address)
        {
            case "/VMC/Ext/Blend/Val":
                // args: string name, float value
                if (args.Count >= 2 && args[0] is string name && args[1] is float val)
                {
                    lock (_dataLock)
                        _stagingBlends[name] = Mathf.Clamp01(val);
                }
                break;

            case "/VMC/Ext/Blend/Apply":
                // Commit staging blendshapes to live data
                lock (_dataLock)
                {
                    foreach (var kv in _stagingBlends)
                        BlendShapes[kv.Key] = kv.Value;
                    _stagingBlends.Clear();
                    DataReady       = true;
                    LastReceiveTime = Time.realtimeSinceStartup;
                }
                break;

            case "/VMC/Ext/Bone/Pos":
                // args: string boneName, float px,py,pz, float qx,qy,qz,qw
                if (args.Count >= 8 && args[0] is string boneName)
                {
                    var pos = new Vector3((float)args[1], (float)args[2], (float)args[3]);
                    var rot = new Quaternion((float)args[4], (float)args[5],
                                            (float)args[6], (float)args[7]);
                    lock (_dataLock)
                        Bones[boneName] = (pos, rot);
                }
                break;

            case "/VMC/Ext/Root/Pos":
                if (args.Count >= 8)
                {
                    lock (_dataLock)
                    {
                        RootPosition = new Vector3((float)args[1], (float)args[2], (float)args[3]);
                        RootRotation = new Quaternion((float)args[4], (float)args[5],
                                                      (float)args[6], (float)args[7]);
                    }
                }
                break;
        }
    }

    // ── OSC Binary Helpers ────────────────────────────────────────────────────

    private static string ReadOSCString(byte[] data, ref int offset)
    {
        int start = offset;
        while (offset < data.Length && data[offset] != 0) offset++;
        string s = Encoding.UTF8.GetString(data, start, offset - start);
        // Advance to next 4-byte boundary
        offset = (offset + 4) & ~3;
        return s;
    }

    private static float ReadFloat(byte[] data, ref int offset)
    {
        if (offset + 4 > data.Length) return 0f;
        // OSC is big-endian
        byte[] buf = { data[offset + 3], data[offset + 2], data[offset + 1], data[offset] };
        offset += 4;
        return BitConverter.ToSingle(buf, 0);
    }

    private static int ReadInt(byte[] data, ref int offset)
    {
        if (offset + 4 > data.Length) return 0;
        int val = (data[offset] << 24) | (data[offset+1] << 16) |
                  (data[offset+2] << 8) | data[offset+3];
        offset += 4;
        return val;
    }
}
