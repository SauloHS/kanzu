using System.ComponentModel;
using System.Runtime.InteropServices;
using Raylib_cs;

namespace Kanzu;

public static class NativeFilePicker
{
    public static unsafe string? OpenAudio()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Drop an audio file into the window to import it.");
        }

        const int capacity = 32768;
        nint buffer = Marshal.AllocHGlobal(capacity * sizeof(char));
        try
        {
            Marshal.WriteInt16(buffer, 0);
            OpenFileName options = new()
            {
                Size = Marshal.SizeOf<OpenFileName>(),
                Owner = (nint)Raylib.GetWindowHandle(),
                Filter = "Audio files (*.mp3;*.ogg;*.wav)\0*.mp3;*.ogg;*.wav\0All files\0*.*\0\0",
                FilterIndex = 1,
                File = buffer,
                MaxFile = capacity,
                Title = "Import the original Newgrounds audio",
                Flags = 0x00080000 | 0x00001000 | 0x00000800 | 0x00000008
            };

            if (GetOpenFileName(ref options))
            {
                return Marshal.PtrToStringUni(buffer);
            }

            uint error = CommDlgExtendedError();
            if (error != 0)
            {
                throw new Win32Exception((int)error, "The file picker could not open. Drop the audio file into the window instead.");
            }

            return null;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [DllImport("comdlg32.dll", EntryPoint = "GetOpenFileNameW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetOpenFileName(ref OpenFileName options);

    [DllImport("comdlg32.dll")]
    private static extern uint CommDlgExtendedError();

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OpenFileName
    {
        public int Size;
        public nint Owner;
        public nint Instance;
        public string? Filter;
        public nint CustomFilter;
        public int MaxCustomFilter;
        public int FilterIndex;
        public nint File;
        public int MaxFile;
        public nint FileTitle;
        public int MaxFileTitle;
        public string? InitialDirectory;
        public string? Title;
        public int Flags;
        public short FileOffset;
        public short FileExtension;
        public string? DefaultExtension;
        public nint CustomData;
        public nint Hook;
        public nint TemplateName;
        public nint Reserved;
        public int ReservedSize;
        public int FlagsEx;
    }
}
