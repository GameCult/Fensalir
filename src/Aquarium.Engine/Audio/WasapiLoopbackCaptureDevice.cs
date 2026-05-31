using System.Runtime.InteropServices;

namespace Aquarium.Engine.Audio;

internal sealed class WasapiLoopbackCaptureDevice : IDisposable
{
    private static readonly bool TraceAudio = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AQUARIUM_AUDIO_TRACE"));

    private readonly IAquariumAudioStemBus stemBus;
    private readonly string profileId;
    private readonly string sourceId;
    private readonly string displayName;
    private readonly Thread thread;
    private volatile bool running = true;
    private volatile bool started;
    private long sequence;

    public WasapiLoopbackCaptureDevice(
        IAquariumAudioStemBus stemBus,
        string profileId,
        string sourceId,
        string displayName)
    {
        this.stemBus = stemBus;
        this.profileId = profileId;
        this.sourceId = sourceId;
        this.displayName = displayName;
        thread = new Thread(CaptureThread)
        {
            IsBackground = true,
            Name = "Aquarium WASAPI Loopback"
        };
        thread.Start();
    }

    public void Dispose()
    {
        running = false;
        if (started)
        {
            thread.Join(TimeSpan.FromSeconds(2.0));
        }
    }

    private void CaptureThread()
    {
        started = true;
        var comInitialized = false;
        IAudioClient? audioClient = null;
        try
        {
            var hr = CoInitializeEx(IntPtr.Zero, CoInit.Multithreaded);
            if (hr < 0)
            {
                Marshal.ThrowExceptionForHR(hr);
            }

            comInitialized = true;
#pragma warning disable CA1416
            var enumeratorType = Type.GetTypeFromCLSID(MMDeviceEnumeratorClsid, throwOnError: true)
                ?? throw new InvalidOperationException("MMDeviceEnumerator COM type is not available.");
#pragma warning restore CA1416
            var deviceEnumerator = (IMMDeviceEnumerator)(Activator.CreateInstance(enumeratorType)
                ?? throw new InvalidOperationException("failed to create MMDeviceEnumerator"));
            Marshal.ThrowExceptionForHR(deviceEnumerator.GetDefaultAudioEndpoint(EDataFlow.Render, ERole.Console, out var device));
            var audioClientId = typeof(IAudioClient).GUID;
            Marshal.ThrowExceptionForHR(device.Activate(ref audioClientId, ClsCtx.All, IntPtr.Zero, out var audioClientObject));
            audioClient = (IAudioClient)audioClientObject;
            Marshal.ThrowExceptionForHR(audioClient.GetMixFormat(out var mixFormatPointer));
            try
            {
                var format = WasapiFormat.FromPointer(mixFormatPointer);
                Marshal.ThrowExceptionForHR(audioClient.Initialize(
                    AudioClientShareMode.Shared,
                    AudioClientStreamFlags.Loopback | AudioClientStreamFlags.NoPersist,
                    0,
                    0,
                    mixFormatPointer,
                    IntPtr.Zero));
                var captureClientId = typeof(IAudioCaptureClient).GUID;
                Marshal.ThrowExceptionForHR(audioClient.GetService(ref captureClientId, out var captureClientObject));
                var captureClient = (IAudioCaptureClient)captureClientObject;
                Marshal.ThrowExceptionForHR(audioClient.Start());
                Console.WriteLine($"Aquarium WASAPI loopback started: profile={profileId} {format.SampleRate} Hz, {format.Channels} channels, {format.BitsPerSample}-bit {format.SampleKind}.");

                while (running)
                {
                    Marshal.ThrowExceptionForHR(captureClient.GetNextPacketSize(out var packetFrames));
                    while (packetFrames > 0)
                    {
                        Marshal.ThrowExceptionForHR(captureClient.GetBuffer(
                            out var buffer,
                            out var frames,
                            out var flags,
                            out _,
                            out _));
                        try
                        {
                            PublishPacket(buffer, frames, flags, format);
                        }
                        finally
                        {
                            Marshal.ThrowExceptionForHR(captureClient.ReleaseBuffer(frames));
                        }

                        Marshal.ThrowExceptionForHR(captureClient.GetNextPacketSize(out packetFrames));
                    }

                    Thread.Sleep(4);
                }

                audioClient.Stop();
            }
            finally
            {
                Marshal.FreeCoTaskMem(mixFormatPointer);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Aquarium WASAPI loopback unavailable: {ex.Message}");
        }
        finally
        {
            audioClient?.Stop();
            if (comInitialized)
            {
                CoUninitialize();
            }
        }
    }

    private void PublishPacket(IntPtr buffer, int frameCount, uint flags, WasapiFormat format)
    {
        if (frameCount <= 0)
        {
            return;
        }

        var mono = (flags & AudioCaptureBufferFlagsSilent) != 0
            ? new float[frameCount]
            : format.SampleKind == WasapiSampleKind.Float32
                ? ReadFloat32(buffer, frameCount, format.Channels)
                : ReadPcm16(buffer, frameCount, format.Channels);
        stemBus.Publish(new AquariumAudioStemFrame(
            profileId,
            [new AquariumAudioStemChannel(0, "system_mix", displayName, sourceId, mono)],
            frameCount,
            format.SampleRate,
            Interlocked.Increment(ref sequence)));

        if (TraceAudio)
        {
            Console.WriteLine($"Aquarium loopback packet: profile={profileId} frames={frameCount} seq={sequence}");
        }
    }

    private static unsafe float[] ReadFloat32(IntPtr buffer, int frameCount, int channels)
    {
        var output = new float[frameCount];
        var input = (float*)buffer;
        for (var frame = 0; frame < frameCount; frame++)
        {
            var sum = 0.0f;
            for (var channel = 0; channel < channels; channel++)
            {
                sum += *input++;
            }

            output[frame] = Math.Clamp(sum / Math.Max(1, channels), -1.0f, 1.0f);
        }

        return output;
    }

    private static unsafe float[] ReadPcm16(IntPtr buffer, int frameCount, int channels)
    {
        var output = new float[frameCount];
        var input = (short*)buffer;
        for (var frame = 0; frame < frameCount; frame++)
        {
            var sum = 0.0f;
            for (var channel = 0; channel < channels; channel++)
            {
                sum += *input++ / 32768.0f;
            }

            output[frame] = Math.Clamp(sum / Math.Max(1, channels), -1.0f, 1.0f);
        }

        return output;
    }

    private readonly record struct WasapiFormat(int Channels, int SampleRate, int BitsPerSample, int BlockAlign, WasapiSampleKind SampleKind)
    {
        public static WasapiFormat FromPointer(IntPtr pointer)
        {
            var tag = Marshal.ReadInt16(pointer, 0);
            var channels = Marshal.ReadInt16(pointer, 2);
            var sampleRate = Marshal.ReadInt32(pointer, 4);
            var blockAlign = Marshal.ReadInt16(pointer, 12);
            var bitsPerSample = Marshal.ReadInt16(pointer, 14);
            var kind = tag switch
            {
                WaveFormatIeeeFloat => WasapiSampleKind.Float32,
                WaveFormatPcm => WasapiSampleKind.Pcm16,
                WaveFormatExtensible when bitsPerSample == 32 => WasapiSampleKind.Float32,
                _ => WasapiSampleKind.Pcm16
            };
            return new WasapiFormat(channels, sampleRate, bitsPerSample, blockAlign, kind);
        }
    }

    private enum WasapiSampleKind
    {
        Float32,
        Pcm16
    }

    private const uint AudioCaptureBufferFlagsSilent = 0x2;
    private const short WaveFormatPcm = 1;
    private const short WaveFormatIeeeFloat = 3;
    private const short WaveFormatExtensible = -2;

    [DllImport("ole32.dll")]
    private static extern int CoInitializeEx(IntPtr pvReserved, CoInit dwCoInit);

    [DllImport("ole32.dll")]
    private static extern void CoUninitialize();

    [Flags]
    private enum CoInit : uint
    {
        Multithreaded = 0x0
    }

    [Flags]
    private enum ClsCtx : uint
    {
        All = 23
    }

    private enum EDataFlow
    {
        Render,
        Capture,
        All
    }

    private enum ERole
    {
        Console,
        Multimedia,
        Communications
    }

    private enum AudioClientShareMode
    {
        Shared,
        Exclusive
    }

    [Flags]
    private enum AudioClientStreamFlags : uint
    {
        Loopback = 0x00020000,
        NoPersist = 0x00080000
    }

    private static readonly Guid MMDeviceEnumeratorClsid = new("BCDE0395-E52F-467C-8E3D-C4579291692E");

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(EDataFlow dataFlow, uint stateMask, out IntPtr devices);

        int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice endpoint);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        int Activate(ref Guid iid, ClsCtx clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object interfaceObject);
    }

    [ComImport]
    [Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioClient
    {
        int Initialize(AudioClientShareMode shareMode, AudioClientStreamFlags streamFlags, long bufferDuration, long periodicity, IntPtr format, IntPtr audioSessionGuid);

        int GetBufferSize(out int bufferFrameCount);

        int GetStreamLatency(out long latency);

        int GetCurrentPadding(out int paddingFrameCount);

        int IsFormatSupported(AudioClientShareMode shareMode, IntPtr format, out IntPtr closestMatch);

        int GetMixFormat(out IntPtr deviceFormat);

        int GetDevicePeriod(out long defaultDevicePeriod, out long minimumDevicePeriod);

        int Start();

        int Stop();

        int Reset();

        int SetEventHandle(IntPtr eventHandle);

        int GetService(ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object serviceInterface);
    }

    [ComImport]
    [Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioCaptureClient
    {
        int GetBuffer(out IntPtr dataBuffer, out int numFramesToRead, out uint bufferFlags, out long devicePosition, out long qpcPosition);

        int ReleaseBuffer(int numFramesRead);

        int GetNextPacketSize(out int numFramesInNextPacket);
    }
}
