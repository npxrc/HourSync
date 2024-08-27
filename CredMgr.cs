using System;
using Windows.Security.Credentials;

namespace HourSync
{
    public class CredentialManager
    {
        public static (string Username, string Password) ReadCredential(string target)
        {
            var vault = new PasswordVault();
            try
            {
                // Retrieve credentials by resource name
                var credential = vault.FindAllByResource(target);

                if (credential.Count == 0)
                {
                    throw new Exception($"No credentials found for target: {target}");
                }

                var firstCredential = credential[0];

                // Return username and password
                return (firstCredential.UserName, firstCredential.Password);
            }
            catch (Exception ex)
            {
                //Probably not there.
                Console.WriteLine(ex.Message);
                return (null, null);
            }
        }

        public static void WriteCredential(string target, string username, string password)
        {
            var vault = new PasswordVault();
            try
            {
                // Remove any existing credentials with the same target
                foreach (var cred in vault.FindAllByResource(target))
                {
                    vault.Remove(cred);
                }

                // Add the new credential
                vault.Add(new PasswordCredential(target, username, password));
            }
            catch (Exception ex)
            {
                // Log or handle the error appropriately
                throw new Exception($"Error saving credential: {ex.Message}");
            }
        }
    }
}
