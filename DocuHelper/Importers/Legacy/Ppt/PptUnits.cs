using System;

namespace DocuHelper.Importers.Legacy.Ppt
{
    internal static class PptUnits
    {
        public const double EmuPerMaster = 914400.0 / 576.0;

        public const long MinSlide = 914400;

        public const long MaxSlide = 51206400;

        public static long Emu(long masterUnits) => (long)Math.Round(masterUnits * EmuPerMaster);

        public static long SlideEmu(int masterUnits) => Math.Clamp(Emu(masterUnits), MinSlide, MaxSlide);
    }
}
