using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>
/// AuraVT — OpenSeeFaceReceiver
/// Connects to the OpenSeeFace face tracker (facetracker.exe / facetracker.py)
/// which sends face landmark data as a binary UDP stream on port 11573.
///
/// OpenSeeFace binary packet layout (verified from OSF source):
///   double  timestamp       (8 bytes)
///   int     id              (4 bytes)
///   float   width, height   (8 bytes)
///   float   eye_l, eye_r    (8 bytes)  — openness 0–1
///   float   eyebrow_steepness_l/r, eyebrow_updown_l/r, eyebrow_quirk_l/r (24 bytes)
///   float   mouth_corner_updown_l/r, mouth_corner_inout_l/r (16 bytes)
///   float   mouth_open, mouth_wide (8 bytes)
///   float   cheek_puff_l/r  (8 bytes)
///   float   nose_wrinkle    (4 bytes)
///   float   translation[3]  (12 bytes) — head position
///   float   rotation[3]     (12 bytes) — head euler angles (pitch, yaw, roll)
///   float   confidence      (4 bytes)
///   ... (features array — 68 landmarks × 2D + 3D = we skip these)
///
/// We map this to a FaceData struct that BlendshapeDriver consumes.
/// </summary>
public class OpenSeeFaceReceiver : MonoBehaviour
{
    // ── Parsed face data (thread-safe) ────────────────────────────────────────
    public struct FaceData
    {
        public float EyeLeft, EyeRight;           // 0=closed, 1=open
        public float MouthOpen, MouthWide;
        public float MouthCornerUpL, MouthCornerUpR;
        public float BrowUpLeft, BrowUpRight;
        public float BrowSteepLeft, BrowSteepRight;
        public float CheekPuffLeft, CheekPuffRight;
        public Vector3 HeadTranslation;
        public Vector3 HeadRotation;              // Euler: pitch, yaw, roll
        public float Confidence;
        public double Timestamp;
    }

    public FaceData  CurrentData  { get; private set; }
    public bool      HasData      { get; private set; }
    public float     LastReceiveTime { get; private set; }

    // ── Config ────────────────────────────────────────────────────────────────
    [SerializeField] private FaceTrackingConfig config;

    // ── Internals ─────────────────────────────────────────────────────────────
    private UdpClient  _udp;
    private Thread     _thread;
    private bool       _running;
    private FaceData   _pendingData;
    private bool       _pendingReady;
    private readonly object _lock = new object();

    private const int OSF_PACKET_SIZE = 1785;  // Full OSF packet with 68 landmarks

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public void StartListening(string ip, int port)
    {
        StopListening();
        try
        {
            _udp     = new UdpClient(port);
            _running = true;
            _thread  = new Thread(ReceiveLoop) { IsBackground = true, Name = "AuraVT_OSF" };
            _thread.Start();
            Debug.Log($"[AuraVT] OpenSeeFaceReceiver: Listening on UDP port {port}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AuraVT] OpenSeeFaceReceiver: Failed to bind port {port}: {ex.Message}");
        }
    }

    public void StopListening()
    {
        _running = false;
        _udp?.Close();
        _thread?.Join(200);
        _udp = null; _thread = null;
    }

    void OnDestroy() => StopListening();

    // ── Main thread poll — call from BlendshapeDriver.Update() ───────────────
    public bool TryGetLatest(out FaceData data)
    {
        lock (_lock)
        {
            if (_pendingReady)
            {
                data          = _pendingData;
                _pendingReady = false;
                HasData       = true;
                LastReceiveTime = Time.realtimeSinceStartup;
                return true;
            }
        }
        data = default;
        return false;
    }

    // ── Background receive loop ───────────────────────────────────────────────

    private void ReceiveLoop()
    {
        var endpoint = new IPEndPoint(IPAddress.Any, 0);
        while (_running)
        {
            try
            {
                byte[] pkt = _udp.Receive(ref endpoint);
                if (pkt.Length < 100) continue;   // Too short to be valid

                var fd = ParsePacket(pkt);
                if (fd.Confidence < 0.3f) continue;  // Low confidence — skip

                lock (_lock)
                {
                    _pendingData  = fd;
                    _pendingReady = true;
                }
            }
            catch (SocketException) { break; }
            catch (Exception ex) { Debug.LogWarning($"[AuraVT] OSF parse: {ex.Message}"); }
        }
    }

    // ── Packet parser ─────────────────────────────────────────────────────────

    private static FaceData ParsePacket(byte[] pkt)
    {
        using var ms     = new MemoryStream(pkt);
        using var reader = new BinaryReader(ms, Encoding.UTF8, false);

        var fd = new FaceData();

        fd.Timestamp    = reader.ReadDouble();   // 8
        int id          = reader.ReadInt32();    // 4
        float width     = reader.ReadSingle();   // 4
        float height    = reader.ReadSingle();   // 4

        fd.EyeLeft      = reader.ReadSingle();   // 4
        fd.EyeRight     = reader.ReadSingle();   // 4

        // Eyebrow data (6 floats)
        fd.BrowSteepLeft  = reader.ReadSingle();
        fd.BrowSteepRight = reader.ReadSingle();
        fd.BrowUpLeft     = reader.ReadSingle();
        fd.BrowUpRight    = reader.ReadSingle();
        float browQuirkL  = reader.ReadSingle();
        float browQuirkR  = reader.ReadSingle();

        // Mouth corners (4 floats)
        fd.MouthCornerUpL = reader.ReadSingle();
        fd.MouthCornerUpR = reader.ReadSingle();
        float mCornerInL  = reader.ReadSingle();
        float mCornerInR  = reader.ReadSingle();

        fd.MouthOpen    = reader.ReadSingle();   // 4
        fd.MouthWide    = reader.ReadSingle();   // 4

        fd.CheekPuffLeft  = reader.ReadSingle(); // 4
        fd.CheekPuffRight = reader.ReadSingle(); // 4

        float noseWrinkle = reader.ReadSingle(); // 4

        // Head pose
        fd.HeadTranslation = new Vector3(
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle()
        );
        fd.HeadRotation = new Vector3(
            reader.ReadSingle(),  // pitch
            reader.ReadSingle(),  // yaw
            reader.ReadSingle()   // roll
        );

        fd.Confidence = reader.ReadSingle();

        return fd;
    }
}
