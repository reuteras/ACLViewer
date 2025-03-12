using System;
using System.Collections.Generic;
using System.Security.AccessControl;
using System.Security.Principal;
using System.IO;
using System.Runtime.InteropServices;

namespace ACLViewer
{
    public static class SecurityHelper
    {
        // Windows API Constants
        private const int SE_PRIVILEGE_ENABLED = 0x00000002;
        private const string SE_RESTORE_NAME = "SeRestorePrivilege";
        private const string SE_BACKUP_NAME = "SeBackupPrivilege";
        private const string SE_SECURITY_NAME = "SeSecurityPrivilege";
        private const string SE_TAKE_OWNERSHIP_NAME = "SeTakeOwnershipPrivilege";
        private const string SE_DEBUG_NAME = "SeDebugPrivilege";

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID_AND_ATTRIBUTES
        {
            public LUID Luid;
            public uint Attributes;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TOKEN_PRIVILEGES
        {
            public uint PrivilegeCount;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
            public LUID_AND_ATTRIBUTES[] Privileges;
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool LookupPrivilegeValue(string lpSystemName, string lpName, ref LUID lpLuid);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool AdjustTokenPrivileges(IntPtr TokenHandle, bool DisableAllPrivileges, 
            ref TOKEN_PRIVILEGES NewState, uint BufferLength, IntPtr PreviousState, IntPtr ReturnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hHandle);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetCurrentProcess();

        // Enable required privileges for managing security
        public static bool EnablePrivileges()
        {
            try
            {
                // Get process token
                IntPtr tokenHandle = IntPtr.Zero;
                if (!OpenProcessToken(GetCurrentProcess(), 0x0020 | 0x0008, out tokenHandle))
                    return false;

                try
                {
                    // Enable required privileges
                    EnablePrivilege(tokenHandle, SE_RESTORE_NAME);
                    EnablePrivilege(tokenHandle, SE_BACKUP_NAME);
                    EnablePrivilege(tokenHandle, SE_SECURITY_NAME);
                    EnablePrivilege(tokenHandle, SE_TAKE_OWNERSHIP_NAME);
                    EnablePrivilege(tokenHandle, SE_DEBUG_NAME);
                    
                    return true;
                }
                finally
                {
                    CloseHandle(tokenHandle);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error enabling privileges: {ex.Message}");
                return false;
            }
        }

        private static bool EnablePrivilege(IntPtr tokenHandle, string privilegeName)
        {
            TOKEN_PRIVILEGES tp = new TOKEN_PRIVILEGES();
            tp.PrivilegeCount = 1;
            tp.Privileges = new LUID_AND_ATTRIBUTES[1];
            tp.Privileges[0].Attributes = SE_PRIVILEGE_ENABLED;

            LUID luid = new LUID();
            if (!LookupPrivilegeValue(null, privilegeName, ref luid))
                return false;

            tp.Privileges[0].Luid = luid;

            return AdjustTokenPrivileges(tokenHandle, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
        }

        // Get owner of a file or directory
        public static string GetOwner(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    DirectoryInfo dir = new DirectoryInfo(path);
                    DirectorySecurity dirSecurity = dir.GetAccessControl();
                    IdentityReference owner = dirSecurity.GetOwner(typeof(NTAccount));
                    return owner.Value;
                }
                else if (File.Exists(path))
                {
                    FileInfo file = new FileInfo(path);
                    FileSecurity fileSecurity = file.GetAccessControl();
                    IdentityReference owner = fileSecurity.GetOwner(typeof(NTAccount));
                    return owner.Value;
                }
                return "Unknown";
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        // Set owner of a file or directory
        public static bool SetOwner(string path, string owner)
        {
            try
            {
                NTAccount ntAccount = new NTAccount(owner);
                SecurityIdentifier sid = (SecurityIdentifier)ntAccount.Translate(typeof(SecurityIdentifier));

                if (Directory.Exists(path))
                {
                    DirectoryInfo dir = new DirectoryInfo(path);
                    DirectorySecurity dirSecurity = dir.GetAccessControl();
                    dirSecurity.SetOwner(sid);
                    dir.SetAccessControl(dirSecurity);
                    return true;
                }
                else if (File.Exists(path))
                {
                    FileInfo file = new FileInfo(path);
                    FileSecurity fileSecurity = file.GetAccessControl();
                    fileSecurity.SetOwner(sid);
                    file.SetAccessControl(fileSecurity);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting owner: {ex.Message}");
                return false;
            }
        }

        // Clear all access rules
        public static bool ClearAccessRules(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    DirectoryInfo dir = new DirectoryInfo(path);
                    DirectorySecurity dirSecurity = dir.GetAccessControl();
                    
                    AuthorizationRuleCollection rules = dirSecurity.GetAccessRules(true, true, typeof(NTAccount));
                    foreach (FileSystemAccessRule rule in rules)
                    {
                        dirSecurity.RemoveAccessRule(rule);
                    }
                    
                    dir.SetAccessControl(dirSecurity);
                    return true;
                }
                else if (File.Exists(path))
                {
                    FileInfo file = new FileInfo(path);
                    FileSecurity fileSecurity = file.GetAccessControl();
                    
                    AuthorizationRuleCollection rules = fileSecurity.GetAccessRules(true, true, typeof(NTAccount));
                    foreach (FileSystemAccessRule rule in rules)
                    {
                        fileSecurity.RemoveAccessRule(rule);
                    }
                    
                    file.SetAccessControl(fileSecurity);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing access rules: {ex.Message}");
                return false;
            }
        }

        // Add a new access rule
        public static bool AddAccessRule(string path, string account, FileSystemRights rights, 
                                        AccessControlType controlType, InheritanceFlags inheritanceFlags, 
                                        PropagationFlags propagationFlags)
        {
            try
            {
                NTAccount ntAccount = new NTAccount(account);
                FileSystemAccessRule rule = new FileSystemAccessRule(
                    ntAccount,
                    rights,
                    inheritanceFlags,
                    propagationFlags,
                    controlType);

                if (Directory.Exists(path))
                {
                    DirectoryInfo dir = new DirectoryInfo(path);
                    DirectorySecurity dirSecurity = dir.GetAccessControl();
                    dirSecurity.AddAccessRule(rule);
                    dir.SetAccessControl(dirSecurity);
                    return true;
                }
                else if (File.Exists(path))
                {
                    FileInfo file = new FileInfo(path);
                    FileSecurity fileSecurity = file.GetAccessControl();
                    fileSecurity.AddAccessRule(rule);
                    file.SetAccessControl(fileSecurity);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding access rule: {ex.Message}");
                return false;
            }
        }

        // Convert file system rights to a bit mask
        public static int GetAccessMask(FileSystemRights rights)
        {
            return (int)rights;
        }

        // Convert bit mask to file system rights
        public static FileSystemRights GetRightsFromMask(int mask)
        {
            return (FileSystemRights)mask;
        }
    }
}
