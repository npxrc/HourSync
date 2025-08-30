using System;
using Windows.Security.Credentials;

namespace HourSync;
public static class CredMgr
{
    private const string ResourceName = "HourSync";

    public static (string userName, string password) GetCreds()
    {
        try
        {
            var vault = new PasswordVault();
            var creds = vault.RetrieveAll();

            foreach (var cred in creds)
            {
                if (cred.Resource == ResourceName)
                {
                    cred.RetrievePassword();
                    return (cred.UserName, cred.Password);
                }
            }

            return (null, null);
        }
        catch (Exception ex)
        {
            FileMgr.LogError("Error reading credentials: " + ex.Message);
            return (null, null);
        }
    }

    public static void SaveCreds(string username, string password)
    {
        try
        {
            var vault = new PasswordVault();

            // Remove old HourSync creds
            foreach (var cred in vault.RetrieveAll())
            {
                if (cred.Resource == ResourceName)
                {
                    vault.Remove(cred);
                }
            }

            vault.Add(new PasswordCredential(ResourceName, username, password));
            FileMgr.Log("Credentials saved to vault");
        }
        catch (Exception ex)
        {
            FileMgr.LogError("Error saving credentials: " + ex.Message);
        }
    }

    public static void ClearCreds()
    {
        try
        {
            var vault = new PasswordVault();
            foreach (var cred in vault.RetrieveAll())
            {
                if (cred.Resource == ResourceName)
                {
                    vault.Remove(cred);
                }
            }
            FileMgr.Log("Credentials cleared from vault");
        }
        catch (Exception ex)
        {
            FileMgr.LogError("Error clearing credentials: " + ex.Message);
        }
    }
}
