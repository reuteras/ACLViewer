using System;
using System.Collections.Generic;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32;

namespace ACLViewer
{
    public static class RegistryHelper
    {
        // Get the root registry key from a path string
        private static RegistryKey GetRootKeyFromString(string path)
        {
            if (path.StartsWith("HKEY_CLASSES_ROOT\\") || path.StartsWith("HKCR\\"))
                return Registry.ClassesRoot;
            if (path.StartsWith("HKEY_CURRENT_USER\\") || path.StartsWith("HKCU\\"))
                return Registry.CurrentUser;
            if (path.StartsWith("HKEY_LOCAL_MACHINE\\") || path.StartsWith("HKLM\\"))
                return Registry.LocalMachine;
            if (path.StartsWith("HKEY_USERS\\") || path.StartsWith("HKU\\"))
                return Registry.Users;
            if (path.StartsWith("HKEY_CURRENT_CONFIG\\") || path.StartsWith("HKCC\\"))
                return Registry.CurrentConfig;
            
            return null;
        }

        // Get the subkey path from a full registry path
        private static string GetSubKeyPath(string path)
        {
            if (path.StartsWith("HKEY_CLASSES_ROOT\\"))
                return path.Substring("HKEY_CLASSES_ROOT\\".Length);
            if (path.StartsWith("HKCR\\"))
                return path.Substring("HKCR\\".Length);
            if (path.StartsWith("HKEY_CURRENT_USER\\"))
                return path.Substring("HKEY_CURRENT_USER\\".Length);
            if (path.StartsWith("HKCU\\"))
                return path.Substring("HKCU\\".Length);
            if (path.StartsWith("HKEY_LOCAL_MACHINE\\"))
                return path.Substring("HKEY_LOCAL_MACHINE\\".Length);
            if (path.StartsWith("HKLM\\"))
                return path.Substring("HKLM\\".Length);
            if (path.StartsWith("HKEY_USERS\\"))
                return path.Substring("HKEY_USERS\\".Length);
            if (path.StartsWith("HKU\\"))
                return path.Substring("HKU\\".Length);
            if (path.StartsWith("HKEY_CURRENT_CONFIG\\"))
                return path.Substring("HKEY_CURRENT_CONFIG\\".Length);
            if (path.StartsWith("HKCC\\"))
                return path.Substring("HKCC\\".Length);
            
            return path;
        }

        // Get registry owner
        public static string GetOwner(string registryPath)
        {
            try
            {
                RegistryKey rootKey = GetRootKeyFromString(registryPath);
                string subKeyPath = GetSubKeyPath(registryPath);
                
                using (RegistryKey subKey = rootKey.OpenSubKey(subKeyPath, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.TakeOwnership))
                {
                    RegistrySecurity security = subKey.GetAccessControl();
                    IdentityReference owner = security.GetOwner(typeof(NTAccount));
                    return owner.Value;
                }
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        // Set registry owner
        public static bool SetOwner(string registryPath, string owner)
        {
            try
            {
                NTAccount ntAccount = new NTAccount(owner);
                SecurityIdentifier sid = (SecurityIdentifier)ntAccount.Translate(typeof(SecurityIdentifier));
                
                RegistryKey rootKey = GetRootKeyFromString(registryPath);
                string subKeyPath = GetSubKeyPath(registryPath);
                
                using (RegistryKey subKey = rootKey.OpenSubKey(subKeyPath, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.TakeOwnership))
                {
                    RegistrySecurity security = subKey.GetAccessControl();
                    security.SetOwner(sid);
                    subKey.SetAccessControl(security);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting registry owner: {ex.Message}");
                return false;
            }
        }

        // Get registry access rules
        public static AuthorizationRuleCollection GetAccessRules(string registryPath)
        {
            try
            {
                RegistryKey rootKey = GetRootKeyFromString(registryPath);
                string subKeyPath = GetSubKeyPath(registryPath);
                
                using (RegistryKey subKey = rootKey.OpenSubKey(subKeyPath, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.ReadPermissions))
                {
                    RegistrySecurity security = subKey.GetAccessControl();
                    return security.GetAccessRules(true, true, typeof(NTAccount));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting registry access rules: {ex.Message}");
                return null;
            }
        }

        // Add registry access rule
        public static bool AddAccessRule(string registryPath, string account, RegistryRights rights, 
                                        AccessControlType controlType, InheritanceFlags inheritanceFlags, 
                                        PropagationFlags propagationFlags)
        {
            try
            {
                NTAccount ntAccount = new NTAccount(account);
                RegistryAccessRule rule = new RegistryAccessRule(
                    ntAccount,
                    rights,
                    inheritanceFlags,
                    propagationFlags,
                    controlType);
                
                RegistryKey rootKey = GetRootKeyFromString(registryPath);
                string subKeyPath = GetSubKeyPath(registryPath);
                
                using (RegistryKey subKey = rootKey.OpenSubKey(subKeyPath, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.ChangePermissions))
                {
                    RegistrySecurity security = subKey.GetAccessControl();
                    security.AddAccessRule(rule);
                    subKey.SetAccessControl(security);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding registry access rule: {ex.Message}");
                return false;
            }
        }

        // Remove registry access rule
        public static bool RemoveAccessRule(string registryPath, string account, RegistryRights rights, 
                                           AccessControlType controlType)
        {
            try
            {
                NTAccount ntAccount = new NTAccount(account);
                RegistryAccessRule rule = new RegistryAccessRule(
                    ntAccount,
                    rights,
                    InheritanceFlags.None,
                    PropagationFlags.None,
                    controlType);
                
                RegistryKey rootKey = GetRootKeyFromString(registryPath);
                string subKeyPath = GetSubKeyPath(registryPath);
                
                using (RegistryKey subKey = rootKey.OpenSubKey(subKeyPath, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.ChangePermissions))
                {
                    RegistrySecurity security = subKey.GetAccessControl();
                    security.RemoveAccessRule(rule);
                    subKey.SetAccessControl(security);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error removing registry access rule: {ex.Message}");
                return false;
            }
        }

        // Clear all registry access rules
        public static bool ClearAccessRules(string registryPath)
        {
            try
            {
                RegistryKey rootKey = GetRootKeyFromString(registryPath);
                string subKeyPath = GetSubKeyPath(registryPath);
                
                using (RegistryKey subKey = rootKey.OpenSubKey(subKeyPath, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.ChangePermissions))
                {
                    RegistrySecurity security = subKey.GetAccessControl();
                    
                    AuthorizationRuleCollection rules = security.GetAccessRules(true, true, typeof(NTAccount));
                    foreach (RegistryAccessRule rule in rules)
                    {
                        security.RemoveAccessRule(rule);
                    }
                    
                    subKey.SetAccessControl(security);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing registry access rules: {ex.Message}");
                return false;
            }
        }
    }
}
