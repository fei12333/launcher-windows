﻿using System;
using System.IO;

namespace SakuraLibrary
{
    public static class Consts
    {
        public const string Version = "2.0.6.2";

        public const string PipeName = "SakuraFrp_ServicePipe";
        public const string ServiceName = "SakuraFrpService";

        public const string UpdaterExecutable = "Updater.exe";
        public const string ServiceExecutable = "SakuraFrpService.exe";

        public const string SakuraLauncherPrefix = "SakuraLauncher_";
        public const string LegacyLauncherPrefix = "LegacySakuraLauncher_";


        public static readonly string WorkingDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), ServiceName) + "\\";
    }
}
