using System;
using Windows.Security.Credentials;

namespace HourSync;
public class CredentialManager
{
    private readonly PasswordVault _vault;

    public CredentialManager()
    {
        _vault = new PasswordVault();
    }

    public (string Username, string Password) ReadCredential(string target)
    {
        if (string.IsNullOrEmpty(target))
        {
            throw new ArgumentException("Target cannot be null or empty.", nameof(target));
        }

        try
        {
            // Retrieve credentials by resource name
            var credentials = _vault.FindAllByResource(target);

            if (credentials.Count == 0)
            {
                // Return null if no credentials found
                return (null, null);
            }

            var firstCredential = credentials[0];

            // Return username and password
            return (firstCredential.UserName, firstCredential.Password);
        }
        catch (Exception ex)
        {
            // Log unexpected errors
            FileMgr.Log(ex.Message);
            return (null, null);
        }
    }

    public void WriteCredential(string target, string username, string password)
    {
        if (string.IsNullOrEmpty(target) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            throw new ArgumentException("Target, username, and password cannot be null or empty.");
        }

        try
        {
            RemoveExistingCredentials(target);
            // Add the new credential
            _vault.Add(new PasswordCredential(target, username, password));
        }
        catch (Exception ex)
        {
            // Log or handle the error appropriately
            FileMgr.Log($"Error saving credential: {ex.Message}");
        }
    }

    private void RemoveExistingCredentials(string target)
    {
        var existingCredentials = _vault.FindAllByResource(target);
        foreach (var cred in existingCredentials)
        {
            _vault.Remove(cred);
        }
    }
}