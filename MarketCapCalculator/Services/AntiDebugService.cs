using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MarketCapCalculator.Services
{
    // Protezione anti-debugging e anti-tampering
    public static class AntiDebugService
    {
        [DllImport("kernel32.dll")]
        private static extern bool IsDebuggerPresent();

        [DllImport("kernel32.dll")]
        private static extern bool CheckRemoteDebuggerPresent(IntPtr hProcess, ref bool pbDebuggerPresent);


        // Verifica se c'è un debugger attivo

        public static bool IsDebuggerAttached()
        {
            try
            {
                // Controlla debugger locale
                if (Debugger.IsAttached)
                    return true;

                // Controlla debugger remoto
                if (IsDebuggerPresent())
                    return true;

                bool remoteDebugger = false;
                CheckRemoteDebuggerPresent(Process.GetCurrentProcess().Handle, ref remoteDebugger);
                if (remoteDebugger)
                    return true;

                // Controlla se l'app è in esecuzione in una macchina virtuale
                if (IsRunningInVirtualMachine())
                    return true;
            }
            catch
            {
                // Ignora errori
            }

            return false;
        }


        // Verifica se l'app è in una macchina virtuale (senza WMI)

        private static bool IsRunningInVirtualMachine()
        {
            try
            {
                // Controlla tramite registry
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Virtual Machine\Guest\Parameters"))
                {
                    if (key != null)
                        return true;
                }

                // Controlla tramite processi attivi
                var processes = Process.GetProcesses();
                foreach (var process in processes)
                {
                    string processName;
                    try { processName = process.ProcessName.ToLower(); }
                    catch { continue; }

                    if (processName.Contains("vmware") ||
                        processName.Contains("virtualbox") ||
                        processName.Contains("vbox") ||
                        processName.Contains("qemu"))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // Ignora errori
            }

            return false;
        }


        // Verifica l'integrità del file eseguibile

        public static bool VerifyIntegrity()
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var location = assembly.Location;

                // Calcola hash del file
                using var sha256 = System.Security.Cryptography.SHA256.Create();
                using var stream = System.IO.File.OpenRead(location);
                var hash = sha256.ComputeHash(stream);

                // Confronta con hash atteso (da calcolare alla prima esecuzione)
                // Questo è un esempio - in produzione useresti un hash hardcoded
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}