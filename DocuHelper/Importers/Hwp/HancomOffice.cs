using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace DocuHelper.Importers.Hwp
{
    public sealed class HancomOffice : IHancomOffice
    {
        private const string ProgId = "HWPFrame.HwpObject";
        private const string Failed = "한글로 .hwp 파일을 만들지 못했어요. 한글이 다른 창에서 일하는 중인지 봐 주세요.";

        private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(90);

        private readonly Lazy<bool> _available = new(Detect);

        public bool IsAvailable => _available.Value;

        public byte[] ToHwp(byte[] hwpx)
        {
            if (!IsAvailable)
                throw new HwpException("이 컴퓨터에 한글이 없어서 .hwp로 저장할 수 없어요.");

            var folder = Path.Combine(Path.GetTempPath(), "문서도우미", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            var source = Path.Combine(folder, "source.hwpx");
            var target = Path.Combine(folder, "result.hwp");
            try
            {
                File.WriteAllBytes(source, hwpx);
                Exception error = null;
                var thread = new Thread(() =>
                {
                    try
                    {
                        Convert(source, target);
                    }
                    catch (Exception e)
                    {
                        error = e;
                    }
                });

                if (OperatingSystem.IsWindows())
                    thread.SetApartmentState(ApartmentState.STA);

                thread.IsBackground = true;
                thread.Start();
                if (!thread.Join(s_timeout))
                    throw new HwpException(Failed);

                if (error != null)
                    throw error as HwpException ?? new HwpException(Failed, error);

                if (!File.Exists(target))
                    throw new HwpException(Failed);

                return File.ReadAllBytes(target);
            }
            finally
            {
                try
                {
                    Directory.Delete(folder, true);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                }
            }
        }

        private static bool Detect()
        {
            if (!OperatingSystem.IsWindows())
                return false;

            try
            {
                return Type.GetTypeFromProgID(ProgId) != null;
            }
            catch (Exception e) when (e is COMException || e is PlatformNotSupportedException)
            {
                return false;
            }
        }

        private static void Convert(string source, string target)
        {
            if (!OperatingSystem.IsWindows())
                throw new HwpException(Failed);

            var type = Type.GetTypeFromProgID(ProgId) ?? throw new HwpException(Failed);
            dynamic hwp = Activator.CreateInstance(type);
            try
            {
                try
                {
                    hwp.RegisterModule("FilePathCheckDLL", "FilePathCheckerModule");
                }
                catch (COMException)
                {
                }

                if (!(bool)hwp.Open(source, "HWPX", "forceopen:true;suspendpassword:true;versionwarning:false"))
                    throw new HwpException(Failed);

                if (!(bool)hwp.SaveAs(target, "HWP", ""))
                    throw new HwpException(Failed);

                hwp.Clear(1);
            }
            finally
            {
                try
                {
                    hwp.Quit();
                }
                catch (COMException)
                {
                }

                Marshal.FinalReleaseComObject(hwp);
            }
        }
    }
}
