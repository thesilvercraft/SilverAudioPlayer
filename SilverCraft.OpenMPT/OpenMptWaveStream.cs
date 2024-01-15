using System.Diagnostics;
using System.Runtime.InteropServices;
using CSCore;
using OpenMPT;
using SilverAudioPlayer.Shared;

namespace SilverCraft.OpenMPT;
public unsafe class OpenMptWaveStream : ISampleSource, ISelector, ILoop
{
    openmpt_module* infile;
    MPTVirtualStreamHelper Helper;
    public static bool ResolverIsSet = false;
    public unsafe OpenMptWaveStream(Stream s)
    {
        Helper = new(s);
        if (!ResolverIsSet)
        {
            ResolverIsSet = true;
            NativeLibrary.SetDllImportResolver(typeof(NativeMethods).Assembly, NativeMethods.DllImportResolver);
        }
        infile = NativeMethods.openmpt_module_create2(Helper.Virtual, 0, null, null, null, null, null, null, null);
        WaveFormat = new WaveFormat(48000, 16, 2);
        Length = (long)(NativeMethods.openmpt_module_get_duration_seconds(infile) * WaveFormat.BytesPerSecond);
    }


    public int Read(float[] buffer, int offset, int count)
    {
        unsafe
        {
            count /= 2;
            float[] left = new float[count];
            float[] right = new float[count];
            int framesRead;
            fixed (float* leftPtr = left, rightPtr = right)
            {
                framesRead = (int)NativeMethods.openmpt_module_read_float_stereo(infile, 48000, (nuint)count, leftPtr, rightPtr);
            }
            for (int i = 0; i < framesRead; i++)
            {
                buffer[offset + i * 2] = left[i];
                buffer[offset + i * 2 + 1] = right[i];
            }
            return framesRead * 2;
        }
    }


    public void Dispose()
    {
        unsafe
        {
            Debug.WriteLine("Disposing OpenMptWaveStream!");
            NativeMethods.openmpt_module_destroy(infile);
            Debug.WriteLine("Disposing MPTVirtualStreamHelper");
            Helper.Dispose();
        }

    }

    public void SetPattern(int pattern)
    {
        _ = NativeMethods.openmpt_module_select_subsong(infile, pattern);
        Length = (long)(NativeMethods.openmpt_module_get_duration_seconds(infile) * WaveFormat.BytesPerSecond);

    }

    public void EnableLoop()
    {
        _ = NativeMethods.openmpt_module_set_repeat_count(infile, -1);
    }

    public void DisableLoop()
    {
        _ = NativeMethods.openmpt_module_set_repeat_count(infile, 0);

    }

    public bool CanSeek => true;
    public WaveFormat WaveFormat { get; set; }
    public unsafe long Position
    {
        get => (long)(NativeMethods.openmpt_module_get_position_seconds(infile) * WaveFormat.BytesPerSecond);
        set
        {
            unsafe
            {
                NativeMethods.openmpt_module_set_position_seconds(infile, value / WaveFormat.BytesPerSecond);
            }
        }
    }

    public long Length { get; set; }

    public int CurrentPattern => NativeMethods.openmpt_module_get_selected_subsong(infile);

    public int NumberOfPatterns => NativeMethods.openmpt_module_get_num_subsongs(infile);

    public bool CanLoop => true;
}